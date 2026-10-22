namespace E2E.Calendar.Data.Tests;

using E2E.Calendar.Data.Repos;
using FluentAssertions;
using Xunit;

public sealed class SettingsRepositoryTests : IAsyncLifetime
{
    private readonly TempDatabase _database = new();
    private SettingsRepository _settings = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _settings = new SettingsRepository(_database.Database);
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task A_missing_key_returns_null()
    {
        var value = await _settings.GetAsync("missing");

        value.Should().BeNull();
    }

    [Fact]
    public async Task A_value_round_trips()
    {
        await _settings.SetAsync("appearance.theme", "Dark");

        var value = await _settings.GetAsync("appearance.theme");

        value.Should().Be("Dark");
    }

    [Fact]
    public async Task Setting_the_same_key_twice_updates_it()
    {
        await _settings.SetAsync("appearance.theme", "Dark");
        await _settings.SetAsync("appearance.theme", "Light");

        var value = await _settings.GetAsync("appearance.theme");

        value.Should().Be("Light");
    }

    [Fact]
    public async Task A_null_value_is_stored_and_returned()
    {
        await _settings.SetAsync("appearance.accent_color", null);

        var value = await _settings.GetAsync("appearance.accent_color");

        value.Should().BeNull();
    }

    [Fact]
    public async Task All_values_can_be_read_as_a_dictionary()
    {
        await _settings.SetAsync("a", "1");
        await _settings.SetAsync("b", "2");

        var values = await _settings.GetAllAsync();

        values.Should().ContainKey("a").WhoseValue.Should().Be("1");
        values.Should().ContainKey("b").WhoseValue.Should().Be("2");
    }
}
