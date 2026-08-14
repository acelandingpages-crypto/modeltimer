using Avalonia;
using Avalonia.Controls;

namespace ModelTimer;

public partial class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        var mainWindow = new MainWindow();
        mainWindow.Show();
        base.OnFrameworkInitializationCompleted();
    }
}
