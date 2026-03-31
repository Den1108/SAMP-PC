using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace SAMPLauncher
{
    public partial class MainWindow : Window
    {
        private const string CurrentLauncherVersion = "1.0.4";

        private string configPath = "config.json";
        private string _gamePath = "";

        private string serverIP = "188.127.241.8";
        private int serverPort = 1179;
        private string distributionUrl = "http://87.106.105.24:12867/distribution.json";

        private DispatcherTimer _queryTimer;
        private bool _settingsLoaded = false;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();

            if (string.IsNullOrEmpty(_gamePath))
                _gamePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "files");

            GamePathText.Text = _gamePath;
            _settingsLoaded = true;

            NickNameBox.TextChanged += (s, e) => AutoSave();
            ResolutionBox.SelectionChanged += (s, e) => AutoSave();
            FpsLimitCheck.Checked += (s, e) => AutoSave();
            FpsLimitCheck.Unchecked += (s, e) => AutoSave();
            FpsLimitBox.SelectionChanged += (s, e) => AutoSave();
            WidescreenCheck.Checked += (s, e) => AutoSave();
            WidescreenCheck.Unchecked += (s, e) => AutoSave();

            UpdateServerInfo();

            _queryTimer = new DispatcherTimer();
            _queryTimer.Interval = TimeSpan.FromSeconds(30);
            _queryTimer.Tick += (s, e) => UpdateServerInfo();
            _queryTimer.Start();

            _ = CheckLauncherUpdate();
        }

        private void AutoSave()
        {
            if (!_settingsLoaded) return;
            SaveSettings();
            ApplyGameSettings();
        }

        private void NavHome_Click(object sender, MouseButtonEventArgs e)
        {
            PageHome.Visibility = Visibility.Visible;
            PageSettings.Visibility = Visibility.Collapsed;
            NavHome.Foreground = Brushes.White;
            NavSettings.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8F, 0x98));
        }

        private void NavSettings_Click(object sender, MouseButtonEventArgs e)
        {
            PageHome.Visibility = Visibility.Collapsed;
            PageSettings.Visibility = Visibility.Visible;
            NavSettings.Foreground = Brushes.White;
            NavHome.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8F, 0x98));
            GamePathText.Text = _gamePath;
        }

        private void FpsLimit_Changed(object sender, RoutedEventArgs e)
        {
            if (FpsLimitBox != null)
                FpsLimitBox.IsEnabled = FpsLimitCheck.IsChecked == true;
        }

        private void ApplyGameSettings()
        {
            try
            {
                // sa-mp.cfg для FPS
                string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string sampDir = Path.Combine(docPath, "GTA San Andreas User Files", "SAMP");
                string sampCfg = Path.Combine(sampDir, "sa-mp.cfg");
                if (!Directory.Exists(sampDir)) Directory.CreateDirectory(sampDir);

                int fps = 60;
                if (FpsLimitCheck.IsChecked == true && FpsLimitBox.SelectedItem is ComboBoxItem fpsItem)
                {
                    var fpsStr = fpsItem.Content.ToString()!.Replace(" FPS", "").Trim();
                    if (fpsStr == "Без ограничений") fps = 0;
                    else int.TryParse(fpsStr, out fps);
                }
                File.WriteAllText(sampCfg,
                    $"pagesize 0\ncheckfiles 0\nfps {fps}\nmulticore 1\ndirectmode 0\n" +
                    $"audiomsgoff 1\naudioproxyoff 0\nimeoff 0\nnovattention 0\n" +
                    $"inbrowsers 0\nnonametagstatus 1\ntimestamp 0\nfontedge 0\n");

                // gta_sa.set для разрешения и widescreen
                string setPath = Path.Combine(docPath, "GTA San Andreas User Files", "gta_sa.set");
                if (!File.Exists(setPath)) return;

                byte[] setData = File.ReadAllBytes(setPath);
                if (setData.Length < 8) return;

                // Таблица индексов разрешений GTA SA
                var resolutionIndex = new Dictionary<string, int>
                {
                    { "640 x 480",   0 },
                    { "800 x 600",   1 },
                    { "1024 x 768",  2 },
                    { "1152 x 864",  3 },
                    { "1280 x 1024", 4 },
                    { "1600 x 900",  5 },
                    { "1920 x 1080", 6 },
                    { "2560 x 1440", 7 },
                };

                // Записываем индекс разрешения на offset 0
                if (ResolutionBox.SelectedItem is ComboBoxItem resItem)
                {
                    string resStr = resItem.Content.ToString()!;
                    if (resolutionIndex.TryGetValue(resStr, out int idx))
                    {
                        byte[] idxBytes = BitConverter.GetBytes((uint)idx);
                        Array.Copy(idxBytes, 0, setData, 0, 4);
                    }
                }

                // Записываем widescreen на offset 4
                uint wide = WidescreenCheck.IsChecked == true ? 1u : 0u;
                byte[] wideBytes = BitConverter.GetBytes(wide);
                Array.Copy(wideBytes, 0, setData, 4, 4);

                File.WriteAllBytes(setPath, setData);
            }
            catch { }
        }

        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Вы уверены? Все файлы игры будут удалены и скачаны заново при следующем запуске.",
                "Очистить кэш", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (Directory.Exists(_gamePath))
                {
                    Directory.Delete(_gamePath, true);
                    Directory.CreateDirectory(_gamePath);
                }
                StatusText.Text = "Кэш очищен";
                MessageBox.Show("Кэш успешно очищен. При следующем нажатии «Играть» файлы будут скачаны заново.", "Flyt RP");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при очистке кэша: " + ex.Message, "Flyt RP");
            }
        }

        private async void RepairGame_Click(object sender, RoutedEventArgs e)
        {
            NavHome_Click(sender, null!);
            StatusText.Text = "Проверка файлов...";
            DownloadProgress.Value = 0;

            bool success = await RunUpdateProcess(forceVerify: true);
            if (success)
                MessageBox.Show("Проверка завершена. Все файлы в порядке!", "Починить игру");
        }

        private async Task CheckLauncherUpdate()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string json = await client.GetStringAsync(distributionUrl);
                    var dist = JsonSerializer.Deserialize<Distribution>(json);

                    if (dist != null && !string.IsNullOrEmpty(dist.LauncherVersion))
                    {
                        if (dist.LauncherVersion != CurrentLauncherVersion)
                        {
                            var result = MessageBox.Show(
                                $"Доступна новая версия Flyt RP ({dist.LauncherVersion}). Обновить?",
                                "Обновление лаунчера", MessageBoxButton.YesNo, MessageBoxImage.Information);

                            if (result == MessageBoxResult.Yes && !string.IsNullOrEmpty(dist.CdnLauncher))
                                await UpdateSelf(dist.CdnLauncher);
                        }
                    }
                }
            }
            catch { }
        }

        private async Task UpdateSelf(string url)
        {
            try
            {
                string currentExe = Process.GetCurrentProcess().MainModule!.FileName!;
                string newExe = currentExe + "_new.exe";

                StatusText.Text = "Обновление лаунчера...";

                using (HttpClient client = new HttpClient())
                {
                    byte[] data = await client.GetByteArrayAsync(url);
                    await File.WriteAllBytesAsync(newExe, data);
                }

                string batchFile = Path.Combine(Path.GetTempPath(), "update_flyt.bat");
                string batContent = $@"
@echo off
timeout /t 2 /nobreak > nul
del ""{currentExe}""
move ""{newExe}"" ""{currentExe}""
start """" ""{currentExe}""
del ""%~f0""
";
                await File.WriteAllTextAsync(batchFile, batContent);
                Process.Start(new ProcessStartInfo { FileName = batchFile, CreateNoWindow = true, UseShellExecute = false });
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при самообновлении: " + ex.Message);
            }
        }

        private void UpdateServerInfo()
        {
            OnlineIndicator.Fill = Brushes.Orange;
            var result = QuerySampServer(serverIP, serverPort);

            if (result.Success)
            {
                OnlineIndicator.Fill = Brushes.LawnGreen;
                OnlineText.Text = $"{result.Players}/{result.MaxPlayers}";
                OnlineText.Foreground = Brushes.White;
                StatusText.Text = "Готово к игре | Flyt RP";
            }
            else
            {
                OnlineIndicator.Fill = Brushes.Red;
                OnlineText.Text = "OFFLINE";
                OnlineText.Foreground = Brushes.Red;
                StatusText.Text = "Сервер недоступен";
            }
        }

        private (bool Success, int Players, int MaxPlayers) QuerySampServer(string ip, int port)
        {
            try
            {
                using (UdpClient udpClient = new UdpClient())
                {
                    udpClient.Connect(ip, port);
                    udpClient.Client.ReceiveTimeout = 2500;
                    byte[] request = new byte[11];
                    using (MemoryStream ms = new MemoryStream(request))
                    {
                        using (BinaryWriter bw = new BinaryWriter(ms))
                        {
                            bw.Write("SAMP".ToCharArray());
                            bw.Write(new byte[] { 0, 0, 0, 0 });
                            bw.Write((ushort)port);
                            bw.Write((byte)'i');
                        }
                    }
                    udpClient.Send(request, request.Length);
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] response = udpClient.Receive(ref remoteEP);
                    if (response.Length >= 15)
                    {
                        int players = BitConverter.ToUInt16(response, 11);
                        int maxPlayers = BitConverter.ToUInt16(response, 13);
                        if (players >= 256 && players % 256 == 0) players /= 256;
                        if (maxPlayers >= 256 && maxPlayers % 256 == 0) maxPlayers /= 256;
                        return (true, players, maxPlayers);
                    }
                }
            }
            catch { }
            return (false, 0, 0);
        }

        private async void Play_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            btn.IsEnabled = false;

            if (!Directory.Exists(_gamePath)) Directory.CreateDirectory(_gamePath);
            SaveSettings();
            ApplyGameSettings();

            bool updateSuccess = await RunUpdateProcess();
            if (!updateSuccess) { btn.IsEnabled = true; return; }

            string sampExe = Path.Combine(_gamePath, "samp.exe");
            if (File.Exists(sampExe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = sampExe,
                    Arguments = $"{serverIP}:{serverPort} -n{NickNameBox.Text.Trim()}",
                    WorkingDirectory = _gamePath,
                    UseShellExecute = true
                });
            }
            else { MessageBox.Show($"Файл samp.exe не найден по пути: {sampExe}"); }

            btn.IsEnabled = true;
        }

        private async Task<bool> RunUpdateProcess(bool forceVerify = false)
        {
            try
            {
                StatusText.Text = forceVerify ? "Проверка файлов..." : "Синхронизация...";
                DownloadProgress.IsIndeterminate = true;

                using (HttpClient client = new HttpClient())
                {
                    string json = await client.GetStringAsync(distributionUrl);
                    var dist = JsonSerializer.Deserialize<Distribution>(json);

                    if (dist?.Cache == null) throw new Exception("Ошибка загрузки списка файлов.");

                    var toDownload = new List<CacheFile>();
                    foreach (var file in dist.Cache)
                    {
                        if (string.IsNullOrEmpty(file.Name)) continue;

                        string cleanPath = file.Name;
                        if (cleanPath.StartsWith("files\\", StringComparison.OrdinalIgnoreCase)) cleanPath = cleanPath.Substring(6);
                        else if (cleanPath.StartsWith("files/", StringComparison.OrdinalIgnoreCase)) cleanPath = cleanPath.Substring(6);

                        string localPath = Path.Combine(_gamePath, cleanPath);
                        long remoteSize = (file.Bytes != null && file.Bytes.Count > 0) ? file.Bytes[0] : 0;

                        if (!File.Exists(localPath) || new FileInfo(localPath).Length != remoteSize)
                            toDownload.Add(file);
                    }

                    if (toDownload.Count > 0)
                    {
                        DownloadProgress.IsIndeterminate = false;
                        for (int i = 0; i < toDownload.Count; i++)
                        {
                            var file = toDownload[i];

                            string cleanName = file.Name!;
                            if (cleanName.StartsWith("files\\", StringComparison.OrdinalIgnoreCase)) cleanName = cleanName.Substring(6);
                            else if (cleanName.StartsWith("files/", StringComparison.OrdinalIgnoreCase)) cleanName = cleanName.Substring(6);

                            StatusText.Text = $"Загрузка: {Path.GetFileName(cleanName)}";
                            DownloadProgress.Value = (double)(i + 1) / toDownload.Count * 100;

                            string baseCdn = dist.CdnCache?.TrimEnd('/') ?? "";
                            string urlPath = file.Name!.Replace("\\", "/");
                            string url = $"{baseCdn}/{urlPath}";

                            byte[] data = await client.GetByteArrayAsync(url);
                            string savePath = Path.Combine(_gamePath, cleanName);

                            string? dir = Path.GetDirectoryName(savePath);
                            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                            await File.WriteAllBytesAsync(savePath, data);
                        }
                        StatusText.Text = "Обновлено!";
                    }
                    else
                    {
                        StatusText.Text = "Версия актуальна";
                        DownloadProgress.Value = 100;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка обновления";
                MessageBox.Show($"Ошибка скачивания: {ex.Message}", "Flyt RP");
                return false;
            }
            finally { DownloadProgress.IsIndeterminate = false; }
        }

        private void SelectPath_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "samp.exe|samp.exe" };
            if (ofd.ShowDialog() == true)
            {
                _gamePath = Path.GetDirectoryName(ofd.FileName) ?? "";
                GamePathText.Text = _gamePath;
                SaveSettings();
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
        private void Close_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void SaveSettings()
        {
            try
            {
                File.WriteAllText(configPath, JsonSerializer.Serialize(new LauncherConfig
                {
                    Nickname = NickNameBox.Text,
                    GamePath = _gamePath,
                    Resolution = (ResolutionBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "1920 x 1080",
                    FpsLimit = FpsLimitCheck.IsChecked == true,
                    FpsLimitValue = (FpsLimitBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "60 FPS",
                    Widescreen = WidescreenCheck.IsChecked == true
                }));
            }
            catch { }
        }

        private void LoadSettings()
        {
            if (File.Exists(configPath))
            {
                try
                {
                    var config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(configPath));
                    NickNameBox.Text = config?.Nickname ?? "Jake_Toren";
                    _gamePath = config?.GamePath ?? "";

                    foreach (ComboBoxItem item in ResolutionBox.Items)
                        if (item.Content?.ToString() == config?.Resolution) { item.IsSelected = true; break; }

                    FpsLimitCheck.IsChecked = config?.FpsLimit ?? false;
                    FpsLimitBox.IsEnabled = config?.FpsLimit ?? false;
                    foreach (ComboBoxItem item in FpsLimitBox.Items)
                        if (item.Content?.ToString() == config?.FpsLimitValue) { item.IsSelected = true; break; }

                    WidescreenCheck.IsChecked = config?.Widescreen ?? false;
                }
                catch { }
            }
        }
    }

    public class Distribution
    {
        [JsonPropertyName("cache")] public List<CacheFile>? Cache { get; set; }
        [JsonPropertyName("cdnCache")] public string? CdnCache { get; set; }
        [JsonPropertyName("launcherVersion")] public string? LauncherVersion { get; set; }
        [JsonPropertyName("cdnLauncher")] public string? CdnLauncher { get; set; }
    }

    public class CacheFile
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("bytes")] public List<long>? Bytes { get; set; }
    }

    public class LauncherConfig
    {
        public string? Nickname { get; set; }
        public string? GamePath { get; set; }
        public string? Resolution { get; set; }
        public bool FpsLimit { get; set; }
        public string? FpsLimitValue { get; set; }
        public bool Widescreen { get; set; }
    }
}
