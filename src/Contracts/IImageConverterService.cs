using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SPConverter.Models;

namespace SPConverter.Contracts;

public interface IImageConverterService
{
    /// <summary>
    /// Converts every file that can be processed; throws ConversionBatchException after the batch if any files fail.
    /// </summary>
    Task ConvertFilesAsync(
        IEnumerable<string> filePaths, 
        string outputDirectory, 
        ConversionOptions options, 
        IProgress<ConversionProgress> progress, 
        CancellationToken cancellationToken);

    /// <summary>
    /// Converts one file or propagates the conversion, write, delete, or cancellation error to the caller.
    /// </summary>
    Task ConvertFileAsync(
        string filePath, 
        string outputDirectory, 
        ConversionOptions options, 
        CancellationToken cancellationToken);
}
