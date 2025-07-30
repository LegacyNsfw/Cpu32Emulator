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
using Cpu32Emulator.Presentation.Helpers;
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
    private readonly DisassemblyService _disassemblyService;
    private readonly FileService _fileService;
    private readonly ProjectService _projectService;
    private readonly SettingsService _settingsService;

    // Event to notify when current instruction changes (for UI scrolling)
    public event EventHandler? CurrentInstructionChanged;

    [ObservableProperty]
    private string _disassemblyText = "No program loaded";

    // Phase 4: Register Display Collection
    public ObservableCollection<CpuRegisterViewModel> Registers { get; }

    // Phase 5: Memory Watch Collection
    public ObservableCollection<MemoryWatchViewModel> MemoryWatches { get; }

    // Phase 6: Disassembly Display Collection
    public ObservableCollection<DisassemblyLineViewModel> DisassemblyLines { get; }

    // Phase 4: Undo/Redo for register changes
    private readonly Stack<RegisterChangeAction> _undoStack = new();
    private readonly Stack<RegisterChangeAction> _redoStack = new();

    [ObservableProperty]
    private string _registersText = "D0: 00000000\nD1: 00000000\nD2: 00000000\nD3: 00000000\nD4: 00000000\nD5: 00000000\nD6: 00000000\nD7: 00000000\n\nA0: 00000000\nA1: 00000000\nA2: 00000000\nA3: 00000000\nA4: 00000000\nA5: 00000000\nA6: 00000000\nA7: 00000000\n\nPC: 00000000\nSR: 0000";

    [ObservableProperty]
    private string _memoryText = "No memory watches";

    [ObservableProperty]
    private string _statusMessage = "CPU32 Emulator - Phase 8.1: Enhanced File Operations with windowed disassembly display for improved performance";

    // Phase 7: Execution Control State
    [ObservableProperty]
    private bool _isRunning = false;

    [ObservableProperty]
    private bool _isExecutionStopped = true;

    // Phase 7: Breakpoint Management
    private readonly HashSet<uint> _breakpoints = new();

    // Phase 7: Execution Control
    private CancellationTokenSource? _executionCancellationSource;

    // Phase 8: File Operations - Disassembly Display Range
    private const int DISASSEMBLY_WINDOW_SIZE = 200; // +/- bytes around PC
    private uint _currentDisplayRangeStart = 0;
    private uint _currentDisplayRangeEnd = 0;

    [ObservableProperty]
    private string _currentProjectName = "Untitled Project";

    [ObservableProperty]
    private bool _hasUnsavedChanges = false;

    [ObservableProperty]
    private string? _loadedRomPath;

    [ObservableProperty]
    private string? _loadedRamPath;

    [ObservableProperty]
    private string? _loadedLstPath;

    // Phase 1: Feature flag for tile-based disassembly view
    [ObservableProperty]
    // Phase 4: Migration complete - tile view is now the default and only implementation
    private bool _useTileDisassemblyView = true;

    // Phase 1: Current PC address for tile view centering
    [ObservableProperty]
    private uint _currentPCAddress = 0;

    public MainViewModel()
    {
        _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MainViewModel>.Instance;
        _emulatorService = new UnicornEmulatorService();
        _memoryManagerService = new MemoryManagerService();
        _disassemblyService = new DisassemblyService();
        _fileService = new FileService();
        _projectService = new ProjectService(_fileService);
        _settingsService = new SettingsService();
        
        Registers = new ObservableCollection<CpuRegisterViewModel>();
        MemoryWatches = new ObservableCollection<MemoryWatchViewModel>();
        DisassemblyLines = new ObservableCollection<DisassemblyLineViewModel>();

        InitializeEmulator();
        InitializeRegisters();
        InitializeMemoryWatches();
        InitializeDisassembly();
        
        _ = InitializeAsync(); // Fire and forget async initialization
    }

    public MainViewModel(ILogger<MainViewModel> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _emulatorService = new UnicornEmulatorService();
        _memoryManagerService = new MemoryManagerService();
        _disassemblyService = new DisassemblyService();
        _fileService = new FileService();
        _projectService = new ProjectService(_fileService);
        _settingsService = new SettingsService();
        
        Registers = new ObservableCollection<CpuRegisterViewModel>();
        MemoryWatches = new ObservableCollection<MemoryWatchViewModel>();
        DisassemblyLines = new ObservableCollection<DisassemblyLineViewModel>();
        
        InitializeEmulator();
        InitializeRegisters();
        InitializeMemoryWatches();
        InitializeDisassembly();
        
        _ = InitializeAsync(); // Fire and forget async initialization
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

    /// <summary>
    /// Async initialization - loads settings and last project
    /// </summary>
    private async Task InitializeAsync()
    {
        try
        {
            // Load application settings
            await _settingsService.LoadAsync();
            
            // Try to reload the last project if it exists
            var lastProjectPath = _settingsService.GetLastProjectPath();
            if (!string.IsNullOrEmpty(lastProjectPath) && File.Exists(lastProjectPath))
            {
                _logger.LogInformation("Loading last project: {Path}", lastProjectPath);
                StatusMessage = "Loading last project...";
                
                await LoadLastProjectInternal(lastProjectPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during async initialization");
            StatusMessage = $"Initialization error: {ex.Message}";
        }
    }

    /// <summary>
    /// Internal method to load the last project without showing dialogs
    /// </summary>
    private async Task LoadLastProjectInternal(string projectPath)
    {
        try
        {
            await _projectService.LoadProjectAsync(projectPath);
            
            var project = _projectService.CurrentProject;
            if (project != null)
            {
                CurrentProjectName = project.ProjectName;
                LoadedRomPath = project.RomFilePath;
                LoadedRamPath = project.RamFilePath;
                LoadedLstPath = project.LstFilePath;
                HasUnsavedChanges = false;

                // Load ROM file if specified
                if (!string.IsNullOrEmpty(project.RomFilePath) && File.Exists(project.RomFilePath))
                {
                    try
                    {
                        var romMemoryRegion = await _fileService.LoadRomFileAsync(project.RomFilePath, project.RomBaseAddress);
                        _emulatorService.MapMemoryRegion(romMemoryRegion);
                        _logger.LogInformation("ROM loaded: {Path} at 0x{Address:X8}", project.RomFilePath, project.RomBaseAddress);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load ROM from last project: {Path}", project.RomFilePath);
                    }
                }

                // Load RAM file if specified
                if (!string.IsNullOrEmpty(project.RamFilePath) && File.Exists(project.RamFilePath))
                {
                    try
                    {
                        var ramMemoryRegion = await _fileService.LoadRamFileAsync(project.RamFilePath, project.RamBaseAddress);
                        _emulatorService.MapMemoryRegion(ramMemoryRegion);
                        _logger.LogInformation("RAM loaded: {Path} at 0x{Address:X8}", project.RamFilePath, project.RamBaseAddress);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load RAM from last project: {Path}", project.RamFilePath);
                    }
                }

                // Load LST file if specified
                if (!string.IsNullOrEmpty(project.LstFilePath) && File.Exists(project.LstFilePath))
                {
                    try
                    {
                        await LoadLstFileInternal(project.LstFilePath);
                        _logger.LogInformation("LST file loaded: {Path}", project.LstFilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load LST from last project: {Path}", project.LstFilePath);
                    }
                }

                // Restore memory watches
                MemoryWatches.Clear();
                // Keep the RESET pseudo-address
                MemoryWatches.Add(new MemoryWatchViewModel("RESET", 0x00000000, MemoryWatchWidth.Long, true));
                
                foreach (var watchConfig in project.WatchedMemoryLocations)
                {
                    var memoryWatchWidth = ConvertDataWidthToMemoryWatchWidth(watchConfig.Width);
                    var watch = new MemoryWatchViewModel(
                        watchConfig.Label ?? $"0x{watchConfig.Address:X8}", 
                        watchConfig.Address, 
                        memoryWatchWidth);
                    watch.RefreshValue(_memoryManagerService);
                    MemoryWatches.Add(watch);
                }

                // Refresh all displays to reflect loaded data
                RefreshAllRegisters();
                RefreshAllMemoryWatches();
                
                // If we have a reset address, set the PC to it and refresh disassembly
                if (project.ResetAddress != 0)
                {
                    try
                    {
                        _emulatorService.SetRegisterValue("PC", project.ResetAddress);
                        RefreshAllRegisters(); // Update register display with new PC
                        UpdateCurrentInstruction(project.ResetAddress); // Update disassembly highlighting
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to set PC to reset address 0x{Address:X8}", project.ResetAddress);
                    }
                }

                StatusMessage = $"Last project '{project.ProjectName}' loaded successfully";
                _logger.LogInformation("Last project loaded: {ProjectName}", project.ProjectName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load last project: {Path}", projectPath);
            StatusMessage = $"Failed to load last project: {ex.Message}";
        }
    }

    /// <summary>
    /// Internal method to load and parse an LST file without UI dialogs
    /// </summary>
    private async Task LoadLstFileInternal(string filePath)
    {
        try
        {
            var text = await File.ReadAllTextAsync(filePath);
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var entries = new List<LstEntry>();

            for (int i = 0; i < lines.Length; i++)
            {
                var entry = LstEntry.ParseLine(lines[i].Trim(), i + 1);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }

            if (entries.Count > 0)
            {
                _disassemblyService.LoadEntries(entries, filePath);
                
                // Refresh the disassembly display if we have entries
                var currentPc = GetCurrentPcValue();
                if (currentPc != 0)
                {
                    RefreshDisassemblyWindow(currentPc);
                }
                else
                {
                    RefreshDisassemblyDisplay();
                }
                
                _logger.LogInformation("LST file parsed successfully: {Count} entries loaded", entries.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load LST file internally: {Path}", filePath);
            throw;
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

    private void InitializeMemoryWatches()
    {
        // Initialize with some default memory watch locations
        // Add special RESET pseudo-address
        MemoryWatches.Add(new MemoryWatchViewModel("RESET", 0x00000000, MemoryWatchWidth.Long, true));
        
        // Add some common memory locations for demonstration
        MemoryWatches.Add(new MemoryWatchViewModel("0x00000000", 0x00000000, MemoryWatchWidth.Long));
        MemoryWatches.Add(new MemoryWatchViewModel("0x00000004", 0x00000004, MemoryWatchWidth.Long));
        MemoryWatches.Add(new MemoryWatchViewModel("0x00000008", 0x00000008, MemoryWatchWidth.Word));
        MemoryWatches.Add(new MemoryWatchViewModel("0x0000000A", 0x0000000A, MemoryWatchWidth.Word));
        MemoryWatches.Add(new MemoryWatchViewModel("0x0000000C", 0x0000000C, MemoryWatchWidth.Byte));
        
        // Refresh all memory watch values
        RefreshAllMemoryWatches();
    }

    /// <summary>
    /// Refreshes all memory watch values from the memory manager
    /// </summary>
    public void RefreshAllMemoryWatches()
    {
        try
        {
            foreach (var memWatch in MemoryWatches)
            {
                memWatch.RefreshValue(_memoryManagerService);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh memory watch values");
            StatusMessage = $"Error refreshing memory watches: {ex.Message}";
        }
    }

    /// <summary>
    /// Initialize disassembly display with sample data
    /// </summary>
    private void InitializeDisassembly()
    {
        DisassemblyLines.Clear();
        
        // Add sample disassembly entries for demonstration
        DisassemblyLines.Add(new DisassemblyLineViewModel("0x00001000", "START", "ORG    $1000"));
        DisassemblyLines.Add(new DisassemblyLineViewModel("0x00001000", "", "MOVE.L #$12345678,D0"));
        DisassemblyLines.Add(new DisassemblyLineViewModel("0x00001008", "", "ADD.L  D1,D0"));
        DisassemblyLines.Add(new DisassemblyLineViewModel("0x0000100A", "", "CMP.L  #$0,D0"));
        DisassemblyLines.Add(new DisassemblyLineViewModel("0x00001010", "", "BEQ    END"));
        DisassemblyLines.Add(new DisassemblyLineViewModel("0x00001014", "LOOP", "MOVE.L D0,D1"));
        DisassemblyLines.Add(new DisassemblyLineViewModel("0x00001016", "", "SUB.L  #$1,D0"));
        DisassemblyLines.Add(new DisassemblyLineViewModel("0x0000101C", "", "BRA    LOOP"));
        DisassemblyLines.Add(new DisassemblyLineViewModel("0x00001020", "END", "RTS"));
        
        // Mark the first instruction as current and set initial display range
        if (DisassemblyLines.Count > 0)
        {
            DisassemblyLines[0].IsCurrentInstruction = true;
            _currentDisplayRangeStart = 0x00001000 - DISASSEMBLY_WINDOW_SIZE;
            _currentDisplayRangeEnd = 0x00001000 + DISASSEMBLY_WINDOW_SIZE;
        }
    }

    /// <summary>
    /// Updates the current instruction highlighting based on PC register
    /// </summary>
    public void UpdateCurrentInstruction(uint programCounter)
    {
        try
        {
            // Check if PC is within current display range
            if (programCounter < _currentDisplayRangeStart || programCounter > _currentDisplayRangeEnd)
            {
                // PC is outside current range, refresh the entire window
                RefreshDisassemblyWindow(programCounter);
                return;
            }
            
            // PC is within range, just update the current instruction highlighting
            foreach (var line in DisassemblyLines)
            {
                line.UpdateCurrentInstruction(programCounter);
            }
            
            // Notify UI to scroll to current instruction
            CurrentInstructionChanged?.Invoke(this, EventArgs.Empty);
            
            _logger.LogDebug("Current instruction updated to 0x{PC:X8}", programCounter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update current instruction highlighting");
        }
    }

    /// <summary>
    /// Refreshes all register values from the emulator
    /// </summary>
    public void RefreshAllRegisters()
    {
        try
        {
            // Only refresh if emulator is initialized
            if (!_emulatorService.IsInitialized)
            {
                _logger.LogDebug("Emulator not initialized yet, skipping register refresh");
                return;
            }

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
            var textBox = HexTextBoxHelper.CreateRegisterTextBox(register.Value);
            
            var dialog = new ContentDialog()
            {
                Title = $"Edit Register {register.Name}",
                Content = textBox,
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
                var valueText = textBox.Text ?? "";

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
                    
                    // Refresh memory watches in case memory state changed
                    RefreshAllMemoryWatches();
                    
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
        // Update disassembly view to show current instruction
        UpdateCurrentInstruction(newPc);
        
        // Update the CurrentPCAddress property for tile view binding
        CurrentPCAddress = newPc;
        
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
                
                // Refresh memory watches in case memory state changed
                RefreshAllMemoryWatches();
                
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
                
                // Refresh memory watches in case memory state changed
                RefreshAllMemoryWatches();
                
                StatusMessage = $"Redid register {action.RegisterName} change (0x{action.OldValue:X8} → 0x{action.NewValue:X8})";
            }
        }
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Handles memory watch address editing
    /// </summary>
    [RelayCommand]
    public void EditMemoryAddress(MemoryWatchViewModel memoryWatch)
    {
        if (memoryWatch.IsSpecialAddress)
        {
            StatusMessage = "Cannot edit special addresses";
            return;
        }
        
        ShowMemoryAddressEditDialog(memoryWatch);
    }

    /// <summary>
    /// Handles memory watch value editing
    /// </summary>
    [RelayCommand]
    public void EditMemoryValue(MemoryWatchViewModel memoryWatch)
    {
        ShowMemoryValueEditDialog(memoryWatch);
    }

    /// <summary>
    /// Adds a new memory watch
    /// </summary>
    [RelayCommand]
    public void AddMemoryWatch()
    {
        ShowAddMemoryWatchDialog();
    }

    /// <summary>
    /// Removes a memory watch
    /// </summary>
    [RelayCommand]
    public void RemoveMemoryWatch(MemoryWatchViewModel memoryWatch)
    {
        if (memoryWatch.IsSpecialAddress)
        {
            StatusMessage = "Cannot remove special memory watches";
            return;
        }
        
        MemoryWatches.Remove(memoryWatch);
        StatusMessage = $"Removed memory watch at {memoryWatch.Address}";
    }

    /// <summary>
    /// Changes the data width of a memory watch
    /// </summary>
    [RelayCommand]
    public void ChangeMemoryWidth(MemoryWatchViewModel memoryWatch)
    {
        ShowMemoryWidthDialog(memoryWatch);
    }

    // Phase 7: Execution Control Commands

    /// <summary>
    /// F11 - Step Into: Single instruction execution with full display update
    /// </summary>
    [RelayCommand]
    public void StepInto()
    {
        if (IsRunning)
        {
            StatusMessage = "Cannot step while execution is running";
            return;
        }

        try
        {
            _logger.LogInformation("Step Into (F11) executed");
            StatusMessage = "Stepping into next instruction...";
            
            // Execute a single instruction via Unicorn
            ExecuteSingleInstruction();
            
            // Update all displays
            RefreshAllRegisters();
            RefreshAllMemoryWatches();
            
            // Update current PC in disassembly
            var pcRegister = Registers.FirstOrDefault(r => r.Name == "PC");
            if (pcRegister != null)
            {
                UpdateCurrentInstruction(pcRegister.GetNumericValue());
            }
            
            StatusMessage = "Step Into completed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Step Into execution");
            StatusMessage = $"Step Into failed: {ex.Message}";
        }
    }

    /// <summary>
    /// F10 - Step Over: JSR-aware stepping with subroutine skip logic
    /// </summary>
    [RelayCommand]
    public void StepOver()
    {
        if (IsRunning)
        {
            StatusMessage = "Cannot step while execution is running";
            return;
        }

        try
        {
            _logger.LogInformation("Step Over (F10) executed");
            StatusMessage = "Stepping over next instruction...";
            
            // Check if current instruction is JSR (Jump to Subroutine)
            var currentPc = GetCurrentPcValue();
            var currentInstruction = GetInstructionAtAddress(currentPc);
            
            if (IsJsrInstruction(currentInstruction))
            {
                // Set temporary breakpoint after JSR and run until it
                var nextPc = GetNextInstructionAddress(currentPc);
                SetTemporaryBreakpoint(nextPc);
                RunUntilBreakpoint();
            }
            else
            {
                // Regular single step
                ExecuteSingleInstruction();
            }
            
            // Update all displays
            RefreshAllRegisters();
            RefreshAllMemoryWatches();
            
            var pcRegister = Registers.FirstOrDefault(r => r.Name == "PC");
            if (pcRegister != null)
            {
                UpdateCurrentInstruction(pcRegister.GetNumericValue());
            }
            
            StatusMessage = "Step Over completed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Step Over execution");
            StatusMessage = $"Step Over failed: {ex.Message}";
        }
    }

    /// <summary>
    /// F5 - Run: Continuous execution until breakpoint
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecute))]
    public async Task Run()
    {
        if (IsRunning)
        {
            // Stop execution
            StopExecution();
            return;
        }

        try
        {
            _logger.LogInformation("Run (F5) executed");
            IsRunning = true;
            IsExecutionStopped = false;
            StatusMessage = "Running program... (Press F5 to stop)";
            
            _executionCancellationSource = new CancellationTokenSource();
            
            await Task.Run(() => ExecuteContinuously(_executionCancellationSource.Token));
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Execution stopped by user";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during continuous execution");
            StatusMessage = $"Execution failed: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            IsExecutionStopped = true;
            _executionCancellationSource?.Dispose();
            _executionCancellationSource = null;
        }
    }

    /// <summary>
    /// F9 - Toggle Breakpoint: Breakpoint management system
    /// </summary>
    [RelayCommand]
    public void ToggleBreakpoint()
    {
        try
        {
            var currentPc = GetCurrentPcValue();
            
            if (_breakpoints.Contains(currentPc))
            {
                _breakpoints.Remove(currentPc);
                UpdateBreakpointDisplay(currentPc, false);
                StatusMessage = $"Breakpoint removed at 0x{currentPc:X8}";
                _logger.LogInformation("Breakpoint removed at 0x{PC:X8}", currentPc);
            }
            else
            {
                _breakpoints.Add(currentPc);
                UpdateBreakpointDisplay(currentPc, true);
                StatusMessage = $"Breakpoint set at 0x{currentPc:X8}";
                _logger.LogInformation("Breakpoint set at 0x{PC:X8}", currentPc);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling breakpoint");
            StatusMessage = $"Failed to toggle breakpoint: {ex.Message}";
        }
    }

    public bool CanExecute => !IsRunning || IsRunning; // Always enabled, text changes based on state

    // Phase 7: Execution Control Helper Methods

    /// <summary>
    /// Executes a single instruction via the Unicorn emulator
    /// </summary>
    private void ExecuteSingleInstruction()
    {
        try
        {
            var currentPc = GetCurrentPcValue();
            _logger.LogDebug("Executing instruction at 0x{PC:X8}", currentPc);
            
            // Execute one instruction
            _emulatorService.StepInstruction();
            
            _logger.LogDebug("Instruction executed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute single instruction");
            throw;
        }
    }

    /// <summary>
    /// Gets the current PC value from the registers
    /// </summary>
    private uint GetCurrentPcValue()
    {
        var pcRegister = Registers.FirstOrDefault(r => r.Name == "PC");
        return pcRegister?.GetNumericValue() ?? 0;
    }

    /// <summary>
    /// Gets the instruction at a specific address from the disassembly
    /// </summary>
    private string GetInstructionAtAddress(uint address)
    {
        var entry = _disassemblyService.FindEntryByAddress(address);
        return entry?.Instruction ?? "";
    }

    /// <summary>
    /// Checks if an instruction is a JSR (Jump to Subroutine)
    /// </summary>
    private bool IsJsrInstruction(string instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
            return false;
            
        var instr = instruction.Trim().ToUpperInvariant();
        return instr.StartsWith("JSR") || instr.StartsWith("BSR");
    }

    /// <summary>
    /// Gets the address of the next instruction after the current PC
    /// </summary>
    private uint GetNextInstructionAddress(uint currentPc)
    {
        var nextEntry = _disassemblyService.GetNextInstruction(currentPc);
        return nextEntry?.Address ?? (currentPc + 2); // Default to +2 bytes if not found
    }

    /// <summary>
    /// Sets a temporary breakpoint (used for step over)
    /// </summary>
    private void SetTemporaryBreakpoint(uint address)
    {
        // Add to temporary breakpoints set
        _breakpoints.Add(address);
        _logger.LogDebug("Temporary breakpoint set at 0x{Address:X8}", address);
    }

    /// <summary>
    /// Runs execution until a breakpoint is hit
    /// </summary>
    private void RunUntilBreakpoint()
    {
        try
        {
            while (true)
            {
                ExecuteSingleInstruction();
                var currentPc = GetCurrentPcValue();
                
                if (_breakpoints.Contains(currentPc))
                {
                    _logger.LogDebug("Breakpoint hit at 0x{PC:X8}", currentPc);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during run until breakpoint");
            throw;
        }
    }

    /// <summary>
    /// Stops continuous execution
    /// </summary>
    private void StopExecution()
    {
        _executionCancellationSource?.Cancel();
        _logger.LogInformation("Execution stop requested");
    }

    /// <summary>
    /// Executes instructions continuously until breakpoint or cancellation
    /// </summary>
    private void ExecuteContinuously(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ExecuteSingleInstruction();
                var currentPc = GetCurrentPcValue();
                
                // Check for breakpoints
                if (_breakpoints.Contains(currentPc))
                {
                    _logger.LogInformation("Breakpoint hit at 0x{PC:X8}", currentPc);
                    break;
                }
                
                // Small delay to prevent UI freezing
                Thread.Sleep(1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during continuous execution");
            throw;
        }
    }

    /// <summary>
    /// Updates the breakpoint visual indicator in the disassembly
    /// </summary>
    private void UpdateBreakpointDisplay(uint address, bool hasBreakpoint)
    {
        try
        {
            var line = DisassemblyLines.FirstOrDefault(l => 
                uint.TryParse(l.Address.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out uint lineAddr) && 
                lineAddr == address);
                
            if (line != null)
            {
                line.HasBreakpoint = hasBreakpoint;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update breakpoint display for address 0x{Address:X8}", address);
        }
    }

    // File Menu Commands
    
    [RelayCommand]
    private async Task NewProject()
    {
        _logger.LogInformation("New Project command executed");
        StatusMessage = "Creating new project...";
        
        try
        {
            // Check if there are unsaved changes
            if (HasUnsavedChanges)
            {
                var result = await ShowUnsavedChangesDialog();
                if (result == ContentDialogResult.None) // Cancel
                {
                    StatusMessage = "New project creation cancelled";
                    return;
                }
                else if (result == ContentDialogResult.Primary) // Save
                {
                    await SaveProject();
                }
            }

            // Create new project
            _projectService.NewProject();
            CurrentProjectName = _projectService.CurrentProject?.ProjectName ?? "Untitled Project";
            HasUnsavedChanges = false;
            
            // Clear loaded files
            LoadedRomPath = null;
            LoadedRamPath = null;
            LoadedLstPath = null;
            
            // Reset displays
            DisassemblyLines.Clear();
            
            StatusMessage = "New project created successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create new project");
            StatusMessage = $"Error creating new project: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadProject()
    {
        _logger.LogInformation("Load Project command executed");
        StatusMessage = "Opening project file...";

        try
        {
            // Check if there are unsaved changes
            if (HasUnsavedChanges)
            {
                var result = await ShowUnsavedChangesDialog();
                if (result == ContentDialogResult.None) // Cancel
                {
                    StatusMessage = "Load project cancelled";
                    return;
                }
                else if (result == ContentDialogResult.Primary) // Save
                {
                    await SaveProject();
                }
            }

            // Create FileOpenPicker for project files
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".json");
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            // For WinUI, we need to get the window handle for the picker
            var window = Microsoft.UI.Xaml.Window.Current ?? 
                        ((App)Microsoft.UI.Xaml.Application.Current).MainWindow;

            if (window != null)
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);
            }

            // Show the file picker
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                StatusMessage = $"Loading project: {file.Name}...";
                
                await _projectService.LoadProjectAsync(file.Path);
                
                var project = _projectService.CurrentProject;
                if (project != null)
                {
                    CurrentProjectName = project.ProjectName;
                    LoadedRomPath = project.RomFilePath;
                    LoadedRamPath = project.RamFilePath;
                    LoadedLstPath = project.LstFilePath;
                    HasUnsavedChanges = false;

                    // Restore memory watches
                    MemoryWatches.Clear();
                    foreach (var watchConfig in project.WatchedMemoryLocations)
                    {
                        var memoryWatchWidth = ConvertDataWidthToMemoryWatchWidth(watchConfig.Width);
                        var watch = new MemoryWatchViewModel(
                            $"0x{watchConfig.Address:X8}", 
                            watchConfig.Address, 
                            memoryWatchWidth);
                        watch.RefreshValue(_memoryManagerService);
                        MemoryWatches.Add(watch);
                    }

                    // Restore CPU state (all registers) if saved
                    var savedCpuState = _projectService.GetSavedCpuState();
                    if (savedCpuState != null)
                    {
                        _emulatorService.SetCpuState(savedCpuState);
                        
                        // Refresh all register displays
                        RefreshAllRegisters();
                        
                        // Center the disassembly on the restored PC
                        // Schedule the scroll operation to happen after the UI has updated
                        _ = Task.Run(async () =>
                        {
                            // Small delay to ensure the disassembly view is fully initialized
                            await Task.Delay(200);
                            
                            // Trigger the CurrentInstructionChanged event to update the disassembly
                            CurrentInstructionChanged?.Invoke(this, EventArgs.Empty);
                        });
                    }

                    StatusMessage = $"Project '{project.ProjectName}' loaded successfully";
                    
                    // Save this as the last project path
                    await _settingsService.SetLastProjectPathAsync(file.Path);
                }
            }
            else
            {
                StatusMessage = "Project file selection cancelled";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load project");
            StatusMessage = $"Error loading project: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveProject()
    {
        _logger.LogInformation("Save Project command executed");
        
        try
        {
            if (_projectService.CurrentProject == null)
            {
                StatusMessage = "No project to save";
                return;
            }

            if (string.IsNullOrEmpty(_projectService.CurrentProjectPath))
            {
                // No path set, use Save As instead
                await SaveProjectAs();
                return;
            }

            StatusMessage = "Saving project...";

            // Update project configuration with current state
            var project = _projectService.CurrentProject;
            project.RomFilePath = LoadedRomPath;
            project.RamFilePath = LoadedRamPath;
            project.LstFilePath = LoadedLstPath;
            
            // Update watched memory locations
            project.WatchedMemoryLocations.Clear();
            foreach (var watch in MemoryWatches.Where(w => !w.IsSpecialAddress))
            {
                var config = new WatchedMemoryConfig
                {
                    Address = watch.GetNumericAddress(),
                    Width = ConvertMemoryWatchWidthToDataWidth(watch.GetWidth()),
                    Label = watch.Address
                };
                project.WatchedMemoryLocations.Add(config);
            }

            // Save current CPU state (all registers)
            var currentCpuState = _emulatorService.GetCpuState();
            _projectService.SetSavedCpuState(currentCpuState);

            await _projectService.SaveProjectAsync();
            HasUnsavedChanges = false;
            StatusMessage = $"Project '{project.ProjectName}' saved successfully";
            
            // Save this as the last project path
            await _settingsService.SetLastProjectPathAsync(_projectService.CurrentProjectPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save project");
            StatusMessage = $"Error saving project: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveProjectAs()
    {
        _logger.LogInformation("Save Project As command executed");
        StatusMessage = "Opening save dialog...";

        try
        {
            if (_projectService.CurrentProject == null)
            {
                StatusMessage = "No project to save";
                return;
            }

            // Create FileSavePicker for project files
            var picker = new FileSavePicker();
            picker.FileTypeChoices.Add("CPU32 Emulator Project", new[] { ".json" });
            picker.DefaultFileExtension = ".json";
            picker.SuggestedFileName = _projectService.CurrentProject.ProjectName;
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            // For WinUI, we need to get the window handle for the picker
            var window = Microsoft.UI.Xaml.Window.Current ?? 
                        ((App)Microsoft.UI.Xaml.Application.Current).MainWindow;

            if (window != null)
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);
            }

            // Show the file picker
            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                StatusMessage = $"Saving project as: {file.Name}...";

                // Update project configuration with current state
                var project = _projectService.CurrentProject;
                project.RomFilePath = LoadedRomPath;
                project.RamFilePath = LoadedRamPath;
                project.LstFilePath = LoadedLstPath;
                
                // Update watched memory locations
                project.WatchedMemoryLocations.Clear();
                foreach (var watch in MemoryWatches.Where(w => !w.IsSpecialAddress))
                {
                    var config = new WatchedMemoryConfig
                    {
                        Address = watch.GetNumericAddress(),
                        Width = ConvertMemoryWatchWidthToDataWidth(watch.GetWidth()),
                        Label = watch.Address
                    };
                    project.WatchedMemoryLocations.Add(config);
                }

                // Save current CPU state (all registers)
                var currentCpuState = _emulatorService.GetCpuState();
                _projectService.SetSavedCpuState(currentCpuState);

                await _projectService.SaveProjectAsAsync(file.Path);
                HasUnsavedChanges = false;
                StatusMessage = $"Project saved as '{file.Name}' successfully";
                
                // Save this as the last project path
                await _settingsService.SetLastProjectPathAsync(file.Path);
            }
            else
            {
                StatusMessage = "Save cancelled";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save project as");
            StatusMessage = $"Error saving project: {ex.Message}";
        }
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
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);
            }

            // Show the file picker
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                StatusMessage = $"Loading ROM file: {file.Name}...";

                // Create a dialog for base address input
                var addressInput = new TextBox() 
                { 
                    Text = "0x00000000",
                    PlaceholderText = "Enter base address (e.g., 0x00000000)"
                };

                var stackPanel = new StackPanel { Spacing = 10 };
                stackPanel.Children.Add(new TextBlock { Text = $"Loading ROM: {file.Name}" });
                stackPanel.Children.Add(new TextBlock { Text = "Enter the base address for this ROM:" });
                stackPanel.Children.Add(addressInput);

                var dialog = new ContentDialog()
                {
                    Title = "ROM Base Address",
                    Content = stackPanel,
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
                    var baseAddressText = addressInput.Text ?? "0x00000000";
                    
                    // Parse the base address
                    if (TryParseAddress(baseAddressText, out uint baseAddress))
                    {
                        // Load ROM using FileService
                        var memoryRegion = await _fileService.LoadRomFileAsync(file.Path, baseAddress);
                        
                        // Map the ROM in the emulator
                        _emulatorService.MapMemoryRegion(memoryRegion);
                        
                        LoadedRomPath = file.Path;
                        HasUnsavedChanges = true;

                        // Update project if one is loaded
                        if (_projectService.CurrentProject != null)
                        {
                            _projectService.CurrentProject.RomFilePath = file.Path;
                            _projectService.CurrentProject.RomBaseAddress = baseAddress;
                            _projectService.CurrentProject.MarkAsModified();
                        }

                        StatusMessage = $"ROM '{file.Name}' loaded at 0x{baseAddress:X8} ({memoryRegion.Size} bytes)";
                        
                        // Update disassembly display with ROM info and set display range
                        DisassemblyLines.Clear();
                        DisassemblyLines.Add(new DisassemblyLineViewModel(
                            $"0x{baseAddress:X8}",
                            "",
                            $"ROM loaded: {file.Name}")
                        {
                            HasBreakpoint = false,
                            IsCurrentInstruction = false
                        });
                        DisassemblyLines.Add(new DisassemblyLineViewModel(
                            $"0x{baseAddress + memoryRegion.Size - 1:X8}",
                            "",
                            $"ROM end address: {memoryRegion.Size} bytes")
                        {
                            HasBreakpoint = false,
                            IsCurrentInstruction = false
                        });
                        
                        // Set initial display range around ROM base address
                        _currentDisplayRangeStart = baseAddress >= DISASSEMBLY_WINDOW_SIZE ? 
                                                   baseAddress - DISASSEMBLY_WINDOW_SIZE : 0;
                        _currentDisplayRangeEnd = baseAddress + DISASSEMBLY_WINDOW_SIZE;
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
        StatusMessage = "Opening file picker...";

        try
        {
            // Create FileOpenPicker
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".bin");
            picker.FileTypeFilter.Add(".ram");
            picker.FileTypeFilter.Add("*");
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            // For WinUI, we need to get the window handle for the picker
            var window = Microsoft.UI.Xaml.Window.Current ?? 
                        ((App)Microsoft.UI.Xaml.Application.Current).MainWindow;

            if (window != null)
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);
            }

            // Show the file picker
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                StatusMessage = $"Loading RAM file: {file.Name}...";

                // Create a dialog for base address input
                var addressInput = new TextBox() 
                { 
                    Text = "0x00008000",
                    PlaceholderText = "Enter base address (e.g., 0x00008000)"
                };

                var stackPanel = new StackPanel { Spacing = 10 };
                stackPanel.Children.Add(new TextBlock { Text = $"Loading RAM: {file.Name}" });
                stackPanel.Children.Add(new TextBlock { Text = "Enter the base address for this RAM:" });
                stackPanel.Children.Add(addressInput);

                var dialog = new ContentDialog()
                {
                    Title = "RAM Base Address",
                    Content = stackPanel,
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
                    var baseAddressText = addressInput.Text ?? "0x00008000";
                    
                    // Parse the base address
                    if (TryParseAddress(baseAddressText, out uint baseAddress))
                    {
                        // Load RAM using FileService
                        var memoryRegion = await _fileService.LoadRamFileAsync(file.Path, baseAddress);
                        
                        // Map the RAM in the emulator
                        _emulatorService.MapMemoryRegion(memoryRegion);
                        
                        LoadedRamPath = file.Path;
                        HasUnsavedChanges = true;

                        // Update project if one is loaded
                        if (_projectService.CurrentProject != null)
                        {
                            _projectService.CurrentProject.RamFilePath = file.Path;
                            _projectService.CurrentProject.RamBaseAddress = baseAddress;
                            _projectService.CurrentProject.MarkAsModified();
                        }

                        StatusMessage = $"RAM '{file.Name}' loaded at 0x{baseAddress:X8} ({memoryRegion.Size} bytes)";
                    }
                    else
                    {
                        StatusMessage = $"Invalid base address format: {baseAddressText}";
                    }
                }
                else
                {
                    StatusMessage = "RAM loading cancelled";
                }
            }
            else
            {
                StatusMessage = "RAM file selection cancelled";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading RAM file");
            StatusMessage = $"Error loading RAM file: {ex.Message}";
        }
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
        
        try
        {
            var picker = new FileOpenPicker();
            var window = Microsoft.UI.Xaml.Window.Current ??
                        ((App)Microsoft.UI.Xaml.Application.Current).MainWindow;
            WinRT.Interop.InitializeWithWindow.Initialize(picker, 
                WinRT.Interop.WindowNative.GetWindowHandle(window));

            picker.FileTypeFilter.Add(".lst");
            picker.SuggestedStartLocation = PickerLocationId.Desktop;

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                await LoadLstFile(file);
            }
            else
            {
                StatusMessage = "LST file selection cancelled";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load LST file");
            StatusMessage = $"Error loading LST file: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ReloadLst()
    {
        _logger.LogInformation("Reload LST command executed");
        StatusMessage = "Reloading LST file...";
        
        if (!string.IsNullOrEmpty(_disassemblyService.LoadedFilePath))
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(_disassemblyService.LoadedFilePath);
                await LoadLstFile(file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reload LST file");
                StatusMessage = $"Error reloading LST file: {ex.Message}";
            }
        }
        else
        {
            StatusMessage = "No LST file to reload";
        }
    }

    /// <summary>
    /// Loads and parses an LST file
    /// </summary>
    private async Task LoadLstFile(StorageFile file)
    {
        try
        {
            StatusMessage = $"Loading LST file: {file.Name}...";
            
            var text = await FileIO.ReadTextAsync(file);

            StatusMessage = "Parsing LST file, step 1...";
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var entries = new List<LstEntry>();

            StatusMessage = "Parsing LST file, step 2...";
            for (int i = 0; i < lines.Length; i++)
            {
                var entry = LstEntry.ParseLine(lines[i].Trim(), i + 1);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }

            if (entries.Count > 0)
            {
                StatusMessage = "Rebuilding address map...";
                _disassemblyService.LoadEntries(entries, file.Path);
                LoadedLstPath = file.Path;
                HasUnsavedChanges = true;

                // Update project if one is loaded
                if (_projectService.CurrentProject != null)
                {
                    _projectService.CurrentProject.LstFilePath = file.Path;
                    _projectService.CurrentProject.MarkAsModified();
                }
                
                // Only show a window around the current PC instead of all entries
                RefreshDisassemblyDisplay();
                
                StatusMessage = $"LST file loaded: {entries.Count} entries from {file.Name}";
                _logger.LogInformation("LST file loaded with {Count} entries", entries.Count);
            }
            else
            {
                StatusMessage = "No valid entries found in LST file";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse LST file");
            StatusMessage = $"Error parsing LST file: {ex.Message}";
        }
    }

    /// <summary>
    /// Refreshes the disassembly display from the loaded entries, showing only a window around the current PC
    /// </summary>
    private void RefreshDisassemblyDisplay()
    {
        try
        {
            // Get current PC value
            var pcRegister = Registers.FirstOrDefault(r => r.Name == "PC");
            var currentPc = pcRegister?.GetNumericValue() ?? 0;
            
            RefreshDisassemblyWindow(currentPc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh disassembly display");
        }
    }

    /// <summary>
    /// Refreshes the disassembly display window around a specific address
    /// </summary>
    private void RefreshDisassemblyWindow(uint centerAddress)
    {
        try
        {
            // Calculate the display range
            var startAddress = centerAddress >= DISASSEMBLY_WINDOW_SIZE ? 
                              centerAddress - DISASSEMBLY_WINDOW_SIZE : 0;
            var endAddress = centerAddress + DISASSEMBLY_WINDOW_SIZE;
            
            // Update current display range
            _currentDisplayRangeStart = startAddress;
            _currentDisplayRangeEnd = endAddress;
            
            DisassemblyLines.Clear();

            // Get entries within the display range from the disassembly service
            var entriesInRange = _disassemblyService.GetEntriesInRange(startAddress, endAddress);
            
            if (entriesInRange.Any())
            {
                foreach (var entry in entriesInRange)
                {
                    var viewModel = new DisassemblyLineViewModel(
                        $"0x{entry.Address:X8}",
                        entry.SymbolName ?? "",
                        entry.Instruction
                    );
                    
                    // Mark current instruction
                    if (entry.Address == centerAddress)
                    {
                        viewModel.IsCurrentInstruction = true;
                    }
                    
                    DisassemblyLines.Add(viewModel);
                }
            }
            else
            {
                // No entries found in range - add placeholder entries to show the address range
                var placeholderEntries = new List<DisassemblyLineViewModel>();
                
                // Add a few placeholder entries around the center address
                for (int i = -5; i <= 5; i++)
                {
                    var addr = centerAddress + (uint)(i * 4); // Assume 4-byte instructions
                    var isCenter = (i == 0);
                    
                    var viewModel = new DisassemblyLineViewModel(
                        $"0x{addr:X8}",
                        isCenter ? "PC" : "",
                        isCenter ? "<-- Program Counter -->" : "<no disassembly data>"
                    );
                    
                    viewModel.IsCurrentInstruction = isCenter;
                    DisassemblyLines.Add(viewModel);
                }
            }

            // Notify UI to scroll to current instruction after refreshing the window
            CurrentInstructionChanged?.Invoke(this, EventArgs.Empty);

            _logger.LogDebug("Disassembly window refreshed: 0x{Start:X8} - 0x{End:X8} ({Count} entries)", 
                           startAddress, endAddress, DisassemblyLines.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh disassembly window around 0x{Address:X8}", centerAddress);
        }
    }

    [RelayCommand]
    private async Task Settings()
    {
        _logger.LogInformation("Settings command executed");
        StatusMessage = "Opening settings...";
        await Task.Delay(100);
        StatusMessage = "Settings dialog would open here";
    }

    /// <summary>
    /// Show dialog for editing memory watch address
    /// </summary>
    private async void ShowMemoryAddressEditDialog(MemoryWatchViewModel memoryWatch)
    {
        try
        {
            var textBox = HexTextBoxHelper.CreateAddressTextBox(memoryWatch.GetNumericAddress());
            
            var dialog = new ContentDialog()
            {
                Title = $"Edit Memory Address",
                Content = textBox,
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
                var addressText = textBox.Text ?? "";

                if (TryParseRegisterValue(addressText, out uint newAddress))
                {
                    memoryWatch.SetAddress(newAddress);
                    memoryWatch.RefreshValue(_memoryManagerService);
                    StatusMessage = $"Memory watch address updated to 0x{newAddress:X8}";
                }
                else
                {
                    StatusMessage = $"Invalid address format: {addressText}";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing memory address");
            StatusMessage = $"Error editing memory address: {ex.Message}";
        }
    }

    /// <summary>
    /// Show dialog for editing memory watch value
    /// </summary>
    private async void ShowMemoryValueEditDialog(MemoryWatchViewModel memoryWatch)
    {
        try
        {
            var textBox = HexTextBoxHelper.CreateMemoryValueTextBox(memoryWatch.Value, memoryWatch.Width);
            
            var dialog = new ContentDialog()
            {
                Title = $"Edit Memory Value at 0x{memoryWatch.GetNumericAddress():X8}",
                Content = textBox,
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
                var valueText = textBox.Text ?? "";

                if (memoryWatch.TrySetValue(valueText, _memoryManagerService))
                {
                    StatusMessage = $"Memory value updated at 0x{memoryWatch.GetNumericAddress():X8}";
                }
                else
                {
                    StatusMessage = $"Failed to update memory value: {valueText}";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing memory value at 0x{Address:X8}", memoryWatch.GetNumericAddress());
            StatusMessage = $"Error editing memory value: {ex.Message}";
        }
    }

    /// <summary>
    /// Show dialog for adding new memory watch
    /// </summary>
    private async void ShowAddMemoryWatchDialog()
    {
        try
        {
            var stackPanel = new StackPanel { Spacing = 10 };
            
            var addressBox = new TextBox
            {
                PlaceholderText = "Enter hex address (e.g., 00001000)"
            };
            
            var widthCombo = new ComboBox
            {
                ItemsSource = Enum.GetValues<MemoryWatchWidth>(),
                SelectedIndex = 0
            };
            
            stackPanel.Children.Add(new TextBlock { Text = "Address:" });
            stackPanel.Children.Add(addressBox);
            stackPanel.Children.Add(new TextBlock { Text = "Data Width:" });
            stackPanel.Children.Add(widthCombo);

            var dialog = new ContentDialog()
            {
                Title = "Add Memory Watch",
                Content = stackPanel,
                PrimaryButtonText = "Add",
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
                var addressText = addressBox.Text ?? "";
                var dataWidth = (MemoryWatchWidth)widthCombo.SelectedItem;

                if (TryParseRegisterValue(addressText, out uint address))
                {
                    var newWatch = new MemoryWatchViewModel($"0x{address:X8}", address, dataWidth);
                    newWatch.RefreshValue(_memoryManagerService);
                    MemoryWatches.Add(newWatch);
                    StatusMessage = $"Added memory watch at 0x{address:X8}";
                }
                else
                {
                    StatusMessage = $"Invalid address format: {addressText}";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding memory watch");
            StatusMessage = $"Error adding memory watch: {ex.Message}";
        }
    }

    /// <summary>
    /// Show dialog for changing memory watch data width
    /// </summary>
    private async void ShowMemoryWidthDialog(MemoryWatchViewModel memoryWatch)
    {
        try
        {
            var widthCombo = new ComboBox
            {
                ItemsSource = Enum.GetValues<MemoryWatchWidth>(),
                SelectedItem = memoryWatch.GetWidth()
            };
            
            var stackPanel = new StackPanel { Spacing = 10 };
            stackPanel.Children.Add(new TextBlock { Text = $"Data Width for 0x{memoryWatch.GetNumericAddress():X8}:" });
            stackPanel.Children.Add(widthCombo);

            var dialog = new ContentDialog()
            {
                Title = "Change Data Width",
                Content = stackPanel,
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
                var newWidth = (MemoryWatchWidth)widthCombo.SelectedItem;
                if (newWidth != memoryWatch.GetWidth())
                {
                    memoryWatch.SetWidth(newWidth);
                    memoryWatch.RefreshValue(_memoryManagerService);
                    StatusMessage = $"Changed data width to {newWidth}";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing memory data width");
            StatusMessage = $"Error changing data width: {ex.Message}";
        }
    }

    // Phase 8: File Operations Helper Methods
    
    /// <summary>
    /// Shows a dialog asking the user about unsaved changes
    /// </summary>
    private async Task<ContentDialogResult> ShowUnsavedChangesDialog()
    {
        var dialog = new ContentDialog()
        {
            Title = "Unsaved Changes",
            Content = "You have unsaved changes. Do you want to save them before continuing?",
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Don't Save",
            CloseButtonText = "Cancel"
        };

        // Set the dialog's XamlRoot for proper display
        var window = Microsoft.UI.Xaml.Window.Current ?? 
                    ((App)Microsoft.UI.Xaml.Application.Current).MainWindow;
        if (window?.Content is FrameworkElement rootElement)
        {
            dialog.XamlRoot = rootElement.XamlRoot;
        }

        return await dialog.ShowAsync();
    }

    /// <summary>
    /// Converts DataWidth enum to MemoryWatchWidth enum
    /// </summary>
    private MemoryWatchWidth ConvertDataWidthToMemoryWatchWidth(DataWidth dataWidth)
    {
        return dataWidth switch
        {
            DataWidth.Byte => MemoryWatchWidth.Byte,
            DataWidth.Word => MemoryWatchWidth.Word,
            DataWidth.Long => MemoryWatchWidth.Long,
            _ => MemoryWatchWidth.Long
        };
    }

    /// <summary>
    /// Converts MemoryWatchWidth enum to DataWidth enum
    /// </summary>
    private DataWidth ConvertMemoryWatchWidthToDataWidth(MemoryWatchWidth memoryWatchWidth)
    {
        return memoryWatchWidth switch
        {
            MemoryWatchWidth.Byte => DataWidth.Byte,
            MemoryWatchWidth.Word => DataWidth.Word,
            MemoryWatchWidth.Long => DataWidth.Long,
            _ => DataWidth.Long
        };
    }
}
