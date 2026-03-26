using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input; // Добавлено для перетаскивания окна
using Microsoft.Win32;

namespace SAMPLauncher
{
    public partial class MainWindow : Window
    {
        private string configPath = "config.json";
        
        // ДАННЫЕ ВАШЕГО СЕРВЕРА
        private string serverIP = "188.127.241.8"; 
        private string serverPort = "1179";

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
            
            // Устанавливаем IP в интерфейс (в блок мониторинга)
            // В будущем здесь можно сделать реальный опрос сервера
            // ServerInfoText.Text = $"Flyt RP | {serverIP}"; 
        }

        // Логика кнопки "ИГРАТЬ" (без изменений, ищем samp.exe)
        private void Play_Click(object sender, RoutedEventArgs e)
        {
            string gameDir = GamePathBox?.Text ?? ""; // GamePathBox нужно добавить в разметку настроек, если решишь делать отдельное окно настроек. Пока используем сохраненный путь.
            
            // Так как в новом дизайне мы убрали поле пути с главного экрана, 
            // получаем путь из настроек. Если пути нет, просим выбрать.
            if (string.IsNullOrEmpty(gameDir) || gameDir == AppDomain.CurrentDomain.BaseDirectory)
            {
                MessageBox.Show("Пожалуйста, укажите путь к папке с игрой в настройках (иконка шестеренки).", "Настройка пути");
                SelectPath_Click(sender, e);
                return;
            }

            string sampExe = Path.Combine(gameDir, "samp.exe");

            if (!File.Exists(sampExe))
            {
                MessageBox.Show("Файл samp.exe не найден! Убедитесь, что в выбранной папке установлен SAMP.", "Ошибка");
                return;
            }

            if (string.IsNullOrWhiteSpace(NickNameBox.Text) || NickNameBox.Text == "Jake_Toren") // "Jake_Toren" - это placeholder
            {
                MessageBox.Show("Пожалуйста, введите ваш уникальный никнейм.", "Внимание");
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
                    WorkingDirectory = gameDir,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
                
                // Application.Current.Shutdown(); // Раскомментируй, если хочешь закрывать лаунчер
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска: {ex.Message}");
            }
        }

        // Выбор пути к игре (теперь вызывается через шестеренку)
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
                // Сохраняем путь к папке в невидимое поле или переменную
                // Для простоты в этом примере мы просто сохраним его в конфиг
                string path = Path.GetDirectoryName(openFileDialog.FileName) ?? "";
                
                // Костыль для примера: обновляем конфиг напрямую
                var config = new LauncherConfig
                {
                    Nickname = NickNameBox.Text,
                    GamePath = path
                };
                string json = JsonSerializer.Serialize(config);
                File.WriteAllText(configPath, json);
                
                MessageBox.Show("Путь к игре успешно сохранен!", "Настройки");
            }
        }

        // Позволяет перетаскивать окно, удерживая верхнюю панель
        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        // Кнопка закрытия окна
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void SaveSettings()
        {
            try
            {
                // Получаем старый конфиг, чтобы не затереть путь к игре
                string gamePath = AppDomain.CurrentDomain.BaseDirectory;
                if (File.Exists(configPath))
                {
                    var oldConfig = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(configPath));
                    gamePath = oldConfig?.GamePath ?? gamePath;
                }

                var config = new LauncherConfig
                {
                    Nickname = NickNameBox.Text,
                    GamePath = gamePath
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
                        // Мы не отображаем путь на главном экране, он просто используется при запуске
                        return;
                    }
                }
                catch { }
            }
            NickNameBox.Text = "Jake_Toren";
        }
    }

    public class LauncherConfig
    {
        public string? Nickname { get; set; }
        public string? GamePath { get; set; }
    }
}
