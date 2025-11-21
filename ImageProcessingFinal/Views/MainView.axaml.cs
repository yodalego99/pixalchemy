using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace ImageProcessingFinal.Views;

public partial class MainView : UserControl
{
    private Image _pictureBox1;
    private Image _pictureBox2;
    private Slider _trackBar1;
    private Button _playButton;
    private bool _suppressTrackBarChange;

    public MainView()
    {
        InitializeComponent();
        _pictureBox1 = PictureBox1;
        _pictureBox2 = PictureBox2;
        _trackBar1 = TrackBar1;
        _playButton = PlayButton;
    }

    private static WriteableBitmap CreateBitmapFromPixelData(byte[] rgbPixelData, int width, int height)
    {
        // Standard - maybe it needs to be changed on some devices
        Vector dpi = new Vector(96, 96);

        var bitmap = new WriteableBitmap(new PixelSize(width, height), dpi, Avalonia.Platform.PixelFormat.Rgb32);
        using (var frameBuffer = bitmap.Lock())
        {
            Marshal.Copy(rgbPixelData, 0, frameBuffer.Address, rgbPixelData.Length);
        }

        return bitmap;
    }

    VideoCaptureInfo? _selectedVideoFile; // kiválasztott videó (felhasználó adja meg)
    VideoCaptureInfo? _webCamVideo; // webkamera videója
    VideoCapture? _exportedVideoFile; // visszajátszásmiatt van // később át lesz írva
    Image<Rgba, Byte>? _webCamFrame; // webkamera videóinak képkockája
    bool _isWebcamBackgroundRemovalOn = false; // jelzi, hogy be van-e kapcsolva a webkamera háttérleválasztása
    bool _isFirstFrame = false; // ellenőrzi, hogy a kikért képkocka az első-e a webkamerának
    bool _isPlaying = false; // jelzi, hogy lejátszódik-e éppen a videó
    bool _isExported = false; // jelzi, hogy megtörtént-e már a videón a háttérleválasztás
    Mat? _currentFrame; // jelenlegi frame Mat típusú képe
    Image<Rgba, Byte>? _exportedCurrentFrame; // kiexportált képkocka - videó exportálásánál használjuk 
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
                            _exportedCurrentFrame ??= new Image<Rgba, Byte>(_currentFrame.Width, _currentFrame.Height);
                            if (_exportedVideoFile.Read(_exportedCurrentFrame))
                            {
                                _pictureBox2.Source = CreateBitmapFromPixelData(_exportedCurrentFrame.Bytes,
                                    _exportedCurrentFrame.Width, _exportedCurrentFrame.Height);
                            }
                        }
                    }

                    var frameImage = _currentFrame.ToImage<Rgba, Byte>();
                    _pictureBox1.Source =
                        CreateBitmapFromPixelData(frameImage.Bytes, frameImage.Width, frameImage.Height);

                    var position = currentVideo.Video.Get(CapProp.PosMsec);
                    if (!double.IsNaN(position) && !double.IsInfinity(position))
                    {
                        _suppressTrackBarChange = true;
                        _trackBar1.Value = Math.Clamp(position, _trackBar1.Minimum, _trackBar1.Maximum);
                        _suppressTrackBarChange = false;
                    }

                    await Task.Delay((int)Math.Max(1, Math.Min(int.MaxValue, Math.Round(frameDelay))));
                }

                var reachedEnd = currentVideo == _selectedVideoFile;
                _isPlaying = false;
                Dispatcher.UIThread.Post(() =>
                {
                    _playButton.Content = "Play";
                    if (reachedEnd && _selectedVideoFile?.Video != null)
                    {
                        var endPosition = _selectedVideoFile.Video.Get(CapProp.PosMsec);
                        if (!double.IsNaN(endPosition) && !double.IsInfinity(endPosition))
                        {
                            _suppressTrackBarChange = true;
                            _trackBar1.Value = Math.Clamp(endPosition, _trackBar1.Minimum, _trackBar1.Maximum);
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
        _trackBar1.Value = _trackBar1.Minimum;
        _suppressTrackBarChange = false;
        _pictureBox1.Source = null;
        _pictureBox2.Source = null;
        _playButton.Content = "Play";
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
        //ToolStripMenuReset();
        StopButton_Click(sender, e);
        if (_selectedVideoFile != null)
        {
            _isPlaying = false;
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider == null)
            {
                return;
            }

            var outputVideo = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save video...", DefaultExtension = ".mp4"
            });
            if (outputVideo is not null)
            {
                var outputVideoLocation = outputVideo.Path.AbsolutePath;
                Samples = new byte[FrameWidth, FrameHeight, N, 3];
                SegMap = new Image<Bgr, Byte>(FrameWidth, FrameHeight);
                CompareFrames = new byte[FrameWidth, FrameHeight, 2, 3];
                FrameImageBytes = FrameImage.Data;
                SegMapBytes = SegMap.Data;

                //ControlsEnabled(false);

                DialogResult useShakyCameraDetection =
                    MessageBox.Show(
                        "Szeretné a videó háttérleválasztásnál használni a jelenleg kísérleti fázisban levő kameramozgás-észlelést?",
                        "Kameramozgás-észlelés", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (useShakyCameraDetection == DialogResult.Yes)
                {
                    ShakyCamera = true;
                    phi = 2;
                }
                else if (useShakyCameraDetection == DialogResult.No)
                {
                    ShakyCamera = false;
                }

                var outputCreation = new Thread(() =>
                {
                    RemovedBackgroundVideo = new VideoWriter(outputVideoLocation,
                        VideoWriter.Fourcc('m', 'p', '4', 'v'), FPS, new Size(FrameWidth, FrameHeight), true);
                    _selectedVideoFile.Set(CapProp.PosFrames, 0);
                    FrameRead = _selectedVideoFile.QueryFrame();
                    FrameImage = FrameRead.ToImage<Bgr, Byte>();
                    FrameImageBytes = FrameImage.Data;
                    BackgroundModelInitialization();

                    for (var i = 0; i < TotalFrames; i++)
                    {
                        _selectedVideoFile.Set(CapProp.PosFrames, i);
                        FrameImage = _selectedVideoFile.QueryFrame().ToImage<Bgr, Byte>();
                        FrameImageBytes = FrameImage.Data;
                        BackgroundModelUpdate(i);
                        SegMap.Data = SegMapBytes;
                        RemovedBackgroundVideo.Write(SegMap.Convert<Bgr, Byte>().Mat);
                        Invoke(new Action(() =>
                        {
                            _trackBar1.Value = i;
                            progressBar1.PerformStep();
                            _pictureBox1.Source = FrameImage.ToBitmap();
                            _pictureBox2.Source = SegMap.ToBitmap();
                        }));
                    }

                    _isExported = true;
                    RemovedBackgroundVideo.Dispose();
                    _exportedVideoFile = new VideoCapture(outputVideoLocation);
                    OnlyBackground = false;
                    OnlyForeground = false;
                    ShakyCamera = false;
                    phi = 16;
                    MessageBox.Show("A háttérleválasztott videó exportálása sikeres!");
                });
                outputCreation.IsBackground = true;
                outputCreation.Start();
            }
        }
    }

    /*private void ControlsEnabled(bool state)
    {
        videóToolStripMenuItem.Enabled = state;
        feldolgozásToolStripMenuItem.Enabled = state;
        playButton.Enabled = state;
        stopButton.Enabled = state;
        trackBar1.Enabled = state;
    }*/

    /*private void ToolStripMenuReset()
    {
        videóMegnyitásaToolStripMenuItem.HideDropDown();
        feldolgozásToolStripMenuItem.HideDropDown();
    }*/

    private void TimeStampBar_Scroll(object sender, RoutedEventArgs e)
    {
        if (_suppressTrackBarChange || _selectedVideoFile?.Video == null)
        {
            return;
        }

        _currentFrame ??= new Mat();

        var requestedPosition = _trackBar1.Value;
        if (double.IsNaN(requestedPosition) || double.IsInfinity(requestedPosition))
        {
            return;
        }

        _isPlaying = false;
        _playButton.Content = "Play";

        if (_selectedVideoFile.Video.Set(CapProp.PosMsec, requestedPosition) &&
            _selectedVideoFile.Video.Read(_currentFrame) && !_currentFrame.IsEmpty)
        {
            var frameImage = _currentFrame.ToImage<Rgba, Byte>();
            _pictureBox1.Source = CreateBitmapFromPixelData(frameImage.Bytes, frameImage.Width,
                frameImage.Height);
        }
    }

    private void VideoCaptureRemover()
    {
        _isPlaying = false;
        if (_webCamVideo != null)
        {
            _webCamVideo = null;
        }

        if (_selectedVideoFile != null)
        {
            _selectedVideoFile = null;
        }

        if (_exportedVideoFile != null)
        {
            _exportedVideoFile.Dispose();
        }

        _pictureBox1.Source = null;
        _pictureBox2.Source = null;
        GC.Collect();
    }

    private async void VideoFromWebcamToolStripMenuItem_Click(object sender, RoutedEventArgs e)
    {
        VideoCaptureRemover();
        //ToolStripMenuReset();
        StopButton_Click(sender, e);
        Thread webCamCapture = new Thread(() =>
        {
            if (_webCamVideo == null)
            {
                //Invoke(new Action(() => { képkockákLementéseToolStripMenuItem.Enabled = false; }));
                _webCamVideo = new VideoCaptureInfo(new VideoCapture(), true, String.Empty);
                //FrameWidth = Convert.ToInt32(WebCamVideo.Get(CapProp.FrameWidth));
                //FrameHeight = Convert.ToInt32(WebCamVideo.Get(CapProp.FrameHeight));
                _webCamVideo.Video.ImageGrabbed += WebCamVideo_ImageGrabbed;
                _isWebcamBackgroundRemovalOn = false;
                //ShakyCamera = false;
                _isFirstFrame = true;
                _webCamVideo.Video.Start();
                //Samples = new byte[FrameWidth, FrameHeight, N, 3];
                //SegMap = new Image<Bgr, Byte>(FrameWidth, FrameHeight);
            }
            else
            {
                _webCamVideo.Video.ImageGrabbed -= WebCamVideo_ImageGrabbed;
                _webCamVideo.Video.Stop();
                _webCamVideo = null;
                _webCamVideo = null;
                _isWebcamBackgroundRemovalOn = false;
                _isFirstFrame = false;
                //OnlyBackground = false;
                //OnlyForeground = false;
                //Invoke(new Action(() => { képkockákLementéseToolStripMenuItem.Enabled = true; }));
            }
        });
        webCamCapture.Start();
        webCamCapture.IsBackground = true;
        await Task.Delay(5);
        _pictureBox1.Source = null;
        _pictureBox2.Source = null;
    }

    private void WebCamVideo_ImageGrabbed(object? sender, EventArgs e)
    {
        try
        {
            if (_webCamVideo != null)
            {
                _webCamFrame = _webCamVideo.Video.QueryFrame().ToImage<Rgba, Byte>();
                Dispatcher.UIThread.Post(() =>
                    _pictureBox1.Source =
                        CreateBitmapFromPixelData(_webCamFrame.Bytes, _webCamFrame.Width, _webCamFrame.Height));
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
        catch (Exception)
        {
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
        //ToolStripMenuReset();
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null)
        {
            return;
        }

        var openVideoFile = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open video...", AllowMultiple = false, FileTypeFilter = new[] { FilePickerFileTypes.All }
        });
        _pictureBox1.Source = null;
        _pictureBox2.Source = null;
        _isExported = false;
        if (openVideoFile.Count >= 1)
        {
            _selectedVideoFile = new VideoCaptureInfo(new VideoCapture(openVideoFile[0].Path.AbsoluteUri), false,
                openVideoFile[0].Path.AbsoluteUri);
            _currentFrame = new Mat();
            _suppressTrackBarChange = true;
            _trackBar1.Minimum = 0;
            _trackBar1.Maximum = Convert.ToDouble(_selectedVideoFile.TotalDuration);
            _trackBar1.Value = _trackBar1.Minimum;
            _suppressTrackBarChange = false;
            _videoFileName = openVideoFile[0].Name;
            if (_selectedVideoFile.Video.Read(_currentFrame) && !_currentFrame.IsEmpty)
            {
                var frameImage = _currentFrame.ToImage<Rgba, Byte>();
                _pictureBox1.Source = CreateBitmapFromPixelData(frameImage.Bytes, frameImage.Width, frameImage.Height);
                _selectedVideoFile.Video.Set(CapProp.PosFrames, 0);
            }

            _isPlaying = false;
            _playButton.Content = "Play";
            _suppressTrackBarChange = true;
            _trackBar1.Value = _trackBar1.Minimum;
            _suppressTrackBarChange = false;
        }
    }
}