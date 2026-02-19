using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Microsoft.VisualBasic;

namespace VideoAudioProcessor;

public partial class MainWindow : Window
{
    private const string ProjectsFolderName = "Projects";
    private ProjectType _currentProjectType;
    private ProjectData? _currentProject;
    private readonly ObservableCollection<ProjectMediaItem> _timelineItems = new();

    private string ProjectsRootPath => Path.Combine(RootPath, "TrackManager", ProjectsFolderName);

    private void ShowVideoCollageProjects_Click(object sender, RoutedEventArgs e)
    {
        ShowProjectList(ProjectType.VideoCollage);
    }


    private void ShowProjectList(ProjectType type)
    {
        if (string.IsNullOrEmpty(RootPath))
        {
            MessageBox.Show("Пожалуйста, сначала установите корневую папку", "Ошибка", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _currentProjectType = type;
        ProjectListTitle.Text = "Проекты медиаколлажей";
        RefreshProjectList();
        HideAllScreens();
        ProjectListScreen.Visibility = Visibility.Visible;
    }

    private void RefreshProjectList()
    {
        var projectsPath = GetProjectsPath(_currentProjectType);
        Directory.CreateDirectory(projectsPath);

        var names = Directory.GetFiles(projectsPath, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(name => name)
            .ToList();

        ProjectsListBox.ItemsSource = names;
    }

    private void CreateProject_Click(object sender, RoutedEventArgs e)
    {
        var name = Interaction.InputBox("Введите название проекта", "Новый проект", "");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var sanitizedName = name.Trim();
        if (!IsValidProjectName(sanitizedName))
        {
            MessageBox.Show("Название проекта содержит недопустимые символы.");
            return;
        }

        var projectPath = GetProjectFilePath(_currentProjectType, sanitizedName);
        if (File.Exists(projectPath))
        {
            MessageBox.Show("Проект с таким названием уже существует.");
            return;
        }

        var project = new ProjectData
        {
            Name = sanitizedName,
            Type = _currentProjectType
        };

        SaveProjectToFile(project);
        OpenProjectEditor(project);
    }

    private void ProjectsListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenSelectedProject();
    }

    private void EditProject_Click(object sender, RoutedEventArgs e)
    {
        OpenSelectedProject();
    }

    private void OpenSelectedProject()
    {
        if (ProjectsListBox.SelectedItem is not string projectName)
        {
            return;
        }

        var projectPath = GetProjectFilePath(_currentProjectType, projectName);
        if (!File.Exists(projectPath))
        {
            MessageBox.Show("Файл проекта не найден.");
            RefreshProjectList();
            return;
        }

        var json = File.ReadAllText(projectPath);
        var project = JsonSerializer.Deserialize<ProjectData>(json);
        if (project == null)
        {
            MessageBox.Show("Не удалось загрузить проект.");
            return;
        }

        OpenProjectEditor(project);
    }

    private void DeleteProject_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectsListBox.SelectedItem is not string projectName)
        {
            return;
        }

        var result = MessageBox.Show($"Удалить проект {projectName}?", "Подтверждение", MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var projectPath = GetProjectFilePath(_currentProjectType, projectName);
        if (File.Exists(projectPath))
        {
            File.Delete(projectPath);
        }

        RefreshProjectList();
    }

    private void OpenProjectEditor(ProjectData project)
    {
        _currentProject = project;
        _timelineItems.Clear();
        foreach (var item in project.Items)
        {
            _timelineItems.Add(item);
        }

        TimelineItemsListBox.ItemsSource = _timelineItems;
        ProjectEditorTitle.Text = "Форма редактирования медиаколлажа";
        ProjectNameTextBox.Text = project.Name;
        SelectedAudioText.Text = string.IsNullOrWhiteSpace(project.AudioPath) ? "Аудио не выбрано" :
            Path.GetFileName(project.AudioPath);
        UseVideoAudioCheckBox.IsChecked = project.UseVideoAudio;
        ProjectOutputFormatComboBox.SelectedIndex = project.OutputFormat switch
        {
            "mkv" => 1,
            "avi" => 2,
            _ => 0
        };
        ProjectWidthTextBox.Text = project.Width.ToString();
        ProjectHeightTextBox.Text = project.Height.ToString();
        ProjectFpsTextBox.Text = project.Fps.ToString();
        ProjectTransitionTextBox.Text = project.TransitionSeconds.ToString();
        SlideDurationTextBox.Text = project.SlideDurationSeconds.ToString();
        MaxClipDurationTextBox.Text = project.MaxClipDurationSeconds.ToString();

        UseVideoAudioCheckBox.Visibility = Visibility.Visible;
        SlideDurationLabel.Visibility = Visibility.Collapsed;
        SlideDurationTextBox.Visibility = Visibility.Collapsed;

        RefreshTimelinePreview();

        HideAllScreens();
        ProjectEditorScreen.Visibility = Visibility.Visible;
    }

    private void AddVideoFromBase_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null)
        {
            return;
        }

        var selected = SelectFromBaseTracks("Видео файлы|*.mp4;*.avi;*.mkv;*.mov;*.wmv");
        if (string.IsNullOrWhiteSpace(selected))
        {
            return;
        }

        AddTimelineItem(selected, ProjectMediaKind.Video);
    }

    private void AddVideoFromComputer_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Видео файлы|*.mp4;*.avi;*.mkv;*.mov;*.wmv",
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() != true)
        {
            return;
        }

        AddTimelineItem(openFileDialog.FileName, ProjectMediaKind.Video);
    }


    private void AddImageFromBase_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null)
        {
            return;
        }

        var selected = SelectFromBaseTracks("Изображения|*.jpg;*.jpeg;*.png;*.bmp;*.gif");
        if (string.IsNullOrWhiteSpace(selected))
        {
            return;
        }

        AddTimelineItem(selected, ProjectMediaKind.Image);
    }

    private void AddImageFromComputer_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() != true)
        {
            return;
        }

        AddTimelineItem(openFileDialog.FileName, ProjectMediaKind.Image);
    }

    private void AddTimelineItem(string path, ProjectMediaKind kind)
    {
        if (_currentProject == null)
        {
            return;
        }

        var duration = kind == ProjectMediaKind.Image
            ? PromptDurationSeconds(3)
            : GetTrimmedDuration(path, ParseDoubleOrDefault(MaxClipDurationTextBox.Text, 0));

        var item = new ProjectMediaItem
        {
            Path = path,
            DurationSeconds = duration,
            Kind = kind
        };

        _timelineItems.Add(item);
        _currentProject.Items = _timelineItems.ToList();
        SaveProjectToFile(_currentProject);
        RefreshTimelinePreview();
    }

    private static double PromptDurationSeconds(double defaultValue)
    {
        var input = Interaction.InputBox("Укажите длительность отображения в секундах", "Длительность",
            defaultValue.ToString("0.##"));
        var duration = ParseDoubleOrDefault(input, defaultValue);
        return Math.Max(0.5, duration);
    }

    private void EditTimelineItemDuration_Click(object sender, RoutedEventArgs e)
    {
        if (TimelineItemsListBox.SelectedItem is not ProjectMediaItem selected || _currentProject == null)
        {
            return;
        }

        selected.DurationSeconds = PromptDurationSeconds(selected.DurationSeconds > 0 ? selected.DurationSeconds : 3);
        TimelineItemsListBox.Items.Refresh();
        _currentProject.Items = _timelineItems.ToList();
        SaveProjectToFile(_currentProject);
        RefreshTimelinePreview();
    }

    private void RemoveTimelineItem_Click(object sender, RoutedEventArgs e)
    {
        if (TimelineItemsListBox.SelectedItem is not ProjectMediaItem selected)
        {
            return;
        }

        _timelineItems.Remove(selected);
        if (_currentProject != null)
        {
            _currentProject.Items = _timelineItems.ToList();
            SaveProjectToFile(_currentProject);
            RefreshTimelinePreview();
        }
    }

    private void SelectAudioFromBase_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectFromBaseTracks("Аудио файлы|*.mp3;*.wav;*.ogg;*.flac;*.aac;*.m4a");
        if (string.IsNullOrWhiteSpace(selected) || _currentProject == null)
        {
            return;
        }

        _currentProject.AudioPath = selected;
        SelectedAudioText.Text = Path.GetFileName(selected);
        UseVideoAudioCheckBox.IsChecked = false;
        SaveProjectToFile(_currentProject);
        RefreshTimelinePreview();
    }

    private void SelectAudioFromComputer_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Аудио файлы|*.mp3;*.wav;*.ogg;*.flac;*.aac;*.m4a",
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() != true || _currentProject == null)
        {
            return;
        }

        _currentProject.AudioPath = openFileDialog.FileName;
        SelectedAudioText.Text = Path.GetFileName(openFileDialog.FileName);
        UseVideoAudioCheckBox.IsChecked = false;
        SaveProjectToFile(_currentProject);
        RefreshTimelinePreview();
    }

    private void ClearAudio_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null)
        {
            return;
        }

        _currentProject.AudioPath = null;
        SelectedAudioText.Text = "Аудио не выбрано";
        SaveProjectToFile(_currentProject);
        RefreshTimelinePreview();
    }

    private void RefreshTimelinePreview()
    {
        VideoTimelinePanel.Children.Clear();
        AudioTimelinePanel.Children.Clear();

        var totalDuration = _timelineItems.Sum(item => Math.Max(0.5, item.DurationSeconds));
        TimelineScaleText.Text = $"0 сек  |  Общая длительность: {totalDuration:0.##} сек";

        foreach (var item in _timelineItems)
        {
            var width = Math.Max(60, item.DurationSeconds * 24);
            var color = item.Kind == ProjectMediaKind.Image ? "#FF5B3A9E" : "#FF1565C0";
            var block = new Border
            {
                Width = width,
                Height = 28,
                Margin = new Thickness(3, 0, 3, 0),
                CornerRadius = new CornerRadius(3),
                Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(color)!,
                Child = new TextBlock
                {
                    Text = $"{Path.GetFileName(item.Path)} ({item.DurationSeconds:0.#}с)",
                    Foreground = System.Windows.Media.Brushes.White,
                    Margin = new Thickness(6, 4, 6, 4),
                    TextTrimming = TextTrimming.CharacterEllipsis
                },
                ToolTip = item.DisplayName
            };

            VideoTimelinePanel.Children.Add(block);
        }

        var audioLabel = string.IsNullOrWhiteSpace(_currentProject?.AudioPath)
            ? (_currentProject?.UseVideoAudio == true ? "Аудио берется из видео дорожки" : "Без аудио")
            : Path.GetFileName(_currentProject.AudioPath);

        var audioBlock = new Border
        {
            Width = Math.Max(120, totalDuration * 24),
            Height = 28,
            Margin = new Thickness(3, 0, 3, 0),
            CornerRadius = new CornerRadius(3),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#FF2E7D32")!,
            Child = new TextBlock
            {
                Text = audioLabel,
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(6, 4, 6, 4),
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        };

        AudioTimelinePanel.Children.Add(audioBlock);
    }

    private void UseVideoAudioCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null)
        {
            return;
        }

        _currentProject.UseVideoAudio = UseVideoAudioCheckBox.IsChecked == true;
        SaveProjectToFile(_currentProject);
        RefreshTimelinePreview();
    }

    private string? SelectFromBaseTracks(string filter)
    {
        if (string.IsNullOrEmpty(RootPath))
        {
            return null;
        }

        var baseTracks = GetBaseTracks();
        if (baseTracks.Count == 0)
        {
            MessageBox.Show("В базе треков нет файлов.");
            return null;
        }

        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Multiselect = false,
            InitialDirectory = RootPath
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private List<string> GetBaseTracks()
    {
        var results = new List<string>();
        if (!string.IsNullOrEmpty(QueuePath) && Directory.Exists(QueuePath))
        {
            results.AddRange(Directory.GetFiles(QueuePath));
        }

        if (!string.IsNullOrEmpty(ProcessedPath) && Directory.Exists(ProcessedPath))
        {
            results.AddRange(Directory.GetFiles(ProcessedPath));
        }

        return results;
    }

    private void SaveProjectVideo_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null)
        {
            return;
        }

        if (!TryRenameCurrentProject())
        {
            return;
        }

        if (!ValidateProjectForRender(_currentProject))
        {
            return;
        }

        _currentProject.OutputFormat = GetSelectedOutputFormat();
        _currentProject.Width = ParseIntOrDefault(ProjectWidthTextBox.Text, 1920);
        _currentProject.Height = ParseIntOrDefault(ProjectHeightTextBox.Text, 1080);
        _currentProject.Fps = ParseIntOrDefault(ProjectFpsTextBox.Text, 30);
        _currentProject.TransitionSeconds = ParseDoubleOrDefault(ProjectTransitionTextBox.Text, 1);
        _currentProject.SlideDurationSeconds = ParseDoubleOrDefault(SlideDurationTextBox.Text, 3);
        _currentProject.MaxClipDurationSeconds = ParseDoubleOrDefault(MaxClipDurationTextBox.Text, 0);
        _currentProject.UseVideoAudio = UseVideoAudioCheckBox.IsChecked == true;
        _currentProject.Items = _timelineItems.ToList();

        SaveProjectToFile(_currentProject);
        _ = RenderProjectAsync(_currentProject);
    }

    private void BackToProjectList_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject != null)
        {
            if (!TryRenameCurrentProject())
            {
                return;
            }
            SaveProjectToFile(_currentProject);
        }

        ShowProjectList(_currentProjectType);
    }

    private void BackToStart_Click(object sender, RoutedEventArgs e)
    {
        HideAllScreens();
        StartScreen.Visibility = Visibility.Visible;
    }

    private bool ValidateProjectForRender(ProjectData project)
    {
        if (project.Items.Count == 0)
        {
            MessageBox.Show("Добавьте элементы в таймлайн.");
            return false;
        }

        if (project.Items.Any(item => !File.Exists(item.Path)))
        {
            MessageBox.Show("Некоторые файлы в таймлайне не найдены.");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(project.AudioPath) && !File.Exists(project.AudioPath))
        {
            MessageBox.Show("Выбранный аудиофайл не найден.");
            return false;
        }

        var outputPath = Path.Combine(ProcessedPath, $"{project.Name}.{GetSelectedOutputFormat()}");
        if (File.Exists(outputPath))
        {
            MessageBox.Show("Файл с таким названием уже существует в обработанных.");
            return false;
        }

        return true;
    }

    private string GetSelectedOutputFormat()
    {
        if (ProjectOutputFormatComboBox.SelectedItem is ComboBoxItem item)
        {
            return item.Content.ToString() ?? "mp4";
        }

        return "mp4";
    }

    private void SaveProjectToFile(ProjectData project)
    {
        var projectPath = GetProjectFilePath(project.Type, project.Name);
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        var json = JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(projectPath, json);
    }

    private void RenameProject_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null)
        {
            return;
        }

        if (TryRenameCurrentProject())
        {
            SaveProjectToFile(_currentProject);
            MessageBox.Show("Название проекта обновлено.", "Успешно", MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private bool TryRenameCurrentProject()
    {
        if (_currentProject == null)
        {
            return false;
        }

        var newName = ProjectNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            MessageBox.Show("Введите название проекта.");
            return false;
        }

        if (!IsValidProjectName(newName))
        {
            MessageBox.Show("Название проекта содержит недопустимые символы.");
            return false;
        }

        if (string.Equals(_currentProject.Name, newName, StringComparison.Ordinal))
        {
            return true;
        }

        var oldProjectPath = GetProjectFilePath(_currentProject.Type, _currentProject.Name);
        var newProjectPath = GetProjectFilePath(_currentProject.Type, newName);
        if (File.Exists(newProjectPath))
        {
            MessageBox.Show("Проект с таким названием уже существует.");
            return false;
        }

        _currentProject.Name = newName;

        if (File.Exists(oldProjectPath))
        {
            File.Move(oldProjectPath, newProjectPath);
        }

        return true;
    }

    private string GetProjectsPath(ProjectType type)
    {
        var folder = type == ProjectType.VideoCollage ? "VideoCollage" : "SlideShow";
        return Path.Combine(ProjectsRootPath, folder);
    }

    private string GetProjectFilePath(ProjectType type, string name)
    {
        return Path.Combine(GetProjectsPath(type), $"{name}.json");
    }

    private static bool IsValidProjectName(string name)
    {
        return name.All(ch => !Path.GetInvalidFileNameChars().Contains(ch));
    }

    private static int ParseIntOrDefault(string? value, int defaultValue)
    {
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static double ParseDoubleOrDefault(string? value, double defaultValue)
    {
        return double.TryParse(value, out var parsed) ? parsed : defaultValue;
    }
}
