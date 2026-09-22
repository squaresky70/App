using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace NotionCalendar.Controls;

/// <summary>
/// 바탕화면 위젯용 어두운 반투명 유리 배경.
/// 기본 아크릴은 창이 비활성이면 불투명한 단색으로 바뀌는데, 위젯은 대부분 비활성 상태로
/// 바탕화면에 떠 있으므로 항상 "활성" 으로 설정해 유리 효과를 유지한다.
/// </summary>
public sealed class GlassBackdrop : SystemBackdrop
{
    private DesktopAcrylicController? _controller;
    private double _transparency = 0.5;

    /// <summary>
    /// 유리 배경의 투명도(0 = 가장 진하게, 1 = 가장 투명하게). 실행 중에 바꿔도 곧바로 반영된다.
    /// 1 이어도 어느 정도 어두운 색은 남긴다. 뒤에 흰 창이나 밝은 바탕화면이 있을 때
    /// 흰 글자가 배경에 묻혀 읽히지 않는 것을 막기 위해서다.
    /// </summary>
    public double Transparency
    {
        get => _transparency;
        set
        {
            _transparency = Math.Clamp(value, 0, 1);
            Apply();
        }
    }

    private void Apply()
    {
        if (_controller is null)
        {
            return;
        }

        var density = (float)(1 - _transparency);
        _controller.TintOpacity = 0.35f + (0.6f * density);
        _controller.LuminosityOpacity = 0.3f + (0.6f * density);
    }

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        _controller = new DesktopAcrylicController
        {
            TintColor = Color.FromArgb(255, 14, 30, 58),
            FallbackColor = Color.FromArgb(235, 18, 34, 62),
        };

        Apply();
        _controller.AddSystemBackdropTarget(connectedTarget);
        _controller.SetSystemBackdropConfiguration(new SystemBackdropConfiguration
        {
            IsInputActive = true,
            Theme = SystemBackdropTheme.Dark,
        });
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);

        if (_controller is not null)
        {
            _controller.RemoveSystemBackdropTarget(disconnectedTarget);
            _controller.Dispose();
            _controller = null;
        }
    }
}
