using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SPConverter.ViewModels;
using SPConverter.Views;

namespace SPConverter.Views.Pages;

public partial class MassConvertPage : Page
{
    public MassConvertPage(MassConvertViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        string? dropPath = FirstDropPath(e);
        if (dropPath != null && DataContext is MassConvertViewModel viewModel)
        {
            viewModel.PreviewDropPath(dropPath);
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = FirstDropPath(e) == null ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        if (DataContext is MassConvertViewModel viewModel)
        {
            viewModel.ClearDropPreview();
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        string? dropPath = FirstDropPath(e);
        if (dropPath != null && DataContext is MassConvertViewModel viewModel)
        {
            viewModel.ClearDropPreview();
            await viewModel.SetSourcePathAsync(dropPath);
        }

        e.Handled = true;
    }

    private void OnOtherFormatsClick(object sender, RoutedEventArgs e)
    {
        if (OtherFormatsButton.ContextMenu == null) return;

        OtherFormatsButton.ContextMenu.MinWidth = OtherFormatsButton.ActualWidth;
        OtherFormatsButton.ContextMenu.PlacementTarget = OtherFormatsButton;
        OtherFormatsButton.ContextMenu.IsOpen = true;
    }

    private void OnOtherFormatMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string format } && DataContext is MassConvertViewModel viewModel)
        {
            viewModel.SelectTargetFormatCommand.Execute(format);
        }
    }

    private void OnPageScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScrollViewerWheel.ScrollByFixedStep(sender, e);
    }

    private static string? FirstDropPath(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return null;
        }

        return (e.Data.GetData(DataFormats.FileDrop) as string[])?.FirstOrDefault();
    }
}
