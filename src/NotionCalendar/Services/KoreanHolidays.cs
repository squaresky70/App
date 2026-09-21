using System.Globalization;

namespace NotionCalendar.Services;

/// <summary>
/// 대한민국 관공서 공휴일(쉬는 날).
///
/// 설날·추석·부처님오신날은 음력이라 해마다 날짜가 달라지므로
/// <see cref="KoreanLunisolarCalendar"/> 로 그해의 양력 날짜를 계산하고,
/// 대체공휴일은 「공휴일에 관한 법률 시행령」 규칙대로 채워 넣는다.
/// 인터넷 연결이나 API 키 없이 어떤 해든 계산할 수 있다.
/// </summary>
public static class KoreanHolidays
{
    private static readonly KoreanLunisolarCalendar Lunar = new();

    private static readonly Dictionary<int, IReadOnlyDictionary<DateOnly, string>> Cache = new();

    /// <summary>
    /// 계산으로는 알 수 없는 임시공휴일. 정부가 그때그때 정하는 날이라 직접 적어 둔다.
    /// </summary>
    private static readonly (DateOnly Date, string Name)[] Temporary =
    {
        (new DateOnly(2020, 8, 17), "임시공휴일"),
        (new DateOnly(2023, 10, 2), "임시공휴일"),
        (new DateOnly(2025, 1, 27), "임시공휴일"),
    };

    /// <summary>공휴일이면 이름을, 아니면 null 을 돌려준다.</summary>
    public static string? NameFor(DateOnly date)
        => ForYear(date.Year).TryGetValue(date, out var name) ? name : null;

    public static IReadOnlyDictionary<DateOnly, string> ForYear(int year)
    {
        if (Cache.TryGetValue(year, out var cached))
        {
            return cached;
        }

        var built = Build(year);
        Cache[year] = built;
        return built;
    }

    private static IReadOnlyDictionary<DateOnly, string> Build(int year)
    {
        var holidays = new Dictionary<DateOnly, string>();

        void Add(DateOnly date, string name)
        {
            // 연휴가 해를 넘어가는 경우가 있어 그해 날짜만 담는다.
            if (date.Year == year)
            {
                holidays.TryAdd(date, name);
            }
        }

        // 날짜가 고정된 공휴일
        Add(new DateOnly(year, 1, 1), "신정");
        Add(new DateOnly(year, 3, 1), "삼일절");
        Add(new DateOnly(year, 5, 5), "어린이날");
        Add(new DateOnly(year, 6, 6), "현충일");
        Add(new DateOnly(year, 8, 15), "광복절");
        Add(new DateOnly(year, 10, 3), "개천절");
        Add(new DateOnly(year, 10, 9), "한글날");
        Add(new DateOnly(year, 12, 25), "성탄절");

        // 음력 명절 (설날·추석은 앞뒤 하루씩 함께 쉰다)
        var seollal = FromLunar(year, 1, 1);
        if (seollal is { } newYear)
        {
            Add(newYear.AddDays(-1), "설날 연휴");
            Add(newYear, "설날");
            Add(newYear.AddDays(1), "설날 연휴");
        }

        var chuseok = FromLunar(year, 8, 15);
        if (chuseok is { } harvest)
        {
            Add(harvest.AddDays(-1), "추석 연휴");
            Add(harvest, "추석");
            Add(harvest.AddDays(1), "추석 연휴");
        }

        var buddha = FromLunar(year, 4, 8);
        if (buddha is { } buddhaDay)
        {
            Add(buddhaDay, "부처님오신날");
        }

        foreach (var (date, name) in Temporary)
        {
            Add(date, name);
        }

        AddSubstitutes(year, holidays, seollal, chuseok, buddha);
        return holidays;
    }

    /// <summary>음력 날짜를 그해의 양력 날짜로 바꾼다. 계산할 수 없는 해면 null.</summary>
    private static DateOnly? FromLunar(int year, int month, int day)
    {
        try
        {
            // 윤달이 있는 해는 그 뒤의 달이 한 칸씩 밀린다.
            var leapMonth = Lunar.GetLeapMonth(year);
            var index = leapMonth > 0 && month >= leapMonth ? month + 1 : month;
            return DateOnly.FromDateTime(Lunar.ToDateTime(year, index, day, 0, 0, 0, 0));
        }
        catch (ArgumentOutOfRangeException)
        {
            // KoreanLunisolarCalendar 가 다루는 범위(1918~2050년경) 밖.
            return null;
        }
    }

    private static void AddSubstitutes(
        int year,
        Dictionary<DateOnly, string> holidays,
        DateOnly? seollal,
        DateOnly? chuseok,
        DateOnly? buddha)
    {
        // 설날·추석 연휴는 "일요일"과 겹칠 때만 연휴 다음 날에 하루를 더한다.
        foreach (var festival in new[] { seollal, chuseok })
        {
            if (festival is not { } day)
            {
                continue;
            }

            var lastDay = day.AddDays(1);
            var overlapsSunday =
                day.AddDays(-1).DayOfWeek == DayOfWeek.Sunday
                || day.DayOfWeek == DayOfWeek.Sunday
                || lastDay.DayOfWeek == DayOfWeek.Sunday;

            if (overlapsSunday)
            {
                AddSubstitute(year, holidays, lastDay);
            }
        }

        // 토·일 또는 다른 공휴일과 겹치면 대체공휴일이 붙는 날들. (신정·현충일은 제외)
        var eligible = new List<DateOnly>
        {
            new(year, 3, 1),
            new(year, 5, 5),
            new(year, 8, 15),
            new(year, 10, 3),
            new(year, 10, 9),
            new(year, 12, 25),
        };

        if (buddha is { } buddhaDay)
        {
            eligible.Add(buddhaDay);
        }

        foreach (var group in eligible.GroupBy(date => date).OrderBy(group => group.Key))
        {
            var date = group.Key;
            var collides = group.Count() > 1;

            if (collides || date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                AddSubstitute(year, holidays, date);
            }
        }
    }

    /// <summary>주어진 날 다음의 첫 번째 "쉬지 않는 날"을 대체공휴일로 잡는다.</summary>
    private static void AddSubstitute(int year, Dictionary<DateOnly, string> holidays, DateOnly after)
    {
        var candidate = after.AddDays(1);

        while (candidate.DayOfWeek == DayOfWeek.Sunday || holidays.ContainsKey(candidate))
        {
            candidate = candidate.AddDays(1);
        }

        if (candidate.Year == year)
        {
            holidays[candidate] = "대체공휴일";
        }
    }
}
