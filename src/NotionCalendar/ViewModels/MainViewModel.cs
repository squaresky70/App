using System.Collections.ObjectModel;
using NotionCalendar.Models;
using NotionCalendar.Services;

namespace NotionCalendar.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    /// <summary>달력은 항상 6주 × 7일 = 42칸으로 고정한다(월마다 높이가 변하지 않도록).</summary>
    public const int CellCount = 42;

    private static readonly string[] WeekdayNames =
        { "일요일", "월요일", "화요일", "수요일", "목요일", "금요일", "토요일" };

    private DateOnly _currentMonth;
    private DateOnly _selectedDate;

    public MainViewModel(ScheduleStore store)
    {
        Store = store;

        var today = DateOnly.FromDateTime(DateTime.Today);
        _selectedDate = today;
        _currentMonth = new DateOnly(today.Year, today.Month, 1);

        for (var i = 0; i < CellCount; i++)
        {
            Days.Add(new CalendarDay());
        }

        _storeChanged = (_, _) => Refresh();
        Store.Changed += _storeChanged;
        Refresh();
    }

    private readonly EventHandler _storeChanged;

    public ScheduleStore Store { get; }

    /// <summary>
    /// 저장소는 본 창과 위젯이 함께 쓰므로, 창을 닫을 때 이벤트 구독을 끊어
    /// 닫힌 창의 화면을 계속 갱신하지 않게 한다.
    /// </summary>
    public void Detach() => Store.Changed -= _storeChanged;

    /// <summary>42개의 칸. 인스턴스는 고정, 내용만 바뀐다.</summary>
    public List<CalendarDay> Days { get; } = new(CellCount);

    /// <summary>선택된 날짜의 전체 일정(우측 패널).</summary>
    public ObservableCollection<ScheduleItem> SelectedDaySchedules { get; } = new();

    /// <summary>화면에 보이는 달(항상 1일).</summary>
    public DateOnly CurrentMonth
    {
        get => _currentMonth;
        private set
        {
            if (Set(ref _currentMonth, value))
            {
                RaiseAll(nameof(MonthTitle), nameof(YearTitle));
            }
        }
    }

    public DateOnly SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (Set(ref _selectedDate, value))
            {
                RaiseAll(
                    nameof(SelectedDateTitle),
                    nameof(SelectedWeekdayTitle),
                    nameof(SelectedDateDiffText),
                    nameof(SelectedHolidayName),
                    nameof(SelectedHolidayVisibility));
                RefreshSelection();
                RefreshSelectedDaySchedules();
            }
        }
    }

    public string MonthTitle => $"{CurrentMonth.Month}월";

    public string YearTitle => $"{CurrentMonth.Year}";

    public string SelectedDateTitle => $"{SelectedDate.Month}월 {SelectedDate.Day}일";

    public string SelectedWeekdayTitle => WeekdayNames[(int)SelectedDate.DayOfWeek];

    /// <summary>선택한 날이 공휴일이면 그 이름.</summary>
    public string SelectedHolidayName => KoreanHolidays.NameFor(SelectedDate) ?? string.Empty;

    public Microsoft.UI.Xaml.Visibility SelectedHolidayVisibility => SelectedHolidayName.Length == 0
        ? Microsoft.UI.Xaml.Visibility.Collapsed
        : Microsoft.UI.Xaml.Visibility.Visible;

    /// <summary>오늘과 선택한 날짜의 차이. 오늘이면 "오늘", 미래면 "D-n", 과거면 "D+n".</summary>
    public string SelectedDateDiffText => DaysFromToday(SelectedDate) switch
    {
        0 => "오늘",
        > 0 and var diff => $"D-{diff}",
        var diff => $"D+{-diff}",
    };

    /// <summary>상단에 한꺼번에 보여줄 주요 일정 D-Day 최대 개수.</summary>
    public const int HeaderDDayLimit = 3;

    /// <summary>디데이가 가까운 순으로 매긴 상위 주요 일정(최대 3개). 헤더가 그대로 그린다.</summary>
    public ObservableCollection<PinnedDDay> HeaderDDays { get; } = new();

    private int _hiddenDDayCount;

    public Microsoft.UI.Xaml.Visibility PinnedVisibility => HeaderDDays.Count == 0
        ? Microsoft.UI.Xaml.Visibility.Collapsed
        : Microsoft.UI.Xaml.Visibility.Visible;

    /// <summary>3개를 넘어 헤더에 못 올린 주요 일정 수. 예: "+2".</summary>
    public string HiddenDDayText => $"+{_hiddenDDayCount}";

    public Microsoft.UI.Xaml.Visibility HiddenDDayVisibility => _hiddenDDayCount > 0
        ? Microsoft.UI.Xaml.Visibility.Visible
        : Microsoft.UI.Xaml.Visibility.Collapsed;

    private static int DaysFromToday(DateOnly date)
        => date.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;

    public string ScheduleCountText => SelectedDaySchedules.Count == 0
        ? "일정 없음"
        : $"일정 {SelectedDaySchedules.Count}개";

    public Microsoft.UI.Xaml.Visibility EmptyStateVisibility => SelectedDaySchedules.Count == 0
        ? Microsoft.UI.Xaml.Visibility.Visible
        : Microsoft.UI.Xaml.Visibility.Collapsed;

    /// <summary>달 이동. delta 가 양수면 다음 달.</summary>
    public void MoveMonth(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        CurrentMonth = CurrentMonth.AddMonths(delta);
        RebuildGrid();
    }

    public void GoToToday()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        CurrentMonth = new DateOnly(today.Year, today.Month, 1);
        SelectedDate = today;
        RebuildGrid();
    }

    /// <summary>선택 날짜가 보이는 달 밖이면 그 달로 이동한다. 이동이 필요하면 방향(±1…)을 돌려준다.</summary>
    public int EnsureMonthVisible(DateOnly date)
    {
        var target = new DateOnly(date.Year, date.Month, 1);
        if (target == CurrentMonth)
        {
            return 0;
        }

        var delta = ((target.Year - CurrentMonth.Year) * 12) + (target.Month - CurrentMonth.Month);
        CurrentMonth = target;
        RebuildGrid();
        return delta;
    }

    public void Refresh()
    {
        RebuildGrid();
        RefreshSelectedDaySchedules();
        RefreshHeaderDDays();
    }

    /// <summary>
    /// 주요 일정에 우선순위를 매긴다: 아직 오지 않은 일정(오늘 포함)이 먼저, 그 안에서 디데이가 가까운 순.
    /// 이미 지난 일정은 그 뒤로, 최근에 지난 것부터. 상위 3개만 헤더에 올린다.
    /// </summary>
    private void RefreshHeaderDDays()
    {
        var ranked = Store.PinnedItems
            .Select(item => (Item: item, Days: DaysFromToday(item.Date)))
            .OrderBy(x => x.Days < 0 ? 1 : 0)
            .ThenBy(x => Math.Abs(x.Days))
            .ThenBy(x => x.Item.Title, StringComparer.CurrentCulture)
            .ToList();

        HeaderDDays.Clear();
        for (var i = 0; i < ranked.Count && i < HeaderDDayLimit; i++)
        {
            HeaderDDays.Add(new PinnedDDay(i + 1, ranked[i].Item, ranked[i].Days));
        }

        _hiddenDDayCount = ranked.Count - HeaderDDays.Count;
        RaiseAll(nameof(PinnedVisibility), nameof(HiddenDDayText), nameof(HiddenDDayVisibility));
    }

    /// <summary>42칸에 날짜와 일정 막대를 채운다. 막대 자리는 주 단위로 계산한다.</summary>
    private void RebuildGrid()
    {
        var firstOfMonth = CurrentMonth;

        // 그 주의 일요일부터 시작 (한국식 달력: 일 ~ 토)
        var gridStart = firstOfMonth.AddDays(-(int)firstOfMonth.DayOfWeek);
        var today = DateOnly.FromDateTime(DateTime.Today);

        for (var week = 0; week < CellCount / 7; week++)
        {
            var weekStart = gridStart.AddDays(week * 7);
            var lanes = BuildWeekLanes(weekStart);

            for (var col = 0; col < 7; col++)
            {
                var date = weekStart.AddDays(col);
                var cell = Days[(week * 7) + col];

                cell.Date = date;
                cell.IsCurrentMonth = date.Month == firstOfMonth.Month && date.Year == firstOfMonth.Year;
                cell.IsToday = date == today;
                cell.IsSelected = date == SelectedDate;
                cell.HolidayName = KoreanHolidays.NameFor(date);

                SyncLanes(cell, lanes, col, date);
            }
        }
    }

    /// <summary>
    /// 한 주(7칸)에 걸치는 일정들을 레인에 배치한다.
    /// 같은 일정이 그 주 내내 같은 레인을 차지하므로 칸을 넘어가도 높이가 어긋나지 않는다.
    /// </summary>
    private List<ScheduleItem?[]> BuildWeekLanes(DateOnly weekStart)
    {
        var weekEnd = weekStart.AddDays(6);

        var items = new List<ScheduleItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var date = weekStart; date <= weekEnd; date = date.AddDays(1))
        {
            foreach (var item in Store.ForDate(date))
            {
                if (seen.Add(item.Id))
                {
                    items.Add(item);
                }
            }
        }

        items.Sort(CompareForLane);

        var lanes = new List<ScheduleItem?[]>();
        foreach (var item in items)
        {
            var from = Math.Max(0, item.Date.DayNumber - weekStart.DayNumber);
            var to = Math.Min(6, item.EndDate.DayNumber - weekStart.DayNumber);

            var lane = TakeFreeLane(lanes, from, to);
            for (var i = from; i <= to; i++)
            {
                lane[i] = item;
            }
        }

        return lanes;
    }

    /// <summary>긴 일정일수록 위 레인에 놓아야 막대가 덜 끊겨 보인다.</summary>
    private static int CompareForLane(ScheduleItem a, ScheduleItem b)
    {
        var byLength = b.DayCount.CompareTo(a.DayCount);
        if (byLength != 0)
        {
            return byLength;
        }

        var byStart = a.Date.CompareTo(b.Date);
        if (byStart != 0)
        {
            return byStart;
        }

        var bySort = a.SortKey.CompareTo(b.SortKey);
        return bySort != 0
            ? bySort
            : string.Compare(a.Title, b.Title, StringComparison.CurrentCulture);
    }

    /// <summary>from~to 구간이 비어 있는 레인을 찾고, 없으면 새 레인을 만든다.</summary>
    private static ScheduleItem?[] TakeFreeLane(List<ScheduleItem?[]> lanes, int from, int to)
    {
        foreach (var lane in lanes)
        {
            var free = true;
            for (var i = from; i <= to; i++)
            {
                if (lane[i] is not null)
                {
                    free = false;
                    break;
                }
            }

            if (free)
            {
                return lane;
            }
        }

        var added = new ScheduleItem?[7];
        lanes.Add(added);
        return added;
    }

    /// <summary>한 칸이 그릴 막대 조각을 만든다. 기존 컬렉션을 재사용해 깜빡임을 줄인다.</summary>
    private static void SyncLanes(CalendarDay cell, List<ScheduleItem?[]> lanes, int col, DateOnly date)
    {
        var segments = new List<ScheduleSegment>();
        var shown = 0;
        var visibleLanes = Math.Min(lanes.Count, CalendarDay.LaneLimit);

        for (var lane = 0; lane < visibleLanes; lane++)
        {
            var item = lanes[lane][col];
            if (item is null)
            {
                // 아래 레인의 높이를 옆 칸과 맞추기 위한 빈 자리.
                segments.Add(ScheduleSegment.Empty);
                continue;
            }

            shown++;
            segments.Add(new ScheduleSegment(
                item,
                isItemStart: item.Date == date,
                isItemEnd: item.EndDate == date,
                showTitle: item.Date == date || col == 0));
        }

        // 맨 아래쪽 빈 레인은 높이만 차지하므로 잘라낸다.
        while (segments.Count > 0 && ReferenceEquals(segments[^1], ScheduleSegment.Empty))
        {
            segments.RemoveAt(segments.Count - 1);
        }

        var total = 0;
        foreach (var lane in lanes)
        {
            if (lane[col] is not null)
            {
                total++;
            }
        }

        cell.MoreCount = total - shown;
        SyncCollection(cell.Lanes, segments);
    }

    private static void SyncCollection(ObservableCollection<ScheduleSegment> target, List<ScheduleSegment> source)
    {
        for (var i = 0; i < source.Count; i++)
        {
            if (i < target.Count)
            {
                if (!ReferenceEquals(target[i], source[i]))
                {
                    target[i] = source[i];
                }
            }
            else
            {
                target.Add(source[i]);
            }
        }

        while (target.Count > source.Count)
        {
            target.RemoveAt(target.Count - 1);
        }
    }

    private void RefreshSelection()
    {
        foreach (var cell in Days)
        {
            cell.IsSelected = cell.Date == SelectedDate;
        }
    }

    private void RefreshSelectedDaySchedules()
    {
        SelectedDaySchedules.Clear();
        foreach (var item in Store.ForDate(SelectedDate))
        {
            SelectedDaySchedules.Add(item);
        }

        RaiseAll(nameof(ScheduleCountText), nameof(EmptyStateVisibility));
    }
}
