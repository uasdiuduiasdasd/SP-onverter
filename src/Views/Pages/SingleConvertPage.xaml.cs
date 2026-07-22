using System.Windows.Controls;
using SPConverter.ViewModels;

namespace SPConverter.Views.Pages;

public partial class SingleConvertPage : Page
{
    public SingleConvertPage(SingleConvertViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
    private static readonly HashSet<string> _acceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".avif", ".bmp", ".tga",
        ".tiff", ".tif", ".heic", ".cr2", ".cr3", ".nef", ".arw", ".dng"
    };

    private void OnDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files != null && files.Length > 0 && DataContext is SingleConvertViewModel vm)
            {
                string ext = System.IO.Path.GetExtension(files[0]);
                if (!string.IsNullOrEmpty(ext) && _acceptedExtensions.Contains(ext))
                {
                    vm.SelectedFile = files[0];
                }
            }
        }
    }
}
