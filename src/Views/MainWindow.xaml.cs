using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui;
using SPConverter.ViewModels;
using SPConverter.Views.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace SPConverter.Views
{
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly SettingsViewModel _settingsViewModel;
        private const string SupportProjectUrl = "https://github.com/sponsors/uasdiuduiasdasd";

        private string _notificationTargetFolder = string.Empty;
        private System.Windows.Threading.DispatcherTimer? _notificationTimer;
        private bool _isSettingsOpen;

        public MainWindow(
            MainViewModel viewModel,
            IServiceProvider serviceProvider,
            ISnackbarService snackbarService,
            SettingsViewModel settingsViewModel)
        {
            DataContext = viewModel;
            _serviceProvider = serviceProvider;
            _settingsViewModel = settingsViewModel;
            InitializeComponent();
            FitAndCenterOnStartup();

            // Initial navigation is needed here because XAML Checked events fire before ContentFrame is initialized
            if (ContentFrame != null)
            {
                ContentFrame.Navigate(_serviceProvider.GetRequiredService<MassConvertPage>());
            }

            snackbarService.SetSnackbarPresenter(SnackbarPresenter);

            UpdateSupportProjectVisibility();
            _settingsViewModel.PropertyChanged += OnSettingsViewModelPropertyChanged;

            // Subscribe to custom theme-matched Notification Toast
            SPConverter.Services.NotificationService.OnShowNotification += ShowNotificationToast;
        }

        private void FitAndCenterOnStartup()
        {
            Rect workArea = SystemParameters.WorkArea;

            double targetWidth = double.IsNaN(Width) ? MinWidth : Width;
            double targetHeight = double.IsNaN(Height) ? MinHeight : Height;

            MinWidth = Math.Min(MinWidth, workArea.Width);
            MinHeight = Math.Min(MinHeight, workArea.Height);
            Width = Math.Min(targetWidth, workArea.Width);
            Height = Math.Min(targetHeight, workArea.Height);

            Left = workArea.Left + Math.Max(0, (workArea.Width - Width) / 2);
            Top = workArea.Top + Math.Max(0, (workArea.Height - Height) / 2);
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

        private void OnSettingsToggleClick(object sender, RoutedEventArgs e)
        {
            if (_isSettingsOpen)
            {
                NavigateToConverter();
            }
            else
            {
                NavigateToSettings();
            }
        }

        private void NavigateToConverter()
        {
            ContentFrame.Navigate(_serviceProvider.GetRequiredService<MassConvertPage>());
            _isSettingsOpen = false;
            SettingsToggleButton.SetResourceReference(ContentControl.ContentProperty, "Nav_Settings");
        }

        private void NavigateToSettings()
        {
            ContentFrame.Navigate(_serviceProvider.GetRequiredService<SettingsPage>());
            _isSettingsOpen = true;
            SettingsToggleButton.SetResourceReference(ContentControl.ContentProperty, "Nav_Convert");
        }

        private void OnSupportProjectClick(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SupportProjectUrl,
                UseShellExecute = true
            });
        }

        private void OnSettingsViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsViewModel.ShowSupportButtonInHeader))
            {
                UpdateSupportProjectVisibility();
            }
        }

        private void UpdateSupportProjectVisibility()
        {
            SupportProjectButton.Visibility = _settingsViewModel.ShowSupportButtonInHeader
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}
