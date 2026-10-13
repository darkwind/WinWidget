# WinWidget

바탕화면에 CPU·메모리·디스크 IO를 수치와 최근 60초 그래프로 보여주는 Windows 위젯.

## 빌드

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

.NET Framework 4.8 기본 컴파일러를 사용하므로 별도 설치가 필요 없다. 결과물: `bin\WinWidget.exe`.

## 사용

- `bin\WinWidget.exe` 실행 → 주 모니터 우상단에 표시
- 왼쪽 드래그로 이동 (위치는 `%APPDATA%\WinWidget\settings.ini`에 저장)
- 위젯 또는 트레이 아이콘 우클릭: 위치 잠금 / Windows 시작 시 실행 / 종료

## 알려진 제약

- 배율(DPI)이 서로 다른 모니터로 옮기거나 실행 중 배율을 바꾸면 글자가 흐려진다. 위젯을 다시 실행하면 정상으로 돌아온다.
- 위젯이 실행 중이면 `bin\WinWidget.exe`가 잠겨 빌드가 실패한다. 트레이 메뉴에서 종료한 뒤 빌드한다.

## 테스트

```powershell
.\bin\WinWidget.Tests.exe              # 단위·통합 테스트
.\bin\WinWidget.Tests.exe dump         # 5초간 실측값 출력
.\bin\WinWidget.Tests.exe render a.png # 합성 데이터로 위젯 이미지 생성
```
