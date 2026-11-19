using System.IO;
using System.Windows;

namespace ReferenceOverlayViewer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // This diagnostic path never shows a window, touches user settings, or registers hotkeys.
        if (e.Args is ["--self-test", var report])
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try { SelfTest.Run(report); Shutdown(0); }
            catch (Exception ex) { File.WriteAllText(report + ".error.txt", ex.ToString()); Shutdown(1); }
            return;
        }
        var window = new ToolbarWindow();
        MainWindow = window;
        window.Show();
        if (e.Args.Length > 0 && File.Exists(e.Args[0])) window.LoadImage(e.Args[0]);
    }
}
