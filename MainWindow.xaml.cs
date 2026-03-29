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
        
        // ХОСТИНГ
        private string distributionUrl = "http://87.106.105.24:12867/distribution.json"; 

        private DispatcherTimer _queryTimer;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();

            // Если путь пустой, по умолчанию папка SAMP в директории лаунчера
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
            var btn = (System.Windows.Controls.Button)sender;
            btn.IsEnabled = false;

            if (!Directory.Exists(_gamePath)) Directory.CreateDirectory(_gamePath);

            SaveSettings();

            // Запуск процесса обновления
            bool updateSuccess = await RunUpdateProcess();

            if (!updateSuccess)
            {
                // Если RunUpdateProcess вернул false, кнопка разблокируется, запуск прерывается
                btn.IsEnabled = true;
                return;
            }

            // Проверка режима тестирования
            if (TestModeCheck.IsChecked == true)
            {
                MessageBox.Show("Файлы проверены. Режим теста активен: запуск игры отменен.", "Flyt RP Тест");
                btn.IsEnabled = true;
                return;
            }

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
                MessageBox.Show($"Файл samp.exe не найден!\nПуть: {sampExe}", "Ошибка запуска");
            }

            btn.IsEnabled = true;
        }

        private async Task<bool> RunUpdateProcess()
        {
            try
            {
                StatusText.Text = "Синхронизация с сервером...";
                DownloadProgress.IsIndeterminate = true;

                using (HttpClient client = new HttpClient())
                {
                    // Загружаем JSON
                    string json = await client.GetStringAsync(distributionUrl);
                    var dist = JsonSerializer.Deserialize<Distribution>(json);

                    if (dist?.Cache == null) 
                        throw new Exception("Файл distribution.json пуст или имеет неверную структуру.");

                    var toDownload = new List<CacheFile>();

                    foreach (var file in dist.Cache)
                    {
                        if (string.IsNullOrEmpty(file.Name)) continue;

                        // Удаляем "SAMP\" из начала пути, чтобы корректно искать файл в выбранной папке
                        string cleanPath = file.Name;
                        if (cleanPath.StartsWith("SAMP\\", StringComparison.OrdinalIgnoreCase))
                            cleanPath = cleanPath.Substring(5);
                        else if (cleanPath.StartsWith("SAMP/", StringComparison.OrdinalIgnoreCase))
                            cleanPath = cleanPath.Substring(5);

                        string localFullPath = Path.Combine(_gamePath, cleanPath);
                        long remoteSize = (file.Bytes != null && file.Bytes.Count > 0) ? file.Bytes[0] : 0;

                        // Сравниваем только реальный размер (не "на диске")
                        if (!File.Exists(localFullPath))
                        {
                            toDownload.Add(file);
                        }
                        else
                        {
                            long localSize = new FileInfo(localFullPath).Length;
                            if (localSize != remoteSize)
                            {
                                toDownload.Add(file);
                            }
                        }
                    }

                    if (toDownload.Count > 0)
                    {
                        DownloadProgress.IsIndeterminate = false;
                        for (int i = 0; i < toDownload.Count; i++)
                        {
                            var file = toDownload[i];
                            
                            // Формируем чистый путь для сохранения (без SAMP\)
                            string cleanName = file.Name!.Replace("SAMP\\", "").Replace("SAMP/", "");
                            
                            StatusText.Text = $"Загрузка: {Path.GetFileName(cleanName)}";
                            DownloadProgress.Value = (double)(i + 1) / toDownload.Count * 100;

                            // Собираем URL для скачивания
                            string baseCdn = dist.CdnCache?.TrimEnd('/') ?? "";
                            string url = $"{baseCdn}/{file.Name.Replace("\\", "/")}";

                            try 
                            {
                                byte[] data = await client.GetByteArrayAsync(url);
                                string savePath = Path.Combine(_gamePath, cleanName);
                                
                                string? dir = Path.GetDirectoryName(savePath);
                                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                                
                                await File.WriteAllBytesAsync(savePath, data);
                            }
                            catch (Exception ex)
                            {
                                throw new Exception($"Ошибка при скачивании файла {file.Name}:\n{ex.Message}");
                            }
                        }
                        StatusText.Text = "Обновление завершено!";
                    }
                    else
                    {
                        StatusText.Text = "Версия игры актуальна.";
                        DownloadProgress.Value = 100;
                    }
                }
                return true;
            }
            catch (Exception ex) 
            { 
                StatusText.Text = "Ошибка обновления"; 
                MessageBox.Show($"Детали ошибки:\n{ex.Message}\n\nВозможно, сервер недоступен или файлы отсутствуют на хостинге.", "Сбой обновления");
                return false;
            }
            finally 
            { 
                DownloadProgress.IsIndeterminate = false; 
            }
        }

        private void SelectPath_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "samp.exe|samp.exe" };
            if (ofd.ShowDialog() == true) 
            { 
                _gamePath = Path.GetDirectoryName(ofd.FileName) ?? ""; 
                SaveSettings(); 
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) 
        { 
            if (e.LeftButton == MouseButtonState.Pressed) DragMove(); 
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void SaveSettings() 
        {
            try 
            { 
                var config = new LauncherConfig { Nickname = NickNameBox.Text, GamePath = _gamePath };
                File.WriteAllText(configPath, JsonSerializer.Serialize(config)); 
            } catch { }
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
                } catch { }
            }
        }
    }

    public class Distribution 
    {
        [JsonPropertyName("cache")] public List<CacheFile>? Cache { get; set; }
        [JsonPropertyName("cdnCache")] public string? CdnCache { get; set; }
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
    }
}
