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

        Store.Changed += (_, _) => Refresh();
        Refresh();
    }

    public ScheduleStore Store { get; }

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
                RaiseAll(nameof(SelectedDateTitle), nameof(SelectedWeekdayTitle));
                RefreshSelection();
                RefreshSelectedDaySchedules();
            }
        }
    }

    public string MonthTitle => $"{CurrentMonth.Month}월";

    public string YearTitle => $"{CurrentMonth.Year}";

    public string SelectedDateTitle => $"{SelectedDate.Month}월 {SelectedDate.Day}일";

    public string SelectedWeekdayTitle => WeekdayNames[(int)SelectedDate.DayOfWeek];

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
    }

    /// <summary>42칸에 날짜와 일정 미리보기를 채운다.</summary>
    private void RebuildGrid()
    {
        var firstOfMonth = CurrentMonth;

        // 그 주의 일요일부터 시작 (한국식 달력: 일 ~ 토)
        var start = firstOfMonth.AddDays(-(int)firstOfMonth.DayOfWeek);
        var today = DateOnly.FromDateTime(DateTime.Today);

        for (var i = 0; i < CellCount; i++)
        {
            var date = start.AddDays(i);
            var cell = Days[i];

            cell.Date = date;
            cell.IsCurrentMonth = date.Month == firstOfMonth.Month && date.Year == firstOfMonth.Year;
            cell.IsToday = date == today;
            cell.IsSelected = date == SelectedDate;

            var all = Store.ForDate(date);
            SyncPreview(cell, all);
        }
    }

    /// <summary>기존 컬렉션을 최대한 재사용해 깜빡임 없이 미리보기를 갱신한다.</summary>
    private static void SyncPreview(CalendarDay cell, IReadOnlyList<ScheduleItem> all)
    {
        var take = Math.Min(all.Count, CalendarDay.PreviewLimit);
        var preview = cell.PreviewSchedules;

        for (var i = 0; i < take; i++)
        {
            if (i < preview.Count)
            {
                if (!ReferenceEquals(preview[i], all[i]))
                {
                    preview[i] = all[i];
                }
            }
            else
            {
                preview.Add(all[i]);
            }
        }

        while (preview.Count > take)
        {
            preview.RemoveAt(preview.Count - 1);
        }

        cell.MoreCount = all.Count - take;
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
