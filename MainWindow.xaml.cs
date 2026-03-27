using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media; // Для смены цвета индикатора
using System.Windows.Threading; // Для таймера
using Microsoft.Win32;

namespace SAMPLauncher
{
    public partial class MainWindow : Window
    {
        private string configPath = "config.json";
        private string _gamePath = "";
        
        // ТВОИ ДАННЫЕ СЕРВЕРА
        private string serverIP = "188.127.241.8"; 
        private int serverPort = 1179; // Порт теперь int
        
        // Таймер для автоматического обновления онлайна
        private DispatcherTimer _queryTimer;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();

            // Запускаем опрос сервера при старте
            UpdateServerInfo();

            // Настраиваем таймер обновления каждые 30 секунд
            _queryTimer = new DispatcherTimer();
            _queryTimer.Interval = TimeSpan.FromSeconds(30);
            _queryTimer.Tick += (s, e) => UpdateServerInfo();
            _queryTimer.Start();
        }

        /// <summary>
        /// Основной метод для опроса сервера и обновления UI
        /// </summary>
        private void UpdateServerInfo()
        {
            // Устанавливаем статус "Опрос..." пока идет запрос
            OnlineIndicator.Fill = Brushes.Orange;

            // Выполняем запрос к SAMP серверу
            var result = QuerySampServer(serverIP, serverPort);

            if (result.Success)
            {
                // Сервер ответил
                OnlineIndicator.Fill = Brushes.LawnGreen; // Зеленый
                OnlineText.Text = $"{result.Players}/{result.MaxPlayers}";
                OnlineText.Foreground = Brushes.White; // Делаем текст ярким
                StatusText.Text = $"Готово к игре | Flyt RP (v1.0)";
            }
            else
            {
                // Сервер не ответил (оффлайн)
                OnlineIndicator.Fill = Brushes.Red; // Красный
                OnlineText.Text = "OFFLINE";
                OnlineText.Foreground = Brushes.Red;
                StatusText.Text = "Сервер недоступен!";
            }
        }

        /// <summary>
        /// Реализация SAMP Query протокола для получения онлайна
        /// </summary>
        private (bool Success, int Players, int MaxPlayers) QuerySampServer(string ip, int port)
        {
            try
            {
                using (UdpClient udpClient = new UdpClient())
                {
                    udpClient.Connect(ip, port);
                    udpClient.Client.ReceiveTimeout = 2500; // Ожидание ответа

                    // Формируем пакет запроса 'i' (Information)
                    byte[] request = new byte[11];
                    using (MemoryStream ms = new MemoryStream(request))
                    {
                        using (BinaryWriter bw = new BinaryWriter(ms))
                        {
                            bw.Write("SAMP".ToCharArray());
                    
                            // Разбиваем IP на байты (для запроса можно отправить 0.0.0.0)
                            bw.Write(new byte[] { 0, 0, 0, 0 }); 
                    
                            // Порт в формате Little Endian
                            bw.Write((ushort)port);
                    
                            // Тип запроса
                            bw.Write((byte)'i');
                        }
                    }

                    udpClient.Send(request, request.Length);

                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] response = udpClient.Receive(ref remoteEP);

            // Разбор ответа
            // Структура SAMP 'i': 
            // 0-3: 'SAMP', 4-9: IP/Port, 10: Password (byte)
            // 11-12: Players (2 bytes, Little Endian)
            // 13-14: MaxPlayers (2 bytes, Little Endian)
                    if (response.Length >= 15)
                    {
                        // Используем BitConverter.ToUInt16 напрямую 
                        // Если снова будет 256, значит сервер шлет данные в Big Endian (редко, но бывает)
                        int players = BitConverter.ToUInt16(response, 11);
                        int maxPlayers = BitConverter.ToUInt16(response, 13);

                        // Защита от переворота байтов (если 1 пришел как 256)
                        if (players >= 256 && players % 256 == 0) players /= 256;
                        if (maxPlayers >= 256 && maxPlayers % 256 == 0) maxPlayers /= 256;

                        return (true, players, maxPlayers);
                    }
                }
            }
            catch { }
            return (false, 0, 0);
        }

        // --- ЛОГИКА КНОПОК И НАСТРОЕК (без изменений) ---

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_gamePath) || _gamePath == AppDomain.CurrentDomain.BaseDirectory)
            {
                MessageBox.Show("Пожалуйста, укажите путь к папке с игрой в настройках (⚙).", "Настройка пути");
                SelectPath_Click(sender, e);
                return;
            }

            string sampExe = Path.Combine(_gamePath, "samp.exe");

            if (!File.Exists(sampExe))
            {
                MessageBox.Show("Файл samp.exe не найден! Убедитесь, что в папке установлен SAMP.", "Ошибка");
                return;
            }

            if (string.IsNullOrWhiteSpace(NickNameBox.Text) || NickNameBox.Text == "Jake_Toren")
            {
                MessageBox.Show("Пожалуйста, введите ваш никнейм.", "Внимание");
                return;
            }

            SaveSettings();

            // Формируем аргументы для samp.exe
            string arguments = $"{serverIP}:{serverPort} -n{NickNameBox.Text.Trim()}";

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = sampExe,
                    Arguments = arguments,
                    WorkingDirectory = _gamePath,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска: {ex.Message}");
            }
        }

        private void SelectPath_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Выберите samp.exe в папке с игрой",
                Filter = "SAMP Executable (samp.exe)|samp.exe",
                CheckFileExists = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _gamePath = Path.GetDirectoryName(openFileDialog.FileName) ?? "";
                SaveSettings();
                UpdateServerInfo(); // Сразу обновляем онлайн, вдруг путь к игре повлиял
                MessageBox.Show("Путь к игре сохранен!", "Flyt RP");
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void SaveSettings()
        {
            try
            {
                var config = new LauncherConfig
                {
                    Nickname = NickNameBox.Text,
                    GamePath = _gamePath
                };
                string json = JsonSerializer.Serialize(config);
                File.WriteAllText(configPath, json);
            }
            catch { }
        }

        private void LoadSettings()
        {
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<LauncherConfig>(json);

                    if (config != null)
                    {
                        NickNameBox.Text = config.Nickname ?? "Jake_Toren";
                        _gamePath = config.GamePath ?? AppDomain.CurrentDomain.BaseDirectory;
                        return;
                    }
                }
                catch { }
            }
            _gamePath = AppDomain.CurrentDomain.BaseDirectory;
            NickNameBox.Text = "Jake_Toren";
        }
    }

    public class LauncherConfig
    {
        public string? Nickname { get; set; }
        public string? GamePath { get; set; }
    }
}
