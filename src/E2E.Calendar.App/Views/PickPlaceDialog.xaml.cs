using E2E.Calendar.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Threading.Tasks;

namespace E2E.Calendar.App.Views;

public sealed partial class PickPlaceDialog : ContentDialog
{
    private readonly PlaceSearchService _searchService;
    private PlaceSearchResult? _selected;

    public PickPlaceDialog(PlaceSearchService searchService)
    {
        _searchService = searchService;
        InitializeComponent();
    }

    public PlaceSearchResult? SelectedPlace => _selected;

    private async void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            await RunSearchAsync();
        }
    }

    private async void OnSearchClick(object sender, RoutedEventArgs e) => await RunSearchAsync();

    private async Task RunSearchAsync()
    {
        var query = SearchBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        StatusText.Visibility = Visibility.Collapsed;
        ResultsList.ItemsSource = null;

        try
        {
            var results = await _searchService.SearchAsync(query);
            ResultsList.ItemsSource = results;

            if (results.Count == 0)
            {
                StatusText.Text = "No results found.";
                StatusText.Visibility = Visibility.Visible;
            }
        }
        catch (InvalidOperationException ex)
        {
            StatusText.Text = ex.Message;
            StatusText.Visibility = Visibility.Visible;
        }
    }

    private void OnResultSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultsList.SelectedItem is PlaceSearchResult result)
        {
            _selected = result;
            UpdatePreview(result);
        }
    }

    private void UpdatePreview(PlaceSearchResult result)
    {
        try
        {
            var url = $"https://www.openstreetmap.org/export/embed.html?bbox={result.Longitude - 0.01},{result.Latitude - 0.01},{result.Longitude + 0.01},{result.Latitude + 0.01}&layer=mapnik&marker={result.Latitude},{result.Longitude}";
            PreviewMap.Source = new Uri(url);
        }
        catch
        {
            // Preview is best-effort; selection still works.
        }
    }
}
