using System;
using Emgu.CV;
using Emgu.CV.Structure;

namespace ImageProcessingFinal.Views;

public class ViBe
{
    public Random Rnd;

    // The fidelity of the background model
    public int N;

    // Distance between two pixels colour in color space
    public int? R;

    // Required matches to be added to the background model
    public int? BgMMin;

    // Rate of decay - bigger values tend to cause ghosting
    public int Phi;
    public Image<Rgb, byte>? FrameImage;

    // Background model
    byte[,,,] _samples;

    // Segmentation map - result of the ViBe background removal operation
    private Image<Rgb, byte> _segMap;
    public SegmapType? SegmapType;
    byte[,,] _frameImageBytes;
    public byte[,,] _segMapBytes;

    // This is the difference between the
    // previous and current frame if the
    // difference is big then we
    // reinitialize the background model
    public double? FrameDifferencePercentage;
    int _matchCount; // Number of matches
    public bool ShakyCamera; // This indicates, whether the camera shaking detection is on or off
    byte[,,,] _compareFrames; // Two consequent frames

    public void BackgroundModelInitialization()
    {
        _samples = new byte[FrameImage.Size.Width, FrameImage.Size.Height, N, FrameImage.NumberOfChannels];
        _frameImageBytes = FrameImage.Data;
        for (var k = 0; k < N; k++)
        {
            for (var x = 0; x < FrameImage.Size.Width; x++)
            {
                for (var y = 0; y < FrameImage.Size.Height; y++)
                {
                    if (ShakyCamera)
                    {
                        _compareFrames[x, y, 0, 0] = _frameImageBytes[y, x, 0];
                        _compareFrames[x, y, 0, 1] = _frameImageBytes[y, x, 1];
                        _compareFrames[x, y, 0, 2] = _frameImageBytes[y, x, 2];
                    }
                    _samples[x, y, k, 0] = _frameImageBytes[y, x, 0];
                    _samples[x, y, k, 1] = _frameImageBytes[y, x, 1];
                    _samples[x, y, k, 2] = _frameImageBytes[y, x, 2];
                }
            }
        }
    }

    public void BackgroundModelUpdate(int i)
    {
        _frameImageBytes = FrameImage.Data;
        _segMapBytes = new byte[FrameImage.Size.Height, FrameImage.Size.Width, FrameImage.NumberOfChannels];
        for (var x = 0; x < FrameImage.Size.Width; x++)
        {
            for (var y = 0; y < FrameImage.Size.Height; y++)
            {
                int count = 0;
                    int index = 0;
                    if (i % 2 == 0 && i != 0 && ShakyCamera)
                    {
                        _compareFrames[x, y, 0, 0] = _frameImageBytes[y, x, 0];
                        _compareFrames[x, y, 0, 1] = _frameImageBytes[y, x, 1];
                        _compareFrames[x, y, 0, 2] = _frameImageBytes[y, x, 2];
                        if ((0.11d * _compareFrames[x, y, 0, 0] + 0.59d * _compareFrames[x, y, 0, 1] +
                             0.3d * _compareFrames[x, y, 0, 2]) == (0.11d * _compareFrames[x, y, 1, 0] +
                                                                   0.59d * _compareFrames[x, y, 1, 1] +
                                                                   0.3d * _compareFrames[x, y, 1, 2]))
                        {
                            _matchCount++;
                        }
                    }
                    else if (i % 2 == 1 && ShakyCamera)
                    {
                        _compareFrames[x, y, 1, 0] = _frameImageBytes[y, x, 0];
                        _compareFrames[x, y, 1, 1] = _frameImageBytes[y, x, 1];
                        _compareFrames[x, y, 1, 2] = _frameImageBytes[y, x, 2];
                        if ((0.11d * _compareFrames[x, y, 0, 0] + 0.59d * _compareFrames[x, y, 0, 1] +
                             0.3d * _compareFrames[x, y, 0, 2]) == (0.11d * _compareFrames[x, y, 1, 0] +
                                                                   0.59d * _compareFrames[x, y, 1, 1] +
                                                                   0.3d * _compareFrames[x, y, 1, 2]))
                        {
                            _matchCount++;
                        }
                    }

                    while ((count < BgMMin) && (index < N))
                    {
                        var db = Math.Abs(_frameImageBytes[y, x, 0] - _samples[x, y, index, 0]);
                        var dg = Math.Abs(_frameImageBytes[y, x, 1] - _samples[x, y, index, 1]);
                        var dr = Math.Abs(_frameImageBytes[y, x, 2] - _samples[x, y, index, 2]);
                        if (db < R && dg < R && dr < R)
                        {
                            count++;
                        }

                        index++;
                    }

                    if (count >= BgMMin)
                    {
                        if (SegmapType == Views.SegmapType.Foreground)
                        {
                            _segMapBytes[y, x, 0] = _frameImageBytes[y, x, 0];
                            _segMapBytes[y, x, 1] = _frameImageBytes[y, x, 1];
                            _segMapBytes[y, x, 2] = _frameImageBytes[y, x, 2];
                        }
                        else
                        {
                            _segMapBytes[y, x, 0] = byte.MinValue;
                            _segMapBytes[y, x, 1] = byte.MinValue;
                            _segMapBytes[y, x, 2] = byte.MinValue;
                        }

                        var rand = Rnd.Next(0, Phi - 1);
                        if (rand == 0)
                        {
                            rand = Rnd.Next(0, N - 1);
                            _samples[x, y, rand, 0] = _frameImageBytes[y, x, 0];
                            _samples[x, y, rand, 1] = _frameImageBytes[y, x, 1];
                            _samples[x, y, rand, 2] = _frameImageBytes[y, x, 2];
                        }

                        rand = Rnd.Next(0, Phi - 1);
                        if (rand == 0)
                        {
                            rand = Rnd.Next(0, N - 1);
                            var xNg = GetRandomNeighbourPixel(x);
                            var yNg = GetRandomNeighbourPixel(y);
                            _samples[xNg, yNg, rand, 0] = _frameImageBytes[y, x, 0];
                            _samples[xNg, yNg, rand, 1] = _frameImageBytes[y, x, 1];
                            _samples[xNg, yNg, rand, 2] = _frameImageBytes[y, x, 2];
                        }
                    }
                    else
                    {
                        if (SegmapType ==  Views.SegmapType.Background)
                        {
                            _segMapBytes[y, x, 0] = _frameImageBytes[y, x, 0];
                            _segMapBytes[y, x, 1] = _frameImageBytes[y, x, 1];
                            _segMapBytes[y, x, 2] = _frameImageBytes[y, x, 2];
                        }
                        else if (SegmapType == Views.SegmapType.Foreground)
                        {
                            if ((x + y) % 2 == 0)
                            {
                                _segMapBytes[y, x, 0] = 255;
                                _segMapBytes[y, x, 1] = 0;
                                _segMapBytes[y, x, 2] = 255;
                            }
                            else
                            {
                                _segMapBytes[y, x, 0] = byte.MinValue;
                                _segMapBytes[y, x, 1] = byte.MinValue;
                                _segMapBytes[y, x, 2] = byte.MinValue;
                            }
                        }
                        else
                        {
                            _segMapBytes[y, x, 0] = byte.MaxValue;
                            _segMapBytes[y, x, 1] = byte.MaxValue;
                            _segMapBytes[y, x, 2] = byte.MaxValue;
                        }
                    }
            }
        }
        if (
            (double)(_matchCount) / (FrameImage.Size.Width * FrameImage.Size.Height) < FrameDifferencePercentage
            && (bool)ShakyCamera
        )
        {
            BackgroundModelInitialization();
        }
        _matchCount = 0;
    }

    private int GetRandomNeighbourPixel(int coord)
    {
        int[] var = [-1, 0, 1];

        var rnd = new Random();

        if (coord == (FrameImage.Size.Height - 1) || (coord == FrameImage.Size.Width - 1) || coord == 0)
        {
            return coord;
        }
        else
        {
            return coord + var[rnd.Next(3)];
        }
    }
}

public static class ViBeExtensions
{
    public static ViBe WithDefaults(this ViBe vibe)
    {
        vibe.Rnd = new Random(DateTime.Now.Millisecond);
        vibe.N = 20;
        vibe.R = 20;
        vibe.BgMMin = 2;
        vibe.Phi = 16;
        vibe.SegmapType = SegmapType.OnlySegmap;
        vibe.FrameDifferencePercentage = 0.125d;
        vibe.ShakyCamera = vibe.ShakyCamera;
        return vibe;
    }
}

public enum SegmapType
{
    OnlySegmap,
    Background,
    Foreground,
}
