using System.Text.Json;
using System.Text.Json.Serialization;
using NotionCalendar.Models;

namespace NotionCalendar.Services;

/// <summary>
/// 일정 저장소. %LOCALAPPDATA%\NotionCalendar\schedules.json 에 보관한다.
/// 날짜별 조회를 위해 메모리에 인덱스를 함께 유지한다.
/// </summary>
public sealed class ScheduleStore
{
    /// <summary>한 일정이 걸칠 수 있는 최대 일수. 파일이 깨졌을 때 색인이 폭주하지 않게 한다.</summary>
    private const int MaxSpanDays = 366;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Dictionary<DateOnly, List<ScheduleItem>> _byDate = new();
    private readonly Dictionary<string, ScheduleItem> _byId = new(StringComparer.Ordinal);
    private readonly string _filePath;

    public ScheduleStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NotionCalendar",
            "schedules.json");
    }

    /// <summary>추가/수정/삭제 후 발생. 화면을 다시 그리는 신호로 쓴다.</summary>
    public event EventHandler? Changed;

    public string FilePath => _filePath;

    public void Load()
    {
        _byDate.Clear();
        _byId.Clear();

        try
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            var json = File.ReadAllText(_filePath);
            var items = JsonSerializer.Deserialize<List<ScheduleItem>>(json, JsonOptions);
            if (items is null)
            {
                return;
            }

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Title))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(item.Id) || _byId.ContainsKey(item.Id))
                {
                    item.Id = Guid.NewGuid().ToString("N");
                }

                // 종료일이 없던 예전 파일은 하루짜리로, 말이 안 되는 기간은 잘라서 받는다.
                if (item.EndDate < item.Date)
                {
                    item.EndDate = item.Date;
                }
                else if (item.EndDate.DayNumber - item.Date.DayNumber > MaxSpanDays)
                {
                    item.EndDate = item.Date.AddDays(MaxSpanDays);
                }

                Index(item);
            }
        }
        catch (Exception ex)
        {
            // 파일이 깨졌더라도 앱은 빈 달력으로 계속 동작해야 한다.
            System.Diagnostics.Debug.WriteLine($"[ScheduleStore] 불러오기 실패: {ex}");
            _byDate.Clear();
            _byId.Clear();
        }
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var all = _byId.Values.OrderBy(i => i.Date).ThenBy(i => i.SortKey).ToList();
            var json = JsonSerializer.Serialize(all, JsonOptions);

            // 쓰다가 죽어도 기존 파일이 남도록 임시 파일에 먼저 쓴다.
            var temp = _filePath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleStore] 저장 실패: {ex}");
        }
    }

    /// <summary>해당 날짜의 일정을 정렬된 상태로 돌려준다.</summary>
    public IReadOnlyList<ScheduleItem> ForDate(DateOnly date)
    {
        if (_byDate.TryGetValue(date, out var list))
        {
            return list.OrderBy(i => i.SortKey).ThenBy(i => i.Title, StringComparer.CurrentCulture).ToList();
        }

        return Array.Empty<ScheduleItem>();
    }

    public int CountForDate(DateOnly date)
        => _byDate.TryGetValue(date, out var list) ? list.Count : 0;

    /// <summary>주요 일정(D-Day)으로 지정된 일정 전부. 여러 개일 수 있다.</summary>
    public IReadOnlyList<ScheduleItem> PinnedItems
        => _byId.Values.Where(item => item.IsPinned).ToList();

    /// <summary>주요 일정 지정을 켜거나 끈다. 개수 제한은 없다.</summary>
    public void TogglePin(ScheduleItem item)
    {
        item.IsPinned = !item.IsPinned;
        Commit();
    }

    public void Add(ScheduleItem item)
    {
        if (string.IsNullOrEmpty(item.Id))
        {
            item.Id = Guid.NewGuid().ToString("N");
        }

        ClampRange(item);
        Index(item);
        Commit();
    }

    /// <summary>편집본의 값을 원본에 반영한다. 기간이 바뀌면 인덱스도 옮긴다.</summary>
    public void Update(ScheduleItem target, ScheduleItem edited)
    {
        if (!_byId.ContainsKey(target.Id))
        {
            // 이미 삭제된 항목을 편집한 경우 새로 추가한다.
            target.CopyValuesFrom(edited);
            Add(target);
            return;
        }

        var oldStart = target.Date;
        var oldEnd = target.EndDate;

        target.CopyValuesFrom(edited);
        ClampRange(target);

        if (oldStart != target.Date || oldEnd != target.EndDate)
        {
            RemoveFromDateIndex(target, oldStart, oldEnd);
            AddToDateIndex(target);
        }

        Commit();
    }

    public void Remove(ScheduleItem item)
    {
        _byId.Remove(item.Id);
        RemoveFromDateIndex(item, item.Date, item.EndDate);
        Commit();
    }

    /// <summary>종료일이 시작일보다 앞서면 하루짜리로 맞춘다.</summary>
    private static void ClampRange(ScheduleItem item)
    {
        if (item.EndDate < item.Date)
        {
            item.EndDate = item.Date;
        }
    }

    private void Index(ScheduleItem item)
    {
        _byId[item.Id] = item;
        AddToDateIndex(item);
    }

    /// <summary>시작일부터 종료일까지 모든 날짜에 걸어 둔다. 그래야 중간 날짜에서도 조회된다.</summary>
    private void AddToDateIndex(ScheduleItem item)
    {
        for (var date = item.Date; date <= item.EndDate; date = date.AddDays(1))
        {
            if (!_byDate.TryGetValue(date, out var list))
            {
                list = new List<ScheduleItem>();
                _byDate[date] = list;
            }

            if (!list.Contains(item))
            {
                list.Add(item);
            }
        }
    }

    private void RemoveFromDateIndex(ScheduleItem item, DateOnly from, DateOnly to)
    {
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (!_byDate.TryGetValue(date, out var list))
            {
                continue;
            }

            list.Remove(item);
            if (list.Count == 0)
            {
                _byDate.Remove(date);
            }
        }
    }

    private void Commit()
    {
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
