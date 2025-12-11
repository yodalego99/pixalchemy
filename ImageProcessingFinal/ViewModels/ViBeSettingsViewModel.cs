using System.Collections.Generic;
using ImageProcessingFinal.Views;

namespace ImageProcessingFinal.ViewModels;

public sealed class ViBeSettingsViewModel : ViewModelBase
{
    private bool _enableShakyCamera;
    private SegmapType _selectedSegmapType = SegmapType.OnlySegmap;

    public bool EnableShakyCamera
    {
        get => _enableShakyCamera;
        set => SetProperty(ref _enableShakyCamera, value);
    }

    public SegmapType SelectedSegmapType
    {
        get => _selectedSegmapType;
        set => SetProperty(ref _selectedSegmapType, value);
    }

    public IReadOnlyList<SegmentationChoice> SegmentationChoices { get; } = new[]
    {
        new SegmentationChoice("Segmentation mask", SegmapType.OnlySegmap),
        new SegmentationChoice("Background image", SegmapType.Background),
        new SegmentationChoice("Foreground overlay", SegmapType.Foreground)
    };

    public ViBeSettingsViewModel Clone()
    {
        return new ViBeSettingsViewModel
        {
            EnableShakyCamera = EnableShakyCamera,
            SelectedSegmapType = SelectedSegmapType
        };
    }

    public void ApplyFrom(ViBeSettingsViewModel source)
    {
        if (source == null)
        {
            return;
        }

        EnableShakyCamera = source.EnableShakyCamera;
        SelectedSegmapType = source.SelectedSegmapType;
    }

    public sealed record SegmentationChoice(string DisplayName, SegmapType Value);
}
