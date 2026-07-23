using System.Collections.Generic;
using SPConverter.Models;

namespace SPConverter.Contracts;

public interface IFileManagementService
{
    /// <summary>
    /// Просканировать файл или папку и вернуть поддерживаемые файлы вместе со сводкой.
    /// </summary>
    ImageScanResult ScanImagesInPath(string path, bool includeSubfolders);

    /// <summary>
    /// Найти все поддерживаемые изображения в папке.
    /// </summary>
    IEnumerable<string> GetImagesInDirectory(string directoryPath, bool includeSubfolders);
    
    /// <summary>
    /// Проверить, является ли файл изображением (в т.ч. RAW).
    /// </summary>
    bool IsSupportedImage(string filePath);
    
    /// <summary>
    /// Зарезервировать уникальный путь выходного файла в папке назначения.
    /// </summary>
    string ReserveUniqueFilePath(string originalPath, string outputDirectory, string targetSuffix);
}
