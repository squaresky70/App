using System.Text.Json.Serialization;

namespace NotionCalendar.Models;

/// <summary>일정의 종류. 예전 저장 파일에는 이 값이 없어 기본값(일정)으로 읽힌다.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScheduleKind
{
    /// <summary>보통 일정.</summary>
    Event,

    /// <summary>체크해서 완료 표시할 수 있는 할 일.</summary>
    Todo,
}
