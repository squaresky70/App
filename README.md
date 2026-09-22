# 캘린더 (WinUI 3)

노션 스타일의 깔끔한 화이트 테마 데스크톱 캘린더입니다.

## 기능

| 기능 | 조작 |
| --- | --- |
| 일정 추가 | 우측 패널의 **＋ 일정 추가** 버튼, 날짜 칸 **더블클릭**, `Ctrl+N` |
| 여러 날 일정 | 입력 창에서 **시작일 / 종료일**을 따로 지정. 달력에 시작일부터 종료일까지 **하나의 막대**로 이어져 표시된다 |
| 공휴일 | 대한민국 관공서 공휴일을 **빨간 글자**로 표시. 날짜 숫자 옆에 이름(설날·추석·대체공휴일 등)이 붙는다 |
| 주요 일정 D-Day | 우측 일정 카드의 **☆** 를 눌러 지정하면 상단 "9월 2026" 옆에 `D-44 CFA 시험 등록 마감` 처럼 표시된다. **여러 개 지정 가능**하며 디데이가 가까운 순으로 우선순위를 매겨 **상단엔 3개까지** 보이고 나머지는 `+N` 으로 접힌다. 지난 일정은 앞으로 남은 일정 뒤로 밀린다. ★ 를 다시 누르면 해제 |
| 일정 수정 | 우측 패널의 일정 카드를 **클릭** → 값을 고치고 **저장** |
| 일정 삭제 | 카드 우측 **🗑 버튼**(확인 후 삭제), 또는 수정 창의 **삭제** 버튼 |
| 다음/이전 달 | **좌우 스와이프**(터치·마우스 드래그·터치패드), **마우스 휠**, `Ctrl+←`/`Ctrl+→`, `PageUp`/`PageDown` |
| 오늘로 이동 | **오늘** 버튼, `Ctrl+T` |
| 원하는 달로 바로 이동 | 상단 **월**을 누르면 1~12월 표, **연도**를 누르면 연도 표(‹ › 로 12년씩 넘김)가 뜬다. 칸을 누르면 그 달로 이동 |
| 주요 일정으로 이동 | 상단 **D-Day** 를 누르면 그 일정 날짜로 달력이 옮겨지고 선택된다 |
| 바탕화면 위젯 | 창 맨 위 "캘린더" 옆 **위젯 추가** 를 누르면 반투명 유리 달력 위젯이 뜬다. 음력·공휴일·일정이 보이고, 다른 창을 누르면 바탕화면 쪽으로 가라앉는다. 머리 부분을 끌어 옮기고 가장자리로 크기 조절, 날짜 **더블클릭** 시 앱이 그 날짜로 열린다. 오른쪽 아래 **투명도** 슬라이더(0~100%)로 배경 진하기를 조절한다(가장 투명해도 글자가 읽히도록 옅은 색은 남는다). 본 창을 닫아도 위젯은 남고, 켜 둔 채 끝내면 다음 실행 때 같은 자리에 다시 뜬다 |

일정에는 제목, 시작일/종료일, 하루 종일 여부, 시작/종료 시각, 색상(9가지), 메모를 넣을 수 있습니다.

### 여러 날에 걸친 일정이 그려지는 방식

구글 캘린더처럼 **주 단위로 레인(줄)을 배정**합니다. 한 일정은 그 주 안에서 항상 같은 레인을
차지하므로 칸이 바뀌어도 높이가 어긋나지 않고, 막대가 칸 끝까지 닿아 옆 칸과 그대로 이어집니다.

- 시작일 쪽만 왼쪽 모서리를, 종료일 쪽만 오른쪽 모서리를 둥글게 해서 하나의 긴 막대로 보입니다
- 제목은 시작일에, 그리고 주가 바뀌어 다음 줄로 넘어간 첫 칸에 다시 적습니다
- 긴 일정일수록 위 레인을 차지해 막대가 덜 끊깁니다
- 한 칸에 3줄까지만 보여주고 나머지는 `+N개 더보기` 로 접습니다

### 공휴일은 어디서 오나

인터넷이나 API 키 없이 [Services/KoreanHolidays.cs](src/NotionCalendar/Services/KoreanHolidays.cs) 가 직접 계산합니다.

- 신정·삼일절·어린이날·현충일·광복절·개천절·한글날·성탄절은 날짜가 고정
- 설날·추석·부처님오신날은 음력이라 `KoreanLunisolarCalendar` 로 그해 양력 날짜를 계산 (설날·추석은 앞뒤 하루씩 연휴)
- 대체공휴일은 「공휴일에 관한 법률 시행령」 규칙대로 채웁니다. 설날·추석 연휴는 **일요일**과 겹칠 때,
  나머지는 **토·일 또는 다른 공휴일**과 겹칠 때 다음 평일 하루를 더합니다(현충일·신정은 제외)
- 정부가 그때그때 정하는 **임시공휴일**만 계산으로 알 수 없어 코드에 직접 적어 뒀습니다

2025~2027년 계산 결과가 공식 공휴일 목록과 일치하는지 대조해 확인했습니다.

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

마우스 휠, `Ctrl+←/→`, `PageUp/PageDown` 으로도 같은 전환이 실행됩니다.

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
