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
        string newFilePath = _fileService.GetUniqueFilePath(filePath, outputDirectory, targetExt);

        // MagickImage.Write - CPU-интенсивная синхронная операция.
        // Заворачиваем в Task.Run, чтобы гарантированно не заморозить UI-поток.
        await Task.Run(() =>
        {
            using var image = new MagickImage(filePath);
            
            // Авто-ориентация на основе EXIF метаданных (для вертикальных фото с телефонов и камер)
            image.AutoOrient();

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
                _ => MagickFormat.Jpeg
            };

            image.Format = format;

            // Если исходный файл имеет прозрачность (например, PNG логотип),
            // а целевой формат ее не поддерживает (JPG, BMP, TGA, HEIC, AVIF), подставляем белый фон.
            bool isNonAlphaFormat = format == MagickFormat.Jpeg || 
                                    format == MagickFormat.Bmp || 
                                    format == MagickFormat.Tga || 
                                    format == MagickFormat.Heic || 
                                    format == MagickFormat.Avif;

            if (isNonAlphaFormat && image.HasAlpha)
            {
                image.BackgroundColor = MagickColors.White;
                image.Alpha(AlphaOption.Remove);
            }

            // Качество (сжатие с потерями) имеет смысл только для этих форматов.
            if (format == MagickFormat.Jpeg || 
                format == MagickFormat.WebP || 
                format == MagickFormat.Avif || 
                format == MagickFormat.Heic)
            {
                int clampedQuality = Math.Clamp(options.Quality, 1, 100);
                image.Quality = (uint)clampedQuality;
            }
            
            image.Write(newFilePath);
            
        }, cancellationToken);

        // Удаляем оригинал только после УСПЕШНОЙ записи нового отличного файла
        if (options.DeleteOriginalFiles && File.Exists(filePath))
        {
            string fullSource = Path.GetFullPath(filePath);
            string fullTarget = Path.GetFullPath(newFilePath);

            if (!string.Equals(fullSource, fullTarget, StringComparison.OrdinalIgnoreCase) && File.Exists(newFilePath))
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
