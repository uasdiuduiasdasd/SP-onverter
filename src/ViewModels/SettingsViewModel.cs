using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

using SPConverter;
using System.Linq;
using System;
using System.IO;
using System.Security;
using System.Text.Json;
using Microsoft.Win32;
using System.Globalization;

namespace SPConverter.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private static readonly object SettingsFileLock = new();

    public static string SettingsPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "settings.json"
    );

    private class SettingsData
    {
        public string CurrentTheme { get; set; } = "System";
        public bool UseTransparency { get; set; } = false;
        public int CurrentLanguageIndex { get; set; } = 0;
        public bool DeleteOriginalsByDefault { get; set; } = false;
        public bool ExtractAllPagesByDefault { get; set; } = false;
        public bool AlwaysIncludeSubfolders { get; set; } = false;
        public bool ShowSupportButtonInHeader { get; set; } = true;
    }

    private bool _isLoading;

    [ObservableProperty]
    private string _currentTheme = "System";

    [ObservableProperty]
    private bool _useTransparency = false;

    [ObservableProperty]
    private int _currentLanguageIndex = 0;

    [ObservableProperty]
    private bool _deleteOriginalsByDefault = false;

    [ObservableProperty]
    private bool _extractAllPagesByDefault = false;

    [ObservableProperty]
    private bool _alwaysIncludeSubfolders = false;

    [ObservableProperty]
    private bool _showSupportButtonInHeader = true;

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
                    ApplySystemAccentColor();
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
                var savedSettings = JsonSerializer.Deserialize<SettingsData>(json);
                if (savedSettings != null)
                {
                    CurrentTheme = NormalizeTheme(savedSettings.CurrentTheme);
                    UseTransparency = savedSettings.UseTransparency;
                    CurrentLanguageIndex = NormalizeLanguageIndex(savedSettings.CurrentLanguageIndex);
                    DeleteOriginalsByDefault = savedSettings.DeleteOriginalsByDefault;
                    ExtractAllPagesByDefault = savedSettings.ExtractAllPagesByDefault;
                    AlwaysIncludeSubfolders = savedSettings.AlwaysIncludeSubfolders;
                    ShowSupportButtonInHeader = savedSettings.ShowSupportButtonInHeader;
                }
            }
            else
            {
                // First launch OS detection
                DetectOsDefaults();
            }
            
            ApplyCurrentSettings();
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not load settings: {ex.Message}");
            ApplyCurrentSettings();
        }
        catch (UnauthorizedAccessException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not load settings: {ex.Message}");
            ApplyCurrentSettings();
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not load settings: {ex.Message}");
            ApplyCurrentSettings();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ApplyCurrentSettings()
    {
        OnCurrentThemeChanged(CurrentTheme);
        OnUseTransparencyChanged(UseTransparency);
        OnCurrentLanguageIndexChanged(CurrentLanguageIndex);
    }

    private void DetectOsDefaults()
    {
        // First launch: always follow system theme
        CurrentTheme = "System";

        string lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        CurrentLanguageIndex = lang.Equals("ru", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }

    private static string NormalizeTheme(string? theme)
    {
        return theme switch
        {
            "System" or "Light" or "Dark" or "BW" => theme,
            _ => "System"
        };
    }

    private static int NormalizeLanguageIndex(int languageIndex)
    {
        return languageIndex == 1 ? 1 : 0;
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

            var savedSettings = new SettingsData
            {
                CurrentTheme = CurrentTheme,
                UseTransparency = UseTransparency,
                CurrentLanguageIndex = CurrentLanguageIndex,
                DeleteOriginalsByDefault = DeleteOriginalsByDefault,
                ExtractAllPagesByDefault = ExtractAllPagesByDefault,
                AlwaysIncludeSubfolders = AlwaysIncludeSubfolders,
                ShowSupportButtonInHeader = ShowSupportButtonInHeader
            };

            string json = JsonSerializer.Serialize(savedSettings);
            lock (SettingsFileLock)
            {
                using var stream = new FileStream(SettingsPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream);
                writer.Write(json);
            }
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not save settings: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not save settings: {ex.Message}");
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
        catch (SecurityException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not read system theme: {ex.Message}");
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not read system theme: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not read system theme: {ex.Message}");
        }
        return false;
    }

    private static void ApplySystemAccentColor()
    {
        try
        {
            Wpf.Ui.Appearance.ApplicationAccentColorManager.ApplySystemAccent();
        }
        catch (InvalidOperationException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not apply system accent color: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not apply system accent color: {ex.Message}");
        }
    }

    private static void ApplyAccentColor(System.Windows.Media.Color color, Wpf.Ui.Appearance.ApplicationTheme theme)
    {
        try
        {
            Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(color, theme, false);
        }
        catch (InvalidOperationException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not apply accent color: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not apply accent color: {ex.Message}");
        }
    }

    partial void OnCurrentThemeChanged(string value)
    {
        string normalizedTheme = NormalizeTheme(value);
        if (value != normalizedTheme)
        {
            CurrentTheme = normalizedTheme;
            return;
        }

        Wpf.Ui.Appearance.ApplicationTheme resolvedTheme;
        string dictToApply;

        if (value == "System")
        {
            bool isDark = IsSystemDark();
            resolvedTheme = isDark ? Wpf.Ui.Appearance.ApplicationTheme.Dark : Wpf.Ui.Appearance.ApplicationTheme.Light;
            dictToApply = isDark ? "Dark" : "Light";
            
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(resolvedTheme);
            ApplySystemAccentColor();
        }
        else if (value == "BW")
        {
            resolvedTheme = Wpf.Ui.Appearance.ApplicationTheme.Dark;
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(resolvedTheme);
            ApplyAccentColor(System.Windows.Media.Color.FromRgb(255, 255, 255), resolvedTheme);
            dictToApply = "BW";
        }
        else if (value == "Dark")
        {
            resolvedTheme = Wpf.Ui.Appearance.ApplicationTheme.Dark;
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(resolvedTheme);
            ApplySystemAccentColor();
            dictToApply = "Dark";
        }
        else
        {
            resolvedTheme = Wpf.Ui.Appearance.ApplicationTheme.Light;
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(resolvedTheme);
            ApplySystemAccentColor();
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
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not apply theme resources: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not apply theme resources: {ex.Message}");
        }
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
        catch (InvalidOperationException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not apply window background: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not apply window background: {ex.Message}");
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

    public string CurrentLanguageName => NormalizeLanguageIndex(CurrentLanguageIndex) == 1 ? "English" : "Русский";

    partial void OnCurrentLanguageIndexChanged(int value)
    {
        int normalizedLanguageIndex = NormalizeLanguageIndex(value);
        if (value != normalizedLanguageIndex)
        {
            CurrentLanguageIndex = normalizedLanguageIndex;
            return;
        }

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
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not apply language resources: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not apply language resources: {ex.Message}");
            }
        }
        SaveSettings();
        OnPropertyChanged(nameof(AppVersion));
        OnPropertyChanged(nameof(CurrentLanguageName));
    }

    partial void OnDeleteOriginalsByDefaultChanged(bool value)
    {
        SaveSettings();
    }

    partial void OnExtractAllPagesByDefaultChanged(bool value)
    {
        SaveSettings();
    }

    partial void OnAlwaysIncludeSubfoldersChanged(bool value)
    {
        SaveSettings();
    }

    partial void OnShowSupportButtonInHeaderChanged(bool value)
    {
        SaveSettings();
    }
}
