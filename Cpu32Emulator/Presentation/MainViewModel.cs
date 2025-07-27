using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Cpu32Emulator.Presentation;

public partial class MainViewModel : ObservableObject
{
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private string _disassemblyText = "No program loaded";

    [ObservableProperty]
    private string _registersText = "D0: 00000000\nD1: 00000000\nD2: 00000000\nD3: 00000000\nD4: 00000000\nD5: 00000000\nD6: 00000000\nD7: 00000000\n\nA0: 00000000\nA1: 00000000\nA2: 00000000\nA3: 00000000\nA4: 00000000\nA5: 00000000\nA6: 00000000\nA7: 00000000\n\nPC: 00000000\nSR: 0000";

    [ObservableProperty]
    private string _memoryText = "No memory watches";

    [ObservableProperty]
    private string _statusMessage = "CPU32 Emulator - Phase 3 implementation ready";

    public MainViewModel()
    {
        _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MainViewModel>.Instance;
    }

    public MainViewModel(ILogger<MainViewModel> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // File Menu Commands
    
    [RelayCommand]
    private async Task NewProject()
    {
        _logger.LogInformation("New Project command executed");
        StatusMessage = "Creating new project...";
        await Task.Delay(100);
        StatusMessage = "New project created";
    }

    [RelayCommand]
    private async Task LoadProject()
    {
        _logger.LogInformation("Load Project command executed");
        StatusMessage = "Loading project...";
        await Task.Delay(300);
        StatusMessage = "Project loaded successfully";
    }

    [RelayCommand]
    private async Task SaveProject()
    {
        _logger.LogInformation("Save Project command executed");
        StatusMessage = "Saving project...";
        await Task.Delay(200);
        StatusMessage = "Project saved successfully";
    }

    [RelayCommand]
    private async Task SaveProjectAs()
    {
        _logger.LogInformation("Save Project As command executed");
        StatusMessage = "Saving project as...";
        await Task.Delay(200);
        StatusMessage = "Project saved to new location";
    }

    [RelayCommand]
    private async Task LoadRom()
    {
        _logger.LogInformation("Load ROM command executed");
        StatusMessage = "Loading ROM file...";
        await Task.Delay(400);
        StatusMessage = "ROM file loaded successfully";
    }

    [RelayCommand]
    private async Task ReloadRom()
    {
        _logger.LogInformation("Reload ROM command executed");
        StatusMessage = "Reloading ROM file...";
        await Task.Delay(300);
        StatusMessage = "ROM file reloaded successfully";
    }

    [RelayCommand]
    private async Task LoadRam()
    {
        _logger.LogInformation("Load RAM command executed");
        StatusMessage = "Loading RAM file...";
        await Task.Delay(300);
        StatusMessage = "RAM file loaded successfully";
    }

    [RelayCommand]
    private async Task ReloadRam()
    {
        _logger.LogInformation("Reload RAM command executed");
        StatusMessage = "Reloading RAM file...";
        await Task.Delay(300);
        StatusMessage = "RAM file reloaded successfully";
    }

    [RelayCommand]
    private async Task LoadLst()
    {
        _logger.LogInformation("Load LST command executed");
        StatusMessage = "Loading LST file...";
        await Task.Delay(300);
        
        // Simulate loading LST content
        DisassemblyText = "MAIN:00001000    ORG    $1000    ; Start of program\nMAIN:00001000    MOVE.L #$12345678,D0    ; Load constant\nMAIN:00001008    ADD.L  D1,D0    ; Add registers\nMAIN:0000100A    BRA    MAIN    ; Branch to start\n\nDATA:00002000    DC.L   $DEADBEEF    ; Data constant";
        StatusMessage = "LST file loaded and disassembly updated";
    }

    [RelayCommand]
    private async Task ReloadLst()
    {
        _logger.LogInformation("Reload LST command executed");
        StatusMessage = "Reloading LST file...";
        await Task.Delay(300);
        StatusMessage = "LST file reloaded successfully";
    }

    [RelayCommand]
    private async Task Settings()
    {
        _logger.LogInformation("Settings command executed");
        StatusMessage = "Opening settings...";
        await Task.Delay(100);
        StatusMessage = "Settings dialog would open here";
    }
}
