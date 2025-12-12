using System;
using System.Collections.Generic;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace ImageProcessingFinal.Views;

public sealed class ParticleMorphProcessor
{
    private readonly List<Particle> _particles = new();
    private readonly int _particleSize;
    private int _height;
    private int _width;

    public ParticleMorphProcessor(int particleSize = 8, int totalSteps = 90, int frameDelayMilliseconds = 33)
    {
        _particleSize = Math.Max(2, particleSize);
        TotalSteps = Math.Max(2, totalSteps);
        FrameDelayMilliseconds = Math.Max(1, frameDelayMilliseconds);
    }

    public int TotalSteps { get; }

    public int FrameDelayMilliseconds { get; }

    public void Initialize(Image<Bgr, byte> source, Image<Bgr, byte> target)
    {
        if (source.Width != target.Width || source.Height != target.Height)
            throw new ArgumentException("Source and target images must share the same size.");

        _width = target.Width;
        _height = target.Height;
        _particles.Clear();

        var sourceBlocks = CreateBlocks(source);
        var targetBlocks = CreateBlocks(target);

        var availableTargets = new List<BlockInfo>(targetBlocks);
        foreach (var block in sourceBlocks)
        {
            var targetIndex = FindClosestTarget(block.Color, availableTargets);
            var destination = availableTargets[targetIndex];
            _particles.Add(new Particle(block.Bounds, destination.Bounds, block.Color));
            availableTargets.RemoveAt(targetIndex);
        }
    }

    public Image<Bgr, byte> RenderFrame(int frameIndex)
    {
        if (_particles.Count == 0)
            throw new InvalidOperationException("Initialize must be called before rendering frames.");

        var progress = Math.Clamp(frameIndex / Math.Max(1.0, TotalSteps - 1), 0.0, 1.0);
        var canvas = new Image<Bgr, byte>(_width, _height, new Bgr(0, 0, 0));
        foreach (var particle in _particles)
        {
            var rect = particle.InterpolateBounds(progress, _width, _height);
            var color = particle.InterpolateColor(progress);
            CvInvoke.Rectangle(canvas, rect, color.MCvScalar, -1, LineType.AntiAlias);
        }

        return canvas;
    }

    private IEnumerable<BlockInfo> CreateBlocks(Image<Bgr, byte> image)
    {
        var data = image.Data;
        for (var y = 0; y < image.Height; y += _particleSize)
        {
            var blockHeight = Math.Min(_particleSize, image.Height - y);
            for (var x = 0; x < image.Width; x += _particleSize)
            {
                var blockWidth = Math.Min(_particleSize, image.Width - x);
                var rect = new Rectangle(x, y, blockWidth, blockHeight);
                var color = CalculateAverageColor(data, rect);
                yield return new BlockInfo(rect, color);
            }
        }
    }

    private static int FindClosestTarget(Bgr color, List<BlockInfo> targets)
    {
        var bestIndex = 0;
        var bestScore = double.MaxValue;
        for (var i = 0; i < targets.Count; i++)
        {
            var score = ColorDistance(color, targets[i].Color);
            if (score >= bestScore) continue;

            bestScore = score;
            bestIndex = i;
        }

        return bestIndex;
    }

    private static double ColorDistance(Bgr a, Bgr b)
    {
        var db = a.Blue - b.Blue;
        var dg = a.Green - b.Green;
        var dr = a.Red - b.Red;
        return db * db + dg * dg + dr * dr;
    }

    private static Bgr CalculateAverageColor(byte[,,] data, Rectangle rect)
    {
        long sumB = 0;
        long sumG = 0;
        long sumR = 0;
        for (var row = rect.Top; row < rect.Top + rect.Height; row++)
        for (var col = rect.Left; col < rect.Left + rect.Width; col++)
        {
            sumB += data[row, col, 0];
            sumG += data[row, col, 1];
            sumR += data[row, col, 2];
        }

        var total = Math.Max(1, rect.Width * rect.Height);
        return new Bgr(sumB / (double)total, sumG / (double)total, sumR / (double)total);
    }

    private readonly struct BlockInfo
    {
        public BlockInfo(Rectangle bounds, Bgr color)
        {
            Bounds = bounds;
            Color = color;
        }

        public Rectangle Bounds { get; }
        public Bgr Color { get; }
    }

    private readonly struct Particle
    {
        private readonly Rectangle _start;
        private readonly Rectangle _end;
        private readonly Bgr _color;

        public Particle(Rectangle start, Rectangle end, Bgr color)
        {
            _start = start;
            _end = end;
            _color = color;
        }

        public Rectangle InterpolateBounds(double progress, int maxWidth, int maxHeight)
        {
            var x = (int)Math.Round(Lerp(_start.X, _end.X, progress));
            var y = (int)Math.Round(Lerp(_start.Y, _end.Y, progress));
            var width = Math.Max(1, (int)Math.Round(Lerp(_start.Width, _end.Width, progress)));
            var height = Math.Max(1, (int)Math.Round(Lerp(_start.Height, _end.Height, progress)));

            x = Math.Clamp(x, 0, Math.Max(0, maxWidth - width));
            y = Math.Clamp(y, 0, Math.Max(0, maxHeight - height));

            return new Rectangle(x, y, width, height);
        }

        public Bgr InterpolateColor(double progress)
        {
            return _color;
        }

        private static double Lerp(double start, double end, double progress)
        {
            return start + (end - start) * progress;
        }
    }
}