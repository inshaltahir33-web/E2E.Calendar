using System.Reflection;
using System.Runtime.Versioning;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class ScaffoldTests
{
    [Fact]
    public void Core_assembly_is_deployed_next_to_the_test_assembly()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "E2E.Calendar.Core.dll");

        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void Core_assembly_loads_with_the_expected_identity()
    {
        var assembly = Assembly.Load("E2E.Calendar.Core");

        assembly.GetName().Name.Should().Be("E2E.Calendar.Core");
    }

    [Fact]
    public void Core_targets_plain_net8_and_not_a_windows_flavour()
    {
        var assembly = Assembly.Load("E2E.Calendar.Core");
        var targetFramework = assembly.GetCustomAttribute<TargetFrameworkAttribute>();

        targetFramework.Should().NotBeNull();
        targetFramework!.FrameworkName.Should().Be(".NETCoreApp,Version=v8.0");
    }

    [Theory]
    [InlineData("Microsoft.UI")]
    [InlineData("Windows.")]
    [InlineData("System.Windows")]
    public void Core_does_not_take_a_dependency_on_windows_or_ui_assemblies(string forbiddenPrefix)
    {
        var assembly = Assembly.Load("E2E.Calendar.Core");

        assembly
            .GetReferencedAssemblies()
            .Should()
            .NotContain(a => a.Name != null && a.Name.StartsWith(forbiddenPrefix, StringComparison.OrdinalIgnoreCase));
    }
}
