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
using System.Text;

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
        // Добавь в начало класса к остальным событиям клика
private void NavDebug_Click(object sender, MouseButtonEventArgs e)
{
    PageHome.Visibility = Visibility.Collapsed;
    PageSettings.Visibility = Visibility.Collapsed;
    PageDebug.Visibility = Visibility.Visible;
    
    NavDebug.Foreground = Brushes.White;
    NavHome.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8F, 0x98));
    NavSettings.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8F, 0x98));
    
    UpdateDebugLog();
}

private void UpdateDebugInfo_Click(object sender, RoutedEventArgs e) => UpdateDebugLog();

private void UpdateDebugLog()
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"=== FLYT RP DEBUG LOG [{DateTime.Now:HH:mm:ss}] ===");
    
    // 1. Проверка пути игры
    sb.AppendLine($"[GAME PATH]: {_gamePath}");
    sb.AppendLine($"[EXISTS]: {Directory.Exists(_gamePath)}");

    // 2. Проверка Реестра (Никнейм)
    try {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\SAMP")) {
            var name = key?.GetValue("PlayerName");
            sb.AppendLine($"[REGISTRY NICK]: {name ?? "NOT FOUND"}");
        }
    } catch (Exception e) { sb.AppendLine($"[REGISTRY ERROR]: {e.Message}"); }

    // 3. Проверка sa-mp.cfg
    string sampCfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GTA San Andreas User Files", "SAMP", "sa-mp.cfg");
    sb.AppendLine($"[SAMP.CFG PATH]: {sampCfg}");
    if (File.Exists(sampCfg)) {
        sb.AppendLine("[SAMP.CFG CONTENT]:");
        sb.AppendLine(File.ReadAllText(sampCfg));
    } else {
        sb.AppendLine("[SAMP.CFG]: FILE MISSING!");
    }

    // 4. Проверка launcher.ini (для плагина)
    string lIni = Path.Combine(_gamePath, "launcher.ini");
    sb.AppendLine($"[LAUNCHER.INI PATH]: {lIni}");
    if (File.Exists(lIni)) {
        sb.AppendLine("[LAUNCHER.INI CONTENT]:");
        sb.AppendLine(File.ReadAllText(lIni));
    } else {
        sb.AppendLine("[LAUNCHER.INI]: FILE MISSING!");
    }

    // 5. Проверка наличия плагина
    string pluginPath = Path.Combine(_gamePath, "FlytLauncherSettings.asi");
    sb.AppendLine($"[ASI PLUGIN]: {(File.Exists(pluginPath) ? "FOUND" : "NOT FOUND (Settings won't work!)")}");

    DebugLogBox.Text = sb.ToString();
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
        // 1. Путь к sa-mp.cfg (Мои документы)
        string docPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GTA San Andreas User Files", "SAMP");
        if (!Directory.Exists(docPath)) Directory.CreateDirectory(docPath);
        
        string sampCfgPath = Path.Combine(docPath, "sa-mp.cfg");

        // Сбор данных из UI
        string fpsVal = "90";
        if (FpsLimitCheck.IsChecked == true && FpsLimitBox.SelectedItem is ComboBoxItem fpsItem)
            fpsVal = fpsItem.Content.ToString()!.Replace(" FPS", "").Replace("Без ограничений", "0").Trim();

        // Формируем чистый конфиг для SA-MP (ANSI кодировка)
        string cfg = $"pagesize 10\nfpslimit {fpsVal}\nmulticore 1\naudiomsgoff 1\ntimestamp 0\n";
        File.WriteAllText(sampCfgPath, cfg, Encoding.Default);

        // 2. Реестр (Никнейм)
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\SAMP"))
        {
            key?.SetValue("PlayerName", NickNameBox.Text.Trim());
        }

        // 3. Launcher.ini (Для твоего .asi плагина)
        if (Directory.Exists(_gamePath))
        {
            int w = 1920, h = 1080;
            if (ResolutionBox.SelectedItem is ComboBoxItem resItem)
            {
                var parts = resItem.Content.ToString()!.Replace(" ", "").Split('x');
                if (parts.Length == 2) { int.TryParse(parts[0], out w); int.TryParse(parts[1], out h); }
            }

            int wide = WidescreenCheck.IsChecked == true ? 1 : 0;
            string iniPath = Path.Combine(_gamePath, "launcher.ini");

            // Важно: Пишем БЕЗ пробелов вокруг '=' и в Encoding.Default (ANSI)
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[Settings]");
            sb.AppendLine($"Width={w}");
            sb.AppendLine($"Height={h}");
            sb.AppendLine($"Widescreen={wide}");
            sb.AppendLine($"FpsLimit={fpsVal}");

            File.WriteAllText(iniPath, sb.ToString(), Encoding.Default);
        }
    }
    catch (Exception ex) { Debug.WriteLine("Ошибка: " + ex.Message); }
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
            
            // Принудительно сохраняем и применяем перед запуском
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
                    // Никнейм теперь берется из реестра, но можно оставить и -n для подстраховки
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
