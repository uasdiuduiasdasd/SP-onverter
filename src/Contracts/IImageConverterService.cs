using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SPConverter.Models;

namespace SPConverter.Contracts;

public interface IImageConverterService
{
    /// <summary>
    /// Массовая конвертация списка файлов.
    /// </summary>
    Task ConvertFilesAsync(
        IEnumerable<string> filePaths, 
        string outputDirectory, 
        ConversionOptions options, 
        IProgress<ConversionProgress> progress, 
        CancellationToken cancellationToken);

    /// <summary>
    /// Конвертация одного файла.
    /// </summary>
    Task ConvertFileAsync(
        string filePath, 
        string outputDirectory, 
        ConversionOptions options, 
        CancellationToken cancellationToken);
}
