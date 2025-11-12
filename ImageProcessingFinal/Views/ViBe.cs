using System;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace ImageProcessingFinal.Views;



public class ViBe
{
    int ProcessorCount = Environment.ProcessorCount; // processzormagok száma
    // The fidelity of the background model
    int N = 20;

    // Distance between two pixels colour in color space
    int R = 20;

    // Required matches to be added to the background model
    int BgM_min = 2;

    // Rate of decay - bigger values tend to cause ghosting
    int phi = 16;
    
    int FrameWidth, FrameHeight;
    Image<Bgr, Byte> FrameImage = Mat.Zeros(1, 1, DepthType.Cv8U, 1).ToImage<Bgr, Byte>();

    // Background model
    byte[,,,] Samples = new byte[0, 0, 0, 0];
    // Segmentation map - result of the ViBe background removal operation
    private Image<Bgr, Byte> SegMap;
    byte[,,] FrameImageBytes = new byte[1, 1, 3];
    byte[,,] SegMapBytes = new byte[1, 1, 3];
    Mat FrameRead = new Mat();
    VideoWriter? RemovedBackgroundVideo;

    byte Background = byte.MinValue;
    byte Foreground = byte.MaxValue;

    bool OnlyBackground = false;
    bool OnlyForeground = false;
    
    double FrameDifferencePercentage = 0.125; // This is the difference between the
                                              // previous and current frame if the
                                              // difference is big then we
                                              // reinitialize the background model
    int MatchCount; // Number of matches
    bool ShakyCamera = false;   // This indicates, whether the camera shaking detection is on or off
    byte[,,,] CompareFrames = new byte[1, 1, 3, 2]; // Two consequent frames

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
                        if ((0.11d * CompareFrames[x, y, 0, 0] + 0.59d * CompareFrames[x, y, 0, 1] +
                             0.3d * CompareFrames[x, y, 0, 2]) == (0.11d * CompareFrames[x, y, 1, 0] +
                                                                   0.59d * CompareFrames[x, y, 1, 1] +
                                                                   0.3d * CompareFrames[x, y, 1, 2]))
                        {
                            MatchCount++;
                        }
                    }
                    else if (i % 2 == 1 && ShakyCamera)
                    {
                        CompareFrames[x, y, 1, 0] = FrameImageBytes[y, x, 0];
                        CompareFrames[x, y, 1, 1] = FrameImageBytes[y, x, 1];
                        CompareFrames[x, y, 1, 2] = FrameImageBytes[y, x, 2];
                        if ((0.11d * CompareFrames[x, y, 0, 0] + 0.59d * CompareFrames[x, y, 0, 1] +
                             0.3d * CompareFrames[x, y, 0, 2]) == (0.11d * CompareFrames[x, y, 1, 0] +
                                                                   0.59d * CompareFrames[x, y, 1, 1] +
                                                                   0.3d * CompareFrames[x, y, 1, 2]))
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
}