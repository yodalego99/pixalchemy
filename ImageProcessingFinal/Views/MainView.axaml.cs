using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Image = Avalonia.Controls.Image;
using Size = System.Drawing.Size;

namespace ImageProcessingFinal.Views;

public partial class MainView : UserControl
{
    private bool _suppressTrackBarChange;

    public MainView()
    {
        InitializeComponent();
    }

    private static WriteableBitmap CreateBitmapFromPixelData(
        byte[] rgbPixelData,
        int width,
        int height
    )
    {
        // Standard - maybe it needs to be changed on some devices
        Vector dpi = new Vector(96, 96);

        var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            dpi,
            Avalonia.Platform.PixelFormats.Bgr24
        );
        using (var frameBuffer = bitmap.Lock())
        {
            Marshal.Copy(rgbPixelData, 0, frameBuffer.Address, rgbPixelData.Length);
        }

        return bitmap;
    }

    VideoCaptureInfo? _selectedVideoFile; // kiválasztott videó (felhasználó adja meg)
    VideoCaptureInfo? _webCamVideo; // webkamera videója
    VideoCaptureInfo? _exportedVideoFile; // visszajátszásmiatt van // később át lesz írva
    Image<Bgr, byte>? _webCamFrame; // webkamera videóinak képkockája
    bool _isWebcamBackgroundRemovalOn = false; // jelzi, hogy be van-e kapcsolva a webkamera háttérleválasztása
    bool _isFirstFrame = false; // ellenőrzi, hogy a kikért képkocka az első-e a webkamerának
    bool _isPlaying = false; // jelzi, hogy lejátszódik-e éppen a videó
    bool _isExported = false; // jelzi, hogy megtörtént-e már a videón a háttérleválasztás
    Mat? _currentFrame; // jelenlegi frame Mat típusú képe
    Image<Bgr,byte> _exportedCurrentFrame; // kiexportált képkocka - videó exportálásánál használjuk
    string? _videoFileName = string.Empty; // kiválasztott videófájl neve

    private async void PlayVideoFile()
    {
        try
        {
            if (_selectedVideoFile?.Video == null && _selectedVideoFile?.FilePath == String.Empty)
            {
                return;
            }

            if (_selectedVideoFile?.Video != null && !_selectedVideoFile.Video.Grab() &&
                _selectedVideoFile?.FilePath != String.Empty)
            {
                _selectedVideoFile.Video = new VideoCapture(_selectedVideoFile.FilePath);
            }

            try
            {
                var currentVideo = _selectedVideoFile;
                _currentFrame ??= new Mat();

                var frameDelay = _selectedVideoFile.DeltaFrameTime ?? 0d;
                while (_isPlaying && currentVideo == _selectedVideoFile)
                {
                    if (!currentVideo.Video.Read(_currentFrame) || _currentFrame.IsEmpty)
                    {
                        break;
                    }

                    if (_isExported)
                    {
                        if (_exportedVideoFile != null)
                        {
                            _exportedCurrentFrame ??= new Image<Bgr, byte>(_currentFrame.Width, _currentFrame.Height);
                            if (_exportedVideoFile.Video.Read(_exportedCurrentFrame))
                            {
                                PictureBox2.Source = CreateBitmapFromPixelData(_exportedCurrentFrame.Bytes,
                                    _exportedCurrentFrame.Width, _exportedCurrentFrame.Height);
                            }
                        }
                    }

                    var frameImage = _currentFrame.ToImage<Bgr, byte>();
                    PictureBox1.Source =
                        CreateBitmapFromPixelData(frameImage.Bytes, frameImage.Width, frameImage.Height);

                    var position = currentVideo.Video.Get(CapProp.PosMsec);
                    if (!double.IsNaN(position) && !double.IsInfinity(position))
                    {
                        _suppressTrackBarChange = true;
                        TrackBar1.Value = Math.Clamp(position, TrackBar1.Minimum, TrackBar1.Maximum);
                        _suppressTrackBarChange = false;
                    }

                    await Task.Delay((int)Math.Max(1, Math.Min(int.MaxValue, Math.Round(frameDelay))));
                }

                var reachedEnd = currentVideo == _selectedVideoFile;
                _isPlaying = false;
                Dispatcher.UIThread.Post(() =>
                {
                    PlayButton.Content = "Play";
                    if (reachedEnd && _selectedVideoFile?.Video != null)
                    {
                        var endPosition = _selectedVideoFile.Video.Get(CapProp.PosMsec);
                        if (!double.IsNaN(endPosition) && !double.IsInfinity(endPosition))
                        {
                            _suppressTrackBarChange = true;
                            TrackBar1.Value = Math.Clamp(endPosition, TrackBar1.Minimum, TrackBar1.Maximum);
                            _suppressTrackBarChange = false;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        Button button = (Button)sender;
        if (_selectedVideoFile != null)
        {
            if (_isPlaying)
            {
                _isPlaying = false;
                button.Content = "Play";
            }
            else
            {
                _isPlaying = true;
                PlayVideoFile();
                button.Content = "Pause";
            }
        }
    }

    private void StopButton_Click(object? sender, RoutedEventArgs e)
    {
        _isPlaying = false;
        _suppressTrackBarChange = true;
        TrackBar1.Value = TrackBar1.Minimum;
        _suppressTrackBarChange = false;
        PictureBox1.Source = null;
        PictureBox2.Source = null;
        PlayButton.Content = "Play";
        if (_selectedVideoFile != null)
        {
            _selectedVideoFile.Video.Set(CapProp.PosMsec, 0.0);
        }
    }

    /*private void VideoFrameExportButton_Click(object sender, RoutedEventArgs e)
    {
        ToolStripMenuReset();
        StopButton_Click(sender, e);
        if (SelectedVideoFile != null)
        {
            FolderBrowserDialog ExportVideoFileFrames = new FolderBrowserDialog();
            IsPlaying = false;
            ExportVideoFileFrames.ShowNewFolderButton = true;
            if (ExportVideoFileFrames.ShowDialog() == DialogResult.OK)
            {
                label1.Text = "Képkockák kinyerése:";
                progressBar1.Visible = true;
                progressBar1.Minimum = 0;
                progressBar1.Maximum = TotalFrames;
                progressBar1.Value = 0;

                ControlsEnabled(false);
                String ExportedFramesLocation = ExportVideoFileFrames.SelectedPath;
                Directory.CreateDirectory(ExportedFramesLocation + @"\" + VideoFileName + @"\Képkockák");
                Thread Export = new Thread(() =>
                {
                    for (int i = 0; i < TotalFrames; i++)
                    {to
                        Mat frame = new Mat();
                        SelectedVideoFile.Set(CapProp.PosFrames, i);
                        SelectedVideoFile.Read(frame);
                        frame.Save(ExportedFramesLocation + @"\" + VideoFileName + @"\Képkockák\" + i + ".jpg");
                        Invoke(new Action(() =>
                        {
                            progressBar1.Value = i;
                        }));
                        frame.Dispose();

                    }
                    StreamWriter fps = new StreamWriter(ExportedFramesLocation + @"\" + VideoFileName + @"\Képkockák\FPS.txt");
                    fps.WriteLine(FPS);
                    fps.Close();

                    Invoke(new Action(() =>
                    {
                        progressBar1.Visible = false;
                        label1.Text = string.Empty;

                        ControlsEnabled(true);
                    }));
                    MessageBox.Show("A videó képkockáinak exportálása sikeres!");
                });
                Export.IsBackground = true;
                Export.Start();
            }
            ExportVideoFileFrames.Dispose();
        }
    }*/

    private async void vBackgroundRemovalButton_Click(object sender, RoutedEventArgs e)
    {
        ToolStripMenuReset();
        StopButton_Click(sender, e);
        if (_selectedVideoFile != null)
        {
            _isPlaying = false;
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider == null)
            {
                return;
            }

            var outputVideo = await topLevel.StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions { Title = "Save video..." }
            );
            if (outputVideo is not null)
            {
                var outputVideoLocation = outputVideo.Path.AbsoluteUri;

                ControlsEnabled(false);

                var outputCreation = new Thread(() =>
                {
                    var removedBackgroundVideo = new VideoWriter(
                        outputVideoLocation, VideoWriter.Fourcc('m','p','4','v'),
                        (double)_selectedVideoFile.Fps,
                        new Size(_selectedVideoFile.Video.Width, _selectedVideoFile.Video.Height),
                        true
                    );
                    var viBeProcess = new ViBe().WithDefaults();
                    _currentFrame = _selectedVideoFile.Video.QueryFrame();
                    viBeProcess.FrameImage = _currentFrame.ToImage<Rgb, byte>();
                    viBeProcess.BackgroundModelInitialization();
                    int counter = 0;
                    while (_selectedVideoFile.Video.Grab())
                    {
                        _selectedVideoFile.Video.Read(_currentFrame);
                        var frameImage = _currentFrame.ToImage<Rgb, byte>();
                        viBeProcess.FrameImage = frameImage;
                        viBeProcess.BackgroundModelUpdate(counter);
                        var segmapImage = new Image<Rgb, byte>(viBeProcess._segMapBytes);
                        removedBackgroundVideo.Write(segmapImage);
                        counter++;
                        Dispatcher.UIThread.Post(() =>
                        {
                            PictureBox1.Source = CreateBitmapFromPixelData(frameImage.Bytes, frameImage.Width, frameImage.Height); 
                            PictureBox2.Source = CreateBitmapFromPixelData(segmapImage.Bytes, segmapImage.Width, segmapImage.Height); 
                        });
                    }

                    _isExported = true;
                    removedBackgroundVideo.Dispose();
                    _exportedVideoFile = new VideoCaptureInfo(new VideoCapture(outputVideoLocation), false, outputVideoLocation);
                });
                outputCreation.IsBackground = true;
                outputCreation.Start();
            }
        }
    }

    private void ControlsEnabled(bool state)
    {
        InputToolstrip.IsEnabled = state;
        ProcessToolstrip.IsEnabled = state;
        PlayButton.IsEnabled = state;
        StopButton.IsEnabled = state;
        TrackBar1.IsEnabled = state;
    }

    private void ToolStripMenuReset()
    {
        InputToolstrip.Close();
        ProcessToolstrip.Close();
    }

    private void TimeStampBar_Scroll(object sender, RoutedEventArgs e)
    {
        if (_suppressTrackBarChange || _selectedVideoFile?.Video == null)
        {
            return;
        }

        var requestedPosition = TrackBar1.Value;
        if (double.IsNaN(requestedPosition) || double.IsInfinity(requestedPosition))
        {
            return;
        }

        _isPlaying = false;
        PlayButton.Content = "Play";

        if (
            _selectedVideoFile.Video.Set(CapProp.PosMsec, requestedPosition)
            && _selectedVideoFile.Video.Read(_currentFrame)
            && _currentFrame is not null
        )
        {
            var frameImage = _currentFrame;
            PictureBox1.Source = CreateBitmapFromPixelData(
                frameImage.GetRawData(),
                frameImage.Width,
                frameImage.Height
            );
        }
    }

    private void VideoCaptureRemover()
    {
        _isPlaying = false;
            _webCamVideo = null;
            _selectedVideoFile = null;
            _exportedVideoFile = null;
        PictureBox1.Source = null;
        PictureBox2.Source = null;
        GC.Collect();
    }

    private async void VideoFromWebcamToolStripMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            VideoCaptureRemover();
            ToolStripMenuReset();
            StopButton_Click(sender, e);
            Thread webCamCapture = new Thread(() =>
            {
                if (_webCamVideo == null)
                {
                    _webCamVideo = new VideoCaptureInfo(new VideoCapture(), true, String.Empty);
                    _webCamVideo.Video.ImageGrabbed += WebCamVideo_ImageGrabbed;
                    _isWebcamBackgroundRemovalOn = false;
                    _isFirstFrame = true;
                    _webCamVideo.Video.Start();
                }
                else
                {
                    _webCamVideo.Video.ImageGrabbed -= WebCamVideo_ImageGrabbed;
                    _webCamVideo.Video.Stop();
                    _webCamVideo = null;
                    _webCamVideo = null;
                    _isWebcamBackgroundRemovalOn = false;
                    _isFirstFrame = false;
                }
            });
            webCamCapture.Start();
            webCamCapture.IsBackground = true;
            await Task.Delay(5);
            PictureBox1.Source = null;
            PictureBox2.Source = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private void WebCamVideo_ImageGrabbed(object? sender, EventArgs e)
    {
        try
        {
            if (_webCamVideo != null)
            {
                _webCamFrame = _webCamVideo.Video.QueryFrame().ToImage<Bgr, byte>();
                Dispatcher.UIThread.Post(() =>
                    PictureBox1.Source = CreateBitmapFromPixelData(
                        _webCamFrame.Bytes,
                        _webCamFrame.Width,
                        _webCamFrame.Height
                    )
                );
                Random rnd = new Random();
                //FrameImage = WebCamFrame.ToImage<Bgr, Byte>();
                //FrameImageBytes = FrameImage.Data;
                //SegMapBytes = SegMap.Data;
                if (_isWebcamBackgroundRemovalOn && _isFirstFrame)
                {
                    //BackgroundModelInitialization();
                    _isFirstFrame = false;
                }
                else if (_isWebcamBackgroundRemovalOn && !_isFirstFrame)
                {
                    //BackgroundModelUpdate(1);
                    //SegMap.Data = SegMapBytes;
                    //Invoke(new Action(() => { pictureBox2.Source = SegMap.ToBitmap(); }));
                    //pictureBox2.Source = CreateBitmapFromPixelData()
                }

                GC.Collect();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    /*private void OnlyForegroundToolStripMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ToolStripMenuReset();
        StopButton_Click(sender, e);
        if (SelectedVideoFile != null)
        {
            OnlyForeground = true;
            OnlyBackground = false;
            vBackgroundRemovalButton_Click(sender, e);
        }
        else if (WebCamVideo != null)
        {
            OnlyForeground = true;
            OnlyBackground = false;
            IsWebcamBackgroundRemovalOn = true;
        }
    }

    private void OnlyBackgroundToolStripMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ToolStripMenuReset();
        StopButton_Click(sender, e);
        if (SelectedVideoFile != null)
        {
            OnlyForeground = false;
            OnlyBackground = true;
            vBackgroundRemovalButton_Click(sender, e);
        }
        else if (WebCamVideo != null)
        {
            OnlyForeground = false;
            OnlyBackground = true;
            IsWebcamBackgroundRemovalOn = true;
        }
    }

    private void SegmentationMapToolStripMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ToolStripMenuReset();
        StopButton_Click(sender, e);
        if (SelectedVideoFile != null)
        {
            OnlyForeground = false;
            OnlyBackground = false;
            vBackgroundRemovalButton_Click(sender, e);
        }
        else if (WebCamVideo != null)
        {
            OnlyForeground = false;
            OnlyBackground = false;
            IsWebcamBackgroundRemovalOn = true;
        }
    }*/
    private async void VideoSelect_Click(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StopButton_Click(this, new RoutedEventArgs());
            VideoCaptureRemover();
        });
        ToolStripMenuReset();
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null)
        {
            return;
        }

        var openVideoFile = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open video...", AllowMultiple = false, FileTypeFilter = new[] { FilePickerFileTypes.All }
        });
        PictureBox1.Source = null;
        PictureBox2.Source = null;
        _isExported = false;
        if (openVideoFile.Count >= 1)
        {
            _selectedVideoFile = new VideoCaptureInfo(new VideoCapture(openVideoFile[0].Path.AbsoluteUri), false,
                openVideoFile[0].Path.AbsoluteUri);
            _currentFrame = new Mat();
            _suppressTrackBarChange = true;
            TrackBar1.Minimum = 0;
            TrackBar1.Maximum = Convert.ToDouble(_selectedVideoFile.TotalDuration);
            TrackBar1.Value = TrackBar1.Minimum;
            _suppressTrackBarChange = false;
            _videoFileName = openVideoFile[0].Name;
            if (_selectedVideoFile.Video.Read(_currentFrame) && !_currentFrame.IsEmpty)
            {
                var frameImage = _currentFrame.ToImage<Rgb, Byte>();
                PictureBox1.Source = CreateBitmapFromPixelData(frameImage.Bytes, frameImage.Width, frameImage.Height);
                _selectedVideoFile.Video.Set(CapProp.PosFrames, 0);
            }

            _isPlaying = false;
            PlayButton.Content = "Play";
            _suppressTrackBarChange = true;
            TrackBar1.Value = TrackBar1.Minimum;
            _suppressTrackBarChange = false;
        }
    }
}
