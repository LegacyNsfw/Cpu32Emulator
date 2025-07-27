using Microsoft.UI.Xaml.Controls;

namespace Cpu32Emulator.Presentation;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainPage()
    {
        this.InitializeComponent();
    }

    private void HamburgerButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        MainSplitView.IsPaneOpen = !MainSplitView.IsPaneOpen;
    }
}
