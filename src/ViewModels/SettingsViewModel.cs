using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

using SPConverter;
using System.Linq;
using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using System.Globalization;

namespace SPConverter.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    public static string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SPConverter",
        "settings.json"
    );

    private class SettingsData
    {
        public string CurrentTheme { get; set; } = "System";
        public bool UseTransparency { get; set; } = true;
        public int CurrentLanguageIndex { get; set; } = 0;
    }

    private bool _isLoading;

    [ObservableProperty]
    private string _currentTheme = "System";

    [ObservableProperty]
    private bool _useTransparency = true;

    [ObservableProperty]
    private int _currentLanguageIndex = 0;

    public SettingsViewModel()
    {
        LoadSettings();
        
        // Подписка на системные изменения темы (важно для режима "System")
        Wpf.Ui.Appearance.ApplicationThemeManager.Changed += OnWpfUiThemeChanged;
    }

    private void OnWpfUiThemeChanged(Wpf.Ui.Appearance.ApplicationTheme currentApplicationTheme, System.Windows.Media.Color systemAccent)
    {
        if (CurrentTheme == "System")
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                bool isDark = IsSystemDark();
                ApplyCustomThemeDictionaries(isDark ? "Dark" : "Light");
                ApplyWindowBackground();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private void LoadSettings()
    {
        try
        {
            _isLoading = true;
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                var data = JsonSerializer.Deserialize<SettingsData>(json);
                if (data != null)
                {
                    CurrentTheme = data.CurrentTheme;
                    UseTransparency = data.UseTransparency;
                    CurrentLanguageIndex = data.CurrentLanguageIndex;
                }
            }
            else
            {
                // First launch OS detection
                DetectOsDefaults();
            }
            
            // Explicitly apply settings on startup
            OnCurrentThemeChanged(CurrentTheme);
            OnUseTransparencyChanged(UseTransparency);
            OnCurrentLanguageIndexChanged(CurrentLanguageIndex);
        }
        catch
        {
            OnCurrentThemeChanged(CurrentTheme);
            OnUseTransparencyChanged(UseTransparency);
            OnCurrentLanguageIndexChanged(CurrentLanguageIndex);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void DetectOsDefaults()
    {
        // First launch: always follow system theme
        CurrentTheme = "System";

        try
        {
            // Detect Transparency from OS settings
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                if (key != null)
                {
                    object? transVal = key.GetValue("EnableTransparency");
                    if (transVal is int transparency)
                    {
                        UseTransparency = transparency == 1;
                    }
                }
            }

            // Detect Language
            string lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            CurrentLanguageIndex = lang.Equals("ru", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        }
        catch
        {
            UseTransparency = true;
            CurrentLanguageIndex = 0;
        }
    }

    private void SaveSettings()
    {
        if (_isLoading) return;

        try
        {
            string? dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var data = new SettingsData
            {
                CurrentTheme = CurrentTheme,
                UseTransparency = UseTransparency,
                CurrentLanguageIndex = CurrentLanguageIndex
            };

            string json = JsonSerializer.Serialize(data);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    public static bool IsSystemDark()
    {
        try
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                if (key != null)
                {
                    object? val = key.GetValue("AppsUseLightTheme");
                    if (val is int lightThemeInt)
                    {
                        return lightThemeInt == 0;
                    }
                }
            }
        }
        catch { }

        try
        {
            var sysTheme = Wpf.Ui.Appearance.ApplicationThemeManager.GetSystemTheme();
            return sysTheme == Wpf.Ui.Appearance.SystemTheme.Dark
                || sysTheme == Wpf.Ui.Appearance.SystemTheme.HC1
                || sysTheme == Wpf.Ui.Appearance.SystemTheme.HC2
                || sysTheme == Wpf.Ui.Appearance.SystemTheme.HCBlack
                || sysTheme == Wpf.Ui.Appearance.SystemTheme.Glow;
        }
        catch
        {
            return false;
        }
    }

    partial void OnCurrentThemeChanged(string value)
    {
        Wpf.Ui.Appearance.ApplicationTheme resolvedTheme;
        string dictToApply;

        if (value == "System")
        {
            bool isDark = IsSystemDark();
            resolvedTheme = isDark ? Wpf.Ui.Appearance.ApplicationTheme.Dark : Wpf.Ui.Appearance.ApplicationTheme.Light;
            dictToApply = isDark ? "Dark" : "Light";

            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(resolvedTheme);
            try { Wpf.Ui.Appearance.ApplicationAccentColorManager.ApplySystemAccent(); } catch { }
        }
        else if (value == "BW")
        {
            resolvedTheme = Wpf.Ui.Appearance.ApplicationTheme.Dark;
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(resolvedTheme);
            try { Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(System.Windows.Media.Color.FromRgb(255, 255, 255), resolvedTheme, false); } catch { }
            dictToApply = "BW";
        }
        else if (value == "Dark")
        {
            resolvedTheme = Wpf.Ui.Appearance.ApplicationTheme.Dark;
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(resolvedTheme);
            try { Wpf.Ui.Appearance.ApplicationAccentColorManager.ApplySystemAccent(); } catch { }
            dictToApply = "Dark";
        }
        else
        {
            resolvedTheme = Wpf.Ui.Appearance.ApplicationTheme.Light;
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(resolvedTheme);
            try { Wpf.Ui.Appearance.ApplicationAccentColorManager.ApplySystemAccent(); } catch { }
            dictToApply = "Light";
        }

        // Apply theme resource dictionary IMMEDIATELY (synchronously)
        ApplyCustomThemeDictionaries(dictToApply);

        // Apply window background after WPF-UI updates internals
        if (Application.Current?.Dispatcher != null)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ApplyWindowBackground();
                SaveSettings();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private void ApplyCustomThemeDictionaries(string themeName)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var existingTheme = dictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Resources/Themes/"));
        if (existingTheme != null)
        {
            dictionaries.Remove(existingTheme);
        }

        string sourcePath = themeName switch
        {
            "BW" => "Resources/Themes/BW.xaml",
            "Dark" => "Resources/Themes/Dark.xaml",
            "Light" => "Resources/Themes/Light.xaml",
            _ => "Resources/Themes/Light.xaml"
        };
        
        try
        {
            dictionaries.Add(new ResourceDictionary { Source = new Uri(sourcePath, UriKind.Relative) });
        }
        catch { }
    }

    partial void OnUseTransparencyChanged(bool value)
    {
        if (Application.Current != null)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => OnUseTransparencyChanged(value));
                return;
            }

            ApplyWindowBackground();
            SaveSettings();
        }
    }

    public void ApplyWindowBackground()
    {
        if (Application.Current?.MainWindow is not FluentWindow window)
            return;

        Version osVersion = Environment.OSVersion.Version;
        bool supportsMica = osVersion.Major >= 10 && osVersion.Build >= 22000;
        bool supportsAcrylic = osVersion.Major >= 10 && osVersion.Build >= 17763;

        if (UseTransparency && supportsAcrylic)
        {
            window.SetResourceReference(System.Windows.Window.BackgroundProperty, "AppBackgroundBrush");
            window.WindowBackdropType = supportsMica ? WindowBackdropType.Mica : WindowBackdropType.Acrylic;
        }
        else
        {
            window.WindowBackdropType = WindowBackdropType.None;
            window.SetResourceReference(System.Windows.Window.BackgroundProperty, "AppSolidBackgroundBrush");
        }
    }

    public string AppVersion
    {
        get
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var prefix = Application.Current.Resources["Settings_VersionPrefix"] as string ?? "Версия ";
            return $"{prefix}{version?.Major}.{version?.Minor}";
        }
    }

    partial void OnCurrentLanguageIndexChanged(int value)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var existingDict = dictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Resources/Strings/"));
        if (existingDict != null)
        {
            dictionaries.Remove(existingDict);
        }

        string sourcePath = value == 1 ? "Resources/Strings/en.xaml" : "Resources/Strings/ru.xaml";
        try
        {
            dictionaries.Add(new ResourceDictionary { Source = new Uri(sourcePath, UriKind.Relative) });
        }
        catch
        {
            // Ignore during unit testing
        }
        SaveSettings();
        OnPropertyChanged(nameof(AppVersion));
    }
}
