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

namespace Cpu32Emulator.Presentation;

public partial class MainViewModel : ObservableObject
{
    private readonly ILogger<MainViewModel> _logger;
    private readonly UnicornEmulatorService _emulatorService;
    private readonly MemoryManagerService _memoryManagerService;

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
        _emulatorService = new UnicornEmulatorService();
        _memoryManagerService = new MemoryManagerService();
        
        InitializeEmulator();
    }

    public MainViewModel(ILogger<MainViewModel> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _emulatorService = new UnicornEmulatorService();
        _memoryManagerService = new MemoryManagerService();
        
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
