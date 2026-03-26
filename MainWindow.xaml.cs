using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace SAMPLauncher
{
    public partial class MainWindow : Window
    {
        // Настройки подключения к серверу
        private string configPath = "config.json";
        private string serverIP = "188.127.241.8"; 
        private string serverPort = "1179";

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings(); // Загружаем ник и путь при запуске
        }

        /// <summary>
        /// Логика кнопки "ИГРАТЬ"
        /// </summary>
        private void Play_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем путь к папке
            string gameDir = GamePathBox.Text ?? "";
            string sampExe = Path.Combine(gameDir, "samp.exe");

            // Проверка: установлен ли SAMP в выбранной папке
            if (!File.Exists(sampExe))
            {
                MessageBox.Show("Файл samp.exe не найден! Выберите папку, где установлен SAMP.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Проверка: введен ли ник
            if (string.IsNullOrWhiteSpace(NickNameBox.Text))
            {
                MessageBox.Show("Пожалуйста, введите ваш никнейм перед игрой.", "Внимание");
                return;
            }

            // Сохраняем данные перед запуском
            SaveSettings();

            // Формируем аргументы для samp.exe: "IP:PORT -nNICKNAME"
            // ВАЖНО: Между -n и ником не должно быть пробела
            string arguments = $"{serverIP}:{serverPort} -n{NickNameBox.Text.Trim()}";

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = sampExe,
                    Arguments = arguments,
                    WorkingDirectory = gameDir, // Запуск из директории игры для подгрузки samp.dll
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                
                // Опционально: закрыть лаунчер после запуска
                // Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось запустить SAMP: {ex.Message}", "Критическая ошибка");
            }
        }

        /// <summary>
        /// Выбор пути к игре через проводник
        /// </summary>
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
                // Записываем путь к папке, а не к самому файлу
                GamePathBox.Text = Path.GetDirectoryName(openFileDialog.FileName) ?? "";
            }
        }

        /// <summary>
        /// Сохранение настроек в config.json
        /// </summary>
        private void SaveSettings()
        {
            try
            {
                var config = new LauncherConfig
                {
                    Nickname = NickNameBox.Text,
                    GamePath = GamePathBox.Text
                };

                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка сохранения конфига: {ex.Message}");
            }
        }

        /// <summary>
        /// Загрузка сохраненных данных
        /// </summary>
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
                        NickNameBox.Text = config.Nickname ?? "";
                        GamePathBox.Text = config.GamePath ?? AppDomain.CurrentDomain.BaseDirectory;
                        return;
                    }
                }
                catch { /* Ошибка чтения — используем стандартные значения */ }
            }

            // Если конфига нет, ставим путь по умолчанию (папка лаунчера)
            GamePathBox.Text = AppDomain.CurrentDomain.BaseDirectory;
        }
    }

    /// <summary>
    /// Класс модели данных для JSON
    /// </summary>
    public class LauncherConfig
    {
        public string? Nickname { get; set; }
        public string? GamePath { get; set; }
    }
}
