using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class ThemeHardcodedColorTests
{
    [Fact]
    public void Application_xaml_does_not_bypass_theme_tokens()
    {
        var appRoot = FindRepositoryRoot();
        var violations = Directory
            .EnumerateFiles(Path.Combine(appRoot, "src", "E2E.Calendar.App"), "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith("Themes\\Tokens.xaml", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => (path, line, lineNumber: index + 1)))
            .Where(item => Regex.IsMatch(
                item.line,
                "#[0-9a-fA-F]{3,8}|Colors\\.[A-Za-z]+|new\\s+SolidColorBrush|(?:Background|Foreground)\\s*=\\s*\"(?:White|Black)\""))
            .Select(item => $"{item.path}:{item.lineNumber}: {item.line.Trim()}")
            .ToArray();

        violations.Should().BeEmpty("visual colors must come from Tokens.xaml ThemeResource dictionaries");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "E2E.Calendar.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
