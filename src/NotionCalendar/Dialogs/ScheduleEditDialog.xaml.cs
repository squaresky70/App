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
        // 정오 + 로컬 오프셋으로 넣어 시간대 변환 때문에 날짜가 하루 밀리지 않게 한다.
        var noon = item.Date.ToDateTime(new TimeOnly(12, 0));
        DatePickerControl.Date = new DateTimeOffset(noon, TimeZoneInfo.Local.GetUtcOffset(noon));
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

        var date = DatePickerControl.Date is { } picked
            ? DateOnly.FromDateTime(picked.DateTime)
            : DateOnly.FromDateTime(DateTime.Today);

        var start = TimeOnly.FromTimeSpan(StartTimePicker.Time);
        var end = TimeOnly.FromTimeSpan(EndTimePicker.Time);

        // 종료가 시작보다 빠르면 한 시간짜리로 맞춰준다.
        if (!AllDaySwitch.IsOn && end <= start)
        {
            end = start.AddHours(1);
        }

        Result.Date = date;
        Result.Title = title;
        Result.Note = NoteBox.Text.Trim();
        Result.IsAllDay = AllDaySwitch.IsOn;
        Result.Start = start;
        Result.End = end;
        Result.ColorKey = _colorKey;
    }
}
