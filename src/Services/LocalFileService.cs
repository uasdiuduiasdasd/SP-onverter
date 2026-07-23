using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SPConverter.Contracts;
using SPConverter.Models;

namespace SPConverter.Services;

public class LocalFileService : IFileManagementService
{
    private static readonly object _fileLock = new();

    public ImageScanResult ScanImagesInPath(string path, bool includeSubfolders)
    {
        if (File.Exists(path))
        {
            bool isSupportedFile = IsSupportedImage(path);
            return new ImageScanResult
            {
                SupportedFiles = isSupportedFile ? new[] { path } : Array.Empty<string>(),
                SkippedFiles = isSupportedFile ? 0 : 1,
                SourceExists = true,
                IsSingleFile = true
            };
        }

        if (!Directory.Exists(path))
        {
            return new ImageScanResult();
        }

        return ScanDirectory(path, includeSubfolders);
    }

    public IEnumerable<string> GetImagesInDirectory(string directoryPath, bool includeSubfolders)
    {
        return ScanImagesInPath(directoryPath, includeSubfolders).SupportedFiles;
    }

    private ImageScanResult ScanDirectory(string directoryPath, bool includeSubfolders)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = includeSubfolders,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
        };

        try
        {
            var supportedFiles = new List<string>();
            int skippedFiles = 0;

            foreach (string filePath in Directory.EnumerateFiles(directoryPath, "*.*", options))
            {
                if (IsSupportedImage(filePath))
                {
                    supportedFiles.Add(filePath);
                }
                else
                {
                    skippedFiles++;
                }
            }

            return new ImageScanResult
            {
                SupportedFiles = supportedFiles,
                SkippedFiles = skippedFiles,
                HasNestedFolders = HasNestedFolders(directoryPath),
                SourceExists = true
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or PathTooLongException)
        {
            System.Diagnostics.Debug.WriteLine($"Directory enumeration error for {directoryPath}: {ex.Message}");
            return new ImageScanResult
            {
                HasNestedFolders = HasNestedFolders(directoryPath),
                SourceExists = true
            };
        }
    }

    public bool IsSupportedImage(string filePath)
    {
        return ImageFormatRules.IsSupportedInputFile(filePath);
    }

    public string ReserveUniqueFilePath(string originalPath, string outputDirectory, string targetSuffix)
    {
        if (string.IsNullOrWhiteSpace(targetSuffix))
            throw new ArgumentException("Target suffix cannot be empty.", nameof(targetSuffix));

        string originalFileName = Path.GetFileNameWithoutExtension(originalPath);
        string normalizedSuffix = NormalizeTargetSuffix(targetSuffix);
        string targetFileName = originalFileName + normalizedSuffix;
        string targetNameWithoutExtension = Path.GetFileNameWithoutExtension(targetFileName);
        string targetExtension = Path.GetExtension(targetFileName);
        string newFilePath = Path.Combine(outputDirectory, targetFileName);

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
                    newFilePath = Path.Combine(outputDirectory, $"{targetNameWithoutExtension}_{counter}{targetExtension}");
                    counter++;
                }
            }
        }
    }

    private static string NormalizeTargetSuffix(string targetSuffix)
    {
        return targetSuffix.StartsWith(".") || targetSuffix.StartsWith("_")
            ? targetSuffix
            : "." + targetSuffix;
    }

    private static bool HasNestedFolders(string directoryPath)
    {
        try
        {
            return Directory.EnumerateDirectories(directoryPath).Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or PathTooLongException)
        {
            System.Diagnostics.Debug.WriteLine($"Directory subfolder inspection error for {directoryPath}: {ex.Message}");
            return false;
        }
    }
}
