using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace SAMPLauncher
{
    public partial class MainWindow : Window
    {
        private string configPath = "config.json";
        private string serverIP = "188.127.241.8"; 
        private string serverPort = "1179";

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings(); 
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            // Используем ?? "" чтобы избежать предупреждения о null
            string gamePath = GamePathBox.Text ?? "";
            string gtaExe = Path.Combine(gamePath, "gta_sa.exe");

            if (!File.Exists(gtaExe))
            {
                MessageBox.Show("Файл gta_sa.exe не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(NickNameBox.Text))
            {
                MessageBox.Show("Введите никнейм!", "Внимание");
                return;
            }

            SaveSettings();

            string arguments = $"-c -h {serverIP} -p {serverPort} -n {NickNameBox.Text}";

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = gtaExe,
                    Arguments = arguments,
                    WorkingDirectory = gamePath 
                };
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при запуске: {ex.Message}");
            }
        }

        private void SelectPath_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "GTA Executable (gta_sa.exe)|gta_sa.exe";
            if (openFileDialog.ShowDialog() == true)
            {
                // Если путь не найден, записываем пустую строку
                GamePathBox.Text = Path.GetDirectoryName(openFileDialog.FileName) ?? "";
            }
        }

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

        private void LoadSettings()
        {
            if (File.Exists(configPath))
            {
                try 
                {
                    string json = File.ReadAllText(configPath);
                    var data = JsonSerializer.Deserialize<LauncherConfig>(json);
                    
                    // Безопасно устанавливаем значения
                    NickNameBox.Text = data?.Nickname ?? "";
                    GamePathBox.Text = data?.GamePath ?? AppDomain.CurrentDomain.BaseDirectory;
                }
                catch 
                {
                    GamePathBox.Text = AppDomain.CurrentDomain.BaseDirectory;
                }
            }
            else
            {
                GamePathBox.Text = AppDomain.CurrentDomain.BaseDirectory;
            }
        }
    }

    public class LauncherConfig
    {
        // Добавляем ?, чтобы разрешить пустые значения и убрать предупреждения компилятора
        public string? Nickname { get; set; }
        public string? GamePath { get; set; }
    }
}
