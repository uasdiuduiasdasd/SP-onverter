using System;
using System.Windows;
using Wpf.Ui;
using SPConverter.ViewModels;
using SPConverter.Views.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace SPConverter.Views
{
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        private readonly IServiceProvider _serviceProvider;

        private string _notificationTargetFolder = string.Empty;
        private System.Windows.Threading.DispatcherTimer? _notificationTimer;

        public MainWindow(
            MainViewModel viewModel,
            IServiceProvider serviceProvider,
            ISnackbarService snackbarService)
        {
            DataContext = viewModel;
            _serviceProvider = serviceProvider;
            InitializeComponent();

            // Initial navigation is needed here because XAML Checked events fire before ContentFrame is initialized
            if (ContentFrame != null)
            {
                ContentFrame.Navigate(_serviceProvider.GetRequiredService<MassConvertPage>());
            }

            snackbarService.SetSnackbarPresenter(SnackbarPresenter);

            // Subscribe to custom theme-matched Notification Toast
            SPConverter.Services.NotificationService.OnShowNotification += ShowNotificationToast;
        }

        private void ShowNotificationToast(SPConverter.Services.NotificationRequest req)
        {
            Dispatcher.InvokeAsync(() =>
            {
                NotificationTitle.Text = req.Title;
                NotificationMessage.Text = req.Message;
                _notificationTargetFolder = req.TargetFolder;

                NotificationOpenFolderBtn.Visibility = !string.IsNullOrWhiteSpace(req.TargetFolder) && System.IO.Directory.Exists(req.TargetFolder)
                    ? Visibility.Visible 
                    : Visibility.Collapsed;

                NotificationIcon.Symbol = req.IsSuccess 
                    ? Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24 
                    : Wpf.Ui.Controls.SymbolRegular.Warning24;

                NotificationToast.Visibility = Visibility.Visible;

                _notificationTimer?.Stop();
                _notificationTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(7)
                };
                _notificationTimer.Tick += (s, e) =>
                {
                    NotificationToast.Visibility = Visibility.Collapsed;
                    _notificationTimer?.Stop();
                };
                _notificationTimer.Start();
            });
        }

        private void OnNotificationOpenFolderClick(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_notificationTargetFolder) && System.IO.Directory.Exists(_notificationTargetFolder))
            {
                System.Diagnostics.Process.Start("explorer.exe", _notificationTargetFolder);
            }
            NotificationToast.Visibility = Visibility.Collapsed;
            _notificationTimer?.Stop();
        }

        private void OnNotificationCloseClick(object sender, RoutedEventArgs e)
        {
            NotificationToast.Visibility = Visibility.Collapsed;
            _notificationTimer?.Stop();
        }

        private void NavSingle_Checked(object sender, RoutedEventArgs e)
        {
            if (ContentFrame != null)
                ContentFrame.Navigate(_serviceProvider.GetRequiredService<SingleConvertPage>());
        }

        private void NavMass_Checked(object sender, RoutedEventArgs e)
        {
            if (ContentFrame != null)
                ContentFrame.Navigate(_serviceProvider.GetRequiredService<MassConvertPage>());
        }

        private void NavSettings_Checked(object sender, RoutedEventArgs e)
        {
            if (ContentFrame != null)
                ContentFrame.Navigate(_serviceProvider.GetRequiredService<SettingsPage>());
        }
    }
}