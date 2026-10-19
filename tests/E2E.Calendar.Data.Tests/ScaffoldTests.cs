using System.Reflection;
using System.Runtime.Versioning;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace E2E.Calendar.Data.Tests;

public sealed class ScaffoldTests
{
    [Fact]
    public void Data_assembly_is_deployed_next_to_the_test_assembly()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "E2E.Calendar.Data.dll");

        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void Data_assembly_loads_with_the_expected_identity()
    {
        var assembly = Assembly.Load("E2E.Calendar.Data");

        assembly.GetName().Name.Should().Be("E2E.Calendar.Data");
    }

    [Fact]
    public void Data_targets_plain_net8_and_not_a_windows_flavour()
    {
        var assembly = Assembly.Load("E2E.Calendar.Data");
        var targetFramework = assembly.GetCustomAttribute<TargetFrameworkAttribute>();

        targetFramework.Should().NotBeNull();
        targetFramework!.FrameworkName.Should().Be(".NETCoreApp,Version=v8.0");
    }

    [Theory]
    [InlineData("E2E.Calendar.App.dll")]
    [InlineData("E2E.Calendar.Sync.dll")]
    [InlineData("E2E.Calendar.Platform.dll")]
    public void Data_does_not_pull_in_the_outer_layers(string outerAssembly)
    {
        var path = Path.Combine(AppContext.BaseDirectory, outerAssembly);

        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public void Sqlite_provider_is_usable()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1;";

        command.ExecuteScalar().Should().Be(1L);
    }
}
