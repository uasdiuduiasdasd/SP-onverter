using System;
using System.Collections.Generic;
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

public class MassConvertViewModelTests
{
    private readonly Mock<IImageConverterService> _converterServiceMock;
    private readonly Mock<IFileManagementService> _fileServiceMock;
    private readonly Mock<ISnackbarService> _snackbarServiceMock;

    public MassConvertViewModelTests()
    {
        _converterServiceMock = new Mock<IImageConverterService>();
        _fileServiceMock = new Mock<IFileManagementService>();
        _snackbarServiceMock = new Mock<ISnackbarService>();
    }

    [Fact]
    public void ClearSourceCommand_ShouldResetState()
    {
        // Arrange
        var sut = new MassConvertViewModel(_converterServiceMock.Object, _fileServiceMock.Object, _snackbarServiceMock.Object)
        {
            SourceDirectory = "C:\\test",
            ProgressStatus = "Done",
            ProgressMax = 10,
            ProgressValue = 5
        };
        sut.FoundFiles.Add("C:\\test\\1.png");

        // Act
        sut.ClearSourceCommand.Execute(null);

        // Assert
        sut.SourceDirectory.Should().BeEmpty();
        sut.FoundFiles.Should().BeEmpty();
        sut.ProgressStatus.Should().BeNull();
        sut.ProgressMax.Should().Be(0);
        sut.ProgressValue.Should().Be(0);
    }

    [Fact]
    public async Task ScanDirectoryCommand_ShouldPopulateFoundFiles()
    {
        // Arrange
        var sut = new MassConvertViewModel(_converterServiceMock.Object, _fileServiceMock.Object, _snackbarServiceMock.Object)
        {
            SourceDirectory = "C:\\test",
            IncludeSubfolders = true
        };

        _fileServiceMock.Setup(x => x.GetImagesInDirectory("C:\\test", true))
            .Returns(new List<string> { "C:\\test\\1.png", "C:\\test\\sub\\2.jpg" });

        // Act
        await sut.ScanDirectoryCommand.ExecuteAsync(null);

        // Assert
        sut.FoundFiles.Should().HaveCount(2);
        sut.ProgressMax.Should().Be(2);
        sut.ProgressValue.Should().Be(0);
    }
}
