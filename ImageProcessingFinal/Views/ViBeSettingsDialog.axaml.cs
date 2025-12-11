using Avalonia.Controls;
using ImageProcessingFinal.ViewModels;

namespace ImageProcessingFinal.Views;

public partial class ViBeSettingsDialog : Window
{
    public ViBeSettingsDialog()
    {
        InitializeComponent();
    }

    private void Ok_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ViBeSettingsViewModel vm)
        {
            if (SegmapComboBox.SelectedItem is ViBeSettingsViewModel.SegmentationChoice choice)
            {
                vm.SelectedSegmapType = choice.Value;
            }
            Close(vm);
        }
        else
        {
            Close(null);
        }
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }
}