using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DynamicIsland.Services;

public class QuickAppItem
{
    public string Name { get; set; } = "";
    public string IconEmoji { get; set; } = "🚀";
    public string Target { get; set; } = "";
    public string Arguments { get; set; } = "";
    public ImageSource? CustomIcon { get; set; }
    public bool IsCustom { get; set; } = false;
}

public class QuickAppsService
{
    private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "quickapps.json");
    public ObservableCollection<QuickAppItem> Items { get; } = new();

    public QuickAppsService()
    {
        InitializeDefaults();
        LoadCustomApps();
    }

    private void InitializeDefaults()
    {
        Items.Add(new QuickAppItem { Name = "Tarayıcı", IconEmoji = "🌐", Target = "https://www.google.com" });
        Items.Add(new QuickAppItem { Name = "Spotify", IconEmoji = "🎵", Target = "spotify:" });
        Items.Add(new QuickAppItem { Name = "Discord", IconEmoji = "💬", Target = "discord:" });
        Items.Add(new QuickAppItem { Name = "Steam", IconEmoji = "🎮", Target = "steam:" });
        Items.Add(new QuickAppItem { Name = "Not Defteri", IconEmoji = "📝", Target = "notepad.exe" });
        Items.Add(new QuickAppItem { Name = "Hesap Makinesi", IconEmoji = "🧮", Target = "calc.exe" });
        Items.Add(new QuickAppItem { Name = "İndirilenler", IconEmoji = "📁", Target = "explorer.exe", Arguments = "shell:Downloads" });
        Items.Add(new QuickAppItem { Name = "Görev Yöneticisi", IconEmoji = "⚙️", Target = "taskmgr.exe" });
    }

    public void Launch(QuickAppItem item)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = item.Target,
                Arguments = item.Arguments,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error launching {item.Name}: {ex.Message}");
        }
    }

    public void AddCustomApp(string filePath)
    {
        if (!File.Exists(filePath)) return;

        string name = Path.GetFileNameWithoutExtension(filePath);
        var item = new QuickAppItem
        {
            Name = name,
            IconEmoji = "✨",
            Target = filePath,
            IsCustom = true
        };

        try
        {
            using var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(filePath);
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
                item.CustomIcon = bi;
            }
        }
        catch { }

        Items.Add(item);
        SaveCustomApps();
    }

    public void RemoveCustomApp(QuickAppItem item)
    {
        if (item.IsCustom)
        {
            Items.Remove(item);
            SaveCustomApps();
        }
    }

    private void SaveCustomApps()
    {
        try
        {
            var customPaths = new System.Collections.Generic.List<string>();
            foreach (var item in Items)
            {
                if (item.IsCustom)
                {
                    customPaths.Add(item.Target);
                }
            }
            string json = JsonSerializer.Serialize(customPaths, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }

    private void LoadCustomApps()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return;
            string json = File.ReadAllText(ConfigPath);
            var list = JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(json);
            if (list == null) return;

            foreach (var path in list)
            {
                if (File.Exists(path))
                {
                    AddCustomApp(path);
                }
            }
        }
        catch { }
    }
}
