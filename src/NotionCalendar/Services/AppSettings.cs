using System.Text.Json;

namespace NotionCalendar.Services;

/// <summary>
/// 일정과 별개인 앱 설정. %LOCALAPPDATA%\NotionCalendar\settings.json 에 보관한다.
/// 지금은 바탕화면 위젯을 켜 두었는지와 그 위치/크기만 기억한다.
/// </summary>
public sealed class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NotionCalendar",
        "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>위젯을 켜 둔 채로 앱이 끝났으면 다음 실행 때 다시 띄운다.</summary>
    public bool WidgetOpen { get; set; }

    // 위젯 위치/크기 (화면 픽셀). 처음이면 null.
    public int? WidgetX { get; set; }

    public int? WidgetY { get; set; }

    public int? WidgetWidth { get; set; }

    public int? WidgetHeight { get; set; }

    /// <summary>위젯 유리 배경의 투명도(%). 0 이 가장 진하고 100 이 가장 투명하다.</summary>
    public int WidgetTransparency { get; set; } = 50;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new();
            }
        }
        catch (Exception ex)
        {
            // 설정이 깨졌으면 기본값으로 시작한다. 일정 데이터와는 무관하다.
            System.Diagnostics.Debug.WriteLine($"[AppSettings] 불러오기 실패: {ex}");
        }

        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, JsonOptions));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppSettings] 저장 실패: {ex}");
        }
    }
}
