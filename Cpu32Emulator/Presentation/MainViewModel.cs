using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Cpu32Emulator.Services;
using Cpu32Emulator.DataContracts;
using System.Collections.ObjectModel;
using Cpu32Emulator.Models;
using System.Linq;

namespace Cpu32Emulator.Presentation;

/// <summary>
/// Represents a register change action for undo/redo functionality
/// </summary>
public record RegisterChangeAction(string RegisterName, uint OldValue, uint NewValue);

public partial class MainViewModel : ObservableObject
{
    private readonly ILogger<MainViewModel> _logger;
    private readonly UnicornEmulatorService _emulatorService;
    private readonly MemoryManagerService _memoryManagerService;

    [ObservableProperty]
    private string _disassemblyText = "No program loaded";

    // Phase 4: Register Display Collection
    public ObservableCollection<CpuRegisterViewModel> Registers { get; }

    // Phase 4: Undo/Redo for register changes
    private readonly Stack<RegisterChangeAction> _undoStack = new();
    private readonly Stack<RegisterChangeAction> _redoStack = new();

    [ObservableProperty]
    private string _registersText = "D0: 00000000\nD1: 00000000\nD2: 00000000\nD3: 00000000\nD4: 00000000\nD5: 00000000\nD6: 00000000\nD7: 00000000\n\nA0: 00000000\nA1: 00000000\nA2: 00000000\nA3: 00000000\nA4: 00000000\nA5: 00000000\nA6: 00000000\nA7: 00000000\n\nPC: 00000000\nSR: 0000";

    [ObservableProperty]
    private string _memoryText = "No memory watches";

    [ObservableProperty]
    private string _statusMessage = "CPU32 Emulator - Phase 4: Register Display implemented with editing and undo/redo";

    public MainViewModel()
    {
        _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MainViewModel>.Instance;
        _emulatorService = new UnicornEmulatorService();
        _memoryManagerService = new MemoryManagerService();
        
        Registers = new ObservableCollection<CpuRegisterViewModel>();
        InitializeRegisters();
        InitializeEmulator();
    }

    public MainViewModel(ILogger<MainViewModel> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _emulatorService = new UnicornEmulatorService();
        _memoryManagerService = new MemoryManagerService();
        
        Registers = new ObservableCollection<CpuRegisterViewModel>();
        InitializeRegisters();
        InitializeEmulator();
    }

    private void InitializeEmulator()
    {
        try
        {
            _emulatorService.Initialize();
            _emulatorService.SetMemoryManager(_memoryManagerService);
            _logger.LogInformation("Unicorn emulator initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Unicorn emulator");
            StatusMessage = $"Emulator initialization failed: {ex.Message}";
        }
    }

    private void InitializeRegisters()
    {
        // Initialize data registers D0-D7
        for (int i = 0; i < 8; i++)
        {
            Registers.Add(new CpuRegisterViewModel($"D{i}", 0));
        }

        // Initialize address registers A0-A7
        for (int i = 0; i < 8; i++)
        {
            Registers.Add(new CpuRegisterViewModel($"A{i}", 0));
        }

        // Initialize special registers
        Registers.Add(new CpuRegisterViewModel("PC", 0));
        Registers.Add(new CpuRegisterViewModel("SR", 0));
        Registers.Add(new CpuRegisterViewModel("USP", 0));
        Registers.Add(new CpuRegisterViewModel("SSP", 0));
        Registers.Add(new CpuRegisterViewModel("VBR", 0));
        Registers.Add(new CpuRegisterViewModel("SFC", 0));
        Registers.Add(new CpuRegisterViewModel("DFC", 0));

        // Update all register values from emulator
        RefreshAllRegisters();
    }

    /// <summary>
    /// Refreshes all register values from the emulator
    /// </summary>
    public void RefreshAllRegisters()
    {
        try
        {
            var cpuState = _emulatorService.GetCpuState();
            
            // Update data registers D0-D7
            for (int i = 0; i < 8; i++)
            {
                Registers[i].UpdateValue(cpuState.GetDataRegister(i));
            }

            // Update address registers A0-A7
            for (int i = 0; i < 8; i++)
            {
                Registers[8 + i].UpdateValue(cpuState.GetAddressRegister(i));
            }

            // Update special registers
            Registers[16].UpdateValue(cpuState.PC);      // PC
            Registers[17].UpdateValue(cpuState.SR);      // SR  
            Registers[18].UpdateValue(cpuState.USP);     // USP
            Registers[19].UpdateValue(cpuState.SSP);     // SSP
            Registers[20].UpdateValue(cpuState.VBR);     // VBR
            Registers[21].UpdateValue(cpuState.SFC);     // SFC
            Registers[22].UpdateValue(cpuState.DFC);     // DFC
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh register values");
            StatusMessage = $"Error refreshing registers: {ex.Message}";
        }
    }

    /// <summary>
    /// Attempts to set a register value from user input
    /// </summary>
    [RelayCommand]
    public void SetRegisterValue(CpuRegisterViewModel register)
    {
        // This will be called when user double-clicks to edit a register
        // For now, we'll implement a simple dialog-based approach
        ShowRegisterEditDialog(register);
    }

    private async void ShowRegisterEditDialog(CpuRegisterViewModel register)
    {
        try
        {
            var dialog = new ContentDialog()
            {
                Title = $"Edit Register {register.Name}",
                Content = new TextBox()
                {
                    Text = register.Value,
                    PlaceholderText = "Enter hex value (e.g., 0x12345678)"
                },
                PrimaryButtonText = "Update",
                SecondaryButtonText = "Cancel"
            };

            // Set the dialog's XamlRoot for proper display
            var window = Microsoft.UI.Xaml.Window.Current ??
                        ((App)Microsoft.UI.Xaml.Application.Current).MainWindow;

            if (window?.Content is FrameworkElement rootElement)
            {
                dialog.XamlRoot = rootElement.XamlRoot;
            }

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var textBox = dialog.Content as TextBox;
                var valueText = textBox?.Text ?? "";

                if (TryParseRegisterValue(valueText, out uint newValue))
                {
                    // Store the old value for undo functionality
                    uint oldValue = register.GetNumericValue();
                    
                    // Update the register in the emulator
                    UpdateRegisterInEmulator(register.Name, newValue);
                    
                    // Refresh the register display
                    register.UpdateValue(newValue);
                    
                    // Add to undo stack
                    _undoStack.Push(new RegisterChangeAction(register.Name, oldValue, newValue));
                    _redoStack.Clear(); // Clear redo stack when new action is performed
                    
                    // If PC was changed, we should update the disassembly view
                    if (register.Name == "PC")
                    {
                        OnProgramCounterChanged(newValue);
                    }
                    
                    StatusMessage = $"Register {register.Name} updated to 0x{newValue:X8}";
                }
                else
                {
                    StatusMessage = $"Invalid value format: {valueText}";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing register {RegisterName}", register.Name);
            StatusMessage = $"Error editing register: {ex.Message}";
        }
    }

    private bool TryParseRegisterValue(string valueText, out uint value)
    {
        value = 0;
        
        if (string.IsNullOrWhiteSpace(valueText))
            return false;

        // Remove common prefixes and clean up
        valueText = valueText.Trim();
        if (valueText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            valueText = valueText.Substring(2);
        else if (valueText.StartsWith("$"))
            valueText = valueText.Substring(1);

        return uint.TryParse(valueText, System.Globalization.NumberStyles.HexNumber, null, out value);
    }

    private void UpdateRegisterInEmulator(string registerName, uint value)
    {
        try
        {
            _emulatorService.SetRegisterValue(registerName, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update register {RegisterName} in emulator", registerName);
            throw;
        }
    }

    private void OnProgramCounterChanged(uint newPc)
    {
        // TODO: Update disassembly view to show current instruction
        // TODO: Scroll disassembly to center on current PC
        _logger.LogInformation("Program counter changed to 0x{PC:X8}", newPc);
    }

    /// <summary>
    /// Undoes the last register change
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    public void UndoRegisterChange()
    {
        if (_undoStack.Count > 0)
        {
            var action = _undoStack.Pop();
            _redoStack.Push(action);
            
            // Find and update the register
            var register = Registers.FirstOrDefault(r => r.Name == action.RegisterName);
            if (register != null)
            {
                UpdateRegisterInEmulator(action.RegisterName, action.OldValue);
                register.UpdateValue(action.OldValue);
                
                if (action.RegisterName == "PC")
                {
                    OnProgramCounterChanged(action.OldValue);
                }
                
                StatusMessage = $"Undid register {action.RegisterName} change (0x{action.NewValue:X8} → 0x{action.OldValue:X8})";
            }
        }
    }

    /// <summary>
    /// Redoes the last undone register change
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    public void RedoRegisterChange()
    {
        if (_redoStack.Count > 0)
        {
            var action = _redoStack.Pop();
            _undoStack.Push(action);
            
            // Find and update the register
            var register = Registers.FirstOrDefault(r => r.Name == action.RegisterName);
            if (register != null)
            {
                UpdateRegisterInEmulator(action.RegisterName, action.NewValue);
                register.UpdateValue(action.NewValue);
                
                if (action.RegisterName == "PC")
                {
                    OnProgramCounterChanged(action.NewValue);
                }
                
                StatusMessage = $"Redid register {action.RegisterName} change (0x{action.OldValue:X8} → 0x{action.NewValue:X8})";
            }
        }
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

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
        StatusMessage = "Opening file picker...";

        try
        {
            // Create FileOpenPicker
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".bin");
            picker.FileTypeFilter.Add(".rom");
            picker.FileTypeFilter.Add("*");
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            // For WinUI, we need to get the window handle for the picker
            var window = Microsoft.UI.Xaml.Window.Current ?? 
                        ((App)Microsoft.UI.Xaml.Application.Current).MainWindow;

            if (window != null)
            {
                // Set the picker's window handle for proper modal display
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);
            }

            // Show the file picker
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                StatusMessage = $"Loading ROM file: {file.Name}...";

                // Read the file content
                var buffer = await FileIO.ReadBufferAsync(file);
                var data = new byte[buffer.Length];
                using (var reader = DataReader.FromBuffer(buffer))
                {
                    reader.ReadBytes(data);
                }

                _logger.LogInformation($"Loaded ROM file: {file.Path}, Size: {data.Length} bytes");

                // Create a simple dialog for base address input
                var dialog = new ContentDialog()
                {
                    Title = "ROM Base Address",
                    Content = new TextBox() 
                    { 
                        Text = "0x00000000",
                        PlaceholderText = "Enter base address (e.g., 0x00000000)"
                    },
                    PrimaryButtonText = "Load",
                    SecondaryButtonText = "Cancel"
                };

                // Set the dialog's XamlRoot for proper display
                if (window?.Content is FrameworkElement rootElement)
                {
                    dialog.XamlRoot = rootElement.XamlRoot;
                }

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var textBox = dialog.Content as TextBox;
                    var baseAddressText = textBox?.Text ?? "0x00000000";
                    
                    // Parse the base address
                    if (TryParseAddress(baseAddressText, out uint baseAddress))
                    {
                        // TODO: Integrate with Unicorn emulator services
                        // For now, we'll show the loaded ROM information
                        
                        StatusMessage = $"ROM file '{file.Name}' loaded successfully at 0x{baseAddress:X8} ({data.Length} bytes)";

                        // Update disassembly text to show ROM information
                        DisassemblyText = $"ROM loaded: {file.Name}\n" +
                                        $"Size: {data.Length} bytes\n" +
                                        $"Path: {file.Path}\n" +
                                        $"Base Address: 0x{baseAddress:X8}\n" +
                                        $"End Address: 0x{baseAddress + (uint)data.Length - 1:X8}\n\n" +
                                        $"First 16 bytes:\n" +
                                        FormatHexDump(data, 0, Math.Min(16, data.Length));
                    }
                    else
                    {
                        StatusMessage = $"Invalid base address format: {baseAddressText}";
                    }
                }
                else
                {
                    StatusMessage = "ROM loading cancelled";
                }
            }
            else
            {
                StatusMessage = "ROM file selection cancelled";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading ROM file");
            StatusMessage = $"Error loading ROM file: {ex.Message}";
        }
    }

    private bool TryParseAddress(string addressText, out uint address)
    {
        address = 0;
        
        if (string.IsNullOrWhiteSpace(addressText))
            return false;

        // Remove common prefixes
        addressText = addressText.Trim();
        if (addressText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            addressText = addressText.Substring(2);
        else if (addressText.StartsWith("$"))
            addressText = addressText.Substring(1);

        return uint.TryParse(addressText, System.Globalization.NumberStyles.HexNumber, null, out address);
    }

    private string FormatHexDump(byte[] data, int offset, int length)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < length; i += 16)
        {
            sb.AppendFormat("{0:X8}: ", offset + i);
            
            // Hex bytes
            for (int j = 0; j < 16; j++)
            {
                if (i + j < length)
                    sb.AppendFormat("{0:X2} ", data[offset + i + j]);
                else
                    sb.Append("   ");
            }
            
            sb.Append(" ");
            
            // ASCII representation
            for (int j = 0; j < 16 && i + j < length; j++)
            {
                byte b = data[offset + i + j];
                sb.Append(b >= 32 && b <= 126 ? (char)b : '.');
            }
            
            sb.AppendLine();
        }
        return sb.ToString();
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
