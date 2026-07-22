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
        AppDomain.CurrentDomain.BaseDirectory,
        "settings.json"
    );

    private class SettingsData
    {
        public string CurrentTheme { get; set; } = "System";
        public bool UseTransparency { get; set; } = false;
        public int CurrentLanguageIndex { get; set; } = 0;
    }

    private bool _isLoading;

    [ObservableProperty]
    private string _currentTheme = "System";

    [ObservableProperty]
    private bool _useTransparency = false;

    [ObservableProperty]
    private int _currentLanguageIndex = 0;

    public SettingsViewModel()
    {
        LoadSettings();
        
        // Listen to system theme changes via built-in Windows events
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnSystemEventsUserPreferenceChanged;
    }

    private void OnSystemEventsUserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category == Microsoft.Win32.UserPreferenceCategory.General && CurrentTheme == "System")
        {
            if (Application.Current != null && Application.Current.Dispatcher != null)
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    bool isDark = IsSystemDark();
                    Wpf.Ui.Appearance.ApplicationThemeManager.Apply(isDark ? Wpf.Ui.Appearance.ApplicationTheme.Dark : Wpf.Ui.Appearance.ApplicationTheme.Light);
                    try { Wpf.Ui.Appearance.ApplicationAccentColorManager.ApplySystemAccent(); } catch { }
                    ApplyCustomThemeDictionaries(isDark ? "Dark" : "Light");
                    ApplyWindowBackground();
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
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
            // Detect Language
            string lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            CurrentLanguageIndex = lang.Equals("ru", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        }
        catch
        {
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
                    object? appVal = key.GetValue("AppsUseLightTheme");
                    object? sysVal = key.GetValue("SystemUsesLightTheme");
                    
                    if (appVal is int appLight) return appLight == 0;
                    if (sysVal is int sysLight) return sysLight == 0;
                }
            }
        }
        catch { }
        return false;
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
        if (Application.Current == null) return;

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
        try
        {
            if (Application.Current?.MainWindow is not FluentWindow window)
                return;

            var rootGrid = window.Content as System.Windows.Controls.Grid;

            Version osVersion = Environment.OSVersion.Version;
            bool supportsMica = osVersion.Major >= 10 && osVersion.Build >= 22000;
            bool supportsAcrylic = osVersion.Major >= 10 && osVersion.Build >= 17763;

            if (UseTransparency && supportsAcrylic)
            {
                if (rootGrid != null) rootGrid.SetResourceReference(System.Windows.Controls.Panel.BackgroundProperty, "AppBackgroundBrush");
                window.WindowBackdropType = supportsMica ? WindowBackdropType.Mica : WindowBackdropType.Acrylic;
            }
            else
            {
                window.WindowBackdropType = WindowBackdropType.None;
                if (rootGrid != null) rootGrid.SetResourceReference(System.Windows.Controls.Panel.BackgroundProperty, "AppSolidBackgroundBrush");
            }
        }
        catch
        {
            // Ignore cosmetic WPF-UI Freezable crash when updating background
        }
    }

    public string AppVersion
    {
        get
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var prefix = Application.Current?.Resources["Settings_VersionPrefix"] as string ?? "Версия ";
            return $"{prefix}{version?.Major}.{version?.Minor}";
        }
    }

    partial void OnCurrentLanguageIndexChanged(int value)
    {
        if (Application.Current != null)
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
        }
        SaveSettings();
        OnPropertyChanged(nameof(AppVersion));
    }
}
