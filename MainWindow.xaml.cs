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
        private string configPath = "config.json";
        private string _gamePath = "";
        
        // ДАННЫЕ СЕРВЕРА
        private string serverIP = "188.127.241.8"; 
        private int serverPort = 1179;
        // Укажи здесь прямую ссылку на свой json
        private string distributionUrl = "http://твой-айпи/distribution.json"; 

        private DispatcherTimer _queryTimer;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();

            // Если путь не задан, используем папку SAMP рядом с лаунчером
            if (string.IsNullOrEmpty(_gamePath))
            {
                _gamePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SAMP");
            }

            UpdateServerInfo();

            _queryTimer = new DispatcherTimer();
            _queryTimer.Interval = TimeSpan.FromSeconds(30);
            _queryTimer.Tick += (s, e) => UpdateServerInfo();
            _queryTimer.Start();
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
            // Автоматически создаем папку SAMP, если её нет
            if (!Directory.Exists(_gamePath)) Directory.CreateDirectory(_gamePath);

            SaveSettings();

            // Запускаем обновление
            await RunUpdateProcess();

            string sampExe = Path.Combine(_gamePath, "samp.exe");
            if (File.Exists(sampExe))
            {
                string arguments = $"{serverIP}:{serverPort} -n{NickNameBox.Text.Trim()}";
                Process.Start(new ProcessStartInfo { 
                    FileName = sampExe, 
                    Arguments = arguments, 
                    WorkingDirectory = _gamePath, 
                    UseShellExecute = true 
                });
            }
            else
            {
                MessageBox.Show("Файл samp.exe не найден после обновления. Проверьте distribution.json", "Ошибка");
            }
        }

        private async Task RunUpdateProcess()
        {
            try
            {
                StatusText.Text = "Проверка файлов...";
                DownloadProgress.IsIndeterminate = true;

                using (HttpClient client = new HttpClient())
                {
                    string json = await client.GetStringAsync(distributionUrl);
                    var dist = JsonSerializer.Deserialize<Distribution>(json);

                    if (dist?.Cache == null) return;

                    var toDownload = new List<CacheFile>();
                    foreach (var file in dist.Cache)
                    {
                        // Теперь скрипт Node.js пишет "SAMP\\...", убираем это для локального пути
                        string localRelativePath = file.Name.Replace("SAMP\\", "").Replace("samp\\", "");
                        string localPath = Path.Combine(_gamePath, localRelativePath);
                        
                        if (!File.Exists(localPath) || new FileInfo(localPath).Length != file.Bytes[0])
                        {
                            toDownload.Add(file);
                        }
                    }

                    if (toDownload.Count > 0)
                    {
                        DownloadProgress.IsIndeterminate = false;
                        for (int i = 0; i < toDownload.Count; i++)
                        {
                            var file = toDownload[i];
                            string displayPath = file.Name.Replace("SAMP\\", "").Replace("samp\\", "");
                            StatusText.Text = $"Загрузка: {displayPath}";
                            DownloadProgress.Value = (double)(i + 1) / toDownload.Count * 100;

                            string url = dist.CdnCache + file.Name.Replace("\\", "/");
                            byte[] data = await client.GetByteArrayAsync(url);

                            string savePath = Path.Combine(_gamePath, displayPath);
                            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
                            await File.WriteAllBytesAsync(savePath, data);
                        }
                        StatusText.Text = "Обновление завершено!";
                    }
                    else
                    {
                        StatusText.Text = "Файлы проверены.";
                        DownloadProgress.Value = 100;
                    }
                }
            }
            catch (Exception ex) { StatusText.Text = "Ошибка обновления."; }
            finally { DownloadProgress.IsIndeterminate = false; }
        }

        private void SelectPath_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "samp.exe|samp.exe" };
            if (ofd.ShowDialog() == true) { _gamePath = Path.GetDirectoryName(ofd.FileName); SaveSettings(); }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
        private void Close_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void SaveSettings() {
            File.WriteAllText(configPath, JsonSerializer.Serialize(new LauncherConfig { Nickname = NickNameBox.Text, GamePath = _gamePath }));
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
        [JsonPropertyName("cache")] public List<CacheFile> Cache { get; set; }
        [JsonPropertyName("cdnCache")] public string CdnCache { get; set; }
    }

    public class CacheFile {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("bytes")] public List<long> Bytes { get; set; }
    }

    public class LauncherConfig {
        public string Nickname { get; set; }
        public string GamePath { get; set; }
    }
}
