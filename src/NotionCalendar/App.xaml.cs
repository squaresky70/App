using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using NotionCalendar.Services;

namespace NotionCalendar;

public partial class App : Application
{
    /// <summary>한 번만 실행되게 하는 이름. 두 번째 실행은 첫 번째 창을 앞으로 불러오고 끝난다.</summary>
    private const string InstanceKey = "NotionCalendar.Main";

    private static DispatcherQueue? _dispatcher;

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

    /// <summary>본 창과 바탕화면 위젯이 함께 쓰는 일정 저장소.</summary>
    public static ScheduleStore Store { get; } = new();

    public static AppSettings Settings { get; private set; } = new();

    public static MainWindow? MainWindowInstance { get; private set; }

    public static WidgetWindow? Widget { get; private set; }

    /// <summary>위젯이 열리거나 닫힐 때 (본 창의 "위젯 추가/닫기" 버튼 글자를 바꾸는 데 쓴다).</summary>
    public static event EventHandler? WidgetStateChanged;

    public static bool IsWidgetOpen => Widget is not null;

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // 위젯이 떠 있는 동안 바탕화면 아이콘을 다시 눌러도 앱이 하나 더 뜨지 않게 한다.
        var mainInstance = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (!mainInstance.IsCurrent)
        {
            await mainInstance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            Exit();
            return;
        }

        _dispatcher = DispatcherQueue.GetForCurrentThread();
        mainInstance.Activated += (_, _) => _dispatcher.TryEnqueue(() => ShowMainWindow());

        Store.Load();
        Settings = AppSettings.Load();

        // 위젯을 켜 둔 채로 끝났으면 다시 띄운다. 본 창이 그 앞에 오도록 위젯을 먼저 연다.
        if (Settings.WidgetOpen)
        {
            OpenWidget();
        }

        ShowMainWindow();
    }

    /// <summary>본 창을 앞으로 불러온다. 닫혀 있으면 새로 연다. date 를 주면 그 날짜로 이동한다.</summary>
    public static void ShowMainWindow(DateOnly? date = null)
    {
        if (MainWindowInstance is null)
        {
            var window = new MainWindow();
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(MainWindowInstance, window))
                {
                    MainWindowInstance = null;
                }
            };

            MainWindowInstance = window;
        }

        if (MainWindowInstance.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
        {
            presenter.Restore();
        }

        MainWindowInstance.Activate();

        if (date is { } target)
        {
            MainWindowInstance.ShowDate(target);
        }
    }

    public static void ToggleWidget()
    {
        if (Widget is null)
        {
            OpenWidget();
        }
        else
        {
            Widget.Close();
        }
    }

    private static void OpenWidget()
    {
        var widget = new WidgetWindow();
        widget.Closed += (_, _) =>
        {
            if (!ReferenceEquals(Widget, widget))
            {
                return;
            }

            Widget = null;
            Settings.WidgetOpen = false;
            Settings.Save();
            WidgetStateChanged?.Invoke(null, EventArgs.Empty);
        };

        Widget = widget;
        widget.Activate();

        Settings.WidgetOpen = true;
        Settings.Save();
        WidgetStateChanged?.Invoke(null, EventArgs.Empty);
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
}
