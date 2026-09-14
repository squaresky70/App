using System.IO.Compression;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace NotionCalendar.Launcher;

/// <summary>
/// 캘린더 앱 폴더를 통째로 품고 다니는 단일 exe.
/// 처음 실행할 때만 %LOCALAPPDATA%\NotionCalendar\app 으로 풀고, 그다음부터는 바로 실행한다.
///
/// WinUI 3 앱 자체는 단일 파일로 만들 수 없다(WinRT 클래스 활성화가 exe 옆의 실제 DLL 파일을
/// SxS 매니페스트로 찾기 때문). 그래서 일반 .NET 인 이 런처가 대신 단일 파일이 된다.
/// </summary>
internal static class Program
{
    private const string ResourceName = "app.zip";
    private const string ExeName = "NotionCalendar.exe";

    [STAThread]
    private static int Main()
    {
        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NotionCalendar");

            var appDir = Path.Combine(root, "app");
            var stampFile = Path.Combine(appDir, ".version");
            var exePath = Path.Combine(appDir, ExeName);

            using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException($"내장 리소스 '{ResourceName}' 를 찾을 수 없습니다.");

            // 페이로드가 바뀌면 stamp 도 바뀌므로 새 버전일 때만 다시 푼다.
            var stamp = payload.Length.ToString();

            if (!File.Exists(exePath) || ReadStamp(stampFile) != stamp)
            {
                Extract(payload, root, appDir);
                File.WriteAllText(stampFile, stamp);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = appDir,
                UseShellExecute = false,
            });

            return 0;
        }
        catch (Exception ex)
        {
            MessageBox(IntPtr.Zero, ex.Message, "캘린더를 실행하지 못했습니다", 0x10);
            return 1;
        }
    }

    private static string? ReadStamp(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>임시 폴더에 먼저 풀고 통째로 교체한다. 중간에 끊겨도 반쯤 풀린 상태가 남지 않는다.</summary>
    private static void Extract(Stream payload, string root, string appDir)
    {
        Directory.CreateDirectory(root);

        var stagingDir = Path.Combine(root, "app.staging");
        DeleteDirectory(stagingDir);
        Directory.CreateDirectory(stagingDir);

        using (var archive = new ZipArchive(payload, ZipArchiveMode.Read, leaveOpen: true))
        {
            archive.ExtractToDirectory(stagingDir, overwriteFiles: true);
        }

        DeleteDirectory(appDir);
        Directory.Move(stagingDir, appDir);
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // 앱이 실행 중이어서 지우지 못하는 경우: 옆으로 치워 둔다.
            Directory.Move(path, path + "." + Guid.NewGuid().ToString("N")[..8] + ".old");
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
