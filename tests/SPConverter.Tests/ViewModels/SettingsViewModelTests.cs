using FluentAssertions;
using SPConverter.ViewModels;
using Xunit;
using System.IO;
using System;

namespace SPConverter.Tests.ViewModels;

public class SettingsViewModelTests : IDisposable
{
    private string _originalSettingsPath;

    public SettingsViewModelTests()
    {
        // Override SettingsPath to a temp file for tests
        _originalSettingsPath = SettingsViewModel.SettingsPath;
        SettingsViewModel.SettingsPath = Path.Combine(Path.GetTempPath(), "SPConverterTests_Settings", "test_settings.json");
    }

    public void Dispose()
    {
        if (File.Exists(SettingsViewModel.SettingsPath))
        {
            File.Delete(SettingsViewModel.SettingsPath);
        }
        SettingsViewModel.SettingsPath = _originalSettingsPath;
    }

    [Fact]
    public void Constructor_ShouldSetDefaultValues_IfNoSettingsFileExists()
    {
        // Arrange
        if (File.Exists(SettingsViewModel.SettingsPath))
            File.Delete(SettingsViewModel.SettingsPath);

        // Act
        var sut = new SettingsViewModel();

        // Assert
        sut.CurrentTheme.Should().Be("System");
        // UseTransparency is true or false depending on OS, but it shouldn't be null
        sut.CurrentLanguageIndex.Should().BeInRange(0, 1);
        sut.CurrentLanguageName.Should().Be(sut.CurrentLanguageIndex == 1 ? "English" : "Русский");
        sut.DeleteOriginalsByDefault.Should().BeFalse();
        sut.ExtractAllPagesByDefault.Should().BeFalse();
        sut.AlwaysIncludeSubfolders.Should().BeFalse();
        sut.ShowSupportButtonInHeader.Should().BeTrue();
    }

    [Fact]
    public void Properties_ShouldTriggerSave_WhenChanged()
    {
        // Arrange
        if (File.Exists(SettingsViewModel.SettingsPath))
            File.Delete(SettingsViewModel.SettingsPath);
        
        var sut = new SettingsViewModel();
        
        // Act
        sut.CurrentTheme = "Dark";
        
        // Since SaveSettings is called async via dispatcher in some cases, or synchronously for Language.
        sut.CurrentLanguageIndex = 1; // triggers synchronous SaveSettings
        sut.ShowSupportButtonInHeader = false;

        // Assert
        File.Exists(SettingsViewModel.SettingsPath).Should().BeTrue();
        var json = File.ReadAllText(SettingsViewModel.SettingsPath);
        json.Should().Contain("\"CurrentLanguageIndex\":1");
        json.Should().Contain("\"ShowSupportButtonInHeader\":false");
    }

    [Fact]
    public void Constructor_ShouldNormalizeInvalidSavedValues()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsViewModel.SettingsPath)!);
        File.WriteAllText(
            SettingsViewModel.SettingsPath,
            "{\"CurrentTheme\":\"Solarized\",\"CurrentLanguageIndex\":99,\"ShowSupportButtonInHeader\":true}");

        var sut = new SettingsViewModel();

        sut.CurrentTheme.Should().Be("System");
        sut.CurrentLanguageIndex.Should().Be(0);
        sut.CurrentLanguageName.Should().Be("Русский");
    }
}
