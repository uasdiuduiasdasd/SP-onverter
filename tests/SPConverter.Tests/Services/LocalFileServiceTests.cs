using System.IO;
using System.Linq;
using FluentAssertions;
using SPConverter.Services;
using Xunit;

namespace SPConverter.Tests.Services;

public class LocalFileServiceTests
{
    [Fact]
    public void GetUniqueFilePath_ShouldReturnOriginal_IfFileDoesNotExist()
    {
        // Arrange
        var sut = new LocalFileService();
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var originalFile = Path.Combine(tempDir, "test.png");
            
            // Act
            var result = sut.GetUniqueFilePath(originalFile, tempDir, ".jpg");
            
            // Assert
            result.Should().Be(Path.Combine(tempDir, "test.jpg"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GetUniqueFilePath_ShouldAppendNumber_IfFileExists()
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
            var result = sut.GetUniqueFilePath(originalFile, tempDir, ".jpg");
            
            // Assert
            result.Should().Be(Path.Combine(tempDir, "test_1.jpg"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
