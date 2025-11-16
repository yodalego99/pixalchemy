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
    private Image pictureBox1;
    private Image pictureBox2;
    private Slider trackBar1;
    private Button playButton;
    private bool suppressTrackBarChange;
    public MainView()
    {
        InitializeComponent();
        pictureBox1 = PictureBox1;
        pictureBox2 = PictureBox2;
        trackBar1 = TrackBar1;
        playButton = PlayButton;
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
    VideoCaptureInfo? SelectedVideoFile; // kiválasztott videó (felhasználó adja meg)
    VideoCaptureInfo? WebCamVideo; // webkamera videója
    VideoCapture? ExportedVideoFile; // visszajátszásmiatt van // később át lesz írva
    Image<Rgba, Byte>? WebCamFrame; // webkamera videóinak képkockája
    bool IsWebcamBackgroundRemovalOn = false; // jelzi, hogy be van-e kapcsolva a webkamera háttérleválasztása
    bool IsFirstFrame = false; // ellenőrzi, hogy a kikért képkocka az első-e a webkamerának
    bool IsPlaying = false; // jelzi, hogy lejátszódik-e éppen a videó
    bool IsExported = false; // jelzi, hogy megtörtént-e már a videón a háttérleválasztás
    Mat? CurrentFrame; // jelenlegi frame Mat típusú képe
    Image<Rgba, Byte>? ExportedCurrentFrame; // kiexportált képkocka - videó exportálásánál használjuk 
    string? VideoFileName = string.Empty; // kiválasztott videófájl neve

    private async void PlayVideoFile()
    {
        try
        {
            if (SelectedVideoFile?.Video == null)
            {
                return;
            }

            try
            {
                var currentVideo = SelectedVideoFile;
                CurrentFrame ??= new Mat();

                var frameDelay = SelectedVideoFile.DeltaFrameTime ?? 0d;
                if (double.IsNaN(frameDelay) || double.IsInfinity(frameDelay) || frameDelay <= 0)
                {
                    frameDelay = 33d;
                }

                while (IsPlaying && currentVideo == SelectedVideoFile)
                {
                    if (!currentVideo.Video.Read(CurrentFrame) || CurrentFrame.IsEmpty)
                    {
                        break;
                    }

                    if (IsExported)
                    {
                        if (ExportedVideoFile != null)
                        {
                            ExportedCurrentFrame ??= new Image<Rgba, Byte>(CurrentFrame.Width, CurrentFrame.Height);
                            if (ExportedVideoFile.Read(ExportedCurrentFrame))
                            {
                                pictureBox2.Source = CreateBitmapFromPixelData(ExportedCurrentFrame.Bytes,
                                    ExportedCurrentFrame.Width, ExportedCurrentFrame.Height);
                            }
                        }
                    }

                    var frameImage = CurrentFrame.ToImage<Rgba, Byte>();
                    pictureBox1.Source = CreateBitmapFromPixelData(frameImage.Bytes, frameImage.Width, frameImage.Height);

                    var position = currentVideo.Video.Get(CapProp.PosMsec);
                    if (!double.IsNaN(position) && !double.IsInfinity(position))
                    {
                        suppressTrackBarChange = true;
                        trackBar1.Value = Math.Clamp(position, trackBar1.Minimum, trackBar1.Maximum);
                        suppressTrackBarChange = false;
                    }

                    await Task.Delay((int)Math.Max(1, Math.Min(int.MaxValue, Math.Round(frameDelay))));
                }

                var reachedEnd = currentVideo == SelectedVideoFile;
                IsPlaying = false;
                Dispatcher.UIThread.Post(() =>
                {
                    playButton.Content = "Play";
                    if (reachedEnd && SelectedVideoFile?.Video != null)
                    {
                        var endPosition = SelectedVideoFile.Video.Get(CapProp.PosMsec);
                        if (!double.IsNaN(endPosition) && !double.IsInfinity(endPosition))
                        {
                            suppressTrackBarChange = true;
                            trackBar1.Value = Math.Clamp(endPosition, trackBar1.Minimum, trackBar1.Maximum);
                            suppressTrackBarChange = false;
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
        if (SelectedVideoFile != null)
        {
            if (IsPlaying)
            {
                IsPlaying = false;
                button.Content = "Play";
            }
            else
            {
                IsPlaying = true;
                PlayVideoFile();
                button.Content = "Pause";
            }
        }
    }

    private void StopButton_Click(object? sender, RoutedEventArgs e)
    {
        IsPlaying = false;
        suppressTrackBarChange = true;
        trackBar1.Value = trackBar1.Minimum;
        suppressTrackBarChange = false;
        pictureBox1.Source = null;
        pictureBox2.Source = null;
        playButton.Content = "Play";
        if (SelectedVideoFile != null)
        {
            SelectedVideoFile.Video.Set(CapProp.PosMsec, 0);
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
                    {
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

    /*private void vBackgroundRemovalButton_Click(object sender, RoutedEventArgs e)
    {
        ToolStripMenuReset();
        StopButton_Click(sender, e);
        if (SelectedVideoFile != null)
        {
            SaveFileDialog OutputVideo = new SaveFileDialog();
            IsPlaying = false;
            OutputVideo.Title = "Output videó mentése...";
            OutputVideo.RestoreDirectory = true;
            OutputVideo.AddExtension = true;
            OutputVideo.Filter = "Videó fájlok (*.mp4)|*.mp4|Minden fájl (*.*)|*.*";
            OutputVideo.DefaultExt = "mp4";
            if (OutputVideo.ShowDialog() == DialogResult.OK)
            {
                String OutputVideoLocation = OutputVideo.FileName;
                TotalFrames = Convert.ToInt32(SelectedVideoFile.Get(CapProp.FrameCount));
                FrameHeight = Convert.ToInt32(SelectedVideoFile.Get(CapProp.FrameHeight));
                FrameWidth = Convert.ToInt32(SelectedVideoFile.Get(CapProp.FrameWidth));
                FPS = Convert.ToInt32(SelectedVideoFile.Get(CapProp.Fps));
                Samples = new byte[FrameWidth, FrameHeight, N, 3];
                SegMap = new Image<Bgr, Byte>(FrameWidth, FrameHeight);
                CompareFrames = new byte[FrameWidth, FrameHeight, 2, 3];
                FrameImageBytes = FrameImage.Data;
                SegMapBytes = SegMap.Data;

                ControlsEnabled(false);

                DialogResult UseShakyCameraDetection = MessageBox.Show("Szeretné a videó háttérleválasztásnál használni a jelenleg kísérleti fázisban levő kameramozgás-észlelést?", "Kameramozgás-észlelés", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (UseShakyCameraDetection == DialogResult.Yes)
                {
                    ShakyCamera = true;
                    phi = 2;
                }
                else if (UseShakyCameraDetection == DialogResult.No)
                {
                    ShakyCamera = false;
                }

                label1.Text = "Videó feldolgozottsága: ";
                progressBar1.Visible = true;
                progressBar1.Minimum = 0;
                progressBar1.Maximum = TotalFrames;
                progressBar1.Value = 0;

                Thread OutputCreation = new Thread(() =>
                {
                    RemovedBackgroundVideo = new VideoWriter(OutputVideoLocation, VideoWriter.Fourcc('m', 'p', '4', 'v'), FPS, new Size(FrameWidth, FrameHeight), true);
                    SelectedVideoFile.Set(CapProp.PosFrames, 0);
                    FrameRead = SelectedVideoFile.QueryFrame();
                    FrameImage = FrameRead.ToImage<Bgr, Byte>();
                    FrameImageBytes = FrameImage.Data;
                    BackgroundModelInitialization();

                    for (int i = 0; i < TotalFrames; i++)
                    {
                        SelectedVideoFile.Set(CapProp.PosFrames, i);
                        FrameImage = SelectedVideoFile.QueryFrame().ToImage<Bgr, Byte>();
                        FrameImageBytes = FrameImage.Data;
                        BackgroundModelUpdate(i);
                        SegMap.Data = SegMapBytes;
                        RemovedBackgroundVideo.Write(SegMap.Convert<Bgr, Byte>().Mat);
                        Invoke(new Action(() =>
                        {
                            trackBar1.Value = i;
                            progressBar1.PerformStep();
                            pictureBox1.Source = FrameImage.ToBitmap();
                            pictureBox2.Source = SegMap.ToBitmap();
                        }));
                    }
                    Invoke(new Action(() =>
                    {
                        progressBar1.Visible = false;
                        label1.Text = string.Empty;
                        ControlsEnabled(true);
                    }));
                    IsExported = true;
                    RemovedBackgroundVideo.Dispose();
                    ExportedVideoFile = new VideoCapture(OutputVideoLocation);
                    OnlyBackground = false; OnlyForeground = false;
                    ShakyCamera = false;
                    phi = 16;
                    MessageBox.Show("A háttérleválasztott videó exportálása sikeres!");
                });
                OutputCreation.IsBackground = true;
                OutputCreation.Start();
            }
        }
    }*/

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
        if (suppressTrackBarChange || SelectedVideoFile?.Video == null)
        {
            return;
        }

        CurrentFrame ??= new Mat();

        var requestedPosition = trackBar1.Value;
        if (double.IsNaN(requestedPosition) || double.IsInfinity(requestedPosition))
        {
            return;
        }

        IsPlaying = false;
        playButton.Content = "Play";

        if (SelectedVideoFile.Video.Set(CapProp.PosMsec, requestedPosition) &&
            SelectedVideoFile.Video.Read(CurrentFrame) && !CurrentFrame.IsEmpty)
        {
            var frameImage = CurrentFrame.ToImage<Rgba, Byte>();
            pictureBox1.Source = CreateBitmapFromPixelData(frameImage.Bytes, frameImage.Width,
                frameImage.Height);
        }
    }

    private void VideoCaptureRemover()
    {
        IsPlaying = false;
        if (WebCamVideo != null)
        {
            WebCamVideo = null;
        }

        if (SelectedVideoFile != null)
        {
            SelectedVideoFile = null;
        }

        if (ExportedVideoFile != null)
        {
            ExportedVideoFile.Dispose();
        }

        pictureBox1.Source = null;
        pictureBox2.Source = null;
        GC.Collect();
    }

    private async void VideoFromWebcamToolStripMenuItem_Click(object sender, RoutedEventArgs e)
    {
        VideoCaptureRemover();
        //ToolStripMenuReset();
        StopButton_Click(sender, e);
        Thread WebCamCapture = new Thread(() =>
        {
            if (WebCamVideo == null)
            {
                //Invoke(new Action(() => { képkockákLementéseToolStripMenuItem.Enabled = false; }));
                WebCamVideo = new VideoCaptureInfo( new VideoCapture(), true);
                //FrameWidth = Convert.ToInt32(WebCamVideo.Get(CapProp.FrameWidth));
                //FrameHeight = Convert.ToInt32(WebCamVideo.Get(CapProp.FrameHeight));
                WebCamVideo.Video.ImageGrabbed += WebCamVideo_ImageGrabbed;
                IsWebcamBackgroundRemovalOn = false;
                //ShakyCamera = false;
                IsFirstFrame = true;
                WebCamVideo.Video.Start();
                //Samples = new byte[FrameWidth, FrameHeight, N, 3];
                //SegMap = new Image<Bgr, Byte>(FrameWidth, FrameHeight);
            }
            else
            {
                WebCamVideo.Video.ImageGrabbed -= WebCamVideo_ImageGrabbed;
                WebCamVideo.Video.Stop();
                WebCamVideo = null;
                WebCamVideo = null;
                IsWebcamBackgroundRemovalOn = false;
                IsFirstFrame = false;
                //OnlyBackground = false;
                //OnlyForeground = false;
                //Invoke(new Action(() => { képkockákLementéseToolStripMenuItem.Enabled = true; }));
            }
        });
        WebCamCapture.Start();
        WebCamCapture.IsBackground = true;
        await Task.Delay(5);
        pictureBox1.Source = null;
        pictureBox2.Source = null;
    }

    private void WebCamVideo_ImageGrabbed(object? sender, EventArgs e)
    {
        try
        {
            if (WebCamVideo != null)
            {
                WebCamFrame = WebCamVideo.Video.QueryFrame().ToImage<Rgba, Byte>();
                Dispatcher.UIThread.Post(() => pictureBox1.Source = CreateBitmapFromPixelData(WebCamFrame.Bytes, WebCamFrame.Width, WebCamFrame.Height));
                Random rnd = new Random();
                //FrameImage = WebCamFrame.ToImage<Bgr, Byte>();
                //FrameImageBytes = FrameImage.Data;
                //SegMapBytes = SegMap.Data;
                if (IsWebcamBackgroundRemovalOn && IsFirstFrame)
                {
                    //BackgroundModelInitialization();
                    IsFirstFrame = false;
                }
                else if (IsWebcamBackgroundRemovalOn && !IsFirstFrame)
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
        var OpenVideoFile = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open video...", AllowMultiple = false, FileTypeFilter = new [] { FilePickerFileTypes.All}
        });
        pictureBox1.Source = null;
        pictureBox2.Source = null;
        IsExported = false;
        if (OpenVideoFile.Count >= 1)
        {
            SelectedVideoFile = new VideoCaptureInfo(new VideoCapture(OpenVideoFile[0].Path.AbsoluteUri), false);
            CurrentFrame = new Mat();
            suppressTrackBarChange = true;
            trackBar1.Minimum = 0;
            trackBar1.Maximum = Convert.ToDouble(SelectedVideoFile.TotalDuration);
            trackBar1.Value = trackBar1.Minimum;
            suppressTrackBarChange = false;
            VideoFileName = OpenVideoFile[0].Name;
            if (SelectedVideoFile.Video.Read(CurrentFrame) && !CurrentFrame.IsEmpty)
            {
                var frameImage = CurrentFrame.ToImage<Rgba, Byte>();
                pictureBox1.Source = CreateBitmapFromPixelData(frameImage.Bytes, frameImage.Width, frameImage.Height);
                SelectedVideoFile.Video.Set(CapProp.PosFrames, 0);
            }
            IsPlaying = false;
            playButton.Content = "Play";
            suppressTrackBarChange = true;
            trackBar1.Value = trackBar1.Minimum;
            suppressTrackBarChange = false;
        }
        
    }
}