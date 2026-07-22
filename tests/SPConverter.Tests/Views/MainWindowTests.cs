using System;
using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace SPConverter.Tests.Views;

public class MainWindowTests
{
    [Fact]
    public void MainWindow_ShouldHaveSufficientInitialHeight_ToPreventScrolling()
    {
        // Arrange
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string xamlPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "src", "Views", "MainWindow.xaml"));
        
        Assert.True(File.Exists(xamlPath), $"Could not find MainWindow.xaml at {xamlPath}");

        // Act
        var xdoc = XDocument.Load(xamlPath);
        var root = xdoc.Root;
        
        var heightAttr = root?.Attribute("Height")?.Value;
        var minHeightAttr = root?.Attribute("MinHeight")?.Value;

        // Assert
        double height = double.Parse(heightAttr ?? "0");
        double minHeight = double.Parse(minHeightAttr ?? "0");

        height.Should().BeGreaterThanOrEqualTo(800, "Window height must be large enough to show all controls without scrolling");
        minHeight.Should().BeGreaterThanOrEqualTo(650, "Window minimum height must prevent UI squishing");
    }
}
