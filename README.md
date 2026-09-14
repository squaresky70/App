# 캘린더 (WinUI 3)

노션 스타일의 깔끔한 화이트 테마 데스크톱 캘린더입니다.

## 기능

| 기능 | 조작 |
| --- | --- |
| 일정 추가 | 우측 패널의 **＋ 일정 추가** 버튼, 날짜 칸 **더블클릭**, `Ctrl+N` |
| 일정 수정 | 우측 패널의 일정 카드를 **클릭** → 값을 고치고 **저장** |
| 일정 삭제 | 카드 우측 **🗑 버튼**(확인 후 삭제), 또는 수정 창의 **삭제** 버튼 |
| 다음/이전 달 | **좌우 스와이프**(터치·마우스 드래그·터치패드), **마우스 휠**, `‹ ›` 버튼, `Ctrl+←`/`Ctrl+→`, `PageUp`/`PageDown` |
| 오늘로 이동 | **오늘** 버튼, `Ctrl+T` |

일정에는 제목, 날짜, 하루 종일 여부, 시작/종료 시각, 색상(9가지), 메모를 넣을 수 있습니다.

## 디자인

- 배경 `#FFFFFF`, 글자 `#37352F`, 테두리 `#EDEDEB` 등 노션 팔레트 기반
- 모든 카드·칸·버튼에 6~18px 라운드 적용
- 오늘 날짜는 파란 원, 선택한 날짜는 연한 배경 + 액센트 테두리
- 주말은 은은한 색으로만 구분(빨강/파랑 강조 최소화)

## 저장 위치

일정은 아래 JSON 파일에 자동 저장됩니다.

```
%LOCALAPPDATA%\NotionCalendar\schedules.json
```

## 빌드 / 실행

필요한 것:

1. **.NET 8 SDK** — <https://dotnet.microsoft.com/download/dotnet/8.0>
2. Windows 10 1809(17763) 이상

Windows App SDK 런타임은 자체 포함(self-contained)으로 빌드되므로 따로 설치할 필요가 없습니다.

```powershell
dotnet run --project src\NotionCalendar
```

ARM64 PC라면 `src\NotionCalendar\NotionCalendar.csproj` 의
`<RuntimeIdentifier>win-x64</RuntimeIdentifier>` 를 `win-arm64` 로 바꾸세요.

배포용 폴더를 만들려면:

```powershell
dotnet publish src\NotionCalendar -c Release -o publish
```

Visual Studio 2022로 열려면 `NotionCalendar.sln` 을 사용하세요
(**.NET 데스크톱 개발** + **Windows 앱 SDK C# 템플릿** 구성 요소 필요).

## 구조

```
src/NotionCalendar/
├─ App.xaml(.cs)                  앱 진입점, 라이트 테마 고정
├─ MainWindow.xaml(.cs)           달력 격자 + 스와이프 + 우측 일정 패널
├─ Controls/DayCell.xaml(.cs)     날짜 칸 하나 (42개 재사용)
├─ Dialogs/ScheduleEditDialog     일정 추가·수정·삭제 다이얼로그
├─ Models/                        ScheduleItem, CalendarDay, 색상 팔레트
├─ Services/ScheduleStore.cs      JSON 저장/불러오기 + 날짜별 인덱스
├─ ViewModels/MainViewModel.cs    42칸 계산, 달 이동, 선택 상태
└─ Themes/Tokens.xaml             색상·라운드·버튼 스타일
```

### 스와이프 동작 방식

`MainWindow.xaml` 의 `SwipeHost` 가 `PointerPressed/Moved/Released` 로 좌우 끌림을 직접 처리합니다.
(WinUI 의 `ManipulationMode` 는 마우스 드래그에서 이벤트가 발생하지 않아, 터치가 없는 PC 에서
동작하지 않으므로 쓰지 않았습니다.)

- 8px 이상 움직여야 "스와이프"로 판정하고 그때 포인터를 캡처합니다.
  덕분에 살짝 흔들린 클릭은 날짜 선택으로 그대로 동작합니다.
- 끄는 동안 `CompositeTransform` 으로 격자가 손가락의 50%만큼 따라 움직이고 살짝 흐려집니다.
- 놓았을 때 **80px 이상 이동**했거나 **속도가 0.7px/ms 이상**이면 달을 넘기고,
  아니면 제자리로 되돌아갑니다.
- 넘길 때는 밀려나며 사라졌다가 반대편에서 들어오는 짧은 전환(약 0.3초)이 재생됩니다.

마우스 휠, `‹ ›` 버튼, `Ctrl+←/→`, `PageUp/PageDown` 으로도 같은 전환이 실행됩니다.

## 실행 파일

두 가지 형태로 만들어 뒀습니다. 둘 다 **Windows 11 / Windows 10(1809+) x64에서 아무것도 설치하지 않고** 돕니다.
.NET 8 런타임과 Windows App SDK 런타임이 모두 포함되어 있습니다.

### 1. 단일 파일 (배포용)

```
dist-portable\Calendar-Portable.exe    (73MB, 파일 하나)
```

이 파일 하나만 복사하면 됩니다. 처음 실행할 때 `%LOCALAPPDATA%\NotionCalendar\app` 에
한 번 풀고(약 2.7초) 캘린더를 띄웁니다. 그다음부터는 바로 실행됩니다(약 0.7초).
exe 안의 내용이 바뀌면 자동으로 다시 풉니다.

### 2. 폴더 (개발/디버깅용)

```
dist\NotionCalendar.exe                (폴더 전체 353개 파일, 162MB)
```

폴더째 복사해야 동작합니다. 압축 해제 단계가 없어 바로 뜹니다.

### 다시 만들기

```powershell
# 1) 앱 폴더 빌드
dotnet publish src\NotionCalendar -c Release -r win-x64 -o dist

# 2) 폴더를 압축해 런처에 심을 페이로드 만들기
Compress-Archive -Path dist\* -DestinationPath packaging\payload\app.zip -Force

# 3) 단일 파일 런처 빌드
dotnet publish src\Launcher -c Release -o dist-portable
```

ARM64 PC용은 1)의 `-r win-x64` 와 `src\Launcher\Launcher.csproj` 의
`RuntimeIdentifier` 를 모두 `win-arm64` 로 바꾸세요.

### 왜 런처가 따로 있나

WinUI 3 앱은 .NET 단일 파일(`PublishSingleFile`)로 만들 수 없습니다.
WinRT 클래스 활성화가 exe **옆에 실제 파일로 있는** DLL을 SxS 매니페스트로 찾기 때문에,
번들에서 임시 폴더로 풀리면 `CLASS_E_CLASSNOTAVAILABLE (0x80040111)` 로 죽습니다.
(Microsoft가 제공하는 단일 파일 지원 경로는 Windows App Runtime이 별도 설치된 환경 전용입니다.)

그래서 WinUI가 아닌 일반 .NET 프로그램인 [src/Launcher/](src/Launcher/) 가 단일 파일이 되고,
캘린더 앱 폴더를 zip으로 품고 다니다가 실행 시 풀어 줍니다.
