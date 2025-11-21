using System;
using Emgu.CV;
using Emgu.CV.Structure;

namespace ImageProcessingFinal.Views;

public class ViBe
{
    private Random _rnd = new Random();

    // The fidelity of the background model
    public int? N;

    // Distance between two pixels colour in color space
    public int? R;

    // Required matches to be added to the background model
    public int? BgMMin;

    // Rate of decay - bigger values tend to cause ghosting
    public int? Phi = 16;

    public int FrameWidth, FrameHeight;
    public Image<Rgba, byte> FrameImage;

    // Background model
    byte[,,,] _samples;

    // Segmentation map - result of the ViBe background removal operation
    private Image<Rgba, byte> _segMap;
    public SegmapType? SegmapType;
    byte[,,] _frameImageBytes;
    byte[,,] _segMapBytes;
    Mat _frameRead;

    // This is the difference between the
    // previous and current frame if the
    // difference is big then we
    // reinitialize the background model
    public double? FrameDifferencePercentage;
    int _matchCount; // Number of matches
    public bool? ShakyCamera; // This indicates, whether the camera shaking detection is on or off
    byte[,,,] _compareFrames; // Two consequent frames

    private void BackgroundModelInitialization()
    {
        for (int k = 0; k < N; k++)
        {
            for (int x = 0; x < FrameWidth; x++)
            {
                for (int y = 0; y < FrameHeight; y++)
                {
                    if ((bool)ShakyCamera)
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

    private void BackgroundModelUpdate(int i)
    {
        for (int x = 0; x < FrameWidth; x++)
        {
            for (int y = 0; y < FrameHeight; y++)
            {
                int count = 0;
                int index = 0;
                int db, dg, dr = 0;
                if (i % 2 == 0 && i != 0 && (bool)ShakyCamera)
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
                else if (i % 2 == 1 && (bool)ShakyCamera)
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
                    db = (int)Math.Abs(_frameImageBytes[y, x, 0] - _samples[x, y, index, 0]);
                    dg = (int)Math.Abs(_frameImageBytes[y, x, 1] - _samples[x, y, index, 1]);
                    dr = (int)Math.Abs(_frameImageBytes[y, x, 2] - _samples[x, y, index, 2]);
                    if (db < R && dg < R && dr < R)
                    {
                        count++;
                    }

                    index++;
                }

                if (count >= BgMMin)
                {
                    if (SegmapType == Views.SegmapType.Background)
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

                    int rand = _rnd.Next(0, (int)(Phi - 1));
                    if (rand == 0)
                    {
                        rand = _rnd.Next(0, (int)(N - 1));
                        _samples[x, y, rand, 0] = _frameImageBytes[y, x, 0];
                        _samples[x, y, rand, 1] = _frameImageBytes[y, x, 1];
                        _samples[x, y, rand, 2] = _frameImageBytes[y, x, 2];
                    }

                    rand = _rnd.Next(0, (int)(Phi - 1));
                    if (rand == 0)
                    {
                        int xNg, yNg;
                        rand = _rnd.Next(0, (int)(N - 1));
                        xNg = GetRandomNeighbourPixel(x);
                        yNg = GetRandomNeighbourPixel(y);
                        _samples[xNg, yNg, rand, 0] = _frameImageBytes[y, x, 0];
                        _samples[xNg, yNg, rand, 1] = _frameImageBytes[y, x, 1];
                        _samples[xNg, yNg, rand, 2] = _frameImageBytes[y, x, 2];
                    }
                }
                else
                {
                    if (SegmapType == Views.SegmapType.Foreground)
                    {
                        _segMapBytes[y, x, 0] = _frameImageBytes[y, x, 0];
                        _segMapBytes[y, x, 1] = _frameImageBytes[y, x, 1];
                        _segMapBytes[y, x, 2] = _frameImageBytes[y, x, 2];
                    }
                    else if (SegmapType == Views.SegmapType.Background)
                    {
                        if ((x + y) % 2 == 0)
                        {
                            _segMapBytes[y, x, 0] = byte.MaxValue;
                            _segMapBytes[y, x, 1] = byte.MinValue;
                            _segMapBytes[y, x, 2] = byte.MaxValue;
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

        if ((double)(_matchCount) / (double)(FrameWidth * FrameHeight) < FrameDifferencePercentage && (bool)ShakyCamera)
        {
            BackgroundModelInitialization();
        }

        _matchCount = 0;
    }

    private int GetRandomNeighbourPixel(int coord)
    {
        int[] var = [-1, 0, 1];

        var rnd = new Random();

        if (coord == (FrameHeight - 1) || (coord == FrameWidth - 1) || coord == 0)
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
    public static ViBe withDefaults(this ViBe vibe)
    {
        vibe.N = vibe.N ?? 20;
        vibe.R = vibe.R ?? 20;
        vibe.BgMMin = vibe.BgMMin ?? 2;
        vibe.Phi = vibe.Phi ?? 16;
        vibe.SegmapType = vibe.SegmapType ?? SegmapType.OnlySegmap;
        vibe.FrameDifferencePercentage = vibe.FrameDifferencePercentage ?? 0.125;
        vibe.ShakyCamera = vibe.ShakyCamera ?? false;
        return vibe;
    }
}

enum SegmapType
{
    OnlySegmap,
    Background,
    Foreground,
}