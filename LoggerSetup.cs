using System;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace KTV
{
    public static class LoggerSetup
    {
        public static void Configure(IConfiguration? configuration)
        {
            var logPath = configuration?["Serilog:WriteTo:1:Args:path"] ?? "Logs/ktv_system.txt";
            var outputTemplate = configuration?["Serilog:WriteTo:1:Args:outputTemplate"] ?? "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {Message:lj} {Exception}{NewLine}";
            
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, outputTemplate: outputTemplate)
                .CreateLogger();
        }

        public static void LogException(Exception ex, string context)
        {
            Log.Error(ex, "Exception in context '{Context}': {Message}", context, ex.Message);
        }
    }
}
