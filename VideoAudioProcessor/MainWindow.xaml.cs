using System.IO;
using System.Windows;
using Microsoft.Win32;
using System.Configuration;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace VideoAudioProcessor;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private static string QueuePath => Path.Combine(RootPath, "TrackManager", "Queue");
    
    private static string RootPath
    {
        get => ConfigurationManager.AppSettings["RootPath"]!;
        set
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["RootPath"].Value = value;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }
    }
    
    public MainWindow()
    {
        InitializeComponent();
        InitializePreviewTimer();
        InitializeProgressTimer();
        VolumeSlider.Value = 0.5;
    }

    private void LoadFile_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(RootPath))
        {
            MessageBox.Show("Пожалуйста, сначала установите корневую папку", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Выберите корневую папку"
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                RootPath = dialog.FileName;
            }
            return;
        }

        var openFileDialog = new OpenFileDialog
        {
            Filter = "Медиа файлы|*.mp3;*.wav;*.ogg;*.flac;*.mp4;*.avi;*.mkv;*.mov;*.wmv|Все файлы|*.*",
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() != true) return;
        try
        {
            Directory.CreateDirectory(QueuePath);
            var destinationPath = Path.Combine(QueuePath, Path.GetFileName(openFileDialog.FileName));
            File.Copy(openFileDialog.FileName, destinationPath, true);
            MessageBox.Show($"Файл сохранен: {destinationPath}", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при загрузке файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowQueue_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(RootPath))
        {
            MessageBox.Show("Пожалуйста, сначала установите корневую папку", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!Directory.Exists(QueuePath))
        {
            MessageBox.Show("Папка очереди не существует", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var files = Directory.GetFiles(QueuePath)
            .Where(f => MediaFormats.Supported.Contains(Path.GetExtension(f).ToLower()))
            .ToList();

        FilesListBox.ItemsSource = files.Select(Path.GetFileName);
        HideAllScreens();
        QueueScreen.Visibility = Visibility.Visible;
    }

    private void ShowProcessed_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(RootPath))
        {
            MessageBox.Show("Пожалуйста, сначала установите корневую папку", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var processedPath = Path.Combine(RootPath, "TrackManager", "Processed");
        if (Directory.Exists(processedPath))
        {
            System.Diagnostics.Process.Start("explorer.exe", processedPath);
        }
        else
        {
            MessageBox.Show("Папка обработанных файлов не существует", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void HideAllScreens()
    {
        StartScreen.Visibility = Visibility.Collapsed;
        QueueScreen.Visibility = Visibility.Collapsed;
        ProcessScreen.Visibility = Visibility.Collapsed;
    }
}