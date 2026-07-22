using System.Windows;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SPConverter.Contracts;
using SPConverter.Services;
using SPConverter.ViewModels;

namespace SPConverter;

public partial class App : Application
{
    private static readonly IHost _host = Host
        .CreateDefaultBuilder()
        .ConfigureServices((context, services) =>
        {
            // Сервисы
            services.AddSingleton<IFileManagementService, LocalFileService>();
            services.AddSingleton<IImageConverterService, MagickImageConverter>();
            services.AddSingleton<Wpf.Ui.ISnackbarService, Wpf.Ui.SnackbarService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddTransient<SingleConvertViewModel>();
            services.AddTransient<MassConvertViewModel>();

            // Страницы
            services.AddTransient<SPConverter.Views.MainWindow>();
            services.AddTransient<SPConverter.Views.Pages.SingleConvertPage>();
            services.AddTransient<SPConverter.Views.Pages.MassConvertPage>();
            services.AddTransient<SPConverter.Views.Pages.SettingsPage>();
        }).Build();

    public static T? GetService<T>() where T : class
    {
        return _host.Services.GetService(typeof(T)) as T;
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _host.Start();

        var settingsVm = GetService<SettingsViewModel>();
        var mainWindow = GetService<SPConverter.Views.MainWindow>();
        
        if (mainWindow != null)
        {
            Application.Current.MainWindow = mainWindow;
            
            // Применяем настройки фона/прозрачности до показа окна, чтобы избежать крашей WindowChrome (Freezable exception)
            settingsVm?.ApplyWindowBackground();
            
            mainWindow.Show();
        }
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _host.StopAsync().Wait();
        _host.Dispose();
    }
}
