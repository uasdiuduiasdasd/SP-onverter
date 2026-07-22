using System;
using System.Collections.ObjectModel;
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
    private readonly IImageConverterService _converterService;
    private readonly IFileManagementService _fileService;
    private readonly ISnackbarService _snackbarService;
    
    private string _lastOutputDir = "";
    private CancellationTokenSource? _cts;

    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(HasSourceDirectory))]
    private string? _sourceDirectory;

    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(HasTargetDirectory))]
    private string? _targetDirectory;

    public bool HasSourceDirectory => !string.IsNullOrEmpty(SourceDirectory);
    public bool HasTargetDirectory => !string.IsNullOrEmpty(TargetDirectory);
    [ObservableProperty] 
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    private bool _includeSubfolders;
    [ObservableProperty] private string _targetFormat = "JPEG";
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomQuality))]
    private int _quality = 100;

    partial void OnQualityChanged(int value)
    {
        if (value < 1) _quality = 1;
        else if (value > 100) _quality = 100;
    }

    public bool IsCustomQuality
    {
        get => Quality != 25 && Quality != 50 && Quality != 80 && Quality != 100;
        set
        {
            if (value && (Quality == 25 || Quality == 50 || Quality == 80 || Quality == 100))
            {
                Quality = 90;
            }
        }
    }

    [ObservableProperty] private bool _deleteOriginal;
    [ObservableProperty] private bool _extractAllPages;
    
    [ObservableProperty] 
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isConverting;
    [ObservableProperty] private int _progressValue;
    
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(ShowProgressDetails))]
    private int _progressMax = 0;
    
    [ObservableProperty] private string? _progressStatus;

    public bool ShowProgressDetails => ProgressMax > 0;

    public ObservableCollection<string> FoundFiles { get; } = new();

    public MassConvertViewModel(
        IImageConverterService converterService, 
        IFileManagementService fileService,
        ISnackbarService snackbarService)
    {
        _converterService = converterService;
        _fileService = fileService;
        _snackbarService = snackbarService;
    }

    partial void OnIncludeSubfoldersChanged(bool value)
    {
        HighlightSubfoldersHint = false;
        _ = ScanDirectoryAsync();
    }

    [RelayCommand]
    private async Task BrowseSourceAsync()
    {
        var title = Application.Current?.Resources["Dialog_SelectSourceFolderTitle"] as string ?? "Select source folder";
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = title
        };
        if (dialog.ShowDialog() == true)
        {
            SourceDirectory = dialog.FolderName;
            await ScanDirectoryAsync();
        }
    }

    [RelayCommand]
    private void BrowseTarget()
    {
        var title = Application.Current?.Resources["Dialog_SelectFolderTitle"] as string ?? "Select target folder";
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = title
        };
        if (dialog.ShowDialog() == true)
        {
            TargetDirectory = dialog.FolderName;
        }
    }

    private CancellationTokenSource? _scanCts;

    [RelayCommand]
    private async Task ScanDirectoryAsync()
    {
        if (string.IsNullOrEmpty(SourceDirectory)) return;

        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        IsConverting = true;
        ProgressStatus = Application.Current?.Resources["Msg_ScanProgress"] as string ?? "Scanning folder...";

        try
        {
            var files = await Task.Run(() => _fileService.GetImagesInDirectory(SourceDirectory, IncludeSubfolders).ToList(), token);
            token.ThrowIfCancellationRequested();

            FoundFiles.Clear();
            foreach (var f in files) 
            {
                if (token.IsCancellationRequested) break;
                FoundFiles.Add(f);
            }
            
            if (!token.IsCancellationRequested)
            {
                var template = Application.Current?.Resources["Mass_FilesFound"] as string ?? "Files found: {0}";
                ProgressStatus = string.Format(template, FoundFiles.Count);
                ProgressMax = FoundFiles.Count;
                ProgressValue = 0;
            }
        }
        catch (OperationCanceledException)
        {
            // Scanning cancelled by a newer scan request
        }
        catch (Exception ex)
        {
            var errTemplate = Application.Current?.Resources["Msg_ErrorScan"] as string ?? "Scan error: {0}";
            ProgressStatus = string.Format(errTemplate, ex.Message);
            FoundFiles.Clear();
        }
        finally
        {
            IsConverting = false;
            ConvertCommand.NotifyCanExecuteChanged();
        }
    }

    [ObservableProperty] private bool _highlightSubfoldersHint;

    private bool CanConvert() => !IsConverting && HasSourceDirectory;

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAsync()
    {
        if (string.IsNullOrEmpty(SourceDirectory) || IsConverting) return;

        if (!FoundFiles.Any())
        {
            bool hasSubfolders = false;
            try
            {
                if (Directory.Exists(SourceDirectory))
                {
                    hasSubfolders = Directory.EnumerateDirectories(SourceDirectory).Any();
                }
            }
            catch { }

            if (hasSubfolders && !IncludeSubfolders)
            {
                HighlightSubfoldersHint = true;

                var hintTitle = Application.Current?.Resources["Msg_SubfoldersHintTitle"] as string ?? "Subfolders Required";
                var hintMsg = Application.Current?.Resources["Msg_SubfoldersHint"] as string ?? "No images directly in this folder, but subfolders exist. Check \"Include subfolders\".";

                NotificationService.Show(hintTitle, hintMsg, string.Empty, false);
                return;
            }
            else
            {
                var errTitle = Application.Current?.Resources["Msg_ErrorTitle"] as string ?? "Error";
                var noFilesMsg = Application.Current?.Resources["Mass_FolderNotSelected"] as string ?? "No supported files found in selected folder";
                NotificationService.Show(errTitle, noFilesMsg, string.Empty, false);
                return;
            }
        }

        HighlightSubfoldersHint = false;
        _cts = new CancellationTokenSource();
        IsConverting = true;
        ProgressValue = 0;
        ProgressMax = FoundFiles.Count;
        ProgressStatus = Application.Current?.Resources["Mass_Preparing"] as string ?? "Preparing...";

        var progress = new Progress<ConversionProgress>(p =>
        {
            ProgressValue = p.ProcessedFiles;
            var template = Application.Current?.Resources["Mass_Converting"] as string ?? "Converting: {0} of {1}";
            ProgressStatus = string.Format(template, p.ProcessedFiles, p.TotalFiles);
        });

        try
        {
            var options = new ConversionOptions
            {
                TargetFormat = TargetFormat,
                Quality = Quality,
                DeleteOriginalFiles = DeleteOriginal,
                ExtractAllPages = ExtractAllPages
            };
            
            _lastOutputDir = string.IsNullOrWhiteSpace(TargetDirectory) 
                ? Path.Combine(SourceDirectory, "Converted") 
                : TargetDirectory;

            if (!string.IsNullOrWhiteSpace(TargetDirectory) && FoundFiles.Count > 5)
            {
                string target = Path.GetFullPath(TargetDirectory);
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string pics = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                string dl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                
                bool isCluttered = string.Equals(target, desktop, StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(target, docs, StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(target, pics, StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(target, dl, StringComparison.OrdinalIgnoreCase) ||
                                   Path.GetPathRoot(target) == target;

                if (isCluttered)
                {
                    string baseFolderName = "Converted";
                    string newFolder = Path.Combine(target, baseFolderName);
                    int counter = 1;
                    
                    while (Directory.Exists(newFolder))
                    {
                        newFolder = Path.Combine(target, $"{baseFolderName} ({counter})");
                        counter++;
                    }
                    
                    _lastOutputDir = newFolder;
                }
            }

            await _converterService.ConvertFilesAsync(FoundFiles, _lastOutputDir, options, progress, _cts.Token);
            ProgressStatus = Application.Current?.Resources["Mass_Completed"] as string ?? "Successfully completed!";
            
            var successTitle = Application.Current?.Resources["Msg_SuccessTitle"] as string ?? "Success";
            var successTemplate = Application.Current?.Resources["Msg_SuccessMass"] as string ?? "Converted files: {0}";
            string successMsg = string.Format(successTemplate, FoundFiles.Count);

            NotificationService.Show(successTitle, successMsg, _lastOutputDir, true);
        }
        catch (OperationCanceledException)
        {
            var cancelledTitle = Application.Current?.Resources["Msg_CancelledTitle"] as string ?? "Cancelled";
            var cancelledMsg = Application.Current?.Resources["Msg_CancelledMass"] as string ?? "Mass conversion was cancelled";
            ProgressStatus = cancelledMsg;

            _snackbarService.Show(
                cancelledTitle,
                cancelledMsg,
                ControlAppearance.Caution,
                new SymbolIcon(SymbolRegular.Warning24),
                TimeSpan.FromSeconds(3)
            );
        }
        catch (Exception ex)
        {
            var errTitle = Application.Current?.Resources["Msg_ErrorTitle"] as string ?? "Error";
            var errTemplate = Application.Current?.Resources["Msg_ErrorSingle"] as string ?? "Could not convert: {0}";
            ProgressStatus = string.Format(errTemplate, ex.Message);

            _snackbarService.Show(
                errTitle,
                string.Format(errTemplate, ex.Message),
                ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(6)
            );
        }
        finally
        {
            IsConverting = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private bool CanCancel() => IsConverting;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cts?.Cancel();
    }
    
    [RelayCommand]
    private void OpenFolder()
    {
        if (Directory.Exists(_lastOutputDir))
            Process.Start("explorer.exe", _lastOutputDir);
    }

    [RelayCommand]
    private void ClearSource()
    {
        SourceDirectory = string.Empty;
        FoundFiles.Clear();
        ProgressStatus = null;
        ProgressMax = 0;
        ProgressValue = 0;
    }

    [RelayCommand]
    private void ClearTarget()
    {
        TargetDirectory = string.Empty;
    }
}
