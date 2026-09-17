using Microsoft.UI.Xaml;

namespace NotionCalendar;

public partial class App : Application
{
    public static Window? MainWindowInstance { get; private set; }

    public App()
    {
        InitializeComponent();

        // UI 스레드에서 새는 예외 하나로 앱이 통째로 사라지지 않게 받아서 기록만 남긴다.
        UnhandledException += (_, e) =>
        {
            e.Handled = true;
            LogCrash(e.Exception);
        };
    }

    private static void LogCrash(Exception exception)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NotionCalendar");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "crash.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{exception}\n\n");
        }
        catch
        {
            // 기록 실패가 또 다른 크래시가 되지 않도록 무시한다.
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindowInstance = new MainWindow();
        MainWindowInstance.Activate();
    }
}
