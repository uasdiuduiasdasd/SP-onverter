using System;
using System.Collections.Concurrent;
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
    private sealed record TargetFormatSpec(string Name, string Extension, MagickFormat Format);

    private static readonly uint[] IcoOutputSizes = { 256, 128, 64, 48, 32, 16 };

    private static readonly IReadOnlyDictionary<string, TargetFormatSpec> TargetFormats =
        new Dictionary<string, TargetFormatSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["JPG"] = new("JPG", ".jpg", MagickFormat.Jpeg),
            ["JPEG"] = new("JPEG", ".jpeg", MagickFormat.Jpeg),
            ["PNG"] = new("PNG", ".png", MagickFormat.Png),
            ["WEBP"] = new("WEBP", ".webp", MagickFormat.WebP),
            ["AVIF"] = new("AVIF", ".avif", MagickFormat.Avif),
            ["BMP"] = new("BMP", ".bmp", MagickFormat.Bmp),
            ["TGA"] = new("TGA", ".tga", MagickFormat.Tga),
            ["HEIC"] = new("HEIC", ".heic", MagickFormat.Heic),
            ["HEIF"] = new("HEIF", ".heif", MagickFormat.Heic),
            ["TIFF"] = new("TIFF", ".tiff", MagickFormat.Tiff),
            ["ICO"] = new("ICO", ".ico", MagickFormat.Ico),
            ["JXL"] = new("JXL", ".jxl", MagickFormat.Jxl),
            ["PDF"] = new("PDF", ".pdf", MagickFormat.Pdf),
            ["GIF"] = new("GIF", ".gif", MagickFormat.Gif),
            ["PSD"] = new("PSD", ".psd", MagickFormat.Psd),
            ["SVG"] = new("SVG", ".svg", MagickFormat.Svg),
            ["DDS"] = new("DDS", ".dds", MagickFormat.Dds),
            ["EXR"] = new("EXR", ".exr", MagickFormat.Exr),
            ["PPM"] = new("PPM", ".ppm", MagickFormat.Ppm),
            ["PGM"] = new("PGM", ".pgm", MagickFormat.Pgm),
            ["PBM"] = new("PBM", ".pbm", MagickFormat.Pbm)
        };

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
        var conversionFailures = new ConcurrentBag<ConversionFailure>();
        TargetFormatSpec targetFormat = ResolveTargetFormat(options.TargetFormat);

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
                await ConvertFileInternalAsync(filePath, outputDirectory, options, targetFormat, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                conversionFailures.Add(new ConversionFailure(filePath, ex.Message));
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

        if (!conversionFailures.IsEmpty)
        {
            throw new ConversionBatchException(conversionFailures.OrderBy(failure => failure.FilePath).ToList());
        }
    }

    public async Task ConvertFileAsync(
        string filePath, 
        string outputDirectory, 
        ConversionOptions options, 
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        TargetFormatSpec targetFormat = ResolveTargetFormat(options.TargetFormat);
        await ConvertFileInternalAsync(filePath, outputDirectory, options, targetFormat, cancellationToken);
    }

    private async Task ConvertFileInternalAsync(
        string filePath, 
        string outputDirectory, 
        ConversionOptions options, 
        TargetFormatSpec targetFormat,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string targetExt = targetFormat.Extension;
        MagickFormat format = targetFormat.Format;

        bool isNonAlphaFormat = format == MagickFormat.Jpeg || format == MagickFormat.Bmp;

        bool isQualityFormat = ImageFormatRules.TargetFormatUsesQuality(targetFormat.Name);

        int effectiveQuality = EffectiveQualityForFormat(targetFormat, options.Quality);
        var generatedFiles = new List<string>();

        void PrepareImageForOutput(IMagickImage<ushort> img)
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
                img.Quality = (uint)effectiveQuality;
            }
        }

        void ProcessAndSave(IMagickImage<ushort> img, string savePath)
        {
            if (format == MagickFormat.Ico)
            {
                SaveIconSet(img, savePath);
                return;
            }

            PrepareImageForOutput(img);
            img.Write(savePath);
        }

        void SaveReservedOutput(IMagickImage<ushort> img, string savePath)
        {
            bool saved = false;
            try
            {
                ProcessAndSave(img, savePath);
                saved = true;
                generatedFiles.Add(savePath);
            }
            finally
            {
                if (!saved)
                {
                    DeleteReservedOutput(savePath);
                }
            }
        }

        // MagickImage.Write - CPU-интенсивная синхронная операция.
        await Task.Run(() =>
        {
            using var fileStream = File.OpenRead(filePath);
            
            if (options.ExtractAllPages)
            {
                using var collection = new MagickImageCollection(fileStream);
                int count = collection.Count;
                int index = 1;
                foreach (var img in collection)
                {
                    string pageSuffix = count > 1 ? $"_page{index}{targetExt}" : targetExt;
                    string targetFile = _fileService.ReserveUniqueFilePath(filePath, outputDirectory, pageSuffix);
                    SaveReservedOutput(img, targetFile);
                    index++;
                }
            }
            else
            {
                using var image = new MagickImage(fileStream);
                string targetFile = _fileService.ReserveUniqueFilePath(filePath, outputDirectory, targetExt);
                SaveReservedOutput(image, targetFile);
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
                File.Delete(filePath);
            }
        }
    }

    private static void DeleteReservedOutput(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not delete reserved output file {filePath}: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not delete reserved output file {filePath}: {ex.Message}");
        }
    }

    private static int EffectiveQualityForFormat(TargetFormatSpec targetFormat, int requestedQuality)
    {
        int clampedQuality = Math.Clamp(requestedQuality, 1, 100);

        // Magick.NET treats AVIF quality 100 as lossless in the bundled HEIC encoder,
        // which currently fails for normal user images. 99 is the highest stable lossy quality.
        return targetFormat.Format == MagickFormat.Avif && clampedQuality == 100
            ? 99
            : clampedQuality;
    }

    private static void SaveIconSet(IMagickImage<ushort> sourceImage, string savePath)
    {
        sourceImage.AutoOrient();

        using var icons = new MagickImageCollection();
        foreach (uint size in IcoOutputSizes)
        {
            IMagickImage<ushort> icon = sourceImage.Clone();
            icon.Format = MagickFormat.Ico;
            icon.Alpha(AlphaOption.Set);
            icon.BackgroundColor = MagickColors.Transparent;
            icon.Resize(new MagickGeometry(size, size)
            {
                IgnoreAspectRatio = false
            });
            icon.Extent(size, size, Gravity.Center, MagickColors.Transparent);
            icons.Add(icon);
        }

        icons.Write(savePath);
    }

    private static TargetFormatSpec ResolveTargetFormat(string targetFormat)
    {
        if (string.IsNullOrWhiteSpace(targetFormat)
            || !TargetFormats.TryGetValue(targetFormat.Trim(), out TargetFormatSpec? targetFormatSpec))
        {
            throw new NotSupportedException($"Output format '{targetFormat}' is not supported.");
        }

        var formatInfo = MagickFormatInfo.Create(targetFormatSpec.Format);
        if (formatInfo?.SupportsWriting != true)
        {
            throw new NotSupportedException(
                $"Output format '{targetFormatSpec.Name}' cannot be written by this build of SP Converter.");
        }

        return targetFormatSpec;
    }
}
