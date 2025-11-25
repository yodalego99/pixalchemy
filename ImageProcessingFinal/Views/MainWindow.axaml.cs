using Avalonia.Controls;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using ImageProcessingFinal.Messages;
using ImageProcessingFinal.ViewModels;
using Size = System.Drawing.Size;

namespace ImageProcessingFinal.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        WeakReferenceMessenger.Default.Register<MainWindow, TestMessage>(this, (w, m) =>
        {
            var dialog = new TestDialog
            {
                DataContext = new TestDialogViewModel()
            };
            m.Reply(dialog.ShowDialog<TestDialogViewModel>(w));
        });
    }

    private bool _suppressTrackBarChange;

    private static WriteableBitmap CreateBitmapFromPixelData(
        byte[] rgbPixelData,
        int width,
        int height
    )
    {
        // Standard - maybe it needs to be changed on some devices
        var dpi = new Vector(96, 96);

        var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            dpi,
            Avalonia.Platform.PixelFormats.Bgr24
        );
        using var frameBuffer = bitmap.Lock();
        Marshal.Copy(rgbPixelData, 0, frameBuffer.Address, rgbPixelData.Length);

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
    Mat? _exportedCurrentFrame; // kiexportált képkocka - videó exportálásánál használjuk
    ViBe _viBeProcess;
    

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
                        if (_exportedVideoFile?.Video != null && !_exportedVideoFile.Video.Grab() &&
                            _exportedVideoFile?.FilePath != String.Empty)
                        {
                            _exportedVideoFile.Video = new VideoCapture(_exportedVideoFile.FilePath);
                        }

                        if (_exportedVideoFile != null)
                        {
                            _exportedCurrentFrame ??= new Mat();
                            if (_exportedVideoFile.Video.Retrieve(_exportedCurrentFrame))
                            {
                                var exportedFrameImage = _exportedCurrentFrame.ToImage<Bgr, byte>();
                                PictureBox2.Source =
                                    CreateBitmapFromPixelData(exportedFrameImage.Bytes, exportedFrameImage.Width,
                                        exportedFrameImage.Height);
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

        if (_exportedVideoFile != null)
        {
            _exportedVideoFile.Video.Set(CapProp.PosMsec, 0.0);
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
                var outputVideoLocation = outputVideo.Path.LocalPath;

                ControlsEnabled(false);

                var outputCreation = new Thread(() =>
                {
                    var removedBackgroundVideo = new VideoWriter(
                        outputVideoLocation, VideoWriter.Fourcc('m', 'p', '4', 'v'),
                        (double)_selectedVideoFile.Fps,
                        new Size(_selectedVideoFile.Video.Width, _selectedVideoFile.Video.Height),
                        true
                    );
                    _viBeProcess = new ViBe().WithDefaults();
                    _currentFrame = _selectedVideoFile.Video.QueryFrame();
                    _viBeProcess.FrameImage = _currentFrame.ToImage<Rgb, byte>();
                    _viBeProcess.BackgroundModelInitialization();
                    var counter = 0;
                    while (_selectedVideoFile.Video.Grab())
                    {
                        _selectedVideoFile.Video.Retrieve(_currentFrame);
                        var frameImage = _currentFrame.ToImage<Rgb, byte>();
                        _viBeProcess.FrameImage = frameImage;
                        _viBeProcess.BackgroundModelUpdate(counter);
                        var segmapImage = new Image<Rgb, byte>(_viBeProcess._segMapBytes);
                        removedBackgroundVideo.Write(segmapImage);
                        counter = (counter + 1) % 2;
                        Dispatcher.UIThread.Post(() =>
                        {
                            PictureBox1.Source =
                                CreateBitmapFromPixelData(frameImage.Bytes, frameImage.Width, frameImage.Height);
                            PictureBox2.Source = CreateBitmapFromPixelData(segmapImage.Bytes, segmapImage.Width,
                                segmapImage.Height);
                        });
                    }

                    _isExported = true;
                    Dispatcher.UIThread.Post(() =>
                    {
                        removedBackgroundVideo.Dispose();
                        _exportedVideoFile = new VideoCaptureInfo(new VideoCapture(outputVideoLocation), false,
                            outputVideoLocation);
                        ControlsEnabled(true);
                        StopButton_Click(sender, e);
                    });
                });
                outputCreation.IsBackground = true;
                outputCreation.Start();
            }
        }
        else
        {
            _isWebcamBackgroundRemovalOn = true;
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

        if (_exportedVideoFile != null
            && _exportedVideoFile.Video.Set(CapProp.PosMsec, requestedPosition)
            && _exportedVideoFile.Video.Read(_exportedCurrentFrame)
            && _exportedCurrentFrame is not null)
        {
            var exportedFrameImage = _exportedCurrentFrame;
            PictureBox2.Source = CreateBitmapFromPixelData(
                exportedFrameImage.GetRawData(),
                exportedFrameImage.Width,
                exportedFrameImage.Height
            );
        }
    }

    private void VideoCaptureRemover()
    {
        _isPlaying = false;
        _isExported = false;
        _isWebcamBackgroundRemovalOn = false;
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
                else if (_webCamVideo.Video != null)
                {
                    _webCamVideo.Video.ImageGrabbed -= WebCamVideo_ImageGrabbed;
                    _webCamVideo.Video.Stop();
                    _webCamVideo.Video.Dispose();
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
                if (_isWebcamBackgroundRemovalOn && _isFirstFrame)
                {
                    _viBeProcess = new ViBe().WithDefaults();
                    _exportedCurrentFrame = _webCamVideo.Video.QueryFrame();
                    _viBeProcess.FrameImage = _exportedCurrentFrame.ToImage<Rgb, byte>();
                    _viBeProcess.BackgroundModelInitialization();
                    _isFirstFrame = false;
                }
                else if (_isWebcamBackgroundRemovalOn && !_isFirstFrame)
                {
                    _exportedCurrentFrame = _webCamVideo.Video.QueryFrame();
                    _viBeProcess.FrameImage = _exportedCurrentFrame.ToImage<Rgb, byte>();
                    _viBeProcess.BackgroundModelUpdate(1);
                    var segmapImage = new Image<Rgb, byte>(_viBeProcess._segMapBytes);
                    Dispatcher.UIThread.Post(() =>
                    {
                        PictureBox2.Source = CreateBitmapFromPixelData(segmapImage.Bytes, segmapImage.Width,
                            segmapImage.Height);
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private async void OpenDialogWithView(object? sender, RoutedEventArgs e)
    {
        var test = await WeakReferenceMessenger.Default.Send(new TestMessage());
    }

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
            var selectedVideoLocation = openVideoFile[0].Path.LocalPath;
            _selectedVideoFile = new VideoCaptureInfo(new VideoCapture(selectedVideoLocation), false,
                selectedVideoLocation);
            _currentFrame = new Mat();
            _suppressTrackBarChange = true;
            TrackBar1.Minimum = 0;
            TrackBar1.Maximum = Convert.ToDouble(_selectedVideoFile.TotalDuration);
            TrackBar1.Value = TrackBar1.Minimum;
            _suppressTrackBarChange = false;
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