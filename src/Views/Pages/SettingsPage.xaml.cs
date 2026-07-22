using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;
using SPConverter.ViewModels;

namespace SPConverter.Views.Pages;

public partial class SettingsPage : Page
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnGitHubLinkClick(object sender, MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/uasdiuduiasdasd/SP-onverter",
            UseShellExecute = true
        });
    }
}
