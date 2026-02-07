using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace VideoAudioProcessor;

public partial class MainWindow : Window
{
    private DispatcherTimer _previewTimer;

    private void InitializePreviewTimer()
    {
        _previewTimer = new DispatcherTimer();
        _previewTimer.Interval = TimeSpan.FromMilliseconds(200);
        _previewTimer.Tick += PreviewTimer_Tick;
    }

    private void PreviewTimer_Tick(object sender, EventArgs e)
    {
        if (PreviewMediaPlayer.NaturalDuration.HasTimeSpan)
        {
            PreviewSlider.Value = PreviewMediaPlayer.Position.TotalSeconds /
                                  PreviewMediaPlayer.NaturalDuration.TimeSpan.TotalSeconds * 100;
            PreviewCurrentTime.Text = PreviewMediaPlayer.Position.ToString(@"hh\:mm\:ss");
        }
    }

    private void PreviewPlay_Click(object sender, RoutedEventArgs e)
    {
        PreviewMediaPlayer.Play();
        _previewTimer.Start();
    }

    private void PreviewPause_Click(object sender, RoutedEventArgs e)
    {
        PreviewMediaPlayer.Pause();
        _previewTimer.Stop();
    }

    private void PreviewStop_Click(object sender, RoutedEventArgs e)
    {
        PreviewMediaPlayer.Stop();
        _previewTimer.Stop();
        PreviewSlider.Value = 0;
        PreviewCurrentTime.Text = "00:00";
    }

    private void PreviewMediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (PreviewMediaPlayer.NaturalDuration.HasTimeSpan)
        {
            PreviewTotalTime.Text = PreviewMediaPlayer.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss");
        }
    }

    private void PreviewMediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        PreviewMediaPlayer.Stop();
        _previewTimer.Stop();
    }

    private void PreviewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!PreviewMediaPlayer.NaturalDuration.HasTimeSpan) return;

        var newPosition = TimeSpan.FromSeconds(PreviewSlider.Value / 100 *
            PreviewMediaPlayer.NaturalDuration.TimeSpan.TotalSeconds);
        PreviewMediaPlayer.Position = newPosition;
        PreviewCurrentTime.Text = newPosition.ToString(@"hh\:mm\:ss");
    }
    
    private string _selectedFormat = "mp4"; // значение по умолчанию

    private void OnOutputFormatChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag })
        {
            _selectedFormat = tag;
        }
    }

    private async void ExecuteProcessing_Click(object sender, RoutedEventArgs e)
    {
        var fileName = FileNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(fileName))
        {
            MessageBox.Show("Введите название файла.");
            return;
        } 

        var start = TrimStartTextBox.Text;
        var end = TrimEndTextBox.Text;
        var inputPath = PreviewMediaPlayer.Source.LocalPath;
        var processedPath = Path.Combine(RootPath, "TrackManager", "Processed");
        var outputPath = Path.Combine(processedPath, $"{fileName}.{_selectedFormat}");

        if (File.Exists(outputPath))
        {
            MessageBox.Show("Файл с таким названием уже существует.");
            return;
        }

        try
        {
            // Базовые параметры
            var args = new StringBuilder($"-i \"{inputPath}\" -ss {start} -to {end}");

            // Выбор кодеков в зависимости от формата
            string videoCodec;
            string audioCodec;

            switch (_selectedFormat)
            {
                case "mp4":
                    videoCodec = "libx264";
                    audioCodec = "aac";
                    break;
                case "avi":
                    videoCodec = "mpeg4";
                    audioCodec = "libmp3lame";
                    break;
                case "mkv":
                    videoCodec = "libx264";
                    audioCodec = "aac";
                    break;
                default:
                    throw new InvalidOperationException("Неизвестный формат.");
            }

            // Обработка VP9
            if (Vp9CheckBox.IsChecked == true)
            {
                videoCodec = "libvpx-vp9";
                args.Append($" -c:v {videoCodec} -crf {Vp9CrfTextBox.Text} -b:v 0");
            }
            else
            {
                args.Append($" -c:v {videoCodec}");
            }

            // 2-pass кодирование
            if (TwoPassCheckBox.IsChecked == true)
            {
                args.Append($" -b:v {TwoPassBitrateTextBox.Text} -pass 1 -f null NUL && " +
                           $"ffmpeg -i \"{inputPath}\" -ss {start} -to {end} -c:v {videoCodec} " +
                           $"-b:v {TwoPassBitrateTextBox.Text} -pass 2");
            }

            // Ускоренное кодирование
            if (FastCheckBox.IsChecked == true)
            {
                args.Append(" -preset ultrafast");
            }

            // Обрезка и изменение разрешения
            if (CropResizeCheckBox.IsChecked == true)
            {
                args.Append($" -vf \"crop={CropTextBox.Text},scale={ScaleTextBox.Text}\"");
            }

            // Извлечение аудио в Opus
            if (ExtractOpusCheckBox.IsChecked == true)
            {
                audioCodec = "libopus";
                args.Append(" -c:a libopus -vn");
            }

            // Альфа-канал
            if (AlphaChannelCheckBox.IsChecked == true)
            {
                args.Append(" -vf \"colorkey=0x000000:0.1:0.1,format=yuva420p\"");
            }

            // Изменение FPS
            if (FpsChangeCheckBox.IsChecked == true)
            {
                args.Append($" -r {FpsTextBox.Text}");
            }

            // Удаление аудио
            if (RemoveAudioCheckBox.IsChecked == true)
            {
                args.Append(" -an");
            }
            else
            {
                args.Append($" -c:a {audioCodec}");
            }

            args.Append($" \"{outputPath}\"");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = args.ToString(),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            string errorOutput = await process.StandardError.ReadToEndAsync(); 
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                MessageBox.Show("Файл успешно обработан!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"Ошибка ffmpeg:\n{errorOutput}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка обработки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private async void ExecuteCustomCommand_Click(object sender, RoutedEventArgs e)
    {
        var fileName = FileNameTextBox.Text.Trim();
    
        if (string.IsNullOrWhiteSpace(fileName))
        {
            MessageBox.Show("Введите название файла.");
            return;
        }

        var inputPath = PreviewMediaPlayer.Source.LocalPath;
        var processedPath = Path.Combine(RootPath, "TrackManager", "Processed");
        var outputPath = Path.Combine(processedPath, $"{fileName}.{_selectedFormat}");

        if (File.Exists(outputPath))
        {
            MessageBox.Show("Файл с таким названием уже существует.");
            return;
        }

        try
        {
            // Заменяем плейсхолдеры в команде
            string command = CustomCommandTextBox.Text
                .Replace("{input}", inputPath)
                .Replace("{output}", outputPath);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = command,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            var errorOutput = await process.StandardError.ReadToEndAsync(); 
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                MessageBox.Show("Команда выполнена успешно!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"Ошибка выполнения команды:\n{errorOutput}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка выполнения команды: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}