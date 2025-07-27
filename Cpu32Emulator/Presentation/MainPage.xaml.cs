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
}
