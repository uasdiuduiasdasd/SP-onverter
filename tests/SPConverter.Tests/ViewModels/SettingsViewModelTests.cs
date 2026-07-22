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

        // Assert
        File.Exists(SettingsViewModel.SettingsPath).Should().BeTrue();
        var json = File.ReadAllText(SettingsViewModel.SettingsPath);
        json.Should().Contain("\"CurrentLanguageIndex\":1");
    }
}
