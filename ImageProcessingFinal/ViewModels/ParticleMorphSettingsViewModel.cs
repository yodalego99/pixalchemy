using System;

namespace ImageProcessingFinal.ViewModels;

public class ParticleMorphSettingsViewModel : ViewModelBase
{
    private int _particleSize = 6;
    private bool _saveAsVideo;

    public int ParticleSize
    {
        get => _particleSize;
        set => SetProperty(ref _particleSize, Math.Clamp(value, MinimumParticleSize, MaximumParticleSize));
    }

    public bool SaveAsVideo
    {
        get => _saveAsVideo;
        set => SetProperty(ref _saveAsVideo, value);
    }

    public int MinimumParticleSize { get; } = 1;

    public int MaximumParticleSize { get; } = 64;
}