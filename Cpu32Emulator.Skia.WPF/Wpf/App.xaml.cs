using WpfApp = System.Windows.Application;
using Uno.UI.Runtime.Skia.Wpf;

namespace Cpu32Emulator.WPF;

public partial class App : WpfApp
{
    public App()
    {
        var host = new WpfHost(Dispatcher, () => new AppHead());
        host.Run();
    }
}
