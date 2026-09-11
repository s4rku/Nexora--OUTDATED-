using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEXORA.Core.Interfaces;
using NEXORA.Core.Models;

namespace NEXORA.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IHardwareService _hardware;

    [ObservableProperty] private HardwareInfo? _hardwareInfo;
    [ObservableProperty] private LiveMetrics _liveMetrics = new();
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _statusMessage = "Ready";

    public MainViewModel(IHardwareService hardware)
    {
        _hardware = hardware;
    }

    [RelayCommand]
    public async Task ScanHardwareAsync()
    {
        IsScanning = true;
        StatusMessage = "Scanning hardware…";
        try
        {
            HardwareInfo = await _hardware.ScanAsync(
                new Progress<string>(msg => StatusMessage = msg));
            StatusMessage = "Hardware scan complete.";
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
}
