using System.Text.Json.Serialization;
using Microsoft.UI.Xaml.Media;
using NotionCalendar.ViewModels;

namespace NotionCalendar.Models;

/// <summary>하루에 달리는 일정 하나.</summary>
public sealed class ScheduleItem : ObservableObject
{
    private DateOnly _date = DateOnly.FromDateTime(DateTime.Today);
    private string _title = string.Empty;
    private string _note = string.Empty;
    private bool _isAllDay = true;
    private TimeOnly _start = new(9, 0);
    private TimeOnly _end = new(10, 0);
    private string _colorKey = ScheduleColors.DefaultKey;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public DateOnly Date
    {
        get => _date;
        set => Set(ref _date, value);
    }

    public string Title
    {
        get => _title;
        set
        {
            if (Set(ref _title, value))
            {
                Raise(nameof(ChipText));
            }
        }
    }

    public string Note
    {
        get => _note;
        set
        {
            if (Set(ref _note, value))
            {
                RaiseAll(nameof(HasNote), nameof(NoteVisibility));
            }
        }
    }

    public bool IsAllDay
    {
        get => _isAllDay;
        set
        {
            if (Set(ref _isAllDay, value))
            {
                RaiseAll(nameof(ChipText), nameof(TimeText));
            }
        }
    }

    public TimeOnly Start
    {
        get => _start;
        set
        {
            if (Set(ref _start, value))
            {
                RaiseAll(nameof(ChipText), nameof(TimeText));
            }
        }
    }

    public TimeOnly End
    {
        get => _end;
        set
        {
            if (Set(ref _end, value))
            {
                Raise(nameof(TimeText));
            }
        }
    }

    public string ColorKey
    {
        get => _colorKey;
        set
        {
            if (Set(ref _colorKey, value))
            {
                RaiseAll(nameof(ChipBackground), nameof(ChipForeground), nameof(AccentBar));
            }
        }
    }

    // ---- 화면 표시용 (저장 대상 아님) ----

    [JsonIgnore]
    public string ChipText => IsAllDay ? Title : $"{Start:HH:mm}  {Title}";

    [JsonIgnore]
    public string TimeText => IsAllDay ? "하루 종일" : $"{Start:HH:mm} – {End:HH:mm}";

    [JsonIgnore]
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    [JsonIgnore]
    public Microsoft.UI.Xaml.Visibility NoteVisibility
        => HasNote ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    [JsonIgnore]
    public Brush ChipBackground => ScheduleColors.Get(ColorKey).Fill;

    [JsonIgnore]
    public Brush ChipForeground => ScheduleColors.Get(ColorKey).Text;

    [JsonIgnore]
    public Brush AccentBar => ScheduleColors.Get(ColorKey).Dot;

    /// <summary>정렬 기준: 하루 종일 일정이 먼저, 그다음 시작 시각 순.</summary>
    [JsonIgnore]
    public int SortKey => IsAllDay ? -1 : (Start.Hour * 60) + Start.Minute;

    public ScheduleItem Clone() => new()
    {
        Id = Id,
        Date = Date,
        Title = Title,
        Note = Note,
        IsAllDay = IsAllDay,
        Start = Start,
        End = End,
        ColorKey = ColorKey,
    };

    /// <summary>편집 다이얼로그에서 수정한 값을 원본에 반영한다.</summary>
    public void CopyValuesFrom(ScheduleItem other)
    {
        Date = other.Date;
        Title = other.Title;
        Note = other.Note;
        IsAllDay = other.IsAllDay;
        Start = other.Start;
        End = other.End;
        ColorKey = other.ColorKey;
    }
}
