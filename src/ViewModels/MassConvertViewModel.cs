using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SPConverter.Contracts;
using SPConverter.Models;
using SPConverter.Services;
using System.Windows;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace SPConverter.ViewModels;

public partial class MassConvertViewModel : ViewModelBase
{
    private static readonly HashSet<string> PopularTargetFormats = new(
        new[] { "JPG", "PNG", "WEBP", "GIF", "AVIF", "JXL", "PDF" },
        StringComparer.OrdinalIgnoreCase);

    private readonly IImageConverterService _converterService;
    private readonly IFileManagementService _fileService;
    private readonly ISnackbarService _snackbarService;
    private readonly SettingsViewModel _settings;

    private string _lastOutputDir = string.Empty;
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _scanCts;
    private bool _isSettingAutomaticTargetDirectory;
    private bool _targetDirectorySelectedManually;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSourceDirectory))]
    private string? _sourceDirectory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTargetDirectory))]
    private string? _targetDirectory;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    [NotifyPropertyChangedFor(nameof(ShowSubfoldersSuggestion))]
    private bool _includeSubfolders;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConversionNoticeText))]
    [NotifyPropertyChangedFor(nameof(HasConversionNotice))]
    [NotifyPropertyChangedFor(nameof(ShowQualityOptions))]
    [NotifyPropertyChangedFor(nameof(IsOtherFormatSelected))]
    [NotifyPropertyChangedFor(nameof(OtherFormatButtonText))]
    private string _targetFormat = "JPG";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomQuality))]
    private int _quality = 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowProgressDetails))]
    private int _progressMax;

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private string? _progressStatus;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isConverting;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    private bool _isScanning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSkippedFiles))]
    private int _skippedFiles;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSubfoldersSuggestion))]
    private bool _hasNestedFolders;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConversionNoticeText))]
    [NotifyPropertyChangedFor(nameof(HasConversionNotice))]
    [NotifyPropertyChangedFor(nameof(ShowExtractAllSuggestion))]
    private bool _extractAllPagesForCurrentConversion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DropZoneTitle))]
    [NotifyPropertyChangedFor(nameof(DropZoneSubtitle))]
    private string _dragDropState = "Normal";

    public MassConvertViewModel(
        IImageConverterService converterService,
        IFileManagementService fileService,
        ISnackbarService snackbarService,
        SettingsViewModel settings)
    {
        _converterService = converterService;
        _fileService = fileService;
        _snackbarService = snackbarService;
        _settings = settings;
        IncludeSubfolders = _settings.AlwaysIncludeSubfolders;
        _settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    public ObservableCollection<string> FoundFiles { get; } = new();
    public bool HasSourceDirectory => !string.IsNullOrWhiteSpace(SourceDirectory);
    public bool HasTargetDirectory => !string.IsNullOrWhiteSpace(TargetDirectory);
    public bool HasSkippedFiles => SkippedFiles > 0;
    public bool ShowProgressDetails => ProgressMax > 0;
    public bool ShowQualityOptions => ImageFormatRules.TargetFormatUsesQuality(TargetFormat);
    public bool IsOtherFormatSelected => !PopularTargetFormats.Contains(TargetFormat);
    public string OtherFormatButtonText => IsOtherFormatSelected ? TargetFormat.ToUpperInvariant() : ResourceText("Format_Other", "Other...");
    public string ConversionNoticeText => BuildConversionNoticeText();
    public bool HasConversionNotice => !string.IsNullOrEmpty(ConversionNoticeText);
    public bool ShowSubfoldersSuggestion => HasNestedFolders && !IncludeSubfolders && !_settings.AlwaysIncludeSubfolders;
    public bool ShowExtractAllSuggestion => HasExtractableFiles() && !_settings.ExtractAllPagesByDefault && !ExtractAllPagesForCurrentConversion;
    public string DropZoneTitle => BuildDropZoneTitle();
    public string DropZoneSubtitle => BuildDropZoneSubtitle();

    public bool IsCustomQuality
    {
        get => Quality != 50 && Quality != 75 && Quality != 90 && Quality != 100;
        set
        {
            if (value && (Quality == 50 || Quality == 75 || Quality == 90 || Quality == 100))
            {
                Quality = 85;
            }
        }
    }

    partial void OnSourceDirectoryChanged(string? value)
    {
        _scanCts?.Cancel();
        ResetScanResults();
        IncludeSubfolders = _settings.AlwaysIncludeSubfolders;
        ApplyAutomaticTargetDirectory(value);
        ConvertCommand.NotifyCanExecuteChanged();
    }

    partial void OnTargetDirectoryChanged(string? value)
    {
        if (!_isSettingAutomaticTargetDirectory)
        {
            _targetDirectorySelectedManually = !string.IsNullOrWhiteSpace(value);
        }
    }

    partial void OnIncludeSubfoldersChanged(bool value)
    {
        HasNestedFolders = false;
        NotifySourceHintsChanged();
    }

    partial void OnTargetFormatChanged(string value)
    {
        if (!ShowQualityOptions && IsCustomQuality)
        {
            Quality = 100;
        }
    }

    partial void OnQualityChanged(int value)
    {
        int clampedQuality = Math.Clamp(value, 1, 100);
        if (clampedQuality != value)
        {
            Quality = clampedQuality;
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.ExtractAllPagesByDefault))
        {
            NotifySourceHintsChanged();
            return;
        }

        if (e.PropertyName != nameof(SettingsViewModel.AlwaysIncludeSubfolders))
        {
            return;
        }

        if (_settings.AlwaysIncludeSubfolders && !IncludeSubfolders)
        {
            IncludeSubfolders = true;
            if (HasSourceDirectory && !IsConverting)
            {
                _ = ScanDirectoryAsync();
            }
        }

        NotifySourceHintsChanged();
    }

    [RelayCommand]
    private async Task BrowseSourceAsync()
    {
        var title = ResourceText("Dialog_SelectSourceFolderTitle", "Select source folder");
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = title };

        if (dialog.ShowDialog() == true)
        {
            await SetSourcePathAsync(dialog.FolderName);
        }
    }

    [RelayCommand]
    private async Task BrowseFileAsync()
    {
        var filter = ResourceText("Dialog_ImageFilter", "Images|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.gif;*.ico;*.pdf|All Files|*.*");
        var title = ResourceText("Dialog_SelectImageTitle", "Select image for conversion");
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = filter, Title = title };

        if (dialog.ShowDialog() == true)
        {
            await SetSourcePathAsync(dialog.FileName);
        }
    }

    [RelayCommand]
    private void BrowseTarget()
    {
        var title = ResourceText("Dialog_SelectFolderTitle", "Select target folder");
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = title };

        if (dialog.ShowDialog() == true)
        {
            TargetDirectory = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void SelectTargetFormat(string format)
    {
        if (ImageFormatRules.IsSupportedTargetFormat(format))
        {
            TargetFormat = format.Trim().ToUpperInvariant();
        }
    }

    [RelayCommand]
    private void EnableExtractAllForCurrentConversion()
    {
        ExtractAllPagesForCurrentConversion = true;
    }

    [RelayCommand]
    private async Task EnableSubfoldersForCurrentSelectionAsync()
    {
        IncludeSubfolders = true;
        await ScanDirectoryAsync();
    }

    public async Task SetSourcePathAsync(string sourcePath)
    {
        SourceDirectory = sourcePath;
        await ScanDirectoryAsync();
    }

    public void PreviewDropPath(string sourcePath)
    {
        DragDropState = ResolveDragDropState(sourcePath);
    }

    public void ClearDropPreview()
    {
        DragDropState = "Normal";
    }

    [RelayCommand]
    private async Task ScanDirectoryAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceDirectory) || IsConverting) return;

        CancellationTokenSource scanCancellation = ResetScanCancellation();
        var token = scanCancellation.Token;

        IsScanning = true;
        ProgressStatus = ResourceText("Msg_ScanProgress", "Scanning...");

        try
        {
            ImageScanResult scanResult = await ScanSourcePathAsync(token);
            ApplyScanResult(scanResult);
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("Source scan cancelled.");
        }
        catch (Exception ex)
        {
            ApplyScanError(ex);
        }
        finally
        {
            FinishCurrentScan(token);
        }
    }

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceDirectory) || IsConverting) return;

        await EnsureSourceScannedAsync();
        if (!FoundFiles.Any())
        {
            ShowNoSupportedFilesMessage();
            return;
        }

        await ConvertFoundFilesAsync();
    }

    private bool CanConvert() => !IsConverting && !IsScanning && HasSourceDirectory;

    private async Task EnsureSourceScannedAsync()
    {
        if (!FoundFiles.Any())
        {
            await ScanDirectoryAsync();
        }
    }

    private async Task ConvertFoundFilesAsync()
    {
        _cts = new CancellationTokenSource();
        StartConversionProgress();

        try
        {
            ConversionOptions options = BuildConversionOptions();
            _lastOutputDir = ResolveOutputDirectory();
            await _converterService.ConvertFilesAsync(FoundFiles, _lastOutputDir, options, CreateProgressReporter(), _cts.Token);
            ShowSuccessfulConversion();
        }
        catch (ConversionBatchException ex)
        {
            ShowPartialConversion(ex);
        }
        catch (OperationCanceledException)
        {
            ShowCancelledConversion();
        }
        catch (Exception ex)
        {
            ShowConversionError(ex);
        }
        finally
        {
            FinishConversion();
        }
    }

    private CancellationTokenSource ResetScanCancellation()
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        return _scanCts;
    }

    private async Task<ImageScanResult> ScanSourcePathAsync(CancellationToken token)
    {
        string sourcePath = SourceDirectory ?? string.Empty;
        return await Task.Run(() => _fileService.ScanImagesInPath(sourcePath, IncludeSubfolders), token);
    }

    private void ApplyScanResult(ImageScanResult scanResult)
    {
        FoundFiles.Clear();
        foreach (string filePath in scanResult.SupportedFiles)
        {
            FoundFiles.Add(filePath);
        }

        SkippedFiles = scanResult.SkippedFiles;
        HasNestedFolders = scanResult.HasNestedFolders;
        ProgressValue = 0;
        ProgressMax = FoundFiles.Count;
        ProgressStatus = BuildScanStatus(scanResult);
        NotifySourceHintsChanged();
    }

    private void ApplyScanError(Exception exception)
    {
        var template = ResourceText("Msg_ErrorScan", "Scan error: {0}");
        ProgressStatus = string.Format(template, ExceptionDisplayMessage.From(exception));
        FoundFiles.Clear();
        NotifySourceHintsChanged();
    }

    private void FinishCurrentScan(CancellationToken token)
    {
        if (_scanCts?.Token.Equals(token) != true) return;

        IsScanning = false;
        ConvertCommand.NotifyCanExecuteChanged();
    }

    private void StartConversionProgress()
    {
        IsConverting = true;
        ProgressValue = 0;
        ProgressMax = FoundFiles.Count;
        ProgressStatus = ResourceText("Mass_Preparing", "Preparing...");
    }

    private ConversionOptions BuildConversionOptions()
    {
        return new ConversionOptions
        {
            TargetFormat = TargetFormat,
            Quality = Quality,
            DeleteOriginalFiles = _settings.DeleteOriginalsByDefault,
            ExtractAllPages = _settings.ExtractAllPagesByDefault || ExtractAllPagesForCurrentConversion
        };
    }

    private IProgress<ConversionProgress> CreateProgressReporter()
    {
        return new Progress<ConversionProgress>(conversionProgress =>
        {
            ProgressValue = conversionProgress.ProcessedFiles;
            var template = ResourceText("Mass_Converting", "Converting: {0} of {1}");
            ProgressStatus = string.Format(template, conversionProgress.ProcessedFiles, conversionProgress.TotalFiles);
        });
    }

    private string ResolveOutputDirectory()
    {
        string selectedTargetDirectory = TargetDirectory ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(selectedTargetDirectory))
        {
            return AvoidCrowdedOutputFolder(selectedTargetDirectory);
        }

        return DefaultOutputDirectoryForSource(SourceDirectory ?? string.Empty);
    }

    private string AvoidCrowdedOutputFolder(string selectedTargetDirectory)
    {
        if (FoundFiles.Count <= 5 || !IsCommonUserFolder(selectedTargetDirectory))
        {
            return selectedTargetDirectory;
        }

        return ReserveConvertedFolder(selectedTargetDirectory);
    }

    private void ShowSuccessfulConversion()
    {
        ProgressStatus = ResourceText("Mass_Completed", "Completed successfully!");
        string title = ResourceText("Msg_SuccessTitle", "Success");
        string message = BuildConversionSummary(FoundFiles.Count, 0);
        NotificationService.Show(title, message, _lastOutputDir, true);
    }

    private void ShowPartialConversion(ConversionBatchException exception)
    {
        int failedFiles = exception.Failures.Count;
        int convertedFiles = Math.Max(0, FoundFiles.Count - failedFiles);
        string title = ResourceText("Msg_PartialTitle", "Completed with errors");
        string message = BuildConversionSummary(convertedFiles, failedFiles);

        ProgressStatus = message;
        NotificationService.Show(title, message, _lastOutputDir, false);
        ShowSnackbar(title, exception.Message, ControlAppearance.Caution, SymbolRegular.Warning24);
    }

    private void ShowCancelledConversion()
    {
        string title = ResourceText("Msg_CancelledTitle", "Cancelled");
        string message = ResourceText("Msg_CancelledMass", "Conversion was cancelled");
        ProgressStatus = message;
        ShowSnackbar(title, message, ControlAppearance.Caution, SymbolRegular.Warning24);
    }

    private void ShowConversionError(Exception exception)
    {
        string title = ResourceText("Msg_ErrorTitle", "Error");
        string template = ResourceText("Msg_ErrorSingle", "Could not convert: {0}");
        string message = string.Format(template, ExceptionDisplayMessage.From(exception));

        ProgressStatus = message;
        ShowSnackbar(title, message, ControlAppearance.Danger, SymbolRegular.ErrorCircle24);
    }

    private void FinishConversion()
    {
        IsConverting = false;
        ExtractAllPagesForCurrentConversion = false;
        _cts?.Dispose();
        _cts = null;
    }

    private void ShowNoSupportedFilesMessage()
    {
        string title = ShowSubfoldersSuggestion
            ? ResourceText("Msg_SubfoldersHintTitle", "Subfolders available")
            : ResourceText("Msg_ErrorTitle", "Error");
        string message = ShowSubfoldersSuggestion
            ? ResourceText("Msg_SubfoldersHint", "This folder has subfolders. Enable them to scan deeper.")
            : ResourceText("Mass_NoSupportedFiles", "No supported files found.");

        NotificationService.Show(title, message, string.Empty, false);
    }

    private string BuildScanStatus(ImageScanResult scanResult)
    {
        if (!scanResult.SourceExists)
        {
            return ResourceText("Mass_SourceNotFound", "Source path was not found.");
        }

        if (scanResult.SupportedFiles.Count == 0)
        {
            return ResourceText("Mass_NoSupportedFiles", "No supported files found.");
        }

        if (scanResult.SkippedFiles > 0)
        {
            var template = ResourceText("Mass_FilesFoundWithSkipped", "Found: {0}. Skipped: {1}.");
            return string.Format(template, scanResult.SupportedFiles.Count, scanResult.SkippedFiles);
        }

        var foundTemplate = ResourceText("Mass_FilesFound", "Files found: {0}");
        return string.Format(foundTemplate, scanResult.SupportedFiles.Count);
    }

    private string BuildConversionSummary(int convertedFiles, int failedFiles)
    {
        var template = ResourceText("Msg_ConversionSummary", "Converted: {0}. Errors: {1}. Skipped: {2}.");
        return string.Format(template, convertedFiles, failedFiles, SkippedFiles);
    }

    private string ResolveDragDropState(string sourcePath)
    {
        ImageScanResult scanResult = _fileService.ScanImagesInPath(sourcePath, includeSubfolders: false);
        if (!scanResult.SourceExists || (scanResult.SupportedFiles.Count == 0 && !scanResult.HasNestedFolders))
        {
            return "Invalid";
        }

        return scanResult.SkippedFiles > 0 || scanResult.HasNestedFolders ? "Partial" : "Ready";
    }

    private string BuildDropZoneTitle()
    {
        return DragDropState switch
        {
            "Ready" => ResourceText("Drop_ReadyTitle", "Release to add"),
            "Partial" => ResourceText("Drop_PartialTitle", "Supported files will be added"),
            "Invalid" => ResourceText("Drop_InvalidTitle", "No supported files"),
            _ => ResourceText("Unified_DropZoneTitle", "Drag and drop a file or folder here")
        };
    }

    private string BuildDropZoneSubtitle()
    {
        return DragDropState switch
        {
            "Ready" => ResourceText("Drop_ReadySub", "The selected path is ready for conversion."),
            "Partial" => ResourceText("Drop_PartialSub", "Unsupported files will be skipped."),
            "Invalid" => ResourceText("Drop_InvalidSub", "Choose an image file or a folder with supported images."),
            _ => ResourceText("Unified_FormatsSupported", "JPG, PNG, WEBP, GIF, HEIC, HEIF, PDF and more")
        };
    }

    private void ApplyAutomaticTargetDirectory(string? sourcePath)
    {
        if (_targetDirectorySelectedManually) return;

        SetAutomaticTargetDirectory(DefaultOutputDirectoryForSource(sourcePath ?? string.Empty));
    }

    private void SetAutomaticTargetDirectory(string targetDirectory)
    {
        _isSettingAutomaticTargetDirectory = true;
        TargetDirectory = targetDirectory;
        _isSettingAutomaticTargetDirectory = false;
    }

    private static string DefaultOutputDirectoryForSource(string sourcePath)
    {
        if (File.Exists(sourcePath))
        {
            return Path.GetDirectoryName(sourcePath) ?? string.Empty;
        }

        return Directory.Exists(sourcePath) ? Path.Combine(sourcePath, "converted") : string.Empty;
    }

    private static bool IsCommonUserFolder(string directoryPath)
    {
        string fullTarget = Path.GetFullPath(directoryPath);
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        return string.Equals(fullTarget, desktop, StringComparison.OrdinalIgnoreCase)
               || string.Equals(fullTarget, documents, StringComparison.OrdinalIgnoreCase)
               || string.Equals(fullTarget, pictures, StringComparison.OrdinalIgnoreCase)
               || string.Equals(fullTarget, downloads, StringComparison.OrdinalIgnoreCase)
               || Path.GetPathRoot(fullTarget) == fullTarget;
    }

    private static string ReserveConvertedFolder(string parentDirectory)
    {
        string outputDirectory = Path.Combine(parentDirectory, "converted");
        int counter = 1;

        while (Directory.Exists(outputDirectory))
        {
            outputDirectory = Path.Combine(parentDirectory, $"converted ({counter})");
            counter++;
        }

        return outputDirectory;
    }

    private bool HasExtractableFiles()
    {
        return FoundFiles.Any(ImageFormatRules.MayContainMultipleImages);
    }

    private string BuildConversionNoticeText()
    {
        var notices = new List<string>();
        AddBatchMultiImageNotice(notices);
        AddBatchAlphaNotice(notices);
        return string.Join(Environment.NewLine + Environment.NewLine, notices);
    }

    private void AddBatchMultiImageNotice(ICollection<string> notices)
    {
        bool extractsAllPages = _settings.ExtractAllPagesByDefault || ExtractAllPagesForCurrentConversion;
        if (HasExtractableFiles() && !extractsAllPages)
        {
            notices.Add(ResourceText(
                "Convert_Notice_MultipleMass",
                "Some found files may contain several frames, pages, or icon sizes. Only the first image of each such file will be converted."));
        }
    }

    private void AddBatchAlphaNotice(ICollection<string> notices)
    {
        if (FoundFiles.Any(ImageFormatRules.SourceMayContainAlpha)
            && ImageFormatRules.TargetFormatReplacesAlphaWithWhite(TargetFormat))
        {
            string template = ResourceText(
                "Convert_Notice_AlphaMass",
                "If source files contain transparency, converting to {0} will replace it with a white background.");
            notices.Add(string.Format(template, TargetFormat.ToUpperInvariant()));
        }
    }

    private void NotifySourceHintsChanged()
    {
        OnPropertyChanged(nameof(ConversionNoticeText));
        OnPropertyChanged(nameof(HasConversionNotice));
        OnPropertyChanged(nameof(ShowExtractAllSuggestion));
        OnPropertyChanged(nameof(ShowSubfoldersSuggestion));
    }

    private void ResetScanResults()
    {
        FoundFiles.Clear();
        ProgressStatus = null;
        ProgressMax = 0;
        ProgressValue = 0;
        SkippedFiles = 0;
        HasNestedFolders = false;
        ExtractAllPagesForCurrentConversion = false;
        NotifySourceHintsChanged();
    }

    private void ShowSnackbar(string title, string message, ControlAppearance appearance, SymbolRegular icon)
    {
        _snackbarService.Show(
            title,
            message,
            appearance,
            new SymbolIcon(icon),
            TimeSpan.FromSeconds(6));
    }

    private static string ResourceText(string key, string fallback)
    {
        return Application.Current?.Resources[key] as string ?? fallback;
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    private bool CanCancel() => IsConverting;

    [RelayCommand]
    private void OpenFolder()
    {
        if (Directory.Exists(_lastOutputDir))
        {
            Process.Start("explorer.exe", _lastOutputDir);
        }
    }

    [RelayCommand]
    private void ClearSource()
    {
        SourceDirectory = string.Empty;
        ResetScanResults();
    }

    [RelayCommand]
    private void ClearTarget()
    {
        _targetDirectorySelectedManually = false;
        TargetDirectory = string.Empty;
        ApplyAutomaticTargetDirectory(SourceDirectory);
    }
}
