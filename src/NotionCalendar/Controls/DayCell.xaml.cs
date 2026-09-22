using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NotionCalendar.Models;
using NotionCalendar.Services;
using Windows.Foundation;

namespace NotionCalendar.Controls;

/// <summary>
/// 달력 격자의 한 칸. Day 프로퍼티만 바꿔 끼우면 화면이 갱신되므로
/// 달을 넘길 때 시각 요소를 새로 만들지 않는다.
/// </summary>
public sealed partial class DayCell : UserControl
{
    private static readonly Brush Transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    private bool _isPointerOver;

    public DayCell()
    {
        InitializeComponent();

        PointerEntered += (_, _) => { _isPointerOver = true; ApplyVisuals(); };
        PointerExited += (_, _) => { _isPointerOver = false; ApplyVisuals(); };
        PointerCanceled += (_, _) => { _isPointerOver = false; ApplyVisuals(); };
        PointerCaptureLost += (_, _) => { _isPointerOver = false; ApplyVisuals(); };

        // 일정이 많아도 칸 밖으로 흘러넘치지 않게 자른다.
        SizeChanged += (_, e) =>
        {
            RootBorder.Clip = new RectangleGeometry
            {
                Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height),
            };
        };
    }

    public static readonly DependencyProperty DayProperty = DependencyProperty.Register(
        nameof(Day),
        typeof(CalendarDay),
        typeof(DayCell),
        new PropertyMetadata(null, OnDayChanged));

    public CalendarDay? Day
    {
        get => (CalendarDay?)GetValue(DayProperty);
        set => SetValue(DayProperty, value);
    }

    private static void OnDayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var cell = (DayCell)d;

        if (e.OldValue is CalendarDay oldDay)
        {
            oldDay.PropertyChanged -= cell.OnDayPropertyChanged;
        }

        if (e.NewValue is CalendarDay newDay)
        {
            newDay.PropertyChanged += cell.OnDayPropertyChanged;
            cell.ChipsRepeater.ItemsSource = newDay.Lanes;
        }
        else
        {
            cell.ChipsRepeater.ItemsSource = null;
        }

        cell.ApplyVisuals();
    }

    private void OnDayPropertyChanged(object? sender, PropertyChangedEventArgs e) => ApplyVisuals();

    /// <summary>
    /// 바탕화면 위젯(어두운 반투명 유리) 위에 그릴 때 켠다. 흰 글자 계열 색을 쓰고 음력 날짜를 함께 보여준다.
    /// Day 를 넣기 전에 정해야 한다.
    /// </summary>
    public bool GlassStyle { get; set; }

    /// <summary>GlassStyle 이면 같은 이름 앞에 "Glass" 가 붙은 색을 쓴다(Tokens.xaml).</summary>
    private Brush Res(string key)
        => (Brush)Application.Current.Resources[GlassStyle ? "Glass" + key : key];

    private void ApplyVisuals()
    {
        var day = Day;
        if (day is null)
        {
            DayText.Text = string.Empty;
            RootBorder.Background = Transparent;
            RootBorder.BorderBrush = Transparent;
            MoreText.Visibility = Visibility.Collapsed;
            HolidayText.Visibility = Visibility.Collapsed;
            LunarText.Visibility = Visibility.Collapsed;
            return;
        }

        DayText.Text = day.DayNumber;

        // --- 음력 날짜 (위젯에서만) ---
        // 칸이 좁아 공휴일 이름과 함께 두면 둘 다 잘리므로, 공휴일인 날은 공휴일 이름을 우선한다.
        if (GlassStyle && !day.IsHoliday && KoreanHolidays.LunarText(day.Date) is { } lunar)
        {
            LunarText.Text = $"(음){lunar}";
            LunarText.Foreground = Res("TextTertiaryBrush");
            LunarText.Visibility = Visibility.Visible;
        }
        else
        {
            LunarText.Visibility = Visibility.Collapsed;
        }

        // --- 공휴일 이름 ---
        if (day.IsHoliday)
        {
            HolidayText.Text = day.HolidayName;
            HolidayText.Foreground = Res("HolidayBrush");
            HolidayText.Visibility = Visibility.Visible;
        }
        else
        {
            HolidayText.Visibility = Visibility.Collapsed;
        }

        // --- 배경 / 테두리 ---
        if (day.IsSelected)
        {
            RootBorder.Background = Res("BgSelectedBrush");
            RootBorder.BorderBrush = Res("AccentBrush");
        }
        else if (_isPointerOver)
        {
            RootBorder.Background = Res("BgHoverBrush");
            RootBorder.BorderBrush = Transparent;
        }
        else
        {
            RootBorder.Background = Transparent;
            RootBorder.BorderBrush = Transparent;
        }

        // --- 날짜 숫자 ---
        if (day.IsToday)
        {
            DayBadge.Background = Res("AccentBrush");
            DayText.Foreground = Res("TextOnAccentBrush");
        }
        else
        {
            DayBadge.Background = Transparent;

            if (!day.IsCurrentMonth)
            {
                DayText.Foreground = Res("TextTertiaryBrush");
            }
            else if (day.IsHoliday)
            {
                DayText.Foreground = Res("HolidayBrush");
            }
            else if (day.IsSunday)
            {
                DayText.Foreground = Res("SundayBrush");
            }
            else if (day.IsSaturday)
            {
                DayText.Foreground = Res("SaturdayBrush");
            }
            else
            {
                DayText.Foreground = Res("TextPrimaryBrush");
            }
        }

        // 지난달/다음달 날짜는 흐리게
        RootBorder.Opacity = day.IsCurrentMonth ? 1d : 0.45d;

        // --- +N개 더보기 ---
        if (day.MoreCount > 0)
        {
            MoreText.Text = day.MoreText;
            MoreText.Foreground = Res("TextTertiaryBrush");
            MoreText.Visibility = Visibility.Visible;
        }
        else
        {
            MoreText.Visibility = Visibility.Collapsed;
        }
    }
}
