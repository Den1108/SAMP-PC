using System;
using System.Windows;
using System.Windows.Threading; // Добавлено для работы с Dispatcher

namespace SAMPLauncher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Переопределяем метод запуска приложения
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            // Подписываемся на перехват необработанных ошибок в главном потоке (Dispatcher)
            // Это поймает ошибки, возникающие при инициализации XAML и UI.
            Current.DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Подписываемся на ошибки в других потоках (если они будут в будущем)
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Продолжаем стандартный запуск (он откроет MainWindow.xaml)
            base.OnStartup(e);
        }

        /// <summary>
        /// Обработчик ошибок в главном UI потоке
        /// </summary>
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Формируем детальное сообщение об ошибке
            string errorMessage = "Произошла критическая ошибка в работе лаунчера Flyt RP.\n\n" +
                                   $"Описание: {e.Exception.Message}\n\n";

            // Если есть внутренняя ошибка, добавляем её (например, ошибка парсинга XAML)
            if (e.Exception.InnerException != null)
            {
                errorMessage += $"Внутренняя ошибка: {e.Exception.InnerException.Message}\n\n";
            }

            errorMessage += $"Тип ошибки: {e.Exception.GetType().Name}\n" +
                            "Рекомендуется: проверить целостность файлов лаунчера.";

            // Показываем окно ошибки пользователю
            MessageBox.Show(errorMessage, 
                            "Критический сбой (Flyt RP)", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Error);

            // Помечаем ошибку как "обработанную", чтобы Windows не показывала стандартное окно "Программа будет закрыта"
            e.Handled = true;

            // Корректно закрываем приложение после показа ошибки
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Обработчик ошибок в фоновых потоках
        /// </summary>
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Здесь MessageBox может не показаться, так как это не UI поток,
            // но для .NET 8 это хорошая практика для логирования.
            if (e.ExceptionObject is Exception ex)
            {
                // Для простоты мы просто пытаемся показать окно, если это возможно.
                Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Критическая фоновая ошибка:\n{ex.Message}", "Ошибка");
                });
            }
        }
    }
}
