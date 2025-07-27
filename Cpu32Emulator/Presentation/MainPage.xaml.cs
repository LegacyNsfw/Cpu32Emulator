using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Cpu32Emulator.Presentation;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainPage()
    {
        this.InitializeComponent();
        
        // Add keyboard accelerators for register operations
        this.KeyDown += MainPage_KeyDown;
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
