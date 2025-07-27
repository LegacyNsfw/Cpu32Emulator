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

    [RelayCommand]
    private async Task LoadProgram()
    {
        _logger.LogInformation("Load Program command executed");
        StatusMessage = "Loading program... (Phase 3 UI placeholder)";
        
        // Simulate loading
        await Task.Delay(500);
        
        DisassemblyText = "00001000    ORG    $1000\n00001000    MOVE.L #$12345678,D0\n00001008    ADD.L  D1,D0\n0000100A    BRA    $1000";
        StatusMessage = "Program loaded successfully";
    }

    [RelayCommand]
    private async Task Run()
    {
        _logger.LogInformation("Run command executed");
        StatusMessage = "Running... (Phase 3 UI placeholder)";
        
        // Simulate execution
        await Task.Delay(100);
        
        RegistersText = "D0: 12345678\nD1: 00000000\nD2: 00000000\nD3: 00000000\nD4: 00000000\nD5: 00000000\nD6: 00000000\nD7: 00000000\n\nA0: 00000000\nA1: 00000000\nA2: 00000000\nA3: 00000000\nA4: 00000000\nA5: 00000000\nA6: 00000000\nA7: 00000000\n\nPC: 00001008\nSR: 0000";
        StatusMessage = "Execution completed";
    }

    [RelayCommand]
    private async Task Step()
    {
        _logger.LogInformation("Step command executed");
        StatusMessage = "Stepping... (Phase 3 UI placeholder)";
        
        // Simulate step
        await Task.Delay(100);
        
        StatusMessage = "Step completed";
    }

    [RelayCommand]
    private void Stop()
    {
        _logger.LogInformation("Stop command executed");
        StatusMessage = "Execution stopped";
    }

    [RelayCommand]
    private void Reset()
    {
        _logger.LogInformation("Reset command executed");
        RegistersText = "D0: 00000000\nD1: 00000000\nD2: 00000000\nD3: 00000000\nD4: 00000000\nD5: 00000000\nD6: 00000000\nD7: 00000000\n\nA0: 00000000\nA1: 00000000\nA2: 00000000\nA3: 00000000\nA4: 00000000\nA5: 00000000\nA6: 00000000\nA7: 00000000\n\nPC: 00000000\nSR: 0000";
        StatusMessage = "CPU reset to initial state";
    }
}
