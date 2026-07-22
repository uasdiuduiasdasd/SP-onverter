using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SPConverter.Contracts;

namespace SPConverter.Services;

public class LocalFileService : IFileManagementService
{
    // Расширения, которые мы поддерживаем (включая популярные RAW форматы)
    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".avif", ".bmp", ".tiff", ".tif",
        ".tga", ".cr2", ".cr3", ".nef", ".arw", ".dng", ".heic", ".psd", ".svg", ".gif", ".ico", ".jxl", ".pdf"
    };

    private static readonly object _fileLock = new();

    public IEnumerable<string> GetImagesInDirectory(string directoryPath, bool includeSubfolders)
    {
        if (!Directory.Exists(directoryPath))
            return Enumerable.Empty<string>();

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = includeSubfolders,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
        };

        try
        {
            return Directory.EnumerateFiles(directoryPath, "*.*", options)
                            .Where(IsSupportedImage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Directory enumeration error for {directoryPath}: {ex.Message}");
            return Enumerable.Empty<string>();
        }
    }

    public bool IsSupportedImage(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return !string.IsNullOrEmpty(ext) && _supportedExtensions.Contains(ext);
    }

    public string GetUniqueFilePath(string originalPath, string outputDirectory, string targetExtension)
    {
        if (!targetExtension.StartsWith("."))
            targetExtension = "." + targetExtension;

        string originalFileName = Path.GetFileNameWithoutExtension(originalPath);
        string newFilePath = Path.Combine(outputDirectory, originalFileName + targetExtension);

        lock (_fileLock)
        {
            int counter = 1;
            while (true)
            {
                try
                {
                    using (var fs = new FileStream(newFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        // File reserved atomically
                    }
                    return newFilePath;
                }
                catch (IOException)
                {
                    newFilePath = Path.Combine(outputDirectory, $"{originalFileName}_{counter}{targetExtension}");
                    counter++;
                }
            }
        }
    }
}
