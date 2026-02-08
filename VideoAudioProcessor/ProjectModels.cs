using System;
using System.Collections.Generic;

namespace VideoAudioProcessor;

public enum ProjectType
{
    VideoCollage,
    SlideShow
}

public sealed class ProjectMediaItem
{
    public string Path { get; set; } = string.Empty;
    public double DurationSeconds { get; set; }

    public string DisplayName
    {
        get
        {
            var fileName = System.IO.Path.GetFileName(Path);
            if (DurationSeconds > 0)
            {
                return $"{fileName} ({DurationSeconds:0.##} сек.)";
            }

            return fileName;
        }
    }
}

public sealed class ProjectData
{
    public string Name { get; set; } = string.Empty;
    public ProjectType Type { get; set; }
    public List<ProjectMediaItem> Items { get; set; } = new();
    public string? AudioPath { get; set; }
    public bool UseVideoAudio { get; set; } = true;
    public string OutputFormat { get; set; } = "mp4";
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public int Fps { get; set; } = 30;
    public double TransitionSeconds { get; set; } = 1;
    public double SlideDurationSeconds { get; set; } = 3;
    public double MaxClipDurationSeconds { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
