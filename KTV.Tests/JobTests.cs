using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Dapper;
using Xunit;

namespace KTV.Tests
{
    public class JobTests
    {
        [Fact]
        public async Task EnqueueJob_ExecutesProcessAndSavesToDb()
        {
            // Arrange
            string tempDbFile = Path.Combine(Path.GetTempPath(), $"KtvJobTest_{Guid.NewGuid():N}.sqlite");
            string connectionString = $"Data Source={tempDbFile};";
            
            // Initialize SQLite DB schema
            DatabaseBootstrap.Initialize(connectionString);

            string tempDownloadDir = Path.Combine(Path.GetTempPath(), $"KtvDownloads_{Guid.NewGuid():N}");
            
            // Instantiate background service with mock tool paths
            var service = new BackgroundJobService(
                connectionString, 
                "nonexistent_ytdlp.exe", 
                "nonexistent_demucs.exe", 
                tempDownloadDir
            );

            var tcs = new TaskCompletionSource<KtvJob>();
            service.JobStatusChanged += (job) =>
            {
                if (job.Status == "Completed" || job.Status == "Failed")
                {
                    tcs.TrySetResult(job);
                }
            };

            try
            {
                // Act
                service.EnqueueJob("Testing Song", "Tester", "https://www.youtube.com/watch?v=dQw4w9WgXcQ");

                // Assert with timeout (max 5 seconds)
                var completedJobTask = await Task.WhenAny(tcs.Task, Task.Delay(5000));
                Assert.Same(tcs.Task, completedJobTask); // Verify it didn't timeout

                var finalJob = await tcs.Task;
                Assert.Equal("Completed", finalJob.Status);
                Assert.Equal(100, finalJob.Progress);

                // Verify SQLite record exists
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    var songs = connection.Query("SELECT * FROM Songs;").ToList();
                    Assert.Single(songs);
                    
                    var song = songs[0];
                    Assert.Equal("Testing Song", (string)song.Title);
                    Assert.Equal("Tester", (string)song.Artist);
                    Assert.Equal("YouTube", (string)song.Source);
                    
                    // Verify files were actually created
                    Assert.True(File.Exists((string)song.FilePath), "Mock Video file should exist");
                    Assert.True(File.Exists((string)song.AccompanimentPath), "Mock Accompaniment file should exist");
                    Assert.True(File.Exists((string)song.VocalPath), "Mock Vocal file should exist");
                }
            }
            finally
            {
                // Shutdown service worker thread
                service.Shutdown();

                // Clean up database file
                if (File.Exists(tempDbFile))
                {
                    try
                    {
                        SqliteConnection.ClearAllPools();
                        File.Delete(tempDbFile);
                    }
                    catch { }
                }

                // Clean up downloads directory
                if (Directory.Exists(tempDownloadDir))
                {
                    try
                    {
                        Directory.Delete(tempDownloadDir, true);
                    }
                    catch { }
                }
            }
        }
    }
}
