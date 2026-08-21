using Avalonia;
using System;
using Velopack;

namespace ModelTimer;

internal class Program
{
    public static void Main(string[] args)
    {
        // Must run first, before anything else touches the UI or filesystem: this is how
        // Velopack recognizes install/update/uninstall hook invocations (e.g. right after an
        // update is applied, to set up shortcuts) and handles them without starting the app.
        VelopackApp.Build().Run();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont();
}
