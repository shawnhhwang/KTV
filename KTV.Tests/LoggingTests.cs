using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Serilog;
using Xunit;

namespace KTV.Tests
{
    public class LoggingTests
    {
        [Fact]
        public void Configure_CorrectlyInitializesLoggerAndWritesToFile()
        {
            // Arrange
            string tempLogDir = Path.Combine(Path.GetTempPath(), $"KtvLogTest_{Guid.NewGuid():N}");
            string logPath = Path.Combine(tempLogDir, "test_log.txt");

            var inMemoryConfig = new Dictionary<string, string?>
            {
                { "Serilog:WriteTo:1:Args:path", logPath },
                { "Serilog:WriteTo:1:Args:outputTemplate", "[{Level:u3}] {Message:lj}{NewLine}" }
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemoryConfig)
                .Build();

            try
            {
                // Act
                LoggerSetup.Configure(configuration);
                Log.Information("Test message to verify file write");
                Log.CloseAndFlush(); // Flush logs to disk

                // Assert
                Assert.True(Directory.Exists(tempLogDir), "Log directory should be created");
                
                var logFiles = Directory.GetFiles(tempLogDir, "test_log*");
                Assert.NotEmpty(logFiles);

                string content = File.ReadAllText(logFiles[0]);
                Assert.Contains("[INF] Test message to verify file write", content);
            }
            finally
            {
                // Clean up
                if (Directory.Exists(tempLogDir))
                {
                    try
                    {
                        Directory.Delete(tempLogDir, true);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }
    }
}
