using System.Text.Json;
using WhisperDesk.Models;

namespace WhisperDesk.Services;

public sealed class SettingsService
{
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WhisperDesk");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch
        {
            // 壊れた設定は既定値へ戻す。文字起こし処理は止めない。
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }
}
