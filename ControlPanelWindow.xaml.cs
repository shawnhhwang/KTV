using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Serilog;

namespace KTV
{
    public partial class ControlPanelWindow : Window
    {
        private PlayerWindow? _playerWindow;
        private int _currentPitch = 0;
        private LlmAgentService? _llmAgentService;
        private BackgroundJobService? _backgroundJobService;

        public ControlPanelWindow()
        {
            InitializeComponent();
            Loaded += ControlPanelWindow_Loaded;
        }

        private void ControlPanelWindow_Loaded(object sender, RoutedEventArgs e)
        {
            OpenPlayerWindow();
            _llmAgentService = new LlmAgentService(App.Configuration);
            
            // Initialize BackgroundJobService
            string connectionString = App.Configuration?.GetSection("Database")["ConnectionString"] ?? "Data Source=KtvDatabase.sqlite;Cache=Shared;";
            string ytDlpPath = App.Configuration?["ExternalTools:YtDlpPath"] ?? "Tools\\yt-dlp.exe";
            string demucsPath = App.Configuration?["ExternalTools:VocalRemoverPath"] ?? "Tools\\demucs.exe";
            string downloadDir = App.Configuration?["ExternalTools:DownloadDirectory"] ?? "Library\\Downloads";

            _backgroundJobService = new BackgroundJobService(connectionString, ytDlpPath, demucsPath, downloadDir);
            _backgroundJobService.JobStatusChanged += BackgroundJobService_JobStatusChanged;

            LoadSongList();
        }

        private void OpenPlayerWindow()
        {
            if (_playerWindow != null) return;

            _playerWindow = new PlayerWindow();

            var screens = System.Windows.Forms.Screen.AllScreens;
            if (screens.Length > 1)
            {
                var secondary = screens[1];
                var bounds = secondary.Bounds;
                
                _playerWindow.Left = bounds.Left;
                _playerWindow.Top = bounds.Top;
                _playerWindow.Width = bounds.Width;
                _playerWindow.Height = bounds.Height;
                _playerWindow.WindowStyle = WindowStyle.None;
                _playerWindow.WindowState = WindowState.Maximized;
            }
            else
            {
                _playerWindow.Left = Left + Width + 10;
                _playerWindow.Top = Top;
                _playerWindow.Width = 640;
                _playerWindow.Height = 480;
                _playerWindow.WindowStyle = WindowStyle.SingleBorderWindow;
            }

            _playerWindow.Show();
        }

        public void LoadSongList()
        {
            try
            {
                string connectionString = App.Configuration?.GetSection("Database")["ConnectionString"] ?? "Data Source=KtvDatabase.sqlite;Cache=Shared;";
                using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString))
                {
                    connection.Open();
                    var songs = Dapper.SqlMapper.Query(connection, "SELECT * FROM Songs ORDER BY CreatedAt DESC");
                    SongsDataGrid.ItemsSource = songs;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load song list from SQLite.");
            }
        }

        private void BackgroundJobService_JobStatusChanged(KtvJob job)
        {
            Dispatcher.Invoke(() =>
            {
                string statusMsg = $"[下載] {job.Title} ({job.Artist}) - {job.Status} ({job.Progress}%)";
                if (job.Status == "Failed")
                {
                    statusMsg += $" | 錯誤: {job.ErrorMessage}";
                }

                ChatHistory.Items.Add($"系統: {statusMsg}");

                if (job.Status == "Completed")
                {
                    LoadSongList();
                }
            });
        }

        private async void SendChat_Click(object sender, RoutedEventArgs e)
        {
            string input = ChatInput.Text.Trim();
            if (string.IsNullOrEmpty(input)) return;

            ChatHistory.Items.Add($"User: {input}");
            ChatInput.Clear();

            // Check if input is a YouTube URL to download
            if (input.Contains("youtube.com/") || input.Contains("youtu.be/"))
            {
                var parts = input.Split(' ', 3);
                string url = parts[0];
                string title = parts.Length > 1 ? parts[1] : "YouTube Song";
                string artist = parts.Length > 2 ? parts[2] : "Unknown";

                ChatHistory.Items.Add($"系統: 已將影片加入下載佇列，正在啟動背景下載...");
                _backgroundJobService?.EnqueueJob(title, artist, url);
                return;
            }

            if (_llmAgentService == null || !_llmAgentService.IsEnabled)
            {
                ChatHistory.Items.Add("系統: AI 點歌助理目前已停用。");
                return;
            }

            ChatHistory.Items.Add("AI: 思考中...");
            int statusIndex = ChatHistory.Items.Count - 1;

            try
            {
                LlmIntent intent = await _llmAgentService.AnalyzeIntentAsync(input);
                ChatHistory.Items[statusIndex] = $"AI (解析動作: {intent.Action}): 已解析意圖並執行。";
                
                ExecuteLlmIntent(intent);
            }
            catch (Exception ex)
            {
                ChatHistory.Items[statusIndex] = $"AI Error: 呼叫失敗 ({ex.Message})";
            }
        }

        private void ExecuteLlmIntent(LlmIntent intent)
        {
            switch (intent.Action.ToUpperInvariant())
            {
                case "PITCHCHANGE":
                    if (intent.Value.HasValue)
                    {
                        int val = intent.Value.Value;
                        _currentPitch = Math.Clamp(_currentPitch + val, -6, 6);
                        PitchText.Text = $"Key: {_currentPitch}";
                        _playerWindow?.SetPitch(_currentPitch);
                        ChatHistory.Items.Add($"系統: 已將 Key 調整至 {_currentPitch}");
                    }
                    break;
                case "VOCALTOGGLE":
                    if (_playerWindow != null)
                    {
                        string result = _playerWindow.ToggleVocalChannel();
                        ChatHistory.Items.Add($"系統: 聲道切換至 {result}");
                    }
                    break;
                case "PLAY":
                    _playerWindow?.GetMediaPlayer()?.Play();
                    ChatHistory.Items.Add("系統: 開始播放");
                    break;
                case "PAUSE":
                    _playerWindow?.GetMediaPlayer()?.Pause();
                    ChatHistory.Items.Add("系統: 暫停播放");
                    break;
                case "NEXT":
                    NextSong_Click(this, new RoutedEventArgs());
                    break;
                case "SEARCH":
                    SearchAndPlaySong(intent.Title, intent.Artist);
                    break;
                default:
                    ChatHistory.Items.Add("系統: 無法識別的指令動作。");
                    break;
            }
        }

        private void SearchAndPlaySong(string? title, string? artist)
        {
            if (string.IsNullOrEmpty(title))
            {
                ChatHistory.Items.Add("系統: 點歌失敗，找不到歌名。");
                return;
            }

            try
            {
                string connectionString = App.Configuration?.GetSection("Database")["ConnectionString"] ?? "Data Source=KtvDatabase.sqlite;Cache=Shared;";
                using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString))
                {
                    connection.Open();
                    
                    string query = "SELECT * FROM Songs WHERE Title LIKE @Title";
                    if (!string.IsNullOrEmpty(artist))
                    {
                        query += " AND Artist LIKE @Artist";
                    }
                    query += " LIMIT 1";

                    var song = Dapper.SqlMapper.QueryFirstOrDefault(connection, query, new { 
                        Title = $"%{title}%", 
                        Artist = $"%{artist}%" 
                    });

                    if (song != null)
                    {
                        string songTitle = song.Title;
                        string songArtist = song.Artist;
                        string filePath = song.FilePath;

                        CurrentSongText.Text = $"當前播放：{songTitle}";
                        CurrentArtistText.Text = $"歌手：{songArtist}";

                        _playerWindow?.PlayMedia(filePath);
                        ChatHistory.Items.Add($"系統: 成功為您點播 【{songArtist} - {songTitle}】");
                    }
                    else
                    {
                        ChatHistory.Items.Add($"系統: 在歌庫中找不到 【{artist ?? "未知"} - {title}】。您可以貼上 YouTube 網址進行下載！");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to search and play song: Title={Title}, Artist={Artist}", title, artist);
                ChatHistory.Items.Add($"系統: 資料庫查詢失敗 ({ex.Message})");
            }
        }

        private void ToggleVocal_Click(object sender, RoutedEventArgs e)
        {
            if (_playerWindow != null)
            {
                string result = _playerWindow.ToggleVocalChannel();
                ChatHistory.Items.Add($"系統: 聲道切換至 {result}");
            }
            else
            {
                MessageBox.Show("播放視窗未啟動。");
            }
        }

        private void PitchDown_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPitch > -6)
            {
                _currentPitch--;
                PitchText.Text = $"Key: {_currentPitch}";
                _playerWindow?.SetPitch(_currentPitch);
            }
        }

        private void PitchUp_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPitch < 6)
            {
                _currentPitch++;
                PitchText.Text = $"Key: {_currentPitch}";
                _playerWindow?.SetPitch(_currentPitch);
            }
        }

        private void NextSong_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("播放下一首");
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _playerWindow?.Close();
            _backgroundJobService?.Shutdown();
            Application.Current.Shutdown();
        }
    }
}
