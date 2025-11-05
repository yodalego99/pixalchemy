using System;
using System.Threading;
using System.Threading.Tasks;
using Gtk;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

using UI = Gtk.Builder.ObjectAttribute;

namespace Kepfeldolgozas
{
    class MainWindow : Window
    {
        [UI] private Label _label1 = null;
        [UI] private Button _button1 = null;

        private int _counter;

        public MainWindow() : this(new Builder("MainWindow.glade"))
        {
        }

        private MainWindow(Builder builder) : base(builder.GetRawOwnedObject("MainWindow"))
        {
            builder.Autoconnect(this);

            DeleteEvent += Window_DeleteEvent;
            //_button1.Clicked += Button1_Clicked;
        }

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            Application.Quit();
        }

        private void Button1_Clicked(object sender, EventArgs a)
        {
            _counter++;
            _label1.Text = "Hello World! This button has been clicked " + _counter + " time(s).";
        }
        
        int ProcessorCount = Environment.ProcessorCount; // processzormagok száma
        VideoCapture? SelectedVideoFile; // kiválasztott videó (felhasználó adja meg)
        VideoCapture? WebCamVideo; // webkamera videója
        VideoCapture? ExportedVideoFile; // visszajátszásmiatt van // később át lesz írva
        Mat WebCamFrame = Mat.Zeros(1, 1, DepthType.Cv8U, 3); // webkamera videóinak képkockája
        bool IsWebcamBackgroundRemovalOn = false; // jelzi, hogy be van-e kapcsolva a webkamera háttérleválasztása
        bool IsFirstFrame = false; // ellenőrzi, hogy a kikért képkocka az első-e a webkamerának
        bool IsPlaying = false; // jelzi, hogy lejátszódik-e éppen a videó
        bool IsExported = false; // jelzi, hogy megtörtént-e már a videón a háttérleválasztás
        int TotalFrames; // videó képkockáinak a száma
        int CurrentFrameNumber; // jelenlegi képkocka sorszáma (kiválasztott videóban melyik képkockánál tart)
        Mat CurrentFrame = Mat.Zeros(1, 1, DepthType.Cv8U, 3); // jelenlegi frame Mat típusú képe
        Mat ExportedCurrentFrame = Mat.Zeros(1, 1, DepthType.Cv8U, 3); // kiexportált képkocka - videó exportálásánál használjuk 
        int FPS; // FPS - képkocka másodpercenként - meghatározza, hogy milyen gyorsan legyenek lejátszva a képkockák - videó sebessége
        String VideoFileName = string.Empty; // kiválasztott videófájl neve

        
        
        /*private async void VideoSelectButton_Click(object sender, EventArgs e)
        {
            StopButton_Click(sender, e);
            ToolStripMenuReset();
            VideoCaptureRemover();
            await Task.Delay(5);
            pictureBox1.Image = null; pictureBox2.Image = null;
            WebCamVideo = null;
            IsExported = false;
            OpenVideoFile.RestoreDirectory = true;
            OpenVideoFile.DefaultExt = "mp4";
            OpenVideoFile.Filter = "Minden fájl (*.*)|*.*";
            OpenVideoFile.FilterIndex = 1;
            OpenVideoFile.Title = "Videó megnyitása...";
            OpenVideoFile.CheckFileExists = true;
            OpenVideoFile.CheckPathExists = true;
            if (OpenVideoFile.ShowDialog() == DialogResult.OK)
            {
                SelectedVideoFile = new VideoCapture(OpenVideoFile.FileName.ToString());
                TotalFrames = Convert.ToInt32(SelectedVideoFile.Get(CapProp.FrameCount));
                FPS = Convert.ToInt32(SelectedVideoFile.Get(CapProp.Fps));
                CurrentFrame = new Mat();
                CurrentFrameNumber = 0;
                trackBar1.Minimum = 0;
                trackBar1.Maximum = TotalFrames;
                trackBar1.Value = 0;
                VideoFileName = Path.GetFileNameWithoutExtension(OpenVideoFile.FileName).ToString();
                pictureBox1.Image = null; 
                PlayVideoFile(); 
            }
            OpenVideoFile.Dispose();
        }

        private async void PlayVideoFile()
        {
            if (SelectedVideoFile == null)
            {
                return;
            }

            try
            {
                while (IsPlaying && CurrentFrameNumber < TotalFrames)
                {
                    SelectedVideoFile.Set(CapProp.PosFrames, CurrentFrameNumber);
                    SelectedVideoFile.Read(CurrentFrame);
                    if (IsExported && ExportedVideoFile != null)
                    {
                        ExportedVideoFile.Set(CapProp.PosFrames, CurrentFrameNumber);
                        ExportedVideoFile.Read(ExportedCurrentFrame);
                        pictureBox2.Image = ExportedCurrentFrame.ToBitmap();
                    }
                    pictureBox1.Image = CurrentFrame.ToBitmap();
                    GC.Collect();
                    trackBar1.Value = CurrentFrameNumber;
                    CurrentFrameNumber++;
                    await Task.Delay(1000 / FPS);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void PlayButton_Click(object sender, EventArgs e)
        {
            if (SelectedVideoFile != null)
            {
                if (IsPlaying)
                {
                    IsPlaying = false;
                    playButton.BackgroundImage = ((System.Drawing.Image)(Properties.Resources.pause_button_1149586));
                }
                else
                {
                    IsPlaying = true;
                    PlayVideoFile();
                    playButton.BackgroundImage = ((System.Drawing.Image)(Properties.Resources.play_button_11495842));
                }
            }
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            IsPlaying = false;
            CurrentFrameNumber = 0;
            trackBar1.Value = 0;
            pictureBox1.Image = null;
            pictureBox1.Invalidate();
            pictureBox2.Image = null;
            pictureBox2.Invalidate();
        }

        private void VideoFrameExportButton_Click(object sender, EventArgs e)
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
        }

        // Háttérleválasztó (ViBe) algoritmus értékei

        // Háttérmodell részletessége - minnél kevesebb, annál gyorsabb, azonban nagyobb az esély a téves foreground pixelek azonosítására
        int N = 20;
        // Két pixel színe közti különbség
        int R = 20;
        // Szükséges egyezések száma, hogy hozzáadja a háttérmodellhez az adott pixelt
        int BgM_min = 2;
        // Ezt azt befolyásolja, hogy milyen hosszú ideig legyenek egyes pixelek a háttérmodell tagjai (minnél nagyobb az érték, annál több a szellem/ jobban látszódik a kameramozgás) - 0-nál nem lehet kisebb
        int phi = 16;
        ///////////////////////////////////////////////////////////////////////////////////////////
        // Adatok
        int FrameWidth, FrameHeight; // Adott videó szélessége/magassága
        // jelenlegi képkocka/kép
        Image<Bgr, Byte> FrameImage = Mat.Zeros(1, 1, DepthType.Cv8U, 1).ToImage<Bgr, Byte>();
        // háttér modellje
        byte[,,,] Samples = new byte[0, 0, 0, 0];
        // szegmentációs térkép - háttérleválasztás eredménye -
        Image<Bgr, Byte> SegMap = Mat.Zeros(1, 1, DepthType.Cv8U, 1).ToImage<Bgr, Byte>();

        byte[,,] FrameImageBytes = new byte[1, 1, 3];
        byte[,,] SegMapBytes = new byte[1, 1, 3];
        Mat FrameRead = new Mat();
        VideoWriter? RemovedBackgroundVideo;

        // háttér és az objektum megkülönböztetésére használt értékek
        byte Background = 0;
        byte Foreground = 255;

        bool OnlyBackground = false;
        bool OnlyForeground = false;


        // Kameramozgás észleléséhez használt értékek/változók
        double FrameDifferencePercentage = 0.125; // Az a minimális hányados, amely alatt újrainicializálja a háttérmodellt
        int MatchCount; // Egyezések száma
        bool ShakyCamera = false; // Kameramozgás észlelése funkció kapcsolója
        byte[,,,] CompareFrames = new byte[1, 1, 3, 2]; // Két egymás utáni frame tárolására van használva

        Random rnd = new Random();

        private void BackgroundModelInitialization()
        {
            for (int k = 0; k < N; k++)
            {
                Parallel.For(0, ProcessorCount, CPUCoreID =>
                {
                    var max = FrameWidth * (CPUCoreID + 1) / ProcessorCount;
                    for (int x = FrameWidth * CPUCoreID / ProcessorCount; x < max; x++)
                    {
                        for (int y = 0; y < FrameHeight; y++)
                        {
                            if (ShakyCamera)
                            {
                                CompareFrames[x, y, 0, 0] = FrameImageBytes[y, x, 0];
                                CompareFrames[x, y, 0, 1] = FrameImageBytes[y, x, 1];
                                CompareFrames[x, y, 0, 2] = FrameImageBytes[y, x, 2];
                            }
                            Samples[x, y, k, 0] = FrameImageBytes[y, x, 0];
                            Samples[x, y, k, 1] = FrameImageBytes[y, x, 1];
                            Samples[x, y, k, 2] = FrameImageBytes[y, x, 2];
                        }
                    }
                });
            }
        }
        private void BackgroundModelUpdate(int i)
        {
            Parallel.For(0, ProcessorCount, CPUCoreID =>
            {
                var max = FrameWidth * (CPUCoreID + 1) / ProcessorCount;
                for (int x = FrameWidth * CPUCoreID / ProcessorCount; x < max; x++)
                {
                    for (int y = 0; y < FrameHeight; y++)
                    {
                        int count = 0;
                        int index = 0;
                        int db, dg, dr = 0;
                        if (i % 2 == 0 && i != 0 && ShakyCamera)
                        {
                            CompareFrames[x, y, 0, 0] = FrameImageBytes[y, x, 0];
                            CompareFrames[x, y, 0, 1] = FrameImageBytes[y, x, 1];
                            CompareFrames[x, y, 0, 2] = FrameImageBytes[y, x, 2];
                            if ((0.11d * CompareFrames[x, y, 0, 0] + 0.59d * CompareFrames[x, y, 0, 1] + 0.3d * CompareFrames[x, y, 0, 2]) == (0.11d * CompareFrames[x, y, 1, 0] + 0.59d * CompareFrames[x, y, 1, 1] + 0.3d * CompareFrames[x, y, 1, 2]))
                            {
                                MatchCount++;
                            }
                        }
                        else if (i % 2 == 1 && ShakyCamera)
                        {
                            CompareFrames[x, y, 1, 0] = FrameImageBytes[y, x, 0];
                            CompareFrames[x, y, 1, 1] = FrameImageBytes[y, x, 1];
                            CompareFrames[x, y, 1, 2] = FrameImageBytes[y, x, 2];
                            if ((0.11d * CompareFrames[x, y, 0, 0] + 0.59d * CompareFrames[x, y, 0, 1] + 0.3d * CompareFrames[x, y, 0, 2]) == (0.11d * CompareFrames[x, y, 1, 0] + 0.59d * CompareFrames[x, y, 1, 1] + 0.3d * CompareFrames[x, y, 1, 2]))
                            {
                                MatchCount++;
                            }
                        }
                        while ((count < BgM_min) && (index < N))
                        {
                            db = (int)Math.Abs(FrameImageBytes[y, x, 0] - Samples[x, y, index, 0]);
                            dg = (int)Math.Abs(FrameImageBytes[y, x, 1] - Samples[x, y, index, 1]);
                            dr = (int)Math.Abs(FrameImageBytes[y, x, 2] - Samples[x, y, index, 2]);
                            if (db < R && dg < R && dr < R)
                            {
                                count++;
                            }
                            index++;
                        }
                        if (count >= BgM_min)
                        {
                            if (OnlyBackground)
                            {
                                SegMapBytes[y, x, 0] = FrameImageBytes[y, x, 0];
                                SegMapBytes[y, x, 1] = FrameImageBytes[y, x, 1];
                                SegMapBytes[y, x, 2] = FrameImageBytes[y, x, 2];
                            }
                            else
                            {
                                SegMapBytes[y, x, 0] = Background;
                                SegMapBytes[y, x, 1] = Background;
                                SegMapBytes[y, x, 2] = Background;
                            }
                            int rand = rnd.Next(0, phi - 1);
                            if (rand == 0)
                            {
                                rand = rnd.Next(0, N - 1);
                                Samples[x, y, rand, 0] = FrameImageBytes[y, x, 0];
                                Samples[x, y, rand, 1] = FrameImageBytes[y, x, 1];
                                Samples[x, y, rand, 2] = FrameImageBytes[y, x, 2];
                            }
                            rand = rnd.Next(0, phi - 1);
                            if (rand == 0)
                            {
                                int xNG, yNG;
                                rand = rnd.Next(0, N - 1);
                                xNG = getRandomNghbPixel(x);
                                yNG = getRandomNghbPixel(y);
                                Samples[xNG, yNG, rand, 0] = FrameImageBytes[y, x, 0];
                                Samples[xNG, yNG, rand, 1] = FrameImageBytes[y, x, 1];
                                Samples[xNG, yNG, rand, 2] = FrameImageBytes[y, x, 2];
                            }
                        }
                        else
                        {
                            if (OnlyForeground)
                            {
                                SegMapBytes[y, x, 0] = FrameImageBytes[y, x, 0];
                                SegMapBytes[y, x, 1] = FrameImageBytes[y, x, 1];
                                SegMapBytes[y, x, 2] = FrameImageBytes[y, x, 2];
                            }
                            else if (OnlyBackground)
                            {
                                if ((x + y) % 2 == 0)
                                {
                                    SegMapBytes[y, x, 0] = 255;
                                    SegMapBytes[y, x, 1] = 0;
                                    SegMapBytes[y, x, 2] = 255;

                                }
                                else
                                {
                                    SegMapBytes[y, x, 0] = Background;
                                    SegMapBytes[y, x, 1] = Background;
                                    SegMapBytes[y, x, 2] = Background;
                                }
                            }
                            else
                            {
                                SegMapBytes[y, x, 0] = Foreground;
                                SegMapBytes[y, x, 1] = Foreground;
                                SegMapBytes[y, x, 2] = Foreground;
                            }
                        }
                    }
                }
            });
            if ((double)(MatchCount) / (double)(FrameWidth * FrameHeight) < FrameDifferencePercentage && ShakyCamera)
            {
                BackgroundModelInitialization();
            }
            MatchCount = 0;
        }

        private void vBackgroundRemovalButton_Click(object sender, EventArgs e)
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
                                pictureBox1.Image = FrameImage.ToBitmap();
                                pictureBox2.Image = SegMap.ToBitmap();
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
        }

        private int getRandomNghbPixel(int coord)
        {
            int[] Var = { -1, 0, 1 };

            Random rnd = new Random();

            if (coord == (FrameHeight - 1) || (coord == FrameWidth - 1))
            {
                return coord;
            }
            else if (coord == 0)
            {
                return coord;
            }
            else
            {
                return coord + Var[rnd.Next(3)];
            }
        }

        private void ControlsEnabled(bool state)
        {
            videóToolStripMenuItem.Enabled = state;
            feldolgozásToolStripMenuItem.Enabled = state;
            playButton.Enabled = state;
            stopButton.Enabled = state;
            trackBar1.Enabled = state;
        }

        private void ToolStripMenuReset()
        {
            videóMegnyitásaToolStripMenuItem.HideDropDown();
            feldolgozásToolStripMenuItem.HideDropDown();
        }

        private void TimeStampBar_Scroll(object sender, EventArgs e)
        {
            if (SelectedVideoFile != null)
            {
                CurrentFrameNumber = trackBar1.Value;
            }
        }

        private void VideoCaptureRemover()
        {
            if (WebCamVideo != null)
            {
                WebCamVideo.ImageGrabbed -= WebCamVideo_ImageGrabbed;
                WebCamVideo.Stop();
                WebCamVideo.Dispose();
                WebCamVideo = null;
            }
            if (SelectedVideoFile != null)
            {
                SelectedVideoFile.Dispose();
            }
            if (ExportedVideoFile != null)
            {
                ExportedVideoFile.Dispose();
            }
            pictureBox1.Image = null;
            pictureBox2.Image = null;
        }

        private async void VideoFromWebcamToolStripMenuItem_Click(object sender, EventArgs e)
        {
            VideoCaptureRemover();
            SelectedVideoFile = null;
            ToolStripMenuReset();
            StopButton_Click(sender, e);
            Thread WebCamCapture = new Thread(() =>
            {
                if (WebCamVideo == null)
                {
                    Invoke(new Action(() =>
                    {
                        képkockákLementéseToolStripMenuItem.Enabled = false;
                    }));
                    WebCamVideo = new VideoCapture(0);
                    FrameWidth = Convert.ToInt32(WebCamVideo.Get(CapProp.FrameWidth));
                    FrameHeight = Convert.ToInt32(WebCamVideo.Get(CapProp.FrameHeight));
                    WebCamVideo.ImageGrabbed += WebCamVideo_ImageGrabbed;
                    IsWebcamBackgroundRemovalOn = false;
                    ShakyCamera = false;
                    IsFirstFrame = true;
                    WebCamVideo.Start();
                    Samples = new byte[FrameWidth, FrameHeight, N, 3];
                    SegMap = new Image<Bgr, Byte>(FrameWidth, FrameHeight);
                }
                else
                {
                    WebCamVideo.ImageGrabbed -= WebCamVideo_ImageGrabbed;
                    WebCamVideo.Stop();
                    WebCamVideo.Dispose();
                    WebCamVideo = null;
                    IsWebcamBackgroundRemovalOn = false;
                    IsFirstFrame = false;
                    OnlyBackground = false; OnlyForeground = false;
                    Invoke(new Action(() =>
                    {
                        képkockákLementéseToolStripMenuItem.Enabled = true;
                    }));
                }
            });
            WebCamCapture.Start();
            WebCamCapture.IsBackground = true;
            await Task.Delay(5);
            pictureBox1.Image = null; pictureBox2.Image = null;
        }

        private void WebCamVideo_ImageGrabbed(object? sender, EventArgs e)
        {
            try
            {
                if (WebCamVideo != null)
                {
                    WebCamFrame = WebCamVideo.QueryFrame();
                    pictureBox1.Image = WebCamFrame.ToBitmap();
                    Random rnd = new Random();
                    FrameImage = WebCamFrame.ToImage<Bgr, Byte>();
                    FrameImageBytes = FrameImage.Data;
                    SegMapBytes = SegMap.Data;
                    if (IsWebcamBackgroundRemovalOn && IsFirstFrame)
                    {
                        BackgroundModelInitialization();
                        IsFirstFrame = false;
                    }
                    else if (IsWebcamBackgroundRemovalOn && !IsFirstFrame)
                    {
                        BackgroundModelUpdate(1);
                        SegMap.Data = SegMapBytes;
                        Invoke(new Action(() =>
                        {
                            pictureBox2.Image = SegMap.ToBitmap();
                        }));
                    }
                    GC.Collect();
                }
            }
            catch (Exception)
            {

            }
        }

        private void OnlyForegroundToolStripMenuItem_Click(object sender, EventArgs e)
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

        private void OnlyBackgroundToolStripMenuItem_Click(object sender, EventArgs e)
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

        private void SegmentationMapToolStripMenuItem_Click(object sender, EventArgs e)
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
    }
}