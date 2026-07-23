using System.IO;
using System.Linq;
using FluentAssertions;
using SPConverter.Services;
using Xunit;

namespace SPConverter.Tests.Services;

public class LocalFileServiceTests
{
    [Fact]
    public void ReserveUniqueFilePath_ShouldReturnOriginal_IfFileDoesNotExist()
    {
        // Arrange
        var sut = new LocalFileService();
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var originalFile = Path.Combine(tempDir, "test.png");
            
            // Act
            var result = sut.ReserveUniqueFilePath(originalFile, tempDir, ".jpg");
            
            // Assert
            result.Should().Be(Path.Combine(tempDir, "test.jpg"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ReserveUniqueFilePath_ShouldAppendNumber_IfFileExists()
    {
        // Arrange
        var sut = new LocalFileService();
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var originalFile = Path.Combine(tempDir, "test.png");
            var existingTarget = Path.Combine(tempDir, "test.jpg");
            
            // Simulate existing file
            File.WriteAllText(existingTarget, "dummy content");
            
            // Act
            var result = sut.ReserveUniqueFilePath(originalFile, tempDir, ".jpg");
            
            // Assert
            result.Should().Be(Path.Combine(tempDir, "test_1.jpg"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ReserveUniqueFilePath_ShouldPreservePageSuffix()
    {
        var sut = new LocalFileService();
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            var originalFile = Path.Combine(tempDir, "test.png");

            var result = sut.ReserveUniqueFilePath(originalFile, tempDir, "_page1.jpg");

            result.Should().Be(Path.Combine(tempDir, "test_page1.jpg"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GetImagesInDirectory_ShouldFindSupportedFormats()
    {
        // Arrange
        var sut = new LocalFileService();
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "image1.jpg"), "dummy");
            File.WriteAllText(Path.Combine(tempDir, "image2.psd"), "dummy");
            File.WriteAllText(Path.Combine(tempDir, "image3.svg"), "dummy");
            File.WriteAllText(Path.Combine(tempDir, "image4.txt"), "dummy"); // Unsupported
            
            // Act
            var result = sut.GetImagesInDirectory(tempDir, includeSubfolders: false).ToList();
            
            // Assert
            result.Should().HaveCount(3);
            result.Should().Contain(Path.Combine(tempDir, "image1.jpg"));
            result.Should().Contain(Path.Combine(tempDir, "image2.psd"));
            result.Should().Contain(Path.Combine(tempDir, "image3.svg"));
            result.Should().NotContain(Path.Combine(tempDir, "image4.txt"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GetImagesInDirectory_ShouldIncludeSubfolders_IfRequested()
    {
        // Arrange
        var sut = new LocalFileService();
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var subDir = Path.Combine(tempDir, "sub");
        Directory.CreateDirectory(subDir);
        
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "image1.png"), "dummy");
            File.WriteAllText(Path.Combine(subDir, "image2.jxl"), "dummy");
            
            // Act
            var resultWithSubfolders = sut.GetImagesInDirectory(tempDir, includeSubfolders: true).ToList();
            var resultWithoutSubfolders = sut.GetImagesInDirectory(tempDir, includeSubfolders: false).ToList();
            
            // Assert
            resultWithSubfolders.Should().HaveCount(2);
            resultWithoutSubfolders.Should().HaveCount(1);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ScanImagesInPath_ShouldReportSkippedUnsupportedFiles()
    {
        var sut = new LocalFileService();
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "image.png"), "dummy");
            File.WriteAllText(Path.Combine(tempDir, "notes.txt"), "dummy");

            var scanResult = sut.ScanImagesInPath(tempDir, includeSubfolders: false);

            scanResult.SupportedFiles.Should().ContainSingle();
            scanResult.SkippedFiles.Should().Be(1);
            scanResult.SourceExists.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ScanImagesInPath_ShouldReportNestedFoldersWithoutScanningThemByDefault()
    {
        var sut = new LocalFileService();
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "sub"));

        try
        {
            var scanResult = sut.ScanImagesInPath(tempDir, includeSubfolders: false);

            scanResult.HasNestedFolders.Should().BeTrue();
            scanResult.SupportedFiles.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
