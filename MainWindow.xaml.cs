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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace SAMPLauncher
{
    public partial class MainWindow : Window
    {
        private const string CurrentLauncherVersion = "1.0.3"; 

        private string configPath = "config.json";
        private string _gamePath = "";
        
        private string serverIP = "188.127.241.8"; 
        private int serverPort = 1179;
        private string distributionUrl = "http://87.106.105.24:12867/distribution.json"; 

        private DispatcherTimer _queryTimer;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();

            if (string.IsNullOrEmpty(_gamePath))
                _gamePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "files");

            UpdateServerInfo();

            _queryTimer = new DispatcherTimer();
            _queryTimer.Interval = TimeSpan.FromSeconds(30);
            _queryTimer.Tick += (s, e) => UpdateServerInfo();
            _queryTimer.Start();

            _ = CheckLauncherUpdate();
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
                            var result = MessageBox.Show($"Доступна новая версия Flyt RP ({dist.LauncherVersion}). Обновить?", 
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
            } catch { }
            return (false, 0, 0);
        }

        private async void Play_Click(object sender, RoutedEventArgs e)
        {
            var btn = (System.Windows.Controls.Button)sender;
            btn.IsEnabled = false;

            if (!Directory.Exists(_gamePath)) Directory.CreateDirectory(_gamePath);
            SaveSettings();

            bool updateSuccess = await RunUpdateProcess();
            if (!updateSuccess) { btn.IsEnabled = true; return; }

            if (TestModeCheck.IsChecked == true)
            {
                MessageBox.Show("Режим теста: запуск отменен.", "Flyt RP");
                btn.IsEnabled = true;
                return;
            }

            string sampExe = Path.Combine(_gamePath, "samp.exe");
            if (File.Exists(sampExe))
            {
                Process.Start(new ProcessStartInfo { 
                    FileName = sampExe, 
                    Arguments = $"{serverIP}:{serverPort} -n{NickNameBox.Text.Trim()}", 
                    WorkingDirectory = _gamePath, 
                    UseShellExecute = true 
                });
            }
            else { MessageBox.Show("Файл samp.exe не найден!"); }

            btn.IsEnabled = true;
        }

        private async Task<bool> RunUpdateProcess()
        {
            try
            {
                StatusText.Text = "Синхронизация...";
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
                        string cleanPath = file.Name.Replace("SAMP\\", "").Replace("SAMP/", "");
                        string localPath = Path.Combine(_gamePath, cleanPath);
                        long remoteSize = (file.Bytes != null && file.Bytes.Count > 0) ? file.Bytes[0] : 0;

                        if (!File.Exists(localPath) || new FileInfo(localPath).Length != remoteSize)
                        {
                            // ОТЛАДКА: показываем первый несовпадающий файл
                            MessageBox.Show(
                                $"НЕ СОВПАДАЕТ:\n" +
                                $"Файл: {localPath}\n" +
                                $"Существует: {File.Exists(localPath)}\n" +
                                $"Размер локальный: {(File.Exists(localPath) ? new FileInfo(localPath).Length : 0)}\n" +
                                $"Размер в манифесте: {remoteSize}",
                                "Отладка");
                            break; // убрать break после того как найдёшь проблему
                            toDownload.Add(file);
                        }
                    }

                    if (toDownload.Count > 0)
                    {
                        DownloadProgress.IsIndeterminate = false;
                        for (int i = 0; i < toDownload.Count; i++)
                        {
                            var file = toDownload[i];
                            string cleanName = file.Name!.Replace("SAMP\\", "").Replace("SAMP/", "");
                            StatusText.Text = $"Загрузка: {Path.GetFileName(cleanName)}";
                            DownloadProgress.Value = (double)(i + 1) / toDownload.Count * 100;

                            string baseCdn = dist.CdnCache?.TrimEnd('/') ?? "";
                            string filePath = file.Name!.Replace("\\", "/").TrimStart('/');
                            string url = $"{baseCdn}/{filePath}";

                            byte[] data = await client.GetByteArrayAsync(url);
                            string savePath = Path.Combine(_gamePath, cleanName);
                            string? dir = Path.GetDirectoryName(savePath);
                            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                            await File.WriteAllBytesAsync(savePath, data);
                        }
                        StatusText.Text = "Обновлено!";
                    }
                    else { StatusText.Text = "Версия актуальна"; DownloadProgress.Value = 100; }
                }
                return true;
            }
            catch (Exception ex) 
            { 
                StatusText.Text = "Ошибка обновления"; 
                MessageBox.Show($"Ошибка: {ex.Message}", "Flyt RP");
                return false;
            }
            finally { DownloadProgress.IsIndeterminate = false; }
        }

        private void SelectPath_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "samp.exe|samp.exe" };
            if (ofd.ShowDialog() == true) { _gamePath = Path.GetDirectoryName(ofd.FileName) ?? ""; SaveSettings(); }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
        private void Close_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void SaveSettings() {
            try { File.WriteAllText(configPath, JsonSerializer.Serialize(new LauncherConfig { Nickname = NickNameBox.Text, GamePath = _gamePath })); } catch { }
        }

        private void LoadSettings() {
            if (File.Exists(configPath)) {
                try {
                    var config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(configPath));
                    NickNameBox.Text = config?.Nickname ?? "Jake_Toren";
                    _gamePath = config?.GamePath ?? "";
                } catch { }
            }
        }
    }

    public class Distribution {
        [JsonPropertyName("cache")] public List<CacheFile>? Cache { get; set; }
        [JsonPropertyName("cdnCache")] public string? CdnCache { get; set; }
        [JsonPropertyName("launcherVersion")] public string? LauncherVersion { get; set; }
        [JsonPropertyName("cdnLauncher")] public string? CdnLauncher { get; set; }
    }

    public class CacheFile {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("bytes")] public List<long>? Bytes { get; set; }
    }

    public class LauncherConfig {
        public string? Nickname { get; set; }
        public string? GamePath { get; set; }
    }
}
