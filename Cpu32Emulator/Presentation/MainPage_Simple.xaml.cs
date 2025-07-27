using Microsoft.UI.Xaml.Controls;

namespace Cpu32Emulator.Presentation;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainPage()
    {
        this.InitializeComponent();
    }
}
