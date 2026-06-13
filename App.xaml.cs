using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Serilog;
using Microsoft.Data.Sqlite;
using Dapper;

namespace KTV
{
    public partial class App : System.Windows.Application
    {
        public static IConfiguration? Configuration { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Setup global exception handlers
            SetupGlobalExceptionHandling();

            // 2. Load Configuration
            LoadConfiguration();

            // 3. Initialize Logger
            ConfigureLogging();

            Log.Information("System starting up...");

            // 4. Initialize Database
            try
            {
                InitializeDatabase();
                Log.Information("Database initialized successfully.");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to initialize SQLite database.");
                MessageBox.Show($"Database Init Error: {ex.Message}\nCheck Logs/ktv_system.txt for details.", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }
        }

        private void SetupGlobalExceptionHandling()
        {
            // UI Thread Exception
            DispatcherUnhandledException += (s, e) =>
            {
                Log.Error(e.Exception, "Unhandled UI exception occurred.");
                MessageBox.Show($"UI Error: {e.Exception.Message}\nDetails logged.", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true; // Prevent application crash
            };

            // Non-UI Thread Exception
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                Log.Fatal(ex, "Unhandled non-UI domain exception occurred. Terminating: {IsTerminating}", e.IsTerminating);
                if (ex != null)
                {
                    MessageBox.Show($"Fatal Thread Error: {ex.Message}\nDetails logged.", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            // Task exception
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Log.Error(e.Exception, "Unobserved task exception occurred.");
                MessageBox.Show($"Task Error: {e.Exception.Message}\nDetails logged.", "Task Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.SetObserved(); // Prevent process termination
            };
        }

        private void LoadConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            Configuration = builder.Build();
        }

        private void ConfigureLogging()
        {
            LoggerSetup.Configure(Configuration);
        }

        private void InitializeDatabase()
        {
            string connectionString = Configuration?.GetSection("Database")["ConnectionString"] ?? "Data Source=KtvDatabase.sqlite;Cache=Shared;";
            DatabaseBootstrap.Initialize(connectionString);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
