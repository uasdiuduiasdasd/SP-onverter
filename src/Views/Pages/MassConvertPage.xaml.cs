using System.Windows.Controls;
using SPConverter.ViewModels;

namespace SPConverter.Views.Pages;

public partial class MassConvertPage : Page
{
    public MassConvertPage(MassConvertViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private static readonly HashSet<string> _acceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".avif", ".bmp", ".tga",
        ".tiff", ".tif", ".heic", ".cr2", ".cr3", ".nef", ".arw", ".dng",
        ".psd", ".svg", ".gif", ".ico", ".jxl"
    };

    private void OnDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            string[]? files = e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0)
            {
                string path = files[0];
                if (System.IO.Directory.Exists(path))
                {
                    e.Effects = System.Windows.DragDropEffects.Copy;
                    e.Handled = true;
                    return;
                }
                if (System.IO.File.Exists(path))
                {
                    string ext = System.IO.Path.GetExtension(path);
                    if (!string.IsNullOrEmpty(ext) && _acceptedExtensions.Contains(ext))
                    {
                        e.Effects = System.Windows.DragDropEffects.Copy;
                        e.Handled = true;
                        return;
                    }
                }
            }
        }
        e.Effects = System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files != null && files.Length > 0 && DataContext is MassConvertViewModel vm)
            {
                string dropped = files[0];
                if (System.IO.Directory.Exists(dropped))
                {
                    vm.SourceDirectory = dropped;
                    vm.ScanDirectoryCommand.Execute(null);
                }
                // Если перетащили файл — берём его родительскую папку
                else if (System.IO.File.Exists(dropped))
                {
                    string? dir = System.IO.Path.GetDirectoryName(dropped);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        vm.SourceDirectory = dir;
                        vm.ScanDirectoryCommand.Execute(null);
                    }
                }
            }
        }
    }
}
