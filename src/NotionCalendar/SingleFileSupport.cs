using System.Runtime.CompilerServices;

namespace NotionCalendar;

/// <summary>
/// 단일 파일(PublishSingleFile)로 게시했을 때 Windows App SDK 런타임이
/// 자기 DLL들을 어디서 찾아야 하는지 알려준다.
/// Main 보다 먼저 실행되어야 해서 모듈 이니셜라이저를 쓴다.
/// 폴더 배포에서는 아무 영향이 없다.
/// </summary>
internal static class SingleFileSupport
{
    private const string BaseDirectoryVariable = "MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY";

    [ModuleInitializer]
    internal static void Initialize()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(BaseDirectoryVariable)))
        {
            Environment.SetEnvironmentVariable(BaseDirectoryVariable, AppContext.BaseDirectory);
        }
    }
}
