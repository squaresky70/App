using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using NotionCalendar.ViewModels;
using Windows.UI;

namespace NotionCalendar.Models;

/// <summary>일정에 붙일 수 있는 색상 하나. 다이얼로그에서 선택 상태도 함께 관리한다.</summary>
public sealed class ScheduleColorOption : ObservableObject
{
    public ScheduleColorOption(string key, string name, string fillHex, string textHex, string dotHex)
    {
        Key = key;
        Name = name;
        Fill = new SolidColorBrush(Hex(fillHex));
        Text = new SolidColorBrush(Hex(textHex));
        Dot = new SolidColorBrush(Hex(dotHex));
    }

    public string Key { get; }

    public string Name { get; }

    /// <summary>칩 배경색.</summary>
    public SolidColorBrush Fill { get; }

    /// <summary>칩 글자색.</summary>
    public SolidColorBrush Text { get; }

    /// <summary>목록의 좌측 색상 막대 / 색상 선택 점.</summary>
    public SolidColorBrush Dot { get; }

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (Set(ref _isSelected, value))
            {
                Raise(nameof(RingOpacity));
            }
        }
    }

    public double RingOpacity => _isSelected ? 1d : 0d;

    public static Color Hex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            hex = "FF" + hex;
        }

        var value = uint.Parse(hex, System.Globalization.NumberStyles.HexNumber);
        return ColorHelper.FromArgb(
            (byte)((value >> 24) & 0xFF),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)(value & 0xFF));
    }
}

/// <summary>노션 팔레트를 참고한 파스텔 색상 모음.</summary>
public static class ScheduleColors
{
    public const string DefaultKey = "blue";

    public static IReadOnlyList<ScheduleColorOption> All { get; } = new List<ScheduleColorOption>
    {
        new("gray",   "회색",   "#EFEFED", "#5F5E5B", "#9B9A97"),
        new("brown",  "갈색",   "#EEE0DA", "#7C5B49", "#A97C5D"),
        new("orange", "주황",   "#FADEC9", "#8A5325", "#D9730D"),
        new("yellow", "노랑",   "#FDECC8", "#7C6112", "#DFAB01"),
        new("green",  "초록",   "#DBEDDB", "#3D6B3D", "#4DAB6D"),
        new("blue",   "파랑",   "#D3E5EF", "#28556E", "#2383E2"),
        new("purple", "보라",   "#E8DEEE", "#5A3D75", "#9065B0"),
        new("pink",   "분홍",   "#F5E0E9", "#7A3B58", "#C14C8A"),
        new("red",    "빨강",   "#FFE2DD", "#8C3A31", "#E03E3E"),
    };

    public static ScheduleColorOption Get(string? key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            foreach (var option in All)
            {
                if (string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return option;
                }
            }
        }

        // 알 수 없는 키는 기본 색상으로.
        foreach (var option in All)
        {
            if (option.Key == DefaultKey)
            {
                return option;
            }
        }

        return All[0];
    }
}
