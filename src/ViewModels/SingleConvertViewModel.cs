using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SPConverter.Contracts;
using SPConverter.Models;
using SPConverter.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace SPConverter.ViewModels;

public partial class SingleConvertViewModel : ViewModelBase
{
    private readonly IImageConverterService _converterService;
    private readonly ISnackbarService _snackbarService;
    
    private string _lastOutputDir = "";
    private CancellationTokenSource? _cts;

    [ObservableProperty] 
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    [NotifyPropertyChangedFor(nameof(SelectedFileName))]
    [NotifyPropertyChangedFor(nameof(HasSelectedFile))]
    private string? _selectedFile;
    
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(HasTargetDirectory))]
    private string? _targetDirectory;

    public string SelectedFileName => string.IsNullOrEmpty(SelectedFile) ? "" : Path.GetFileName(SelectedFile);
    public bool HasSelectedFile => !string.IsNullOrEmpty(SelectedFile);
    public bool HasTargetDirectory => !string.IsNullOrEmpty(TargetDirectory);
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
    [ObservableProperty] 
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isConverting;

    public SingleConvertViewModel(IImageConverterService converterService, ISnackbarService snackbarService)
    {
        _converterService = converterService;
        _snackbarService = snackbarService;
    }

    [RelayCommand]
    private void SelectFile()
    {
        var filter = System.Windows.Application.Current?.Resources["Dialog_ImageFilter"] as string ?? "Images|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tga;*.avif;*.heic;*.cr2;*.nef;*.arw;*.dng|All Files|*.*";
        var title = System.Windows.Application.Current?.Resources["Dialog_SelectImageTitle"] as string ?? "Select image for conversion";

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = filter,
            Title = title
        };
        if (dialog.ShowDialog() == true)
        {
            SelectedFile = dialog.FileName;
        }
    }

    [RelayCommand]
    private void ClearSelectedFile()
    {
        SelectedFile = null;
    }

    [RelayCommand]
    private void BrowseTarget()
    {
        var title = System.Windows.Application.Current?.Resources["Dialog_SelectFolderTitle"] as string ?? "Select target folder";
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = title
        };
        if (dialog.ShowDialog() == true)
        {
            TargetDirectory = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void ClearTarget()
    {
        TargetDirectory = null;
    }

    private bool CanConvert() => !IsConverting && !string.IsNullOrEmpty(SelectedFile);

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAsync()
    {
        if (string.IsNullOrEmpty(SelectedFile) || IsConverting) return;
        
        _cts = new CancellationTokenSource();
        IsConverting = true;
        try
        {
            var options = new ConversionOptions
            {
                TargetFormat = TargetFormat,
                Quality = Quality,
                DeleteOriginalFiles = DeleteOriginal
            };
            
            _lastOutputDir = !string.IsNullOrWhiteSpace(TargetDirectory) 
                ? TargetDirectory 
                : Path.GetDirectoryName(SelectedFile) ?? string.Empty;

            await _converterService.ConvertFileAsync(SelectedFile, _lastOutputDir, options, _cts.Token);
            
            var title = System.Windows.Application.Current?.Resources["Msg_SuccessTitle"] as string ?? "Success";
            var msg = System.Windows.Application.Current?.Resources["Msg_SuccessSingle"] as string ?? "Conversion completed successfully";

            NotificationService.Show(title, msg, _lastOutputDir, true);
        }
        catch (OperationCanceledException)
        {
            var title = System.Windows.Application.Current?.Resources["Msg_CancelledTitle"] as string ?? "Cancelled";
            var msg = System.Windows.Application.Current?.Resources["Msg_CancelledSingle"] as string ?? "Single conversion was cancelled";

            _snackbarService.Show(
                title,
                msg,
                ControlAppearance.Caution,
                new SymbolIcon(SymbolRegular.Warning24),
                TimeSpan.FromSeconds(3)
            );
        }
        catch (Exception ex)
        {
            var title = System.Windows.Application.Current?.Resources["Msg_ErrorTitle"] as string ?? "Error";
            var template = System.Windows.Application.Current?.Resources["Msg_ErrorSingle"] as string ?? "Could not convert: {0}";

            _snackbarService.Show(
                title,
                string.Format(template, ex.Message),
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
    private void ClearFile()
    {
        SelectedFile = null;
    }
}
