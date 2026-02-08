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

    private void ShowSlideShowProjects_Click(object sender, RoutedEventArgs e)
    {
        ShowProjectList(ProjectType.SlideShow);
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
        ProjectListTitle.Text = type == ProjectType.VideoCollage ? "Проекты видеоколлажей" : "Проекты слайд-шоу";
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
        ProjectEditorTitle.Text = project.Type == ProjectType.VideoCollage ? "Форма создания видеоколлажа" :
            "Форма создания слайд-шоу";
        ProjectNameText.Text = $"Проект: {project.Name}";
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

        var isVideoCollage = project.Type == ProjectType.VideoCollage;
        VideoCollageButtonsPanel.Visibility = isVideoCollage ? Visibility.Visible : Visibility.Collapsed;
        SlideShowButtonsPanel.Visibility = isVideoCollage ? Visibility.Collapsed : Visibility.Visible;
        UseVideoAudioCheckBox.Visibility = isVideoCollage ? Visibility.Visible : Visibility.Collapsed;
        SlideDurationLabel.Visibility = isVideoCollage ? Visibility.Collapsed : Visibility.Visible;
        SlideDurationTextBox.Visibility = isVideoCollage ? Visibility.Collapsed : Visibility.Visible;

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

        AddTimelineItem(selected);
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

        AddTimelineItem(openFileDialog.FileName);
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

        AddTimelineItem(openFileDialog.FileName);
    }

    private void AddTimelineItem(string path)
    {
        if (_currentProject == null)
        {
            return;
        }

        var duration = _currentProject.Type == ProjectType.SlideShow
            ? ParseDoubleOrDefault(SlideDurationTextBox.Text, 3)
            : GetTrimmedDuration(path, ParseDoubleOrDefault(MaxClipDurationTextBox.Text, 0));

        var item = new ProjectMediaItem
        {
            Path = path,
            DurationSeconds = duration
        };

        _timelineItems.Add(item);
        _currentProject.Items = _timelineItems.ToList();
        SaveProjectToFile(_currentProject);
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
