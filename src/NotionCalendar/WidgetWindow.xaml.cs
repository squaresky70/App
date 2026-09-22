using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using NotionCalendar.Controls;
using NotionCalendar.ViewModels;
using Windows.Graphics;

namespace NotionCalendar;

/// <summary>
/// 바탕화면 위젯. 제목 표시줄 없는 반투명 유리 창에 한 달 달력을 띄운다.
///
/// - 다른 창을 누르면 맨 뒤로 가라앉아 바탕화면 위에 머문다.
/// - 작업 표시줄·Alt+Tab 에는 나타나지 않는다.
/// - 머리 부분을 끌어 옮기고, 가장자리로 크기를 바꾼다. 위치/크기는 다음 실행 때도 유지된다.
/// - 본 앱과 같은 일정 저장소를 쓰므로 일정을 바꾸면 곧바로 반영된다.
/// </summary>
public sealed partial class WidgetWindow : Window
{
    private const int HwndBottom = 1;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint WmNcLButtonDown = 0x00A1;
    private const int HtCaption = 2;

    private static readonly string[] Weekdays = { "일", "월", "화", "수", "목", "금", "토" };

    private readonly IntPtr _hwnd;
    private readonly DayCell[] _cells = new DayCell[MainViewModel.CellCount];
    private readonly DispatcherQueueTimer _saveTimer;
    private readonly DispatcherQueueTimer _dayTimer;

    private DateTime _lastToday = DateTime.Today;
    private int _wheelAccumulator;

    public WidgetWindow()
    {
        InitializeComponent();

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        VM = new MainViewModel(App.Store);

        Title = "캘린더 위젯";
        SystemBackdrop = new GlassBackdrop();

        // 제목 표시줄은 없애고 크기 조절용 테두리만 남긴다.
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(true, false);
        }

        AppWindow.IsShownInSwitchers = false;
        PlaceWindow();

        BuildWeekdayRow();
        BuildDaysGrid();
        UpdateMonthLabel();
        VM.PropertyChanged += OnViewModelPropertyChanged;

        // 끌거나 크기를 바꿀 때마다 저장하면 너무 잦으므로 잠깐 멈췄을 때 한 번 저장한다.
        _saveTimer = DispatcherQueue.CreateTimer();
        _saveTimer.Interval = TimeSpan.FromMilliseconds(600);
        _saveTimer.IsRepeating = false;
        _saveTimer.Tick += (_, _) => SaveBounds();
        AppWindow.Changed += OnAppWindowChanged;

        // 위젯은 며칠씩 켜져 있을 수 있어, 날짜가 바뀌면 "오늘" 표시를 옮긴다.
        _dayTimer = DispatcherQueue.CreateTimer();
        _dayTimer.Interval = TimeSpan.FromMinutes(1);
        _dayTimer.Tick += (_, _) => RefreshIfDayChanged();
        _dayTimer.Start();

        Activated += OnActivated;
        Closed += OnClosed;
    }

    public MainViewModel VM { get; }

    /// <summary>맨 뒤로 보내 바탕화면 위에만 보이게 한다(포커스는 빼앗지 않는다).</summary>
    public void SendToBottom()
        => SetWindowPos(_hwnd, (IntPtr)HwndBottom, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);

    // ================= 창 배치 =================

    private void PlaceWindow()
    {
        var settings = App.Settings;
        if (settings is { WidgetX: int x, WidgetY: int y, WidgetWidth: int w, WidgetHeight: int h }
            && w > 100 && h > 100
            && DisplayArea.GetFromRect(new RectInt32(x, y, w, h), DisplayAreaFallback.None) is not null)
        {
            AppWindow.MoveAndResize(new RectInt32(x, y, w, h));
            return;
        }

        // 처음이거나 저장된 위치가 화면 밖이면 주 모니터 오른쪽 위에 놓는다.
        var scale = GetDpiForWindow(_hwnd) / 96d;
        var work = DisplayArea.Primary.WorkArea;
        var width = (int)(780 * scale);
        var height = (int)(580 * scale);
        var gap = (int)(24 * scale);
        AppWindow.MoveAndResize(new RectInt32(work.X + work.Width - width - gap, work.Y + gap, width, height));
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPositionChange || args.DidSizeChange)
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }
    }

    private void SaveBounds()
    {
        var settings = App.Settings;
        settings.WidgetX = AppWindow.Position.X;
        settings.WidgetY = AppWindow.Position.Y;
        settings.WidgetWidth = AppWindow.Size.Width;
        settings.WidgetHeight = AppWindow.Size.Height;
        settings.Save();
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        // 다른 창으로 넘어가면 바탕화면 쪽으로 가라앉는다.
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            SendToBottom();
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _saveTimer.Stop();
        _dayTimer.Stop();
        SaveBounds();
        VM.PropertyChanged -= OnViewModelPropertyChanged;
        VM.Detach();
    }

    /// <summary>머리 부분을 누르면 Windows 기본 "창 옮기기" 를 시작한다.</summary>
    private void OnDragBarPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(DragBar).Properties.IsLeftButtonPressed)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(_hwnd, WmNcLButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
    }

    // ================= 달력 =================

    private void BuildWeekdayRow()
    {
        for (var c = 0; c < 7; c++)
        {
            WeekdayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text = Weekdays[c],
                Margin = new Thickness(8, 0, 0, 0),
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Res(c switch
                {
                    0 => "GlassSundayBrush",
                    6 => "GlassSaturdayBrush",
                    _ => "GlassTextTertiaryBrush",
                }),
            };

            Grid.SetColumn(label, c);
            WeekdayRow.Children.Add(label);
        }
    }

    private void BuildDaysGrid()
    {
        for (var c = 0; c < 7; c++)
        {
            DaysGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (var r = 0; r < 6; r++)
        {
            DaysGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }

        var lineBrush = Res("GlassLineBrush");

        for (var r = 0; r < 6; r++)
        {
            if (r > 0)
            {
                var line = new Border
                {
                    BorderBrush = lineBrush,
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    VerticalAlignment = VerticalAlignment.Top,
                    IsHitTestVisible = false,
                };

                Grid.SetRow(line, r);
                Grid.SetColumnSpan(line, 7);
                DaysGrid.Children.Add(line);
            }

            for (var c = 0; c < 7; c++)
            {
                var index = (r * 7) + c;
                var cell = new DayCell { GlassStyle = true };
                cell.Tapped += OnDayTapped;
                cell.DoubleTapped += OnDayDoubleTapped;

                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                DaysGrid.Children.Add(cell);

                _cells[index] = cell;
                cell.Day = VM.Days[index];
            }
        }
    }

    private void OnDayTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is DayCell { Day: { } day })
        {
            VM.SelectedDate = day.Date;
        }
    }

    /// <summary>날짜를 두 번 누르면 캘린더 앱을 그 날짜로 연다.</summary>
    private void OnDayDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is DayCell { Day: { } day })
        {
            App.ShowMainWindow(day.Date);
        }
    }

    private void OnPrevClick(object sender, RoutedEventArgs e) => VM.MoveMonth(-1);

    private void OnNextClick(object sender, RoutedEventArgs e) => VM.MoveMonth(1);

    private void OnTodayClick(object sender, RoutedEventArgs e) => VM.GoToToday();

    private void OnOpenAppClick(object sender, RoutedEventArgs e) => App.ShowMainWindow();

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(DaysGrid).Properties.MouseWheelDelta;
        e.Handled = true;

        // 정밀 터치패드는 작은 값이 여러 번 오므로 모아서 판단한다.
        if (Math.Sign(delta) != Math.Sign(_wheelAccumulator))
        {
            _wheelAccumulator = 0;
        }

        _wheelAccumulator += delta;
        if (Math.Abs(_wheelAccumulator) < 60)
        {
            return;
        }

        VM.MoveMonth(_wheelAccumulator > 0 ? -1 : 1);
        _wheelAccumulator = 0;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.MonthTitle) or nameof(MainViewModel.YearTitle))
        {
            UpdateMonthLabel();
        }
    }

    private void UpdateMonthLabel() => MonthLabel.Text = $"{VM.YearTitle}년 {VM.MonthTitle}";

    private void RefreshIfDayChanged()
    {
        if (DateTime.Today == _lastToday)
        {
            return;
        }

        _lastToday = DateTime.Today;
        VM.Refresh();
    }

    private static Brush Res(string key) => (Brush)Application.Current.Resources[key];

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);
}
