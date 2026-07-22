using System.Collections.Generic;
using System.Threading.Tasks;

namespace SPConverter.Contracts;

public interface IFileManagementService
{
    /// <summary>
    /// Найти все поддерживаемые изображения в папке.
    /// </summary>
    IEnumerable<string> GetImagesInDirectory(string directoryPath, bool includeSubfolders);
    
    /// <summary>
    /// Проверить, является ли файл изображением (в т.ч. RAW).
    /// </summary>
    bool IsSupportedImage(string filePath);
    
    /// <summary>
    /// Сгенерировать уникальное имя файла, если такой уже существует в папке назначения (из ТЗ).
    /// </summary>
    string GetUniqueFilePath(string originalPath, string outputDirectory, string targetExtension);
}
