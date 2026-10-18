using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DynamicIsland.Services;

public class FileShelfItem
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string FileSizeFormatted { get; set; } = "";
    public bool IsDirectory { get; set; }
    public ImageSource? Icon { get; set; }
}

public class FileShelfService
{
    private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shelf.json");
    public ObservableCollection<FileShelfItem> Items { get; } = new();

    public FileShelfService()
    {
        LoadItems();
    }

    public void AddFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        // Check if already in items
        foreach (var it in Items)
        {
            if (string.Equals(it.FilePath, path, StringComparison.OrdinalIgnoreCase))
                return;
        }

        bool isDir = Directory.Exists(path);
        bool isFile = File.Exists(path);
        if (!isDir && !isFile) return;

        var item = new FileShelfItem
        {
            FilePath = path,
            FileName = Path.GetFileName(path) ?? path,
            IsDirectory = isDir
        };

        if (string.IsNullOrEmpty(item.FileName))
        {
            item.FileName = path;
        }

        if (isDir)
        {
            item.FileSizeFormatted = "Klasör";
        }
        else
        {
            try
            {
                var fi = new FileInfo(path);
                item.FileSizeFormatted = FormatBytes(fi.Length);
            }
            catch
            {
                item.FileSizeFormatted = "";
            }
        }

        // Extract Icon
        try
        {
            if (isFile)
            {
                using var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (sysIcon != null)
                {
                    using var bmp = sysIcon.ToBitmap();
                    using var ms = new MemoryStream();
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;

                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = ms;
                    bi.EndInit();
                    bi.Freeze();
                    item.Icon = bi;
                }
            }
        }
        catch { }

        Items.Insert(0, item);
        SaveItems();
    }

    public void RemoveFile(FileShelfItem item)
    {
        Items.Remove(item);
        SaveItems();
    }

    public void ClearAll()
    {
        Items.Clear();
        SaveItems();
    }

    private void SaveItems()
    {
        try
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var item in Items)
            {
                list.Add(item.FilePath);
            }
            string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }

    private void LoadItems()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return;
            string json = File.ReadAllText(ConfigPath);
            var list = JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(json);
            if (list == null) return;

            foreach (var path in list)
            {
                if (File.Exists(path) || Directory.Exists(path))
                {
                    AddFile(path);
                }
            }
        }
        catch { }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}
