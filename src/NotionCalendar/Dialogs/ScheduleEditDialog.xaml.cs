using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NotionCalendar.Models;

namespace NotionCalendar.Dialogs;

/// <summary>
/// 일정 추가 / 수정 다이얼로그.
/// 결과: Primary = 저장, Secondary = 삭제, None = 취소.
/// </summary>
public sealed partial class ScheduleEditDialog : ContentDialog
{
    private string _colorKey = ScheduleColors.DefaultKey;
    private Dictionary<string, Border> _colorRings = null!;

    /// <summary>시작일/종료일을 코드로 맞출 때 DateChanged 가 서로 물고 늘어지지 않게 한다.</summary>
    private bool _syncingDates;

    /// <param name="existing">수정할 일정. null 이면 새 일정 추가 모드.</param>
    /// <param name="defaultDate">추가 모드일 때 기본 날짜.</param>
    public ScheduleEditDialog(ScheduleItem? existing, DateOnly defaultDate)
    {
        InitializeComponent();

        IsEditMode = existing is not null;
        Title = IsEditMode ? "일정 수정" : "새 일정";

        if (IsEditMode)
        {
            // 수정 모드에서만 삭제 버튼을 노출한다.
            SecondaryButtonText = "삭제";
        }

        _colorRings = new Dictionary<string, Border>(StringComparer.OrdinalIgnoreCase)
        {
            ["gray"] = RingGray,
            ["brown"] = RingBrown,
            ["orange"] = RingOrange,
            ["yellow"] = RingYellow,
            ["green"] = RingGreen,
            ["blue"] = RingBlue,
            ["purple"] = RingPurple,
            ["pink"] = RingPink,
            ["red"] = RingRed,
        };

        var source = existing?.Clone() ?? new ScheduleItem { Date = defaultDate };
        LoadFrom(source);

        PrimaryButtonClick += OnPrimaryClick;
        Opened += (_, _) => TitleBox.Focus(FocusState.Programmatic);
    }

    public bool IsEditMode { get; }

    /// <summary>저장 버튼을 눌렀을 때 채워지는 결과값.</summary>
    public ScheduleItem Result { get; } = new();

    private void LoadFrom(ScheduleItem item)
    {
        TitleBox.Text = item.Title;
        NoteBox.Text = item.Note;

        _syncingDates = true;
        StartDatePicker.Date = ToPickerDate(item.Date);
        EndDatePicker.Date = ToPickerDate(item.EndDate < item.Date ? item.Date : item.EndDate);
        _syncingDates = false;

        AllDaySwitch.IsOn = item.IsAllDay;
        StartTimePicker.Time = item.Start.ToTimeSpan();
        EndTimePicker.Time = item.End.ToTimeSpan();

        SelectColor(item.ColorKey);
        UpdateTimeRowVisibility();
        UpdateSaveEnabled();
    }

    private void SelectColor(string key)
    {
        _colorKey = key;
        foreach (var (ringKey, ring) in _colorRings)
        {
            ring.Opacity = string.Equals(ringKey, key, StringComparison.OrdinalIgnoreCase) ? 1d : 0d;
        }
    }

    private void OnColorTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string key })
        {
            SelectColor(key);
        }
    }

    /// <summary>시간대 변환 때문에 날짜가 하루 밀리지 않도록 정오 + 로컬 오프셋으로 넣는다.</summary>
    private static DateTimeOffset ToPickerDate(DateOnly date)
    {
        var noon = date.ToDateTime(new TimeOnly(12, 0));
        return new DateTimeOffset(noon, TimeZoneInfo.Local.GetUtcOffset(noon));
    }

    private static DateOnly? FromPickerDate(DateTimeOffset? picked)
        => picked is { } value ? DateOnly.FromDateTime(value.DateTime) : null;

    /// <summary>시작일을 종료일보다 뒤로 옮기면 종료일도 같이 끌고 간다.</summary>
    private void OnStartDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (_syncingDates)
        {
            return;
        }

        var start = FromPickerDate(args.NewDate);
        var end = FromPickerDate(EndDatePicker.Date);
        if (start is null || end is null || end >= start)
        {
            return;
        }

        _syncingDates = true;
        EndDatePicker.Date = ToPickerDate(start.Value);
        _syncingDates = false;
    }

    /// <summary>종료일을 시작일보다 앞으로 옮기면 시작일에 맞춘다.</summary>
    private void OnEndDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (_syncingDates)
        {
            return;
        }

        var end = FromPickerDate(args.NewDate);
        var start = FromPickerDate(StartDatePicker.Date);
        if (start is null || end is null || end >= start)
        {
            return;
        }

        _syncingDates = true;
        EndDatePicker.Date = ToPickerDate(start.Value);
        _syncingDates = false;
    }

    private void OnAllDayToggled(object sender, RoutedEventArgs e) => UpdateTimeRowVisibility();

    private void UpdateTimeRowVisibility()
        => TimeRow.Visibility = AllDaySwitch.IsOn ? Visibility.Collapsed : Visibility.Visible;

    private void OnTitleChanged(object sender, TextChangedEventArgs e) => UpdateSaveEnabled();

    private void UpdateSaveEnabled()
        => IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(TitleBox.Text);

    private void OnPrimaryClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var title = TitleBox.Text.Trim();
        if (string.IsNullOrEmpty(title))
        {
            // 제목 없이는 닫지 않는다.
            args.Cancel = true;
            return;
        }

        var date = FromPickerDate(StartDatePicker.Date) ?? DateOnly.FromDateTime(DateTime.Today);
        var endDate = FromPickerDate(EndDatePicker.Date) ?? date;

        // 종료일이 시작일보다 빠르면 하루짜리로 맞춰준다.
        if (endDate < date)
        {
            endDate = date;
        }

        var start = TimeOnly.FromTimeSpan(StartTimePicker.Time);
        var end = TimeOnly.FromTimeSpan(EndTimePicker.Time);

        // 종료가 시작보다 빠르면 한 시간짜리로 맞춰준다.
        if (!AllDaySwitch.IsOn && end <= start)
        {
            end = start.AddHours(1);
        }

        Result.Date = date;
        Result.EndDate = endDate;
        Result.Title = title;
        Result.Note = NoteBox.Text.Trim();
        Result.IsAllDay = AllDaySwitch.IsOn;
        Result.Start = start;
        Result.End = end;
        Result.ColorKey = _colorKey;
    }
}
