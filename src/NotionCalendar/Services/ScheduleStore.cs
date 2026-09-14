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

    public void Add(ScheduleItem item)
    {
        if (string.IsNullOrEmpty(item.Id))
        {
            item.Id = Guid.NewGuid().ToString("N");
        }

        Index(item);
        Commit();
    }

    /// <summary>편집본의 값을 원본에 반영한다. 날짜가 바뀌면 인덱스도 옮긴다.</summary>
    public void Update(ScheduleItem target, ScheduleItem edited)
    {
        if (!_byId.ContainsKey(target.Id))
        {
            // 이미 삭제된 항목을 편집한 경우 새로 추가한다.
            target.CopyValuesFrom(edited);
            Add(target);
            return;
        }

        var oldDate = target.Date;
        target.CopyValuesFrom(edited);

        if (oldDate != target.Date)
        {
            if (_byDate.TryGetValue(oldDate, out var oldList))
            {
                oldList.Remove(target);
                if (oldList.Count == 0)
                {
                    _byDate.Remove(oldDate);
                }
            }

            AddToDateIndex(target);
        }

        Commit();
    }

    public void Remove(ScheduleItem item)
    {
        _byId.Remove(item.Id);

        if (_byDate.TryGetValue(item.Date, out var list))
        {
            list.Remove(item);
            if (list.Count == 0)
            {
                _byDate.Remove(item.Date);
            }
        }

        Commit();
    }

    private void Index(ScheduleItem item)
    {
        _byId[item.Id] = item;
        AddToDateIndex(item);
    }

    private void AddToDateIndex(ScheduleItem item)
    {
        if (!_byDate.TryGetValue(item.Date, out var list))
        {
            list = new List<ScheduleItem>();
            _byDate[item.Date] = list;
        }

        if (!list.Contains(item))
        {
            list.Add(item);
        }
    }

    private void Commit()
    {
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
