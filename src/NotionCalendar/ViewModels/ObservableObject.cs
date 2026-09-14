using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NotionCalendar.ViewModels;

/// <summary>변경 알림을 위한 최소 구현 베이스 클래스.</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Raise(name);
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected void RaiseAll(params string[] names)
    {
        foreach (var n in names)
        {
            Raise(n);
        }
    }
}
