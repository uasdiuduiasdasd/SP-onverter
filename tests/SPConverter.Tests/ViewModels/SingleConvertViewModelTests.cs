using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using SPConverter.Contracts;
using SPConverter.Models;
using SPConverter.ViewModels;
using Wpf.Ui;
using Xunit;

namespace SPConverter.Tests.ViewModels;

public class SingleConvertViewModelTests
{
    private readonly Mock<IImageConverterService> _converterServiceMock;
    private readonly Mock<ISnackbarService> _snackbarServiceMock;
    
    public SingleConvertViewModelTests()
    {
        _converterServiceMock = new Mock<IImageConverterService>();
        _snackbarServiceMock = new Mock<ISnackbarService>();
    }

    [Fact]
    public void ClearFileCommand_ShouldResetState()
    {
        // Arrange
        var sut = new SingleConvertViewModel(_converterServiceMock.Object, _snackbarServiceMock.Object)
        {
            SelectedFile = "C:\\test\\image.jpg"
        };

        // Act
        sut.ClearFileCommand.Execute(null);

        // Assert
        sut.SelectedFile.Should().BeNullOrEmpty();
        sut.HasSelectedFile.Should().BeFalse();
    }

    [Fact]
    public async Task ConvertAsync_ShouldCallConverterService()
    {
        // Arrange
        var sut = new SingleConvertViewModel(_converterServiceMock.Object, _snackbarServiceMock.Object)
        {
            SelectedFile = "C:\\test\\image.png",
            TargetFormat = "JXL",
            Quality = 95
        };

        // Act
        await sut.ConvertCommand.ExecuteAsync(null);

        // Assert
        _converterServiceMock.Verify(x => x.ConvertFileAsync(
            "C:\\test\\image.png",
            "C:\\test",
            It.Is<ConversionOptions>(o => o.TargetFormat == "JXL" && o.Quality == 95),
            It.IsAny<CancellationToken>()
        ), Times.Once);
        
        sut.IsConverting.Should().BeFalse();
    }
}
