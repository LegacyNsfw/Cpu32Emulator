using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using System.Linq;

namespace Cpu32Emulator.Presentation;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainPage()
    {
        this.InitializeComponent();
        
        // Add keyboard accelerators for register operations
        this.KeyDown += MainPage_KeyDown;
        
        // Subscribe to ViewModel property changes to handle PC changes
        this.Loaded += MainPage_Loaded;
        this.Unloaded += MainPage_Unloaded;
    }

    private void MainPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // Subscribe to current instruction change events to handle scrolling
        if (ViewModel != null)
        {
            ViewModel.CurrentInstructionChanged += ViewModel_CurrentInstructionChanged;
            
            // Phase 1: Initialize tile view if needed
            InitializeTileView();
        }
    }

    private void MainPage_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // Unsubscribe from events to prevent memory leaks
        if (ViewModel != null)
        {
            ViewModel.CurrentInstructionChanged -= ViewModel_CurrentInstructionChanged;
        }
    }

    private void ViewModel_CurrentInstructionChanged(object? sender, EventArgs e)
    {
        // When the current instruction changes, scroll to it
        ScrollToCurrentInstruction();
        
        // Phase 1: Update current PC address for tile view
        if (ViewModel != null)
        {
            // Get PC value from the register that stores it as a uint
            var pcRegister = ViewModel.Registers?.FirstOrDefault(r => r.Name == "PC");
            if (pcRegister != null)
            {
                ViewModel.CurrentPCAddress = pcRegister.GetNumericValue();
            }
        }
    }

    /// <summary>
    /// Phase 1: Initialize tile view with DisassemblyService
    /// </summary>
    private void InitializeTileView()
    {
        if (DisassemblyTileView != null && ViewModel != null)
        {
            // For Phase 1, we need to pass the DisassemblyService to the tile view
            // Phase 2 will handle proper dependency injection
            var disassemblyService = ViewModel.GetType()
                .GetField("_disassemblyService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .GetValue(ViewModel) as Cpu32Emulator.Services.DisassemblyService;
            
            if (disassemblyService != null)
            {
                DisassemblyTileView.SetDisassemblyService(disassemblyService);
            }
        }
    }

    /// <summary>
    /// Phase 4: Scrolls the disassembly tile view to show the current instruction
    /// Replaces the old ListView-based ScrollToCurrentInstruction method
    /// </summary>
    public async void ScrollToCurrentInstruction()
    {
        if (DisassemblyTileView == null)
            return;

        try
        {
            // Use the new tile-based scrolling implementation
            await DisassemblyTileView.ScrollToCurrentInstructionAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error scrolling to current instruction: {ex.Message}");
        }
    }

    private void MainPage_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Handle keyboard shortcuts
        var ctrlPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        
        if (ctrlPressed)
        {
            switch (e.Key)
            {
                case VirtualKey.Z:
                    ViewModel.UndoRegisterChangeCommand.Execute(null);
                    e.Handled = true;
                    break;
                case VirtualKey.Y:
                    ViewModel.RedoRegisterChangeCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
        else
        {
            // Handle execution control function keys
            switch (e.Key)
            {
                case VirtualKey.F5:
                    // Run - Continuous execution until breakpoint
                    ViewModel.RunCommand.Execute(null);
                    e.Handled = true;
                    break;
                case VirtualKey.F9:
                    // Toggle Breakpoint at current line
                    ViewModel.ToggleBreakpointCommand.Execute(null);
                    e.Handled = true;
                    break;
                case VirtualKey.F10:
                    // Step Over - JSR-aware stepping
                    ViewModel.StepOverCommand.Execute(null);
                    e.Handled = true;
                    break;
                case VirtualKey.F11:
                    // Step Into - Single instruction execution
                    ViewModel.StepIntoCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
    }

    private void HamburgerButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        MainSplitView.IsPaneOpen = !MainSplitView.IsPaneOpen;
    }

    private void RegisterList_ItemClick(object sender, Microsoft.UI.Xaml.Controls.ItemClickEventArgs e)
    {
        if (e.ClickedItem is CpuRegisterViewModel register)
        {
            ViewModel.SetRegisterValueCommand.Execute(register);
        }
    }

    private void OnMemoryAddressTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement element && 
            element.DataContext is MemoryWatchViewModel memoryWatch)
        {
            ViewModel.EditMemoryAddressCommand.Execute(memoryWatch);
        }
    }

    private void OnMemoryValueTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement element && 
            element.DataContext is MemoryWatchViewModel memoryWatch)
        {
            ViewModel.EditMemoryValueCommand.Execute(memoryWatch);
        }
    }

    private void OnMemoryWidthTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement element && 
            element.DataContext is MemoryWatchViewModel memoryWatch)
        {
            ViewModel.ChangeMemoryWidthCommand.Execute(memoryWatch);
        }
    }

    private void OnRemoveMemoryWatch(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement element && 
            element.DataContext is MemoryWatchViewModel memoryWatch)
        {
            ViewModel.RemoveMemoryWatchCommand.Execute(memoryWatch);
        }
    }
}
