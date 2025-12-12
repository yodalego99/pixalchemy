using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace ImageProcessingFinal.Views;

/// <summary>
///     Builds photo mosaics from pre-defined tile images.
/// </summary>
public sealed class MosaicProcessor
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".gif",
        ".tif",
        ".tiff"
    };

    private static readonly Bgr[] FallbackPalette =
    {
        new(255, 0, 0),
        new(0, 255, 0),
        new(0, 0, 255),
        new(255, 255, 0),
        new(255, 0, 255),
        new(0, 255, 255),
        new(128, 128, 128),
        new(255, 255, 255),
        new(0, 0, 0)
    };

    private readonly object _initializationGate = new();

    private readonly string _tileDirectory;
    private readonly List<MosaicTile> _tiles = new();
    private readonly int _tileSize;

    public MosaicProcessor(string tileDirectory, int tileSize = 24)
    {
        _tileDirectory = tileDirectory;
        _tileSize = Math.Max(4, tileSize);
    }

    public void EnsureTileLibraryLoaded()
    {
        if (_tiles.Count > 0) return;

        lock (_initializationGate)
        {
            if (_tiles.Count > 0) return;

            LoadTileLibrary();
        }
    }

    private void LoadTileLibrary()
    {
        Directory.CreateDirectory(_tileDirectory);
        foreach (var path in Directory.EnumerateFiles(_tileDirectory)
                     .Where(f => SupportedExtensions.Contains(Path.GetExtension(f) ?? string.Empty)))
            try
            {
                var resized = new Image<Bgr, byte>(path).Resize(_tileSize, _tileSize, Inter.Area);
                var avgColor = CalculateAverageColor(resized.Data, 0, 0, _tileSize, _tileSize);
                _tiles.Add(new MosaicTile(resized, avgColor, Path.GetFileName(path)));
            }
            catch
            {
                // Ignore unreadable tiles so the rest of the set can still be used.
            }

        if (_tiles.Count == 0)
            foreach (var color in FallbackPalette)
            {
                var generator = new Image<Bgr, byte>(_tileSize, _tileSize, color);
                _tiles.Add(new MosaicTile(generator, color,
                    $"Fallback-{color.Blue.ToString(CultureInfo.InvariantCulture)}"));
            }
    }

    public Image<Bgr, byte> BuildMosaic(Image<Bgr, byte> source)
    {
        EnsureTileLibraryLoaded();
        var mosaic = new Image<Bgr, byte>(source.Size);
        var data = source.Data;
        for (var y = 0; y < source.Height; y += _tileSize)
        {
            var blockHeight = Math.Min(_tileSize, source.Height - y);
            for (var x = 0; x < source.Width; x += _tileSize)
            {
                var blockWidth = Math.Min(_tileSize, source.Width - x);
                var avgColor = CalculateAverageColor(data, x, y, blockWidth, blockHeight);
                var tile = FindClosestTile(avgColor);
                using var resizedTile = tile.Image.Resize(blockWidth, blockHeight, Inter.Area);
                using var destination = mosaic.GetSubRect(new Rectangle(x, y, blockWidth, blockHeight));
                resizedTile.CopyTo(destination);
            }
        }

        return mosaic;
    }

    private MosaicTile FindClosestTile(Bgr avgColor)
    {
        MosaicTile? closest = null;
        var bestScore = double.MaxValue;
        foreach (var tile in _tiles)
        {
            var score = tile.DistanceSquared(avgColor);
            if (!(score < bestScore)) continue;

            bestScore = score;
            closest = tile;
        }

        return closest ?? _tiles[0];
    }

    private static Bgr CalculateAverageColor(byte[,,] data, int startX, int startY, int width, int height)
    {
        long sumB = 0;
        long sumG = 0;
        long sumR = 0;
        for (var row = startY; row < startY + height; row++)
        for (var col = startX; col < startX + width; col++)
        {
            sumB += data[row, col, 0];
            sumG += data[row, col, 1];
            sumR += data[row, col, 2];
        }

        var total = Math.Max(1, width * height);
        return new Bgr(sumB / (double)total, sumG / (double)total, sumR / (double)total);
    }

    private sealed class MosaicTile
    {
        public MosaicTile(Image<Bgr, byte> image, Bgr averageColor, string? name)
        {
            Image = image;
            AverageColor = averageColor;
            Name = name ?? string.Empty;
        }

        public Image<Bgr, byte> Image { get; }
        public Bgr AverageColor { get; }
        public string Name { get; }

        public double DistanceSquared(Bgr color)
        {
            var db = AverageColor.Blue - color.Blue;
            var dg = AverageColor.Green - color.Green;
            var dr = AverageColor.Red - color.Red;
            return db * db + dg * dg + dr * dr;
        }
    }
}