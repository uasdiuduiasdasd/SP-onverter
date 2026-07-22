using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using ImageMagick;
using Moq;
using SPConverter.Contracts;
using SPConverter.Models;
using SPConverter.Services;
using Xunit;

namespace SPConverter.Tests.Services;

public class MagickImageConverterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IFileManagementService> _fileServiceMock;

    public MagickImageConverterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SPConverterTests_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        
        _fileServiceMock = new Mock<IFileManagementService>();
        
        // Mock to always return the target name in the temp dir
        _fileServiceMock.Setup(f => f.GetUniqueFilePath(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string src, string dir, string ext) => Path.Combine(dir, Path.GetFileNameWithoutExtension(src) + ext));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private string CreateTestImage(string format = "png", bool hasAlpha = false)
    {
        var filePath = Path.Combine(_tempDir, $"test_image.{format}");
        using var image = new MagickImage(MagickColors.Red, 10, 10);
        
        if (hasAlpha)
        {
            image.Alpha(AlphaOption.Set);
            image.BackgroundColor = MagickColors.Transparent;
        }

        image.Format = format.ToUpperInvariant() switch {
            "PNG" => MagickFormat.Png,
            "JPG" => MagickFormat.Jpeg,
            "JPEG" => MagickFormat.Jpeg,
            "WEBP" => MagickFormat.WebP,
            _ => MagickFormat.Png
        };
        
        image.Write(filePath);
        return filePath;
    }

    [Fact]
    public async Task ConvertFileAsync_ShouldConvertPngToJpg()
    {
        // Arrange
        var sut = new MagickImageConverter(_fileServiceMock.Object);
        var sourceFile = CreateTestImage("png");
        
        var options = new ConversionOptions
        {
            TargetFormat = "JPG",
            Quality = 90,
            DeleteOriginalFiles = false
        };

        // Act
        await sut.ConvertFileAsync(sourceFile, _tempDir, options, CancellationToken.None);

        // Assert
        var expectedFile = Path.Combine(_tempDir, "test_image.jpg");
        File.Exists(expectedFile).Should().BeTrue();
        
        // Verify format
        using var outputImage = new MagickImage(expectedFile);
        outputImage.Format.Should().Be(MagickFormat.Jpeg);
    }

    [Fact]
    public async Task ConvertFileAsync_ShouldRemoveAlphaChannel_WhenConvertingToJpg()
    {
        // Arrange
        var sut = new MagickImageConverter(_fileServiceMock.Object);
        var sourceFile = CreateTestImage("png", hasAlpha: true);
        
        var options = new ConversionOptions
        {
            TargetFormat = "JPG",
            Quality = 90,
            DeleteOriginalFiles = false
        };

        // Act
        await sut.ConvertFileAsync(sourceFile, _tempDir, options, CancellationToken.None);

        // Assert
        var expectedFile = Path.Combine(_tempDir, "test_image.jpg");
        using var outputImage = new MagickImage(expectedFile);
        outputImage.HasAlpha.Should().BeFalse();
        // Since original red image is red, background doesn't matter much for 1x1, but the alpha should be gone.
    }

    [Fact]
    public async Task ConvertFileAsync_ShouldDeleteOriginal_IfOptionIsSet()
    {
        // Arrange
        var sut = new MagickImageConverter(_fileServiceMock.Object);
        var sourceFile = CreateTestImage("png");
        
        var options = new ConversionOptions
        {
            TargetFormat = "WEBP",
            Quality = 100,
            DeleteOriginalFiles = true
        };

        // Act
        await sut.ConvertFileAsync(sourceFile, _tempDir, options, CancellationToken.None);

        // Assert
        File.Exists(sourceFile).Should().BeFalse();
        File.Exists(Path.Combine(_tempDir, "test_image.webp")).Should().BeTrue();
    }
}
