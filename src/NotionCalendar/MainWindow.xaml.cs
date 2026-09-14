using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using NotionCalendar.Controls;
using NotionCalendar.Dialogs;
using NotionCalendar.Models;
using NotionCalendar.Services;
using NotionCalendar.ViewModels;
using Windows.System;

namespace NotionCalendar;

public sealed partial class MainWindow : Window
{
    /// <summary>스와이프로 달을 넘기기 위한 최소 이동 거리(px).</summary>
    private const double SwipeDistanceThreshold = 80;

    /// <summary>빠르게 튕겼을 때 거리와 무관하게 넘기는 속도 기준(px/ms).</summary>
    private const double SwipeVelocityThreshold = 0.7;

    /// <summary>드래그 중 따라 움직이는 정도(1이면 손가락과 1:1).</summary>
    private const double DragFollowRatio = 0.5;

    /// <summary>전환 애니메이션에서 격자가 밀려나는 거리.</summary>
    private const double SlideDistance = 52;

    private readonly DayCell[] _cells = new DayCell[MainViewModel.CellCount];
    private readonly CompositeTransform _slide = new();
    private readonly ScheduleStore _store = new();

    private Storyboard? _activeStoryboard;
    private bool _isAnimating;
    private int _wheelAccumulator;

    public MainWindow()
    {
        _store.Load();
        VM = new MainViewModel(_store);

        InitializeComponent();

        Title = "캘린더";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1180, 820));

        SwipeHost.RenderTransform = _slide;
        ScheduleList.ItemsSource = VM.SelectedDaySchedules;

        BuildCalendarGrid();
        BindCells();
        RegisterAccelerators();
    }

    public MainViewModel VM { get; }

    // ================= 달력 격자 구성 =================

    /// <summary>7×6 격자와 주 구분선을 한 번만 만들어 둔다.</summary>
    private void BuildCalendarGrid()
    {
        for (var c = 0; c < 7; c++)
        {
            DaysGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (var r = 0; r < 6; r++)
        {
            DaysGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }

        var separatorBrush = (Brush)Application.Current.Resources["BorderBrushSoft"];

        for (var r = 0; r < 6; r++)
        {
            // 첫 줄을 제외한 각 주 위에 아주 옅은 구분선
            if (r > 0)
            {
                var line = new Border
                {
                    BorderBrush = separatorBrush,
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    VerticalAlignment = VerticalAlignment.Top,
                    IsHitTestVisible = false,
                    Margin = new Thickness(4, 0, 4, 0),
                };

                Grid.SetRow(line, r);
                Grid.SetColumn(line, 0);
                Grid.SetColumnSpan(line, 7);
                DaysGrid.Children.Add(line);
            }

            for (var c = 0; c < 7; c++)
            {
                var cell = new DayCell();
                cell.Tapped += OnDayCellTapped;
                cell.DoubleTapped += OnDayCellDoubleTapped;

                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                DaysGrid.Children.Add(cell);

                _cells[(r * 7) + c] = cell;
            }
        }
    }

    /// <summary>뷰모델의 42개 CalendarDay 를 셀에 연결한다(한 번만 하면 이후엔 내용만 갱신됨).</summary>
    private void BindCells()
    {
        for (var i = 0; i < _cells.Length; i++)
        {
            _cells[i].Day = VM.Days[i];
        }
    }

    // ================= 달 이동 =================

    private void OnPrevMonthClick(object sender, RoutedEventArgs e) => _ = GoToMonthAsync(-1);

    private void OnNextMonthClick(object sender, RoutedEventArgs e) => _ = GoToMonthAsync(1);

    private void OnTodayClick(object sender, RoutedEventArgs e) => GoToToday();

    private void GoToToday()
    {
        var direction = Math.Sign(
            ((DateTime.Today.Year - VM.CurrentMonth.Year) * 12) + (DateTime.Today.Month - VM.CurrentMonth.Month));

        if (direction == 0)
        {
            VM.GoToToday();
            return;
        }

        _ = RunMonthTransitionAsync(direction, VM.GoToToday);
    }

    /// <param name="delta">-1 이전 달, +1 다음 달.</param>
    private Task GoToMonthAsync(int delta)
        => RunMonthTransitionAsync(delta, () => VM.MoveMonth(delta));

    /// <summary>격자를 살짝 밀어냈다가 새 달을 채우고 반대쪽에서 밀어 넣는다.</summary>
    private async Task RunMonthTransitionAsync(int direction, Action applyChange)
    {
        if (_isAnimating || direction == 0)
        {
            return;
        }

        _isAnimating = true;
        try
        {
            var outX = -Math.Sign(direction) * SlideDistance;

            await AnimateAsync(_slide.TranslateX, outX, SwipeHost.Opacity, 0d, 120);

            applyChange();

            await AnimateAsync(-outX, 0d, 0d, 1d, 190);
        }
        finally
        {
            _slide.TranslateX = 0;
            SwipeHost.Opacity = 1;
            _isAnimating = false;
        }
    }

    /// <summary>
    /// 진행 중인 전환 애니메이션을 멈추고 값을 다시 직접 제어할 수 있게 돌려놓는다.
    /// (Storyboard 가 HoldEnd 로 값을 붙잡고 있으면 드래그가 먹히지 않는다.)
    /// </summary>
    private void StopActiveAnimation()
    {
        if (_activeStoryboard is null)
        {
            return;
        }

        _activeStoryboard.Stop();
        _activeStoryboard = null;
    }

    private Task AnimateAsync(double fromX, double toX, double fromOpacity, double toOpacity, int milliseconds)
    {
        StopActiveAnimation();

        var tcs = new TaskCompletionSource<bool>();
        var duration = new Duration(TimeSpan.FromMilliseconds(milliseconds));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var slideAnimation = new DoubleAnimation
        {
            From = fromX,
            To = toX,
            Duration = duration,
            EasingFunction = ease,
        };
        Storyboard.SetTarget(slideAnimation, _slide);
        Storyboard.SetTargetProperty(slideAnimation, "TranslateX");

        var fadeAnimation = new DoubleAnimation
        {
            From = fromOpacity,
            To = toOpacity,
            Duration = duration,
            EasingFunction = ease,
        };
        Storyboard.SetTarget(fadeAnimation, SwipeHost);
        Storyboard.SetTargetProperty(fadeAnimation, "Opacity");

        var storyboard = new Storyboard();
        storyboard.Children.Add(slideAnimation);
        storyboard.Children.Add(fadeAnimation);
        storyboard.Completed += (_, _) =>
        {
            // 애니메이션을 멈추고 최종 값을 로컬 값으로 남긴다.
            storyboard.Stop();
            if (ReferenceEquals(_activeStoryboard, storyboard))
            {
                _activeStoryboard = null;
            }

            _slide.TranslateX = toX;
            SwipeHost.Opacity = toOpacity;
            tcs.TrySetResult(true);
        };

        _activeStoryboard = storyboard;
        storyboard.Begin();

        return tcs.Task;
    }

    // ================= 스와이프 / 휠 =================

    private void OnManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
    {
        if (_isAnimating)
        {
            e.Complete();
            return;
        }

        // 되돌아가는 중이었다면 멈추고 손가락을 따라가게 한다.
        StopActiveAnimation();
    }

    private void OnManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (_isAnimating)
        {
            return;
        }

        var x = e.Cumulative.Translation.X * DragFollowRatio;
        _slide.TranslateX = x;

        // 끌수록 살짝 흐려지게 해서 "넘어가는 중"임을 보여준다.
        SwipeHost.Opacity = 1 - Math.Min(0.4, Math.Abs(x) / 400);
    }

    private void OnManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        if (_isAnimating)
        {
            return;
        }

        var distance = e.Cumulative.Translation.X;
        var velocity = e.Velocities.Linear.X;

        // 오른쪽으로 끌면 이전 달, 왼쪽으로 끌면 다음 달.
        if (distance > SwipeDistanceThreshold || velocity > SwipeVelocityThreshold)
        {
            _ = SwipeToMonthAsync(-1);
        }
        else if (distance < -SwipeDistanceThreshold || velocity < -SwipeVelocityThreshold)
        {
            _ = SwipeToMonthAsync(1);
        }
        else
        {
            // 기준에 못 미치면 제자리로 되돌린다.
            _ = AnimateAsync(_slide.TranslateX, 0, SwipeHost.Opacity, 1, 160);
        }
    }

    /// <summary>드래그로 이미 밀려난 위치에서 이어서 넘긴다.</summary>
    private async Task SwipeToMonthAsync(int delta)
    {
        if (_isAnimating)
        {
            return;
        }

        _isAnimating = true;
        try
        {
            var outX = -Math.Sign(delta) * SlideDistance;

            await AnimateAsync(_slide.TranslateX, outX, SwipeHost.Opacity, 0d, 110);
            VM.MoveMonth(delta);
            await AnimateAsync(-outX, 0d, 0d, 1d, 190);
        }
        finally
        {
            _slide.TranslateX = 0;
            SwipeHost.Opacity = 1;
            _isAnimating = false;
        }
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(SwipeHost).Properties.MouseWheelDelta;
        e.Handled = true;

        if (_isAnimating)
        {
            return;
        }

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

        // 휠을 위로 굴리면 이전 달.
        var direction = _wheelAccumulator > 0 ? -1 : 1;
        _wheelAccumulator = 0;
        _ = GoToMonthAsync(direction);
    }

    // ================= 날짜 선택 =================

    private void OnDayCellTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is DayCell { Day: { } day })
        {
            SelectDate(day.Date);
        }
    }

    private void OnDayCellDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is DayCell { Day: { } day })
        {
            SelectDate(day.Date);
            _ = ShowScheduleDialogAsync(null, day.Date);
        }
    }

    /// <summary>날짜를 선택한다. 지난달/다음달 칸을 누르면 그 달로 부드럽게 이동한다.</summary>
    private void SelectDate(DateOnly date)
    {
        VM.SelectedDate = date;

        var target = new DateOnly(date.Year, date.Month, 1);
        if (target == VM.CurrentMonth || _isAnimating)
        {
            return;
        }

        var delta = ((target.Year - VM.CurrentMonth.Year) * 12) + (target.Month - VM.CurrentMonth.Month);
        _ = RunMonthTransitionAsync(Math.Sign(delta), () => VM.EnsureMonthVisible(date));
    }

    // ================= 일정 추가 / 수정 / 삭제 =================

    private void OnAddScheduleClick(object sender, RoutedEventArgs e)
        => _ = ShowScheduleDialogAsync(null, VM.SelectedDate);

    private void OnScheduleCardTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ScheduleItem item })
        {
            e.Handled = true;
            _ = ShowScheduleDialogAsync(item, item.Date);
        }
    }

    private void OnScheduleCardDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        // 더블클릭으로 다이얼로그가 두 번 뜨지 않도록 흡수만 한다.
        e.Handled = true;
    }

    private void OnDeleteScheduleClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ScheduleItem item })
        {
            _ = ConfirmDeleteAsync(item);
        }
    }

    private async Task ShowScheduleDialogAsync(ScheduleItem? existing, DateOnly date)
    {
        if (Content?.XamlRoot is null)
        {
            return;
        }

        var dialog = new ScheduleEditDialog(existing, date)
        {
            XamlRoot = Content.XamlRoot,
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            if (existing is null)
            {
                var created = dialog.Result.Clone();
                created.Id = Guid.NewGuid().ToString("N");
                _store.Add(created);
                VM.SelectedDate = created.Date;
                EnsureVisible(created.Date);
            }
            else
            {
                _store.Update(existing, dialog.Result);
                VM.SelectedDate = existing.Date;
                EnsureVisible(existing.Date);
            }
        }
        else if (result == ContentDialogResult.Secondary && existing is not null)
        {
            // 다이얼로그 안의 "삭제" 버튼
            _store.Remove(existing);
        }
    }

    private async Task ConfirmDeleteAsync(ScheduleItem item)
    {
        if (Content?.XamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            RequestedTheme = ElementTheme.Light,
            Title = "일정을 삭제할까요?",
            Content = item.Title,
            PrimaryButtonText = "삭제",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _store.Remove(item);
        }
    }

    /// <summary>일정이 다른 달로 옮겨졌을 때 그 달로 따라간다.</summary>
    private void EnsureVisible(DateOnly date)
    {
        var target = new DateOnly(date.Year, date.Month, 1);
        if (target != VM.CurrentMonth)
        {
            VM.EnsureMonthVisible(date);
        }
    }

    // ================= 단축키 =================

    private void RegisterAccelerators()
    {
        AddAccelerator(VirtualKey.Left, VirtualKeyModifiers.Control, () => _ = GoToMonthAsync(-1));
        AddAccelerator(VirtualKey.Right, VirtualKeyModifiers.Control, () => _ = GoToMonthAsync(1));
        AddAccelerator(VirtualKey.PageUp, VirtualKeyModifiers.None, () => _ = GoToMonthAsync(-1));
        AddAccelerator(VirtualKey.PageDown, VirtualKeyModifiers.None, () => _ = GoToMonthAsync(1));
        AddAccelerator(VirtualKey.T, VirtualKeyModifiers.Control, GoToToday);
        AddAccelerator(VirtualKey.N, VirtualKeyModifiers.Control, () => _ = ShowScheduleDialogAsync(null, VM.SelectedDate));
    }

    private void AddAccelerator(VirtualKey key, VirtualKeyModifiers modifiers, Action action)
    {
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        accelerator.Invoked += (_, args) =>
        {
            args.Handled = true;
            action();
        };

        RootGrid.KeyboardAccelerators.Add(accelerator);
    }
}
