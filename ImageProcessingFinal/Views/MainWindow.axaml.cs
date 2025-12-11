using Avalonia.Controls;
using System;
using System.IO;
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
            var dialog = new ViBeSettingsDialog
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

    private MosaicProcessor EnsureMosaicProcessor()
    {
        _mosaicProcessor ??= new MosaicProcessor(GetMosaicTileDirectory(), DefaultMosaicTileSize);
        _mosaicProcessor.EnsureTileLibraryLoaded();
        return _mosaicProcessor;
    }

    private string GetMosaicTileDirectory()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, MosaicTilesFolderName);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private void CancelParticleMorphAnimation()
    {
        if (_particleMorphCts == null)
        {
            return;
        }

        try
        {
            _particleMorphCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            _particleMorphCts.Dispose();
            _particleMorphCts = null;
        }
    }

    private FilePickerSaveOptions BuildVideoSaveOptions(string title, string suffix)
    {
        return new FilePickerSaveOptions
        {
            Title = title,
            FileTypeChoices = VideoSaveFileTypes,
            DefaultExtension = "mp4",
            SuggestedFileName = $"{GetBaseOutputFileName()}-{suffix}"
        };
    }

    private string GetBaseOutputFileName()
    {
        if (!string.IsNullOrWhiteSpace(_selectedVideoFile?.FilePath))
        {
            var baseName = Path.GetFileNameWithoutExtension(_selectedVideoFile.FilePath);
            if (!string.IsNullOrWhiteSpace(baseName))
            {
                return baseName;
            }
        }

        return "output";
    }

    private static string EnsureVideoExtension(string path, string fallbackExtension = ".mp4")
    {
        if (string.IsNullOrWhiteSpace(Path.GetExtension(path)))
        {
            return path + fallbackExtension;
        }

        return path;
    }

    private static int ResolveFourCcForPath(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".avi" => VideoWriter.Fourcc('M', 'J', 'P', 'G'),
            _ => VideoWriter.Fourcc('m', 'p', '4', 'v')
        };
    }

    private async Task<ViBeSettingsViewModel?> ShowViBeSettingsDialogAsync()
    {
        var dialog = new ViBeSettingsDialog
        {
            DataContext = new ViBeSettingsViewModel()
        };

        return await dialog.ShowDialog<ViBeSettingsViewModel?>(this);
    }

    private async Task<ParticleMorphSettingsViewModel?> ShowParticleMorphSettingsDialogAsync()
    {
        var dialog = new ParticleMorphSettingsDialog
        {
            DataContext = new ParticleMorphSettingsViewModel()
        };

        return await dialog.ShowDialog<ParticleMorphSettingsViewModel?>(this);
    }

    private async Task StartParticleMorphAsync(string targetPath, string sourcePath, int particleSize, string? videoOutputPath)
    {
        CancelParticleMorphAnimation();
        await Task.Run(async () =>
        {
            try
            {
                using var targetImage = new Image<Bgr, byte>(targetPath);
                using var sourceImage = new Image<Bgr, byte>(sourcePath);
                using var resizedSource = sourceImage.Resize(targetImage.Width, targetImage.Height, Inter.Area);

                var targetBytes = targetImage.Bytes;
                var targetWidth = targetImage.Width;
                var targetHeight = targetImage.Height;
                var sourceBytes = resizedSource.Bytes;

                Dispatcher.UIThread.Post(() =>
                {
                    PictureBox1.Source = CreateBitmapFromPixelData(targetBytes, targetWidth, targetHeight);
                    PictureBox2.Source = CreateBitmapFromPixelData(sourceBytes, targetWidth, targetHeight);
                });

                var processor = new ParticleMorphProcessor(particleSize: particleSize, totalSteps: ParticleMorphDefaultSteps);
                processor.Initialize(resizedSource, targetImage);

                var cts = new CancellationTokenSource();
                _particleMorphCts = cts;
                var token = cts.Token;

                VideoWriter? morphVideo = null;
                try
                {
                    if (!string.IsNullOrWhiteSpace(videoOutputPath))
                    {
                        var outputPath = EnsureVideoExtension(videoOutputPath);
                        var fourCc = ResolveFourCcForPath(outputPath);
                        var fps = Math.Max(1.0, 1000.0 / processor.FrameDelayMilliseconds);
                        morphVideo = new VideoWriter(
                            outputPath,
                            fourCc,
                            fps,
                            new Size(targetWidth, targetHeight),
                            true
                        );
                    }

                    for (var frameIndex = 0; frameIndex < processor.TotalSteps; frameIndex++)
                    {
                        token.ThrowIfCancellationRequested();
                        using var frame = processor.RenderFrame(frameIndex);
                        morphVideo?.Write(frame.Mat);

                        var frameBytes = frame.Bytes;
                        var frameWidth = frame.Width;
                        var frameHeight = frame.Height;

                        Dispatcher.UIThread.Post(() =>
                        {
                            PictureBox2.Source = CreateBitmapFromPixelData(frameBytes, frameWidth, frameHeight);
                        });

                        await Task.Delay(processor.FrameDelayMilliseconds, token);

                        if (morphVideo != null && (frameIndex == 0 || frameIndex == processor.TotalSteps - 1))
                        {
                            for (var hold = 0; hold < ParticleMorphHoldFrameCount; hold++)
                            {
                                morphVideo.Write(frame.Mat);
                            }
                        }
                    }
                }
                finally
                {
                    morphVideo?.Dispose();
                    if (ReferenceEquals(_particleMorphCts, cts))
                    {
                        _particleMorphCts.Dispose();
                        _particleMorphCts = null;
                    }
                    else
                    {
                        cts.Dispose();
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        });
    }

    VideoCaptureInfo? _selectedVideoFile; // kiválasztott videó (felhasználó adja meg)
    VideoCaptureInfo? _webCamVideo; // webkamera videója
    VideoCaptureInfo? _exportedVideoFile; // visszajátszásmiatt van // később át lesz írva
    Image<Bgr, byte>? _webCamFrame; // webkamera videóinak képkockája
    bool _isWebcamBackgroundRemovalOn = false; // jelzi, hogy be van-e kapcsolva a webkamera háttérleválasztása
    bool _isFirstFrame = false; // ellenőrzi, hogy a kikért képkocka az első-e a webkamerának
    bool _isPlaying = false; // jelzi, hogy lejátszódik-e éppen a videó
    bool _isExported = false; // jelzi, hogy megtörtént-e már a videón a háttérleválasztás
    bool _isWebcamMosaicOn = false;
    Mat? _currentFrame; // jelenlegi frame Mat típusú képe
    Mat? _exportedCurrentFrame; // kiexportált képkocka - videó exportálásánál használjuk
    ViBe _viBeProcess;
    MosaicProcessor? _mosaicProcessor;
    private const string MosaicTilesFolderName = "MosaicTiles";
    private const int DefaultMosaicTileSize = 24;
    private static readonly FilePickerFileType VideoOpenFileType = new("Video files")
    {
        Patterns = new[] { "*.mp4", "*.mov", "*.avi", "*.mkv", "*.wmv" },
        AppleUniformTypeIdentifiers = new[] { "public.movie" },
        MimeTypes = new[] { "video/mp4", "video/quicktime", "video/x-msvideo", "video/x-matroska", "video/x-ms-wmv" }
    };
    private static readonly FilePickerFileType Mp4SaveFileType = new("MP4 video (*.mp4)")
    {
        Patterns = new[] { "*.mp4" },
        AppleUniformTypeIdentifiers = new[] { "public.mpeg-4" },
        MimeTypes = new[] { "video/mp4" }
    };
    private static readonly FilePickerFileType AviSaveFileType = new("AVI video (*.avi)")
    {
        Patterns = new[] { "*.avi" },
        AppleUniformTypeIdentifiers = new[] { "public.avi" },
        MimeTypes = new[] { "video/x-msvideo" }
    };
    private static readonly FilePickerFileType[] VideoSaveFileTypes = { Mp4SaveFileType, AviSaveFileType };
    private static readonly FilePickerFileType ImageFileType = new("Image files")
    {
        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.tif", "*.tiff" },
        AppleUniformTypeIdentifiers = new[] { "public.image" },
        MimeTypes = new[]
        {
            "image/png", "image/jpeg", "image/bmp", "image/gif", "image/tiff"
        }
    };
    private CancellationTokenSource? _particleMorphCts;
    private const int ParticleMorphDefaultSteps = 90;
    private const int ParticleMorphHoldFrameCount = 100;
    private ViBeSettingsViewModel? _viBeSettings;
    

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
                CancelParticleMorphAnimation();
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
        CancelParticleMorphAnimation();
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

        // Show ViBe settings dialog
        var settings = await ShowViBeSettingsDialogAsync();
        if (settings == null)
        {
            return;
        }
        _viBeSettings = settings;

        if (_selectedVideoFile != null)
        {
            _isPlaying = false;
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider == null)
            {
                return;
            }

            var outputVideo = await topLevel.StorageProvider.SaveFilePickerAsync(
                BuildVideoSaveOptions("Save video...", "vibe")
            );
            if (outputVideo is not null)
            {
                var outputVideoLocation = EnsureVideoExtension(outputVideo.Path.LocalPath);
                var outputFourCc = ResolveFourCcForPath(outputVideoLocation);

                ControlsEnabled(false);

                var shakyCamera = settings.EnableShakyCamera;
                var segmapType = settings.SelectedSegmapType;

                var outputCreation = new Thread(() =>
                {
                    var removedBackgroundVideo = new VideoWriter(
                        outputVideoLocation, outputFourCc,
                        (double)_selectedVideoFile.Fps,
                        new Size(_selectedVideoFile.Video.Width, _selectedVideoFile.Video.Height),
                        true
                    );
                    _viBeProcess = new ViBe().WithDefaults();
                    _viBeProcess.ShakyCamera = shakyCamera;
                    _viBeProcess.SegmapType = segmapType;
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

                    _selectedVideoFile.Video.Set(CapProp.PosFrames, 0);
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
            _isWebcamMosaicOn = false;
            _isFirstFrame = true;
        }
    }

    private async void MosaicEffectButton_Click(object sender, RoutedEventArgs e)
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
                BuildVideoSaveOptions("Save mosaic video...", "mosaic")
            );
            if (outputVideo is null)
            {
                return;
            }

            var outputVideoLocation = EnsureVideoExtension(outputVideo.Path.LocalPath);
            var outputFourCc = ResolveFourCcForPath(outputVideoLocation);
            ControlsEnabled(false);

            var outputCreation = new Thread(() =>
            {
                VideoWriter? mosaicVideo = null;
                try
                {
                    var mosaicProcessor = EnsureMosaicProcessor();
                    mosaicVideo = new VideoWriter(
                        outputVideoLocation, outputFourCc,
                        (double)_selectedVideoFile.Fps,
                        new Size(_selectedVideoFile.Video.Width, _selectedVideoFile.Video.Height),
                        true
                    );

                    _currentFrame ??= new Mat();
                    _selectedVideoFile.Video.Set(CapProp.PosFrames, 0);
                    while (_selectedVideoFile.Video.Read(_currentFrame))
                    {
                        if (_currentFrame.IsEmpty)
                        {
                            break;
                        }

                        var frameImage = _currentFrame.ToImage<Bgr, byte>();
                        var mosaicImage = mosaicProcessor.BuildMosaic(frameImage);
                        mosaicVideo.Write(mosaicImage);

                        var frameBytes = frameImage.Bytes;
                        var frameWidth = frameImage.Width;
                        var frameHeight = frameImage.Height;
                        var mosaicBytes = mosaicImage.Bytes;
                        var mosaicWidth = mosaicImage.Width;
                        var mosaicHeight = mosaicImage.Height;

                        Dispatcher.UIThread.Post(() =>
                        {
                            PictureBox1.Source =
                                CreateBitmapFromPixelData(frameBytes, frameWidth, frameHeight);
                            PictureBox2.Source =
                                CreateBitmapFromPixelData(mosaicBytes, mosaicWidth, mosaicHeight);
                        });

                        frameImage.Dispose();
                        mosaicImage.Dispose();
                    }

                    _isExported = true;
                    Dispatcher.UIThread.Post(() =>
                    {
                        _exportedVideoFile = new VideoCaptureInfo(new VideoCapture(outputVideoLocation), false,
                            outputVideoLocation);
                        ControlsEnabled(true);
                        StopButton_Click(sender, new RoutedEventArgs());
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Dispatcher.UIThread.Post(() =>
                    {
                        ControlsEnabled(true);
                        StopButton_Click(sender, new RoutedEventArgs());
                    });
                }
                finally
                {
                    mosaicVideo?.Dispose();
                }
            });
            outputCreation.IsBackground = true;
            outputCreation.Start();
        }
        else
        {
            _isWebcamMosaicOn = true;
            _isWebcamBackgroundRemovalOn = false;
        }
    }

    private async void ParticleMorphButton_Click(object sender, RoutedEventArgs e)
    {
        ToolStripMenuReset();
        StopButton_Click(sender, e);
        CancelParticleMorphAnimation();
        VideoCaptureRemover();
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null)
        {
            return;
        }

        var targetSelection = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select target image...",
            AllowMultiple = false,
            FileTypeFilter = new[] { ImageFileType, FilePickerFileTypes.All }
        });
        if (targetSelection.Count == 0)
        {
            return;
        }

        var sourceSelection = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select source image...",
            AllowMultiple = false,
            FileTypeFilter = new[] { ImageFileType, FilePickerFileTypes.All }
        });
        if (sourceSelection.Count == 0)
        {
            return;
        }

        var targetPath = targetSelection[0].Path.LocalPath;
        var sourcePath = sourceSelection[0].Path.LocalPath;
        var settings = await ShowParticleMorphSettingsDialogAsync();
        if (settings == null)
        {
            return;
        }

        string? videoPath = null;
        if (settings.SaveAsVideo)
        {
            var saveFile = await topLevel.StorageProvider.SaveFilePickerAsync(
                BuildVideoSaveOptions("Save morph video...", "morph")
            );
            if (saveFile is null)
            {
                return;
            }

            videoPath = EnsureVideoExtension(saveFile.Path.LocalPath);
        }

        await StartParticleMorphAsync(targetPath, sourcePath, settings.ParticleSize, videoPath);
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
        _isWebcamMosaicOn = false;
        CancelParticleMorphAnimation();
        if (_webCamVideo?.Video != null)
        {
            _webCamVideo.Video.Stop();
            _webCamVideo.Video.Dispose();
        }
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
                    _isWebcamMosaicOn = false;
                    _isFirstFrame = true;
                    _webCamVideo.Video.Start();
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
                    if (_viBeSettings != null)
                    {
                        _viBeProcess.ShakyCamera = _viBeSettings.EnableShakyCamera;
                        _viBeProcess.SegmapType = _viBeSettings.SelectedSegmapType;
                    }
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
                else if (_isWebcamMosaicOn)
                {
                    try
                    {
                        var mosaicProcessor = EnsureMosaicProcessor();
                        using var frameCopy = _webCamFrame.Clone();
                        using var mosaicImage = mosaicProcessor.BuildMosaic(frameCopy);
                        var mosaicBytes = mosaicImage.Bytes;
                        var mosaicWidth = mosaicImage.Width;
                        var mosaicHeight = mosaicImage.Height;
                        Dispatcher.UIThread.Post(() =>
                        {
                            PictureBox2.Source = CreateBitmapFromPixelData(mosaicBytes, mosaicWidth, mosaicHeight);
                        });
                    }
                    catch (Exception mosaicEx)
                    {
                        Console.WriteLine(mosaicEx.Message);
                    }
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
            Title = "Open video...",
            AllowMultiple = false,
            FileTypeFilter = new[] { VideoOpenFileType, FilePickerFileTypes.All }
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