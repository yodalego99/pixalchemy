using Avalonia.Controls;
using Avalonia.Interactivity;
using ImageProcessingFinal.ViewModels;

namespace ImageProcessingFinal.Views;

public partial class ParticleMorphSettingsDialog : Window
{
    public ParticleMorphSettingsDialog()
    {
        InitializeComponent();
    }

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ParticleMorphSettingsViewModel vm)
            Close(vm);
        else
            Close(null);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}