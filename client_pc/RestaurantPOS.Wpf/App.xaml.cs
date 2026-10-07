using System.Windows;
using System.Windows.Threading;
using RestaurantPOS.Wpf.Services;
using RestaurantPOS.Wpf.Views;

namespace RestaurantPOS.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. UI Thread unhandled exceptions
        DispatcherUnhandledException += App_DispatcherUnhandledException;

        // 2. Background thread unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        // 3. Task unobserved exceptions
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        PosLogger.Info("[POS Application] Started successfully.");

        // Check for --export-screens argument
        foreach (var arg in e.Args)
        {
            if (arg.StartsWith("--export-screens=", StringComparison.OrdinalIgnoreCase))
            {
                var dir = arg.Substring("--export-screens=".Length).Trim('"');
                ScreenExporter.ExportScreens(dir);
                Shutdown();
                return;
            }
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        PosLogger.Error("[Dispatcher Exception] " + e.Exception.Message, e.Exception);
        ErrorDialog.Show(e.Exception.Message, "ข้อผิดพลาดระบบ (UI Dispatcher)", e.Exception.ToString());
        e.Handled = true; // Prevent app termination
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            PosLogger.Fatal("[AppDomain Fatal Exception] " + ex.Message, ex);
            ErrorDialog.Show(ex.Message, "ข้อผิดพลาดร้ายแรงของระบบ (AppDomain)", ex.ToString());
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        PosLogger.Error("[Task Unobserved Exception] " + e.Exception.Message, e.Exception);
        e.SetObserved();
    }
}
