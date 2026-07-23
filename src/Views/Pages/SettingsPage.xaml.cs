using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;
using SPConverter.ViewModels;
using SPConverter.Views;

namespace SPConverter.Views.Pages;

public partial class SettingsPage : Page
{
    private const string GitHubUrl = "https://github.com/uasdiuduiasdasd/SP-onverter";
    private const string SupportProjectUrl = "https://github.com/sponsors/uasdiuduiasdasd";

    public SettingsPage(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnGitHubLinkClick(object sender, MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = GitHubUrl,
            UseShellExecute = true
        });
    }

    private void OnSupportProjectClick(object sender, System.Windows.RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = SupportProjectUrl,
            UseShellExecute = true
        });
    }

    private void OnLanguageButtonClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (LanguageButton.ContextMenu == null) return;

        LanguageButton.ContextMenu.MinWidth = LanguageButton.ActualWidth;
        LanguageButton.ContextMenu.PlacementTarget = LanguageButton;
        LanguageButton.ContextMenu.IsOpen = true;
    }

    private void OnLanguageMenuItemClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel
            || sender is not MenuItem { Tag: string indexText }
            || !int.TryParse(indexText, out int languageIndex))
        {
            return;
        }

        viewModel.CurrentLanguageIndex = languageIndex;
    }

    private void OnPageScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScrollViewerWheel.ScrollByFixedStep(sender, e);
    }
}
