using System.Collections.ObjectModel;
using NotionCalendar.ViewModels;

namespace NotionCalendar.Models;

/// <summary>달력 격자의 칸 하나(42칸 중 하나). 인스턴스는 재사용되고 내용만 갱신된다.</summary>
public sealed class CalendarDay : ObservableObject
{
    /// <summary>한 칸에 미리 보여줄 일정 최대 개수.</summary>
    public const int PreviewLimit = 3;

    private DateOnly _date = DateOnly.FromDateTime(DateTime.Today);
    private bool _isCurrentMonth = true;
    private bool _isToday;
    private bool _isSelected;
    private int _moreCount;

    public DateOnly Date
    {
        get => _date;
        set
        {
            if (Set(ref _date, value))
            {
                RaiseAll(nameof(DayNumber), nameof(IsSunday), nameof(IsSaturday));
            }
        }
    }

    public bool IsCurrentMonth
    {
        get => _isCurrentMonth;
        set => Set(ref _isCurrentMonth, value);
    }

    public bool IsToday
    {
        get => _isToday;
        set => Set(ref _isToday, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    public int MoreCount
    {
        get => _moreCount;
        set
        {
            if (Set(ref _moreCount, value))
            {
                Raise(nameof(MoreText));
            }
        }
    }

    public string MoreText => MoreCount > 0 ? $"+{MoreCount}개 더보기" : string.Empty;

    public string DayNumber => Date.Day.ToString();

    public bool IsSunday => Date.DayOfWeek == DayOfWeek.Sunday;

    public bool IsSaturday => Date.DayOfWeek == DayOfWeek.Saturday;

    /// <summary>칸에 표시되는 일정 미리보기. 컬렉션 인스턴스는 유지하고 내용만 교체한다.</summary>
    public ObservableCollection<ScheduleItem> PreviewSchedules { get; } = new();
}
