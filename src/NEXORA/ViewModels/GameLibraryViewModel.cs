using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEXORA.Core.Interfaces;
using NEXORA.Core.Models;
using System.Collections.ObjectModel;

namespace NEXORA.ViewModels;

public sealed partial class GameLibraryViewModel : ObservableObject
{
    private readonly IGameDetectionService _detection;

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _statusMessage = "Click Scan to find your games.";
    [ObservableProperty] private GameEntry? _selectedGame;
    [ObservableProperty] private string _filterText = string.Empty;

    public ObservableCollection<GameEntry> Games { get; } = new();
    public ObservableCollection<GameEntry> FilteredGames { get; } = new();

    public GameLibraryViewModel(IGameDetectionService detection)
    {
        _detection = detection;
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    public async Task ScanGamesAsync()
    {
        IsScanning = true;
        Games.Clear();
        FilteredGames.Clear();
        StatusMessage = "Scanning for games…";

        try
        {
            var found = await _detection.DetectGamesAsync(
                new Progress<string>(m => StatusMessage = m));

            foreach (var g in found.OrderBy(g => g.Name))
            {
                Games.Add(g);
                FilteredGames.Add(g);
            }

            StatusMessage = Games.Count == 0
                ? "No games found. Supported stores: Steam, Epic, GOG, and more."
                : $"{Games.Count} games found.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void ApplyFilter()
    {
        FilteredGames.Clear();
        var filter = FilterText.Trim().ToLowerInvariant();
        var source = string.IsNullOrEmpty(filter)
            ? Games
            : Games.Where(g => g.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
        foreach (var g in source)
            FilteredGames.Add(g);
    }
}
