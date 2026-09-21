using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace NotionCalendar.Models;

/// <summary>
/// 날짜 칸 한 줄(레인)에 그려지는 일정 막대 한 조각.
/// 같은 일정은 그 주 안에서 항상 같은 레인에 놓이고, 칸 사이 여백만큼 바깥으로 늘어나
/// 옆 칸의 조각과 맞닿는다. 그래서 여러 날에 걸친 일정이 하나의 긴 막대로 보인다.
/// </summary>
public sealed class ScheduleSegment
{
    /// <summary>막대가 시작/끝나는 쪽만 살짝 띄운다. 이어지는 쪽은 0 이라 옆 칸과 맞닿는다.</summary>
    private const double CapInset = 2;

    private static readonly SolidColorBrush TransparentBrush = new(Colors.Transparent);

    /// <summary>일정이 없는 레인. 아래 레인의 높이를 옆 칸과 맞추기 위한 빈 자리.</summary>
    public static ScheduleSegment Empty { get; } = new();

    private ScheduleSegment()
    {
        Background = TransparentBrush;
        Foreground = TransparentBrush;
        Text = string.Empty;
    }

    /// <param name="isItemStart">이 칸이 일정의 시작일인가.</param>
    /// <param name="isItemEnd">이 칸이 일정의 종료일인가.</param>
    /// <param name="showTitle">제목을 적을지(시작일과, 주가 바뀌어 다시 시작하는 칸에만).</param>
    public ScheduleSegment(ScheduleItem item, bool isItemStart, bool isItemEnd, bool showTitle)
    {
        Item = item;
        Background = item.ChipBackground;
        Foreground = item.ChipForeground;
        Text = showTitle ? item.ChipText : string.Empty;

        // 시작/종료 쪽만 둥글게 해서 가운데 칸들은 직선으로 이어지게 한다.
        Corner = new CornerRadius(
            isItemStart ? 5 : 0,
            isItemEnd ? 5 : 0,
            isItemEnd ? 5 : 0,
            isItemStart ? 5 : 0);

        // 이어지는 쪽은 여백 0 → 옆 칸의 막대와 그대로 붙는다.
        Margin = new Thickness(isItemStart ? CapInset : 0, 0, isItemEnd ? CapInset : 0, 0);
        Padding = new Thickness(isItemStart ? 6 : 4, 1, isItemEnd ? 6 : 4, 2);
    }

    public ScheduleItem? Item { get; }

    public Brush Background { get; }

    public Brush Foreground { get; }

    public string Text { get; }

    public CornerRadius Corner { get; }

    public Thickness Margin { get; }

    public Thickness Padding { get; } = new(6, 1, 6, 2);
}
