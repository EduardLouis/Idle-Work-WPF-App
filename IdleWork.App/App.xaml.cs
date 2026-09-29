// [v0.1: App] Application entrypoint with global unhandled exception trapping and startup diagnostics
using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace IdleWork.App
{
    public partial class App : Application
    {
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IdleWork");
        private static readonly string StartupLogPath = Path.Combine(LogDir, "startup.log");
        private static readonly string CrashLogPath = Path.Combine(LogDir, "startup_crash.log");
        private static readonly string LocalBinLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");

        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            LogStartup("Application OnStartup started.");

            try
            {
                // [v0.1: SQLite] Initialize SQLitePCL raw provider bundle
                LogStartup("Initializing SQLite batteries...");
                SQLitePCL.Batteries_V2.Init();
                LogStartup("SQLite batteries initialized successfully.");
            }
            catch (Exception ex)
            {
                LogCrash("SQLitePCL_Init", ex);
            }

            base.OnStartup(e);

            try
            {
                LogStartup("Creating MainWindow instance...");
                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                LogStartup("Showing MainWindow...");
                mainWindow.Show();
                mainWindow.Activate();
                LogStartup("MainWindow displayed successfully.");
            }
            catch (Exception ex)
            {
                LogCrash("MainWindow_Startup", ex);
                MessageBox.Show(
                    $"Error starting Idle-Work MainWindow:\n\n{ex.Message}\n\nCheck log at:\n{StartupLogPath}\nor\n{LocalBinLog}",
                    "Idle-Work Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogCrash("DispatcherUnhandledException", e.Exception);
            MessageBox.Show(
                $"Unhandled UI Error:\n\n{e.Exception.Message}\n\nCheck logs at:\n{CrashLogPath}",
                "Idle-Work Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogCrash("CurrentDomain_UnhandledException", ex);
            }
        }

        public static void LogStartup(string message)
        {
            try
            {
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                System.Diagnostics.Debug.WriteLine($"[IdleWork.Startup] {message}");

                try
                {
                    File.AppendAllText(LocalBinLog, entry);
                }
                catch { }

                try
                {
                    if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir);
                    File.AppendAllText(StartupLogPath, entry);
                }
                catch { }
            }
            catch { }
        }

        public static void LogCrash(string context, Exception ex)
        {
            try
            {
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{context}]{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}";
                System.Diagnostics.Debug.WriteLine($"[IdleWork.Crash] {context}: {ex}");

                try
                {
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_crash.log"), entry);
                    File.AppendAllText(LocalBinLog, entry);
                }
                catch { }

                try
                {
                    if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir);
                    File.AppendAllText(CrashLogPath, entry);
                    File.AppendAllText(StartupLogPath, entry);
                }
                catch { }
            }
            catch { }
        }
    }
}
