using Microsoft.Win32;

namespace NotionCalendar.Services;

/// <summary>
/// 컴퓨터를 다시 켜도 위젯이 저절로 뜨게 하는 등록.
///
/// 현재 사용자 계정의 시작프로그램 목록(HKCU\...\Run)에 "<exe> --widget" 을 넣는다.
/// 관리자 권한이 필요 없고, 위젯을 닫으면 그 등록도 함께 지운다.
/// </summary>
public static class StartupRegistration
{
    /// <summary>이 인자로 실행하면 본창 없이 위젯만 띄운다.</summary>
    public const string WidgetArgument = "--widget";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "NotionCalendarWidget";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(ValueName) is not null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartupRegistration] 확인 실패: {ex}");
                return false;
            }
        }
    }

    public static void Enable()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                return;
            }

            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.SetValue(ValueName, $"\"{exePath}\" {WidgetArgument}");
        }
        catch (Exception ex)
        {
            // 등록에 실패해도 위젯 자체는 계속 쓸 수 있어야 한다.
            System.Diagnostics.Debug.WriteLine($"[StartupRegistration] 등록 실패: {ex}");
        }
    }

    public static void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StartupRegistration] 해제 실패: {ex}");
        }
    }
}
