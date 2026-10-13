# WinWidget — 바탕화면 시스템 모니터 위젯 설계

- 작성일: 2026-10-13
- 대상: 본인 PC 상시 모니터링용 (단일 사용자, Windows 11)

## 1. 목표와 성공 기준

바탕화면에 상시 떠 있는 작은 위젯으로 CPU, 메모리, 디스크 IO를 확인한다.

성공 기준:
- 수치가 작업 관리자와 일치한다 (CPU ±5%p, 메모리 ±0.2GB, 디스크 동일 자릿수).
- 위젯 자체 CPU 사용률이 평균 1% 미만, 메모리(Working Set) 40MB 미만이다.
- 별도 런타임/SDK 설치 없이 빌드·실행된다.
- 재부팅 후 자동 실행 시 마지막 위치에 나타난다.

## 2. 기술 스택

- C# WinForms, .NET Framework 4.8 (Windows 기본 탑재)
- 빌드: `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` (C# 5 문법 한도 — `$""` 보간 문자열, `?.`, 식 본문 멤버 사용 불가)
- 외부 라이브러리 없음

## 3. 화면

크기 약 240×200px, 테두리 없는 반투명 다크 패널(둥근 모서리), 1초 갱신.

```
┌──────────────────────────────┐
│ CPU        23%   ▁▂▃▅▃▂▁▂▃▄  │
│ MEM  9.8/31.9GB 31% ▃▃▃▃▄▄▄  │
│ DISK R 12.4 MB/s             │
│      W  3.1 MB/s ▁▁▅▂▁▁▂▁▁▁  │
└──────────────────────────────┘
```

- 각 항목: 라벨, 현재값, 최근 60초 스파크라인(채움 영역 + 라인).
- CPU/MEM 색: 70% 미만 기본(청록), 70% 이상 주황, 90% 이상 빨강.
- 디스크: 읽기/쓰기 2색 라인. Y축 최댓값 = 60초 창 내 최댓값(최소 1 MB/s).
- 단위 자동 변환: B/s, KB/s, MB/s, GB/s.

## 4. 창 동작

- 바탕화면 고정: `WS_EX_TOOLWINDOW`(작업표시줄·Alt+Tab 숨김), 활성화 시 `SetWindowPos(HWND_BOTTOM)`로 항상 맨 뒤 유지, `WS_EX_NOACTIVATE`.
- Win+D(바탕화면 보기) 후에도 보이도록 최소화 요청 무시 및 주기적 재표시.
- 왼쪽 드래그로 이동(위치 잠금 해제 시), 이동 종료 시 위치 저장.
- 트레이 아이콘 우클릭 메뉴: 위치 잠금(토글), Windows 시작 시 실행(토글), 종료.

## 5. 구성 요소

| 파일 | 책임 | 의존 |
|---|---|---|
| `src/Program.cs` | 진입점. 위젯 실행, 중복 실행 방지(Mutex). | WidgetForm |
| `tests/*.cs` | 콘솔용 `bin\WinWidget.Tests.exe`. 인자 없음 = 단위/통합 테스트, `dump` = 5초간 실측값 출력, `render <png>` = 합성 데이터로 위젯을 PNG로 렌더. (GUI exe는 콘솔 출력을 안정적으로 낼 수 없어 분리) | src 전체(Program.cs 제외) |
| `src/MetricsSampler.cs` | `Sample()` 호출 시 `Snapshot`(CpuPercent, MemUsedBytes, MemTotalBytes, DiskReadBps, DiskWriteBps; 실패 항목은 NaN) 반환. `RingBuffer`(고정 60개) 포함. | PerformanceCounter, GlobalMemoryStatusEx |
| `src/WidgetForm.cs` | 1초 타이머로 샘플링·버퍼 적재·다시 그리기, 창 스타일, 드래그, 트레이 메뉴. | MetricsSampler, Settings |
| `src/Settings.cs` | `%APPDATA%\WinWidget\settings.ini`의 X, Y, Locked 읽기/쓰기. HKCU `...\CurrentVersion\Run` 자동 실행 등록/해제. | 없음 |
| `build.ps1` | csc 호출로 `bin\WinWidget.exe` 생성 (`/target:winexe`). | csc.exe |

데이터 소스:
- CPU: `Processor Information(_Total)\% Processor Utility` (작업 관리자와 동일 기준). 생성 실패 시 `Processor(_Total)\% Processor Time`으로 대체. 100 초과 값은 100으로 자름.
- 메모리: `GlobalMemoryStatusEx` P/Invoke (`ullTotalPhys - ullAvailPhys`).
- 디스크: `PhysicalDisk(_Total)\Disk Read Bytes/sec`, `Disk Write Bytes/sec`.

## 6. 데이터 흐름

Timer(1s) → `MetricsSampler.Sample()` → 각 RingBuffer에 Push → `Invalidate()` → `OnPaint`에서 버퍼 읽어 그림.
모두 UI 스레드에서 동작 (샘플링 비용이 수 ms 이하라 별도 스레드 불필요).

## 7. 오류 처리

- 카운터 생성 실패: 해당 항목 NaN → 화면에 "N/A", 그래프 생략. 나머지는 정상 동작.
- 첫 샘플(카운터 기준값)은 버린다.
- 설정 파일 손상/누락: 기본 위치(주 모니터 우상단)로 시작.
- 저장된 위치가 현재 어느 화면에도 없으면 기본 위치로 이동.

## 8. 검증

- `bin\WinWidget.Tests.exe dump`: 5초간 1초마다 값 출력 → 작업 관리자와 비교.
- 실행 후 스크린샷으로 레이아웃 확인, Win+D·드래그·재실행 위치 복원·트레이 메뉴 수동 점검.
- 작업 관리자에서 위젯 자체 CPU/메모리 확인.

## 9. 범위 밖 (YAGNI)

드라이브별 디스크 분리, 네트워크/GPU, 테마·크기 설정 UI, 다국어, 설치 프로그램.
