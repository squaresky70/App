namespace NotionCalendar.Models;

/// <summary>헤더에 띄우는 주요 일정 D-Day 한 개. Rank 는 디데이가 가까운 순서(1부터).</summary>
public sealed class PinnedDDay
{
    public PinnedDDay(int rank, ScheduleItem item, int daysLeft)
    {
        Rank = rank;
        Title = item.Title;
        DaysLeft = daysLeft;
        DDayText = daysLeft switch
        {
            0 => "D-DAY",
            > 0 => $"D-{daysLeft}",
            _ => $"D+{-daysLeft}",
        };
        ToolTip = $"우선순위 {rank} · {item.Date.Year}년 {item.Date.Month}월 {item.Date.Day}일";
    }

    public int Rank { get; }

    public string Title { get; }

    public int DaysLeft { get; }

    public string DDayText { get; }

    public string ToolTip { get; }
}
