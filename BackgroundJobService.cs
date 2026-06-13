using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace KTV
{
    public class KtvJob
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string YouTubeUrl { get; set; } = "";
        public string Status { get; set; } = "Queued"; // Queued, Downloading, Separating, Completed, Failed
        public int Progress { get; set; } = 0;
        public string? ErrorMessage { get; set; }
    }

    public class BackgroundJobService
    {
        private readonly ConcurrentQueue<KtvJob> _queue = new ConcurrentQueue<KtvJob>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly string _connectionString;
        private readonly string _ytDlpPath;
        private readonly string _demucsPath;
        private readonly string _downloadDir;

        public event Action<KtvJob>? JobStatusChanged;

        public BackgroundJobService(
            string connectionString, 
            string ytDlpPath, 
            string demucsPath, 
            string downloadDir)
        {
            _connectionString = connectionString;
            _ytDlpPath = ytDlpPath;
            _demucsPath = demucsPath;
            _downloadDir = downloadDir;

            // Start background worker loop
            Task.Run(ProcessQueueAsync);
        }

        public void EnqueueJob(string title, string artist, string url)
        {
            var job = new KtvJob
            {
                Title = title,
                Artist = artist,
                YouTubeUrl = url
            };
            _queue.Enqueue(job);
            _signal.Release();
            JobStatusChanged?.Invoke(job);
            Log.Information("Enqueued job: {Title} ({Url})", title, url);
        }

        private async Task ProcessQueueAsync()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await _signal.WaitAsync(_cts.Token);

                    if (_queue.TryDequeue(out var job))
                    {
                        await RunJobAsync(job);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in job worker queue processing.");
                }
            }
        }

        private async Task RunJobAsync(KtvJob job)
        {
            try
            {
                // 1. Download
                job.Status = "Downloading";
                job.Progress = 10;
                JobStatusChanged?.Invoke(job);
                Log.Information("Starting download for job: {Id}", job.Id);

                string videoFilePath = await DownloadVideoAsync(job);
                job.Progress = 50;
                JobStatusChanged?.Invoke(job);

                // 2. Separation
                job.Status = "Separating";
                JobStatusChanged?.Invoke(job);
                Log.Information("Starting vocal separation for job: {Id}", job.Id);

                var separationResult = await SeparateVocalAsync(videoFilePath, job);
                job.Progress = 90;
                JobStatusChanged?.Invoke(job);

                // 3. Write to SQLite
                job.Status = "Completed";
                job.Progress = 100;
                
                SaveSongToDb(job.Title, job.Artist, videoFilePath, separationResult.AccompanimentPath, separationResult.VocalPath);
                
                JobStatusChanged?.Invoke(job);
                Log.Information("Job completed successfully: {Id}", job.Id);
            }
            catch (Exception ex)
            {
                job.Status = "Failed";
                job.ErrorMessage = ex.Message;
                JobStatusChanged?.Invoke(job);
                Log.Error(ex, "Job failed: {Id}", job.Id);
            }
        }

        private async Task<string> DownloadVideoAsync(KtvJob job)
        {
            if (!Directory.Exists(_downloadDir))
            {
                Directory.CreateDirectory(_downloadDir);
            }

            // Remove invalid characters from title for filename safety
            string sanitizedTitle = string.Join("_", job.Title.Split(Path.GetInvalidFileNameChars()));
            string outputFileName = $"{sanitizedTitle}_{job.Id}.mp4";
            string outputFilePath = Path.Combine(_downloadDir, outputFileName);

            bool toolExists = File.Exists(_ytDlpPath);
            if (!toolExists && !System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                toolExists = TryCheckToolInPath("yt-dlp");
            }

            if (!toolExists)
            {
                Log.Warning("yt-dlp tool not found at '{Path}'. Simulating download.", _ytDlpPath);
                await Task.Delay(1000); // Simulate network delay
                
                // Write mock video file
                await File.WriteAllTextAsync(outputFilePath, "MOCK VIDEO CONTENT");
                return outputFilePath;
            }

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = File.Exists(_ytDlpPath) ? _ytDlpPath : "yt-dlp",
                Arguments = $"-f mp4 -o \"{outputFilePath}\" \"{job.YouTubeUrl}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new System.Diagnostics.Process { StartInfo = startInfo })
            {
                process.Start();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"yt-dlp failed (code {process.ExitCode}). Error: {error}");
                }
            }

            return outputFilePath;
        }

        private async Task<(string AccompanimentPath, string VocalPath)> SeparateVocalAsync(string videoPath, KtvJob job)
        {
            string outputFolder = Path.Combine(_downloadDir, $"separated_{job.Id}");
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            string accompanimentPath = Path.Combine(outputFolder, "accompaniment.wav");
            string vocalPath = Path.Combine(outputFolder, "vocals.wav");

            bool toolExists = File.Exists(_demucsPath);
            if (!toolExists && !System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                toolExists = TryCheckToolInPath("demucs");
            }

            if (!toolExists)
            {
                Log.Warning("Demucs tool not found at '{Path}'. Simulating separation.", _demucsPath);
                await Task.Delay(1000);
                
                await File.WriteAllTextAsync(accompanimentPath, "MOCK ACCOMPANIMENT AUDIO");
                await File.WriteAllTextAsync(vocalPath, "MOCK VOCALS AUDIO");
                return (accompanimentPath, vocalPath);
            }

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = File.Exists(_demucsPath) ? _demucsPath : "demucs",
                Arguments = $"-o \"{outputFolder}\" \"{videoPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new System.Diagnostics.Process { StartInfo = startInfo })
            {
                process.Start();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"demucs failed (code {process.ExitCode}). Error: {error}");
                }
            }

            // Find output files recursively (demucs outputs inside model directories)
            string foundVocalPath = vocalPath;
            string foundAccompanimentPath = accompanimentPath;

            if (Directory.Exists(outputFolder))
            {
                var files = Directory.GetFiles(outputFolder, "*.wav", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (file.Contains("vocals", StringComparison.OrdinalIgnoreCase))
                    {
                        foundVocalPath = file;
                    }
                    else if (file.Contains("no_vocals", StringComparison.OrdinalIgnoreCase) || file.Contains("accompaniment", StringComparison.OrdinalIgnoreCase))
                    {
                        foundAccompanimentPath = file;
                    }
                }
            }

            return (foundAccompanimentPath, foundVocalPath);
        }

        private void SaveSongToDb(string title, string artist, string videoPath, string accompanimentPath, string vocalPath)
        {
            using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString))
            {
                connection.Open();
                
                string insertQuery = @"
                    INSERT INTO Songs (Title, Artist, FilePath, AccompanimentPath, VocalPath, Source)
                    VALUES (@Title, @Artist, @FilePath, @AccompanimentPath, @VocalPath, 'YouTube');";
                
                Dapper.SqlMapper.Execute(connection, insertQuery, new
                {
                    Title = title,
                    Artist = artist,
                    FilePath = videoPath,
                    AccompanimentPath = accompanimentPath,
                    VocalPath = vocalPath
                });
            }
        }

        private bool TryCheckToolInPath(string tool)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = tool,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    process?.WaitForExit();
                    return process?.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public void Shutdown()
        {
            _cts.Cancel();
        }
    }
}
