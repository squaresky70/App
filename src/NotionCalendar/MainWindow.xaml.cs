using System.Diagnostics;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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

    /// <summary>이 거리를 넘겨야 "스와이프"로 보고 날짜 클릭을 취소한다.</summary>
    private const double DragStartThreshold = 8;

    /// <summary>전환 애니메이션에서 격자가 밀려나는 거리.</summary>
    private const double SlideDistance = 52;

    private readonly DayCell[] _cells = new DayCell[MainViewModel.CellCount];
    private readonly CompositeTransform _slide = new();
    private readonly ScheduleStore _store = new();

    private Storyboard? _activeStoryboard;
    private bool _isAnimating;
    private int _wheelAccumulator;

    // 스와이프(포인터 드래그) 상태
    private bool _isPointerDown;
    private bool _isDragging;
    private uint _dragPointerId;
    private double _dragStartX;
    private double _dragLastX;
    private long _dragLastTicks;
    private double _dragVelocity;

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

    // ================= 헤더에서 바로 이동 (월/연도 선택 표, 주요 일정 D-Day) =================

    /// <summary>연도 선택 표가 다룰 범위.</summary>
    private const int MinPickerYear = 1900;
    private const int MaxPickerYear = 2100;

    /// <summary>한 페이지에 보여줄 연도 수(4열 × 3줄).</summary>
    private const int YearsPerPage = 12;

    /// <summary>주요 일정 D-Day 를 누르면 그 일정 날짜로 달력을 옮기고 선택한다.</summary>
    private void OnPinnedDDayClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PinnedDDay dday })
        {
            SelectDate(dday.Date);
        }
    }

    private void OnMonthTitleClick(object sender, RoutedEventArgs e)
    {
        var flyout = CreatePickerFlyout();
        var grid = CreateCellGrid();
        var shown = VM.CurrentMonth;

        for (var m = 1; m <= 12; m++)
        {
            var month = m;
            var cell = CreatePickerCell($"{m}월", month == shown.Month);
            cell.Click += (_, _) =>
            {
                flyout.Hide();
                JumpToMonth(new DateOnly(shown.Year, month, 1));
            };

            Grid.SetRow(cell, (m - 1) / 4);
            Grid.SetColumn(cell, (m - 1) % 4);
            grid.Children.Add(cell);
        }

        flyout.Content = grid;
        flyout.ShowAt((FrameworkElement)sender);
    }

    private void OnYearTitleClick(object sender, RoutedEventArgs e)
    {
        var flyout = CreatePickerFlyout();

        // 지금 보고 있는 해가 가운데쯤 오도록 페이지를 시작한다.
        ShowYearPage(flyout, VM.CurrentMonth.Year - 5);
        flyout.ShowAt((FrameworkElement)sender);
    }

    /// <summary>연도 12개 한 페이지를 그린다. ‹ › 로 12년씩 넘긴다.</summary>
    private void ShowYearPage(Flyout flyout, int firstYear)
    {
        firstYear = Math.Clamp(firstYear, MinPickerYear, MaxPickerYear - YearsPerPage + 1);
        var lastYear = firstYear + YearsPerPage - 1;
        var shownYear = VM.CurrentMonth.Year;

        var root = new Grid { RowSpacing = 8 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // 윗줄: ‹  2021 – 2032  ›
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var prev = CreatePageButton("", "이전 12년");
        prev.IsEnabled = firstYear > MinPickerYear;
        prev.Click += (_, _) => ShowYearPage(flyout, firstYear - YearsPerPage);

        var range = new TextBlock
        {
            Text = $"{firstYear} – {lastYear}",
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Res("TextSecondaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var next = CreatePageButton("", "다음 12년");
        next.IsEnabled = lastYear < MaxPickerYear;
        next.Click += (_, _) => ShowYearPage(flyout, firstYear + YearsPerPage);

        Grid.SetColumn(range, 1);
        Grid.SetColumn(next, 2);
        header.Children.Add(prev);
        header.Children.Add(range);
        header.Children.Add(next);

        var grid = CreateCellGrid();
        Grid.SetRow(grid, 1);

        for (var i = 0; i < YearsPerPage; i++)
        {
            var year = firstYear + i;
            var cell = CreatePickerCell($"{year}", year == shownYear);
            cell.Click += (_, _) =>
            {
                flyout.Hide();
                JumpToMonth(new DateOnly(year, VM.CurrentMonth.Month, 1));
            };

            Grid.SetRow(cell, i / 4);
            Grid.SetColumn(cell, i % 4);
            grid.Children.Add(cell);
        }

        root.Children.Add(header);
        root.Children.Add(grid);
        flyout.Content = root;
    }

    /// <summary>보고 있는 달을 target 달로 옮긴다(선택한 날짜는 그대로 둔다).</summary>
    private void JumpToMonth(DateOnly target)
    {
        var delta = ((target.Year - VM.CurrentMonth.Year) * 12) + (target.Month - VM.CurrentMonth.Month);
        if (delta == 0)
        {
            return;
        }

        if (_isAnimating)
        {
            // 전환 중이면 애니메이션은 건너뛰고 바로 옮긴다(클릭이 무시되지 않게).
            VM.EnsureMonthVisible(target);
            return;
        }

        _ = RunMonthTransitionAsync(Math.Sign(delta), () => VM.EnsureMonthVisible(target));
    }

    /// <summary>
    /// 선택 표는 팝업(Flyout) 안에 뜬다. 팝업 안에서는 ItemsRepeater/ItemsControl 이 항목을 그리지 못했으므로
    /// 칸 버튼을 Grid 에 직접 배치한다.
    /// </summary>
    private static Flyout CreatePickerFlyout() => new()
    {
        Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
        FlyoutPresenterStyle = (Style)Application.Current.Resources["PickerFlyoutPresenterStyle"],
    };

    private static Grid CreateCellGrid()
    {
        var grid = new Grid { ColumnSpacing = 4, RowSpacing = 4 };
        for (var c = 0; c < 4; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        for (var r = 0; r < 3; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        return grid;
    }

    /// <summary>선택 표의 칸 하나. 지금 보고 있는 달/해는 파랗게 표시한다.</summary>
    private static Button CreatePickerCell(string text, bool isCurrent)
    {
        var cell = new Button
        {
            Content = text,
            Width = 64,
            Height = 34,
            FontSize = 13,
            Style = (Style)Application.Current.Resources["ChipButtonStyle"],
        };

        if (isCurrent)
        {
            cell.Background = Res("AccentSoftBrush");
            cell.Foreground = Res("AccentBrush");
            cell.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        }

        return cell;
    }

    private static Button CreatePageButton(string glyph, string toolTip)
    {
        var button = new Button
        {
            Content = glyph,
            FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
            FontSize = 11,
            Style = (Style)Application.Current.Resources["GhostIconButtonStyle"],
        };

        ToolTipService.SetToolTip(button, toolTip);
        return button;
    }

    private static Brush Res(string key) => (Brush)Application.Current.Resources[key];

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

    /// <summary>
    /// 스와이프는 Manipulation 이벤트 대신 포인터 이벤트로 직접 처리한다.
    /// Manipulation 은 마우스 드래그에서 발생하지 않아 터치가 없는 PC 에서는 동작하지 않는다.
    /// </summary>
    private void OnSwipePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_isAnimating)
        {
            return;
        }

        var point = e.GetCurrentPoint(SwipeHost);

        // 마우스는 왼쪽 버튼일 때만.
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse
            && !point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isPointerDown = true;
        _isDragging = false;
        _dragPointerId = e.Pointer.PointerId;
        _dragStartX = point.Position.X;
        _dragLastX = _dragStartX;
        _dragLastTicks = Stopwatch.GetTimestamp();
        _dragVelocity = 0;
    }

    private void OnSwipePointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPointerDown || e.Pointer.PointerId != _dragPointerId)
        {
            return;
        }

        var x = e.GetCurrentPoint(SwipeHost).Position.X;
        var distance = x - _dragStartX;

        if (!_isDragging)
        {
            // 살짝 흔들린 정도로는 드래그로 보지 않는다(날짜 클릭을 방해하지 않도록).
            if (Math.Abs(distance) < DragStartThreshold)
            {
                return;
            }

            _isDragging = true;
            SwipeHost.CapturePointer(e.Pointer);
            StopActiveAnimation();
        }

        var now = Stopwatch.GetTimestamp();
        var elapsedMs = (now - _dragLastTicks) * 1000d / Stopwatch.Frequency;
        if (elapsedMs > 0)
        {
            _dragVelocity = (x - _dragLastX) / elapsedMs;
        }

        _dragLastX = x;
        _dragLastTicks = now;

        var shift = distance * DragFollowRatio;
        _slide.TranslateX = shift;

        // 끌수록 살짝 흐려지게 해서 "넘어가는 중"임을 보여준다.
        SwipeHost.Opacity = 1 - Math.Min(0.4, Math.Abs(shift) / 400);
        e.Handled = true;
    }

    private void OnSwipePointerReleased(object sender, PointerRoutedEventArgs e) => EndSwipe(e.Pointer);

    private void OnSwipePointerLost(object sender, PointerRoutedEventArgs e)
    {
        // SwipeHost 가 캡처를 가져가면 자식(DayCell)이 캡처를 잃고 그 이벤트가 여기까지 올라온다.
        // 우리가 캡처를 쥐고 있는 동안에는 드래그를 끝내면 안 된다.
        // PointerCaptures 는 캡처가 하나도 없으면 빈 목록이 아니라 null 을 돌려준다.
        var captures = SwipeHost.PointerCaptures;
        if (captures is not null)
        {
            foreach (var captured in captures)
            {
                if (captured.PointerId == e.Pointer.PointerId)
                {
                    return;
                }
            }
        }

        EndSwipe(e.Pointer);
    }

    private void EndSwipe(Pointer pointer)
    {
        if (!_isPointerDown || pointer.PointerId != _dragPointerId)
        {
            return;
        }

        var wasDragging = _isDragging;
        var distance = _dragLastX - _dragStartX;
        var velocity = _dragVelocity;

        // 캡처 해제가 PointerCaptureLost 로 다시 들어오므로 상태를 먼저 정리한다.
        _isPointerDown = false;
        _isDragging = false;

        if (wasDragging)
        {
            SwipeHost.ReleasePointerCaptures();
        }
        else
        {
            return;
        }

        if (_isAnimating)
        {
            return;
        }

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

    /// <summary>
    /// 삭제 버튼을 누른 탭이 카드 전체의 OnScheduleCardTapped 로 다시 버블링되어
    /// 수정 다이얼로그와 삭제 확인 다이얼로그가 동시에 뜨려다 충돌하는 것을 막는다.
    /// </summary>
    private void OnDeleteButtonTapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;

    /// <summary>헤더에 D-Day 로 띄울 주요 일정을 지정하거나 해제한다.</summary>
    private void OnPinScheduleClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ScheduleItem item })
        {
            _store.TogglePin(item);
        }
    }

    private void OnPinButtonTapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;

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
