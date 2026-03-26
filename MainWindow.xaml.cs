using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace SAMPLauncher
{
    public partial class MainWindow : Window
    {
        private string configPath = "config.json";
        
        // Поля для хранения настроек в памяти
        private string _gamePath = "";
        
        // ДАННЫЕ ВАШЕГО СЕРВЕРА
        private string serverIP = "188.127.241.8"; 
        private string serverPort = "1179";

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings(); // Загружаем ник и путь к игре из конфига
        }

        // Логика кнопки "ИГРАТЬ"
        private void Play_Click(object sender, RoutedEventArgs e)
        {
            // Используем переменную _gamePath вместо отсутствующего GamePathBox
            if (string.IsNullOrEmpty(_gamePath) || _gamePath == AppDomain.CurrentDomain.BaseDirectory)
            {
                MessageBox.Show("Пожалуйста, укажите путь к папке с игрой в настройках (иконка шестеренки).", "Настройка пути");
                SelectPath_Click(sender, e);
                return;
            }

            string sampExe = Path.Combine(_gamePath, "samp.exe");

            if (!File.Exists(sampExe))
            {
                MessageBox.Show("Файл samp.exe не найден! Проверьте путь в настройках.", "Ошибка");
                return;
            }

            if (string.IsNullOrWhiteSpace(NickNameBox.Text) || NickNameBox.Text == "Jake_Toren")
            {
                MessageBox.Show("Пожалуйста, введите ваш никнейм.", "Внимание");
                return;
            }

            SaveSettings();

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

        // Выбор пути к игре (через шестеренку)
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
                SaveSettings(); // Сразу сохраняем выбранный путь
                MessageBox.Show("Путь к игре успешно сохранен!", "Flyt RP");
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
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
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
