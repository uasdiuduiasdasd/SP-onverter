using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using SPConverter.Contracts;
using SPConverter.Models;

namespace SPConverter.Services;

public class MagickImageConverter : IImageConverterService
{
    private readonly IFileManagementService _fileService;

    public MagickImageConverter(IFileManagementService fileService)
    {
        _fileService = fileService;
    }

    public async Task ConvertFilesAsync(
        IEnumerable<string> filePaths, 
        string outputDirectory, 
        ConversionOptions options, 
        IProgress<ConversionProgress> progress, 
        CancellationToken cancellationToken)
    {
        var filesList = filePaths.ToList();
        int totalFiles = filesList.Count;
        int processedFiles = 0;

        if (totalFiles == 0) return;

        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        // Ponytail: Встроенный Parallel.ForEachAsync - идеален. Сам управляет потоками, не съест всю ОЗУ.
        var parallelOptions = new ParallelOptions 
        { 
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken 
        };

        await Parallel.ForEachAsync(filesList, parallelOptions, async (filePath, token) =>
        {
            try
            {
                await ConvertFileInternalAsync(filePath, outputDirectory, options, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error converting {filePath}: {ex.Message}");
            }
            finally
            {
                int processed = Interlocked.Increment(ref processedFiles);
                progress?.Report(new ConversionProgress
                {
                    TotalFiles = totalFiles,
                    ProcessedFiles = processed,
                    CurrentFileName = Path.GetFileName(filePath)
                });
            }
        });
    }

    public async Task ConvertFileAsync(
        string filePath, 
        string outputDirectory, 
        ConversionOptions options, 
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        await ConvertFileInternalAsync(filePath, outputDirectory, options, cancellationToken);
    }

    private async Task ConvertFileInternalAsync(
        string filePath, 
        string outputDirectory, 
        ConversionOptions options, 
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string targetExt = "." + options.TargetFormat.ToLowerInvariant();
        
        var format = options.TargetFormat.ToUpperInvariant() switch
        {
            "JPG" => MagickFormat.Jpeg,
            "JPEG" => MagickFormat.Jpeg,
            "PNG" => MagickFormat.Png,
            "WEBP" => MagickFormat.WebP,
            "AVIF" => MagickFormat.Avif,
            "BMP" => MagickFormat.Bmp,
            "TGA" => MagickFormat.Tga,
            "HEIC" => MagickFormat.Heic,
            "TIFF" => MagickFormat.Tiff,
            "ICO" => MagickFormat.Ico,
            "JXL" => MagickFormat.Jxl,
            "PDF" => MagickFormat.Pdf,
            "GIF" => MagickFormat.Gif,
            _ => MagickFormat.Jpeg
        };

        bool isNonAlphaFormat = format == MagickFormat.Jpeg || 
                                format == MagickFormat.Bmp || 
                                format == MagickFormat.Tga || 
                                format == MagickFormat.Heic || 
                                format == MagickFormat.Avif;

        bool isQualityFormat = format == MagickFormat.Jpeg || 
                               format == MagickFormat.WebP || 
                               format == MagickFormat.Avif || 
                               format == MagickFormat.Heic ||
                               format == MagickFormat.Jxl;

        int clampedQuality = Math.Clamp(options.Quality, 1, 100);

        void ProcessAndSave(IMagickImage<ushort> img, string savePath)
        {
            img.AutoOrient();
            img.Format = format;
            if (isNonAlphaFormat && img.HasAlpha)
            {
                img.BackgroundColor = MagickColors.White;
                img.Alpha(AlphaOption.Remove);
            }
            if (isQualityFormat)
            {
                img.Quality = (uint)clampedQuality;
            }
            img.Write(savePath);
        }

        var generatedFiles = new List<string>();

        // MagickImage.Write - CPU-интенсивная синхронная операция.
        await Task.Run(() =>
        {
            if (options.ExtractAllPages)
            {
                using var collection = new MagickImageCollection(filePath);
                int count = collection.Count;
                int index = 1;
                foreach (var img in collection)
                {
                    string extModifier = count > 1 ? $"_page{index}{targetExt}" : targetExt;
                    string targetFile = _fileService.GetUniqueFilePath(filePath, outputDirectory, extModifier);
                    ProcessAndSave(img, targetFile);
                    generatedFiles.Add(targetFile);
                    index++;
                }
            }
            else
            {
                using var image = new MagickImage(filePath);
                string targetFile = _fileService.GetUniqueFilePath(filePath, outputDirectory, targetExt);
                ProcessAndSave(image, targetFile);
                generatedFiles.Add(targetFile);
            }
            
        }, cancellationToken);

        // Удаляем оригинал только после УСПЕШНОЙ записи новых файлов
        if (options.DeleteOriginalFiles && File.Exists(filePath))
        {
            string fullSource = Path.GetFullPath(filePath);
            
            bool safeToDelete = generatedFiles.All(gf => 
                !string.Equals(fullSource, Path.GetFullPath(gf), StringComparison.OrdinalIgnoreCase) && 
                File.Exists(gf));

            if (safeToDelete && generatedFiles.Count > 0)
            {
                try 
                { 
                    File.Delete(filePath); 
                } 
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not delete original file {filePath}: {ex.Message}");
                }
            }
        }
    }
}
