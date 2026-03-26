using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace SAMPLauncher
{
    public partial class MainWindow : Window
    {
        // Путь к файлу конфигурации
        private string configPath = "config.json";
        
        // Данные сервера (можно вынести в конфиг)
        private string serverIP = "127.0.0.1"; 
        private string serverPort = "7777";

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings(); // Загружаем настройки при старте
        }

        // Логика кнопки "ИГРАТЬ"
        private void Play_Click(object sender, RoutedEventArgs e)
        {
            string gtaExe = Path.Combine(GamePathBox.Text, "gta_sa.exe");

            // Проверяем наличие exe файла
            if (!File.Exists(gtaExe))
            {
                MessageBox.Show("Файл gta_sa.exe не найден по указанному пути!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(NickNameBox.Text))
            {
                MessageBox.Show("Введите никнейм!", "Внимание");
                return;
            }

            SaveSettings(); // Сохраняем перед запуском

            // Формируем аргументы запуска для SAMP
            // -c (подключение) -h (хост) -p (порт) -n (ник)
            string arguments = $"-c -h {serverIP} -p {serverPort} -n {NickNameBox.Text}";

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = gtaExe,
                    Arguments = arguments,
                    WorkingDirectory = GamePathBox.Text // Важно запускать из рабочей директории игры
                };
                Process.Start(startInfo);
                
                // Закрываем лаунчер после успешного запуска (по желанию)
                // Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при запуске: {ex.Message}");
            }
        }

        // Выбор пути к игре
        private void SelectPath_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "GTA Executable (gta_sa.exe)|gta_sa.exe";
            if (openFileDialog.ShowDialog() == true)
            {
                // Сохраняем только путь к папке
                GamePathBox.Text = Path.GetDirectoryName(openFileDialog.FileName);
            }
        }

        // Сохранение настроек в JSON
        private void SaveSettings()
        {
            var data = new LauncherConfig
            {
                Nickname = NickNameBox.Text,
                GamePath = GamePathBox.Text
            };
            string json = JsonSerializer.Serialize(data);
            File.WriteAllText(configPath, json);
        }

        // Загрузка настроек из JSON
        private void LoadSettings()
        {
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                var data = JsonSerializer.Deserialize<LauncherConfig>(json);
                NickNameBox.Text = data.Nickname;
                GamePathBox.Text = data.GamePath;
            }
            else
            {
                // Путь по умолчанию — папка с лаунчером
                GamePathBox.Text = AppDomain.CurrentDomain.BaseDirectory;
            }
        }
    }

    // Класс для структуры конфига
    public class LauncherConfig
    {
        public string Nickname { get; set; }
        public string GamePath { get; set; }
    }
}
