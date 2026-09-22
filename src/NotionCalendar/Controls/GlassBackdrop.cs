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

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        _controller = new DesktopAcrylicController
        {
            TintColor = Color.FromArgb(255, 14, 30, 58),
            TintOpacity = 0.45f,
            LuminosityOpacity = 0.35f,
            FallbackColor = Color.FromArgb(235, 18, 34, 62),
        };

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
