# WinWidget Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 바탕화면에 붙어 CPU·메모리·디스크 IO를 수치와 60초 스파크라인으로 보여주는 경량 Windows 위젯(단일 exe)을 만든다.

**Architecture:** 순수 로직(RingBuffer, Format, Sparkline, Settings, Placement)과 OS 의존부(MetricsSampler, AutoStart, WidgetForm)를 파일 단위로 분리한다. 콘솔용 테스트 exe가 Program.cs를 뺀 모든 src를 함께 컴파일해 리플렉션으로 `*Tests` 클래스의 public static 메서드를 실행한다. WidgetForm은 partial class로 렌더링(`WidgetForm.cs`)과 바탕화면 동작(`WidgetForm.Desktop.cs`)을 나눈다.

**Tech Stack:** C# 5 / .NET Framework 4.8 WinForms, `csc.exe` (Framework64 v4.0.30319), PerformanceCounter, user32/kernel32 P/Invoke, PowerShell 빌드 스크립트.

**Spec:** `docs/superpowers/specs/2026-10-13-desktop-widget-design.md`

## Global Constraints

- 컴파일러: `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` — C# 5 한도. `$""`, `?.`, `nameof`, 식 본문 멤버, 자동 속성 초기화, `out var` 사용 금지.
- 소스 인코딩 UTF-8, csc에 `/codepage:65001` 전달 (한글 메뉴 문자열).
- 외부 라이브러리·NuGet 없음. 참조는 System, System.Drawing, System.Windows.Forms만.
- 갱신 주기 1초, 이력 60개.
- 색 임계값: 70% 미만 기본, 70% 이상 주황, 90% 이상 빨강.
- 디스크 그래프 Y축 최솟값 1 MB/s (1048576 B/s).
- 설정 파일 `%APPDATA%\WinWidget\settings.ini`, 자동 실행 HKCU `Software\Microsoft\Windows\CurrentVersion\Run` 값 이름 `WinWidget`.
- 성능 목표: 위젯 CPU 평균 1% 미만, Working Set 40MB 미만.

## Review Focus

1. **디스크 유휴(모든 값 0)** — 그래프가 0으로 나누지 않고 바닥에 평평한 선으로 그려져야 한다. → Task 1 `ScaleMax` 테스트, Task 4 `Sparkline` 테스트.
2. **모니터 구성 변경으로 저장 위치가 화면 밖** — 기본 위치(주 모니터 우상단)로 나타나야 한다. → Task 3 `Placement` 테스트.
3. **설정 파일 손상(숫자 아닌 값, 깨진 줄)** — 크래시 없이 해당 값만 기본값 사용. → Task 3 `Parse_IgnoresGarbage` 테스트.
4. **자동 실행 + 수동 실행으로 두 번 실행** — 두 번째 프로세스는 조용히 종료, 위젯은 하나만. → Task 5 Step 6 프로세스 수 확인.
5. **고DPI(125%/150%) 디스플레이** — 글자·그래프가 흐리거나 잘리지 않고 비율대로 커져야 한다. → Task 4 `S()` 스케일링, Task 6 수동 점검 항목.

---

## File Structure

| 파일 | 책임 |
|---|---|
| `build.ps1` | 위젯 exe(winexe)와 테스트 exe(exe) 빌드 |
| `.gitignore` | `bin/` 제외 |
| `src/RingBuffer.cs` | 고정 용량 double 순환 버퍼 |
| `src/Format.cs` | 단위 변환·퍼센트·임계 레벨·클램프·스케일 계산 (순수 함수) |
| `src/MetricsSampler.cs` | `Snapshot` 구조체, 성능 카운터·메모리 API 샘플링 |
| `src/Settings.cs` | ini 파싱/저장 |
| `src/Placement.cs` | 저장 위치 유효성 판단과 기본 위치 계산 |
| `src/AutoStart.cs` | HKCU Run 키 등록/해제 |
| `src/Sparkline.cs` | 버퍼 → 그래프 좌표 변환 (순수 함수) |
| `src/WidgetForm.cs` | 창 생성, 1초 타이머, GDI+ 렌더링 |
| `src/WidgetForm.Desktop.cs` | 바탕화면 고정, 드래그, 위치 저장, 트레이 메뉴 |
| `src/Program.cs` | 진입점, DPI 인식, 중복 실행 방지 |
| `tests/TestRunner.cs` | 테스트 탐색·실행, `dump`/`render` 모드, `Check` 단언 |
| `tests/*Tests.cs` | 영역별 테스트 |

---

### Task 1: 빌드 체계 + RingBuffer + Format

**Files:**
- Create: `build.ps1`, `.gitignore`, `src/Program.cs`(임시 스텁), `src/RingBuffer.cs`, `src/Format.cs`
- Test: `tests/TestRunner.cs`, `tests/RingBufferTests.cs`, `tests/FormatTests.cs`

**Interfaces:**
- Produces:
  - `RingBuffer(int capacity)`, `int Capacity`, `int Count`, `void Push(double)`, `double this[int i]` (0 = 가장 오래된 값), `double Max()` (NaN 무시, 비면 NaN)
  - `enum Level { Normal, Warn, Critical }`
  - `Format.Rate(double bps) : string`, `Format.Percent(double) : string`, `Format.Gb(double bytes) : string`, `Format.LevelOf(double percent) : Level`, `Format.ClampPercent(double) : double`, `Format.ScaleMax(double maxBps) : double`, `Format.MemPercent(double used, double total) : double`, `Format.OneMb` 상수
  - 테스트 단언: `Check.Eq(object expected, object actual)`, `Check.True(bool, string)`

- [ ] **Step 1: 저장소와 빌드 스크립트 생성**

```powershell
git init
```

`.gitignore`:
```
bin/
```

`build.ps1`:
```powershell
$ErrorActionPreference = 'Stop'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$root = $PSScriptRoot
$bin = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null

$common = @('/nologo', '/codepage:65001', '/optimize+',
    '/r:System.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll')
$lib = @(Get-ChildItem (Join-Path $root 'src\*.cs') | Where-Object { $_.Name -ne 'Program.cs' } | ForEach-Object { $_.FullName })
$tests = @(Get-ChildItem (Join-Path $root 'tests\*.cs') | ForEach-Object { $_.FullName })

& $csc @common /target:winexe "/out:$bin\WinWidget.exe" (Join-Path $root 'src\Program.cs') @lib
if ($LASTEXITCODE -ne 0) { throw 'WinWidget.exe build failed' }

& $csc @common /target:exe "/out:$bin\WinWidget.Tests.exe" @tests @lib
if ($LASTEXITCODE -ne 0) { throw 'WinWidget.Tests.exe build failed' }

Write-Host 'Build OK'
```

`src/Program.cs` (임시 스텁, Task 4에서 교체):
```csharp
using System;

namespace WinWidget
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
        }
    }
}
```

- [ ] **Step 2: 테스트 러너 작성**

`tests/TestRunner.cs`:
```csharp
using System;
using System.Linq;
using System.Reflection;

namespace WinWidget.Tests
{
    public static class Check
    {
        public static void Eq(object expected, object actual)
        {
            if (!Equals(expected, actual))
                throw new Exception("expected <" + expected + "> but was <" + actual + ">");
        }

        public static void True(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }

    static class TestRunner
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "dump") return Modes.Dump();
            if (args.Length > 1 && args[0] == "render") return Modes.Render(args[1]);

            int failed = 0, passed = 0;
            var methods = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.Name.EndsWith("Tests"))
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Where(m => m.GetParameters().Length == 0 && m.ReturnType == typeof(void))
                .OrderBy(m => m.DeclaringType.Name + "." + m.Name);

            foreach (MethodInfo m in methods)
            {
                string name = m.DeclaringType.Name + "." + m.Name;
                try
                {
                    m.Invoke(null, null);
                    passed++;
                    Console.WriteLine("PASS " + name);
                }
                catch (TargetInvocationException ex)
                {
                    failed++;
                    Console.WriteLine("FAIL " + name + ": " + ex.InnerException.Message);
                }
            }

            Console.WriteLine(failed == 0 ? "ALL PASSED (" + passed + ")" : failed + " FAILED, " + passed + " passed");
            return failed == 0 ? 0 : 1;
        }
    }

    static partial class Modes
    {
        public static int Dump()
        {
            Console.WriteLine("dump not implemented");
            return 1;
        }

        public static int Render(string path)
        {
            Console.WriteLine("render not implemented");
            return 1;
        }
    }
}
```

(`Modes`는 Task 2에서 Dump, Task 4에서 Render를 채운다. 그때 이 파일의 해당 메서드를 교체한다.)

- [ ] **Step 3: 실패하는 테스트 작성**

`tests/RingBufferTests.cs`:
```csharp
using System;

namespace WinWidget.Tests
{
    public static class RingBufferTests
    {
        public static void KeepsInsertionOrder()
        {
            var b = new RingBuffer(5);
            b.Push(1); b.Push(2); b.Push(3);
            Check.Eq(3, b.Count);
            Check.Eq(1.0, b[0]);
            Check.Eq(3.0, b[2]);
        }

        public static void DropsOldestWhenFull()
        {
            var b = new RingBuffer(3);
            for (int i = 1; i <= 5; i++) b.Push(i);
            Check.Eq(3, b.Count);
            Check.Eq(3.0, b[0]);
            Check.Eq(5.0, b[2]);
        }

        public static void MaxIgnoresNaN()
        {
            var b = new RingBuffer(4);
            Check.Eq(double.NaN, b.Max());
            b.Push(double.NaN); b.Push(2); b.Push(7); b.Push(double.NaN);
            Check.Eq(7.0, b.Max());
        }

        public static void IndexOutOfRangeThrows()
        {
            var b = new RingBuffer(3);
            b.Push(1);
            bool threw = false;
            try { double unused = b[1]; }
            catch (ArgumentOutOfRangeException) { threw = true; }
            Check.True(threw, "expected ArgumentOutOfRangeException");
        }
    }
}
```

`tests/FormatTests.cs`:
```csharp
namespace WinWidget.Tests
{
    public static class FormatTests
    {
        public static void RateScalesUnits()
        {
            Check.Eq("0 B/s", Format.Rate(0));
            Check.Eq("512 B/s", Format.Rate(512));
            Check.Eq("1.5 KB/s", Format.Rate(1536));
            Check.Eq("12.4 MB/s", Format.Rate(12.4 * 1048576));
            Check.Eq("2.0 GB/s", Format.Rate(2.0 * 1073741824));
            Check.Eq("0 B/s", Format.Rate(-5));
            Check.Eq("N/A", Format.Rate(double.NaN));
        }

        public static void PercentRounds()
        {
            Check.Eq("23%", Format.Percent(23.4));
            Check.Eq("24%", Format.Percent(23.6));
            Check.Eq("N/A", Format.Percent(double.NaN));
        }

        public static void GbOneDecimal()
        {
            Check.Eq("9.8", Format.Gb(9.8 * 1073741824));
            Check.Eq("N/A", Format.Gb(double.NaN));
        }

        public static void LevelThresholds()
        {
            Check.Eq(Level.Normal, Format.LevelOf(69.9));
            Check.Eq(Level.Warn, Format.LevelOf(70));
            Check.Eq(Level.Warn, Format.LevelOf(89.9));
            Check.Eq(Level.Critical, Format.LevelOf(90));
            Check.Eq(Level.Normal, Format.LevelOf(double.NaN));
        }

        public static void ClampPercentBounds()
        {
            Check.Eq(100.0, Format.ClampPercent(105));
            Check.Eq(0.0, Format.ClampPercent(-1));
            Check.Eq(42.0, Format.ClampPercent(42));
            Check.Eq(double.NaN, Format.ClampPercent(double.NaN));
        }

        public static void ScaleMaxHasOneMbFloor()
        {
            Check.Eq(1048576.0, Format.ScaleMax(0));
            Check.Eq(1048576.0, Format.ScaleMax(double.NaN));
            Check.Eq(5242880.0, Format.ScaleMax(5242880));
        }

        public static void MemPercentHandlesZeroTotal()
        {
            Check.Eq(25.0, Format.MemPercent(8, 32));
            Check.Eq(double.NaN, Format.MemPercent(8, 0));
            Check.Eq(double.NaN, Format.MemPercent(double.NaN, 32));
        }
    }
}
```

- [ ] **Step 4: 빌드해서 실패 확인**

Run: `powershell -ExecutionPolicy Bypass -File .\build.ps1`
Expected: 컴파일 오류 — `RingBuffer`, `Format`, `Level` 형식을 찾을 수 없음 (`error CS0246`)

- [ ] **Step 5: 최소 구현**

`src/RingBuffer.cs`:
```csharp
using System;

namespace WinWidget
{
    public sealed class RingBuffer
    {
        private readonly double[] _items;
        private int _start;
        private int _count;

        public RingBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException("capacity");
            _items = new double[capacity];
        }

        public int Capacity { get { return _items.Length; } }

        public int Count { get { return _count; } }

        public void Push(double value)
        {
            _items[(_start + _count) % _items.Length] = value;
            if (_count < _items.Length) _count++;
            else _start = (_start + 1) % _items.Length;
        }

        // 0 = oldest sample
        public double this[int i]
        {
            get
            {
                if (i < 0 || i >= _count) throw new ArgumentOutOfRangeException("i");
                return _items[(_start + i) % _items.Length];
            }
        }

        public double Max()
        {
            double max = double.NaN;
            for (int i = 0; i < _count; i++)
            {
                double v = this[i];
                if (double.IsNaN(v)) continue;
                if (double.IsNaN(max) || v > max) max = v;
            }
            return max;
        }
    }
}
```

`src/Format.cs`:
```csharp
using System;
using System.Globalization;

namespace WinWidget
{
    public enum Level { Normal, Warn, Critical }

    public static class Format
    {
        public const double OneMb = 1048576;
        private const double OneGb = 1073741824;
        private static readonly string[] RateUnits = { "B/s", "KB/s", "MB/s", "GB/s" };

        public static string Rate(double bps)
        {
            if (double.IsNaN(bps)) return "N/A";
            double v = Math.Max(0, bps);
            int unit = 0;
            while (v >= 1024 && unit < RateUnits.Length - 1)
            {
                v /= 1024;
                unit++;
            }
            return v.ToString(unit == 0 ? "0" : "0.0", CultureInfo.InvariantCulture) + " " + RateUnits[unit];
        }

        public static string Percent(double percent)
        {
            if (double.IsNaN(percent)) return "N/A";
            return Math.Round(percent, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "%";
        }

        public static string Gb(double bytes)
        {
            if (double.IsNaN(bytes)) return "N/A";
            return (bytes / OneGb).ToString("0.0", CultureInfo.InvariantCulture);
        }

        public static Level LevelOf(double percent)
        {
            if (double.IsNaN(percent) || percent < 70) return Level.Normal;
            return percent < 90 ? Level.Warn : Level.Critical;
        }

        public static double ClampPercent(double percent)
        {
            if (double.IsNaN(percent)) return double.NaN;
            return Math.Min(100, Math.Max(0, percent));
        }

        public static double ScaleMax(double maxBps)
        {
            if (double.IsNaN(maxBps) || maxBps < OneMb) return OneMb;
            return maxBps;
        }

        public static double MemPercent(double used, double total)
        {
            if (double.IsNaN(used) || double.IsNaN(total) || total <= 0) return double.NaN;
            return used / total * 100;
        }
    }
}
```

- [ ] **Step 6: 빌드 + 테스트 통과 확인**

Run: `powershell -ExecutionPolicy Bypass -File .\build.ps1; if ($?) { .\bin\WinWidget.Tests.exe }`
Expected: `Build OK`, 11개 PASS, `ALL PASSED (11)`

- [ ] **Step 7: Commit**

```bash
git add .gitignore build.ps1 src tests docs
git commit -m "feat: add build script, RingBuffer and Format helpers"
```

---

### Task 2: MetricsSampler + dump 모드

**Files:**
- Create: `src/MetricsSampler.cs`
- Modify: `tests/TestRunner.cs` (`Modes.Dump` 교체)
- Test: `tests/MetricsSamplerTests.cs`

**Interfaces:**
- Consumes: `Format.ClampPercent`, `Format.Percent`, `Format.Gb`, `Format.Rate`, `Format.MemPercent`
- Produces:
  - `struct Snapshot { double CpuPercent, MemUsedBytes, MemTotalBytes, DiskReadBps, DiskWriteBps; static Snapshot Empty }` (실패 항목은 NaN)
  - `sealed class MetricsSampler : IDisposable { MetricsSampler(); Snapshot Sample(); void Dispose(); }` — 생성자에서 카운터를 한 번 읽어 기준값을 잡는다(첫 샘플 폐기).

- [ ] **Step 1: 실패하는 테스트 작성**

`tests/MetricsSamplerTests.cs`:
```csharp
using System.Threading;

namespace WinWidget.Tests
{
    public static class MetricsSamplerTests
    {
        public static void EmptySnapshotIsAllNaN()
        {
            Snapshot s = Snapshot.Empty;
            Check.Eq(double.NaN, s.CpuPercent);
            Check.Eq(double.NaN, s.MemUsedBytes);
            Check.Eq(double.NaN, s.MemTotalBytes);
            Check.Eq(double.NaN, s.DiskReadBps);
            Check.Eq(double.NaN, s.DiskWriteBps);
        }

        public static void SampleReturnsPlausibleValues()
        {
            using (var sampler = new MetricsSampler())
            {
                Thread.Sleep(1000);
                Snapshot s = sampler.Sample();
                Check.True(s.CpuPercent >= 0 && s.CpuPercent <= 100, "cpu out of range: " + s.CpuPercent);
                Check.True(s.MemTotalBytes > Format.OneMb * 1024, "mem total too small: " + s.MemTotalBytes);
                Check.True(s.MemUsedBytes > 0 && s.MemUsedBytes <= s.MemTotalBytes, "mem used invalid: " + s.MemUsedBytes);
                Check.True(s.DiskReadBps >= 0, "disk read invalid: " + s.DiskReadBps);
                Check.True(s.DiskWriteBps >= 0, "disk write invalid: " + s.DiskWriteBps);
            }
        }
    }
}
```

(NaN은 모든 비교에서 false이므로 위 `>=` 검사는 NaN도 실패로 잡는다.)

- [ ] **Step 2: 빌드해서 실패 확인**

Run: `powershell -ExecutionPolicy Bypass -File .\build.ps1`
Expected: `error CS0246: 'Snapshot'` / `'MetricsSampler'` 형식을 찾을 수 없음

- [ ] **Step 3: 구현**

`src/MetricsSampler.cs`:
```csharp
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinWidget
{
    public struct Snapshot
    {
        public double CpuPercent;
        public double MemUsedBytes;
        public double MemTotalBytes;
        public double DiskReadBps;
        public double DiskWriteBps;

        public static Snapshot Empty
        {
            get
            {
                Snapshot s;
                s.CpuPercent = double.NaN;
                s.MemUsedBytes = double.NaN;
                s.MemTotalBytes = double.NaN;
                s.DiskReadBps = double.NaN;
                s.DiskWriteBps = double.NaN;
                return s;
            }
        }
    }

    public sealed class MetricsSampler : IDisposable
    {
        private readonly PerformanceCounter _cpu;
        private readonly PerformanceCounter _diskRead;
        private readonly PerformanceCounter _diskWrite;

        public MetricsSampler()
        {
            // "% Processor Utility" matches Task Manager; older systems only have "% Processor Time".
            _cpu = TryCreate("Processor Information", "% Processor Utility", "_Total")
                ?? TryCreate("Processor", "% Processor Time", "_Total");
            _diskRead = TryCreate("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
            _diskWrite = TryCreate("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
        }

        public Snapshot Sample()
        {
            Snapshot s = Snapshot.Empty;
            s.CpuPercent = Format.ClampPercent(Read(_cpu));
            s.DiskReadBps = Read(_diskRead);
            s.DiskWriteBps = Read(_diskWrite);

            var mem = new MEMORYSTATUSEX();
            mem.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            if (GlobalMemoryStatusEx(ref mem))
            {
                s.MemTotalBytes = mem.ullTotalPhys;
                s.MemUsedBytes = mem.ullTotalPhys - mem.ullAvailPhys;
            }
            return s;
        }

        public void Dispose()
        {
            if (_cpu != null) _cpu.Dispose();
            if (_diskRead != null) _diskRead.Dispose();
            if (_diskWrite != null) _diskWrite.Dispose();
        }

        // The first NextValue() of a rate counter is always 0, so reading it here discards that sample.
        private static PerformanceCounter TryCreate(string category, string counter, string instance)
        {
            try
            {
                var c = new PerformanceCounter(category, counter, instance, true);
                c.NextValue();
                return c;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static double Read(PerformanceCounter counter)
        {
            if (counter == null) return double.NaN;
            try
            {
                return counter.NextValue();
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);
    }
}
```

- [ ] **Step 4: dump 모드 구현**

`tests/TestRunner.cs`에서 `Modes.Dump()` 본문을 다음으로 교체하고, 파일 상단에 `using System.Threading;`을 추가한다:
```csharp
        public static int Dump()
        {
            using (var sampler = new MetricsSampler())
            {
                for (int i = 0; i < 5; i++)
                {
                    Thread.Sleep(1000);
                    Snapshot s = sampler.Sample();
                    Console.WriteLine(string.Format("CPU {0,5}  MEM {1}/{2}GB {3,4}  R {4,11}  W {5,11}",
                        Format.Percent(s.CpuPercent),
                        Format.Gb(s.MemUsedBytes), Format.Gb(s.MemTotalBytes),
                        Format.Percent(Format.MemPercent(s.MemUsedBytes, s.MemTotalBytes)),
                        Format.Rate(s.DiskReadBps), Format.Rate(s.DiskWriteBps)));
                }
            }
            return 0;
        }
```

- [ ] **Step 5: 테스트 + dump 확인**

Run: `powershell -ExecutionPolicy Bypass -File .\build.ps1; if ($?) { .\bin\WinWidget.Tests.exe; .\bin\WinWidget.Tests.exe dump }`
Expected: `ALL PASSED (13)`, 이어서 5줄의 `CPU  12%  MEM 9.8/31.9GB  31%  R  1.2 MB/s  W 300 KB/s` 형태 출력 (N/A 없음). 같은 시점의 작업 관리자 값과 CPU ±5%p, 메모리 ±0.2GB 이내인지 눈으로 비교.

- [ ] **Step 6: Commit**

```bash
git add src/MetricsSampler.cs tests
git commit -m "feat: sample CPU, memory and disk IO via performance counters"
```

---

### Task 3: Settings + Placement + AutoStart

**Files:**
- Create: `src/Settings.cs`, `src/Placement.cs`, `src/AutoStart.cs`
- Test: `tests/SettingsTests.cs`, `tests/PlacementTests.cs`, `tests/AutoStartTests.cs`

**Interfaces:**
- Produces:
  - `sealed class Settings { int? X; int? Y; bool Locked; static string DefaultPath; static Settings Parse(string); string Serialize(); static Settings Load(string path); void Save(string path); }` — Load/Save는 IO 예외를 삼키고 기본값/무시로 처리
  - `static class Placement { const int Margin = 16; static Point Resolve(int? x, int? y, Size size, Rectangle[] workingAreas, Rectangle primary); }` — 저장 위치가 어떤 작업 영역과 가로·세로 각각 절반 이상 겹치면 그대로, 아니면 `(primary.Right - size.Width - 16, primary.Top + 16)`
  - `static class AutoStart { const string RunKey; const string AppName = "WinWidget"; static bool IsEnabled(string keyPath, string name); static void Set(string keyPath, string name, string exePath, bool enabled); }`

- [ ] **Step 1: 실패하는 테스트 작성**

`tests/SettingsTests.cs`:
```csharp
using System;
using System.IO;

namespace WinWidget.Tests
{
    public static class SettingsTests
    {
        public static void ParseReadsValues()
        {
            Settings s = Settings.Parse("X=100\r\nY=-200\nLocked=true");
            Check.Eq((int?)100, s.X);
            Check.Eq((int?)-200, s.Y);
            Check.Eq(true, s.Locked);
        }

        public static void ParseIgnoresGarbage()
        {
            Settings s = Settings.Parse("X=abc\n???\n=5\nLocked=maybe\nY=5");
            Check.Eq(null, s.X);
            Check.Eq((int?)5, s.Y);
            Check.Eq(false, s.Locked);
        }

        public static void ParseNullGivesDefaults()
        {
            Settings s = Settings.Parse(null);
            Check.Eq(null, s.X);
            Check.Eq(false, s.Locked);
        }

        public static void SerializeRoundTrips()
        {
            var s = new Settings();
            s.X = 10; s.Y = 20; s.Locked = true;
            Settings r = Settings.Parse(s.Serialize());
            Check.Eq((int?)10, r.X);
            Check.Eq((int?)20, r.Y);
            Check.Eq(true, r.Locked);
        }

        public static void LoadMissingFileGivesDefaults()
        {
            string path = Path.Combine(Path.GetTempPath(), "WinWidgetTests-" + Guid.NewGuid().ToString("N"), "settings.ini");
            Settings s = Settings.Load(path);
            Check.Eq(null, s.X);
        }

        public static void SaveCreatesDirectoryAndLoads()
        {
            string dir = Path.Combine(Path.GetTempPath(), "WinWidgetTests-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(dir, "settings.ini");
            try
            {
                var s = new Settings();
                s.X = 7; s.Y = 8;
                s.Save(path);
                Settings r = Settings.Load(path);
                Check.Eq((int?)7, r.X);
                Check.Eq((int?)8, r.Y);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
```

`tests/PlacementTests.cs`:
```csharp
using System.Drawing;

namespace WinWidget.Tests
{
    public static class PlacementTests
    {
        private static readonly Size WidgetSize = new Size(240, 164);
        private static readonly Rectangle Primary = new Rectangle(0, 0, 1920, 1040);
        private static readonly Rectangle Secondary = new Rectangle(1920, 0, 1920, 1040);

        public static void NoSavedPositionUsesTopRight()
        {
            Point p = Placement.Resolve(null, null, WidgetSize, new[] { Primary }, Primary);
            Check.Eq(new Point(1920 - 240 - 16, 16), p);
        }

        public static void VisiblePositionIsKept()
        {
            Point p = Placement.Resolve(100, 200, WidgetSize, new[] { Primary }, Primary);
            Check.Eq(new Point(100, 200), p);
        }

        public static void OffScreenPositionFallsBack()
        {
            Point p = Placement.Resolve(2500, 100, WidgetSize, new[] { Primary }, Primary);
            Check.Eq(new Point(1664, 16), p);
        }

        public static void MostlyHiddenPositionFallsBack()
        {
            // only 40px of 240px width visible
            Point p = Placement.Resolve(1880, 100, WidgetSize, new[] { Primary }, Primary);
            Check.Eq(new Point(1664, 16), p);
        }

        public static void PositionOnSecondMonitorIsKept()
        {
            Point p = Placement.Resolve(2500, 100, WidgetSize, new[] { Primary, Secondary }, Primary);
            Check.Eq(new Point(2500, 100), p);
        }
    }
}
```

`tests/AutoStartTests.cs`:
```csharp
using Microsoft.Win32;

namespace WinWidget.Tests
{
    public static class AutoStartTests
    {
        private const string TestRoot = @"Software\WinWidgetTests";
        private const string TestKey = TestRoot + @"\Run";

        public static void SetEnablesAndDisables()
        {
            try
            {
                Check.Eq(false, AutoStart.IsEnabled(TestKey, "WinWidget"));
                AutoStart.Set(TestKey, "WinWidget", @"C:\Apps\WinWidget.exe", true);
                Check.Eq(true, AutoStart.IsEnabled(TestKey, "WinWidget"));
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(TestKey))
                {
                    Check.Eq("\"C:\\Apps\\WinWidget.exe\"", key.GetValue("WinWidget"));
                }
                AutoStart.Set(TestKey, "WinWidget", @"C:\Apps\WinWidget.exe", false);
                Check.Eq(false, AutoStart.IsEnabled(TestKey, "WinWidget"));
            }
            finally
            {
                Registry.CurrentUser.DeleteSubKeyTree(TestRoot, false);
            }
        }
    }
}
```

- [ ] **Step 2: 빌드해서 실패 확인**

Run: `powershell -ExecutionPolicy Bypass -File .\build.ps1`
Expected: `error CS0246` — `Settings`, `Placement`, `AutoStart` 없음

- [ ] **Step 3: 구현**

`src/Settings.cs`:
```csharp
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace WinWidget
{
    public sealed class Settings
    {
        public int? X;
        public int? Y;
        public bool Locked;

        public static string DefaultPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WinWidget", "settings.ini");
            }
        }

        public static Settings Parse(string text)
        {
            var s = new Settings();
            if (text == null) return s;

            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                int n;
                bool b;
                if (key == "X" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) s.X = n;
                else if (key == "Y" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) s.Y = n;
                else if (key == "Locked" && bool.TryParse(value, out b)) s.Locked = b;
            }
            return s;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            if (X.HasValue) sb.Append("X=").Append(X.Value.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            if (Y.HasValue) sb.Append("Y=").Append(Y.Value.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            sb.Append("Locked=").Append(Locked ? "true" : "false").Append("\r\n");
            return sb.ToString();
        }

        public static Settings Load(string path)
        {
            try
            {
                return File.Exists(path) ? Parse(File.ReadAllText(path)) : new Settings();
            }
            catch (IOException)
            {
                return new Settings();
            }
            catch (UnauthorizedAccessException)
            {
                return new Settings();
            }
        }

        // A failed save only loses the position; the widget keeps running.
        public void Save(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, Serialize());
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
```

`src/Placement.cs`:
```csharp
using System.Drawing;

namespace WinWidget
{
    public static class Placement
    {
        public const int Margin = 16;

        public static Point Resolve(int? x, int? y, Size size, Rectangle[] workingAreas, Rectangle primary)
        {
            if (x.HasValue && y.HasValue)
            {
                var rect = new Rectangle(x.Value, y.Value, size.Width, size.Height);
                foreach (Rectangle area in workingAreas)
                {
                    Rectangle hit = Rectangle.Intersect(area, rect);
                    if (hit.Width * 2 >= size.Width && hit.Height * 2 >= size.Height) return rect.Location;
                }
            }
            return new Point(primary.Right - size.Width - Margin, primary.Top + Margin);
        }
    }
}
```

`src/AutoStart.cs`:
```csharp
using Microsoft.Win32;

namespace WinWidget
{
    public static class AutoStart
    {
        public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string AppName = "WinWidget";

        public static bool IsEnabled(string keyPath, string name)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath))
            {
                return key != null && key.GetValue(name) != null;
            }
        }

        public static void Set(string keyPath, string name, string exePath, bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
            {
                if (enabled) key.SetValue(name, "\"" + exePath + "\"");
                else key.DeleteValue(name, false);
            }
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `powershell -ExecutionPolicy Bypass -File .\build.ps1; if ($?) { .\bin\WinWidget.Tests.exe }`
Expected: `ALL PASSED (25)`

- [ ] **Step 5: Commit**

```bash
git add src/Settings.cs src/Placement.cs src/AutoStart.cs tests
git commit -m "feat: add settings persistence, placement and autostart"
```

---

### Task 4: Sparkline + WidgetForm 렌더링 + Program

**Files:**
- Create: `src/Sparkline.cs`, `src/WidgetForm.cs`
- Modify: `src/Program.cs` (스텁 교체), `tests/TestRunner.cs` (`Modes.Render` 교체)
- Test: `tests/SparklineTests.cs`

**Interfaces:**
- Consumes: `RingBuffer`, `Format.*`, `Level`, `Snapshot`, `MetricsSampler`, `Placement.Resolve`
- Produces:
  - `static class Sparkline { static PointF[] Points(RingBuffer series, double max, RectangleF area); }` — 최신값이 `area.Right`에 오도록 오른쪽 정렬, x 간격 `area.Width / (Capacity - 1)`, y는 `area.Bottom - clamp(v/max, 0, 1) * area.Height`, NaN 건너뜀, `max <= 0`이나 NaN이면 빈 배열
  - `partial class WidgetForm : Form { WidgetForm(); internal void Push(Snapshot s); partial void InitDesktop(); partial void OnTickDesktop(); partial void DisposeDesktop(); }` — Task 5가 partial 메서드를 구현한다

- [ ] **Step 1: 실패하는 테스트 작성**

`tests/SparklineTests.cs`:
```csharp
using System.Drawing;

namespace WinWidget.Tests
{
    public static class SparklineTests
    {
        private static readonly RectangleF Area = new RectangleF(0, 0, 59, 20);

        public static void PointsAreRightAligned()
        {
            var b = new RingBuffer(60);
            b.Push(0); b.Push(100);
            PointF[] pts = Sparkline.Points(b, 100, Area);
            Check.Eq(2, pts.Length);
            Check.Eq(new PointF(58, 20), pts[0]);
            Check.Eq(new PointF(59, 0), pts[1]);
        }

        public static void ValuesAboveMaxAreClampedToTop()
        {
            var b = new RingBuffer(60);
            b.Push(200);
            Check.Eq(0f, Sparkline.Points(b, 100, Area)[0].Y);
        }

        public static void NaNIsSkipped()
        {
            var b = new RingBuffer(60);
            b.Push(1); b.Push(double.NaN); b.Push(1);
            Check.Eq(2, Sparkline.Points(b, 100, Area).Length);
        }

        public static void ZeroMaxGivesNoPoints()
        {
            var b = new RingBuffer(60);
            b.Push(0);
            Check.Eq(0, Sparkline.Points(b, 0, Area).Length);
            Check.Eq(0, Sparkline.Points(b, double.NaN, Area).Length);
        }

        public static void IdleDiskDrawsFlatBottomLine()
        {
            var b = new RingBuffer(60);
            b.Push(0); b.Push(0);
            PointF[] pts = Sparkline.Points(b, Format.ScaleMax(b.Max()), Area);
            Check.Eq(20f, pts[0].Y);
            Check.Eq(20f, pts[1].Y);
        }
    }
}
```

- [ ] **Step 2: 빌드해서 실패 확인**

Run: `powershell -ExecutionPolicy Bypass -File .\build.ps1`
Expected: `error CS0103: 'Sparkline' 이름이 현재 컨텍스트에 없습니다` 또는 `CS0246`

- [ ] **Step 3: Sparkline 구현**

`src/Sparkline.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Drawing;

namespace WinWidget
{
    public static class Sparkline
    {
        public static PointF[] Points(RingBuffer series, double max, RectangleF area)
        {
            var points = new List<PointF>(series.Count);
            if (double.IsNaN(max) || max <= 0) return points.ToArray();

            float step = area.Width / (series.Capacity - 1);
            for (int i = 0; i < series.Count; i++)
            {
                double v = series[i];
                if (double.IsNaN(v)) continue;
                double ratio = Math.Min(1, Math.Max(0, v / max));
                float x = area.Right - (series.Count - 1 - i) * step;
                float y = area.Bottom - (float)(ratio * area.Height);
                points.Add(new PointF(x, y));
            }
            return points.ToArray();
        }
    }
}
```

- [ ] **Step 4: Sparkline 테스트 통과 확인**

Run: `powershell -ExecutionPolicy Bypass -File .\build.ps1; if ($?) { .\bin\WinWidget.Tests.exe }`
Expected: `ALL PASSED (30)`

- [ ] **Step 5: WidgetForm 렌더링 구현**

`src/WidgetForm.cs`:
```csharp
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace WinWidget
{
    public sealed partial class WidgetForm : Form
    {
        private const int History = 60;
        private static readonly Color BgColor = Color.FromArgb(26, 32, 44);
        private static readonly Color TrackColor = Color.FromArgb(18, 255, 255, 255);
        private static readonly Color LabelColor = Color.FromArgb(160, 174, 192);
        private static readonly Color ValueColor = Color.FromArgb(237, 242, 247);
        private static readonly Color NormalColor = Color.FromArgb(79, 209, 197);
        private static readonly Color WarnColor = Color.FromArgb(246, 173, 85);
        private static readonly Color CriticalColor = Color.FromArgb(245, 101, 101);
        private static readonly Color ReadColor = Color.FromArgb(99, 179, 237);
        private static readonly Color WriteColor = Color.FromArgb(183, 148, 244);

        private readonly MetricsSampler _sampler = new MetricsSampler();
        private readonly RingBuffer _cpu = new RingBuffer(History);
        private readonly RingBuffer _mem = new RingBuffer(History);
        private readonly RingBuffer _read = new RingBuffer(History);
        private readonly RingBuffer _write = new RingBuffer(History);
        private readonly Timer _timer = new Timer();
        private readonly Font _labelFont = new Font("Segoe UI", 8.5f);
        private readonly Font _valueFont = new Font("Segoe UI Semibold", 9.5f);
        private readonly float _scale;
        private Snapshot _last = Snapshot.Empty;

        public WidgetForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = BgColor;
            Opacity = 0.88;
            DoubleBuffered = true;
            Text = "WinWidget";

            using (Graphics g = CreateGraphics()) _scale = g.DpiX / 96f;
            ClientSize = new Size(S(240), S(164));
            using (GraphicsPath path = RoundedRect(new Rectangle(Point.Empty, ClientSize), S(10)))
                Region = new Region(path);

            Rectangle primary = Screen.PrimaryScreen.WorkingArea;
            Location = Placement.Resolve(null, null, Size, new[] { primary }, primary);

            InitDesktop();

            _timer.Interval = 1000;
            _timer.Tick += OnTick;
            _timer.Start();
        }

        partial void InitDesktop();
        partial void OnTickDesktop();
        partial void DisposeDesktop();

        internal void Push(Snapshot s)
        {
            _last = s;
            _cpu.Push(s.CpuPercent);
            _mem.Push(Format.MemPercent(s.MemUsedBytes, s.MemTotalBytes));
            _read.Push(s.DiskReadBps);
            _write.Push(s.DiskWriteBps);
            Invalidate();
        }

        private void OnTick(object sender, EventArgs e)
        {
            Push(_sampler.Sample());
            OnTickDesktop();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            float pad = S(12);
            float rowHeight = S(48);
            float width = ClientSize.Width - 2 * pad;
            double memPercent = Format.MemPercent(_last.MemUsedBytes, _last.MemTotalBytes);

            DrawRow(g, pad, pad, width, "CPU",
                Format.Percent(_last.CpuPercent),
                ColorFor(_last.CpuPercent), _cpu, 100, null, Color.Empty);

            DrawRow(g, pad, pad + rowHeight, width, "MEM",
                Format.Gb(_last.MemUsedBytes) + "/" + Format.Gb(_last.MemTotalBytes) + "GB  " + Format.Percent(memPercent),
                ColorFor(memPercent), _mem, 100, null, Color.Empty);

            double diskMax = Format.ScaleMax(MaxOf(_read.Max(), _write.Max()));
            DrawRow(g, pad, pad + 2 * rowHeight, width, "DISK",
                "R " + Format.Rate(_last.DiskReadBps) + "   W " + Format.Rate(_last.DiskWriteBps),
                ReadColor, _read, diskMax, _write, WriteColor);
        }

        private void DrawRow(Graphics g, float x, float y, float width, string label, string value,
            Color color, RingBuffer series, double max, RingBuffer series2, Color color2)
        {
            using (var labelBrush = new SolidBrush(LabelColor))
                g.DrawString(label, _labelFont, labelBrush, x, y);

            using (var valueBrush = new SolidBrush(ValueColor))
            using (var format = new StringFormat())
            {
                format.Alignment = StringAlignment.Far;
                g.DrawString(value, _valueFont, valueBrush, new RectangleF(x, y - S(1), width, S(20)), format);
            }

            var area = new RectangleF(x, y + S(20), width, S(20));
            using (var track = new SolidBrush(TrackColor))
                g.FillRectangle(track, area);

            DrawSeries(g, series, max, area, color, true);
            if (series2 != null) DrawSeries(g, series2, max, area, color2, false);
        }

        private void DrawSeries(Graphics g, RingBuffer series, double max, RectangleF area, Color color, bool fill)
        {
            PointF[] points = Sparkline.Points(series, max, area);
            if (points.Length < 2) return;

            if (fill)
            {
                var polygon = new PointF[points.Length + 2];
                points.CopyTo(polygon, 0);
                polygon[points.Length] = new PointF(points[points.Length - 1].X, area.Bottom);
                polygon[points.Length + 1] = new PointF(points[0].X, area.Bottom);
                using (var brush = new SolidBrush(Color.FromArgb(60, color)))
                    g.FillPolygon(brush, polygon);
            }

            using (var pen = new Pen(color, 1.5f * _scale))
            {
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, points);
            }
        }

        private static Color ColorFor(double percent)
        {
            switch (Format.LevelOf(percent))
            {
                case Level.Critical: return CriticalColor;
                case Level.Warn: return WarnColor;
                default: return NormalColor;
            }
        }

        private static double MaxOf(double a, double b)
        {
            if (double.IsNaN(a)) return b;
            if (double.IsNaN(b)) return a;
            return Math.Max(a, b);
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private int S(float value)
        {
            return (int)Math.Round(value * _scale);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                _sampler.Dispose();
                _labelFont.Dispose();
                _valueFont.Dispose();
                DisposeDesktop();
            }
            base.Dispose(disposing);
        }
    }
}
```

- [ ] **Step 6: Program 교체**

`src/Program.cs`:
```csharp
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace WinWidget
{
    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, @"Local\WinWidget.SingleInstance", out createdNew))
            {
                if (!createdNew) return;

                SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new WidgetForm());
            }
        }
    }
}
```

- [ ] **Step 7: render 모드 구현**

`tests/TestRunner.cs`에서 `Modes.Render(string path)` 본문을 다음으로 교체하고, 상단에 `using System.Drawing;`, `using System.Drawing.Imaging;`을 추가한다:
```csharp
        // Feeds 60 synthetic samples (CPU ramps into warn/critical, disk bursts) and saves the widget as PNG.
        public static int Render(string path)
        {
            using (var form = new WidgetForm())
            {
                for (int i = 0; i < 60; i++)
                {
                    Snapshot s = Snapshot.Empty;
                    s.CpuPercent = 20 + 75.0 * i / 59;
                    s.MemTotalBytes = 32.0 * 1073741824;
                    s.MemUsedBytes = (10 + 3 * Math.Sin(i / 6.0)) * 1073741824;
                    s.DiskReadBps = (i % 15 == 0 ? 40 : 2) * Format.OneMb;
                    s.DiskWriteBps = (i % 20 == 5 ? 25 : 0.5) * Format.OneMb;
                    form.Push(s);
                }
                using (var bmp = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bmp, new Rectangle(Point.Empty, form.Size));
                    bmp.Save(path, ImageFormat.Png);
                }
            }
            Console.WriteLine("rendered " + path);
            return 0;
        }
```

- [ ] **Step 8: 테스트 + 렌더 결과 확인**

Run: `powershell -ExecutionPolicy Bypass -File .\build.ps1; if ($?) { .\bin\WinWidget.Tests.exe; .\bin\WinWidget.Tests.exe render "$env:TEMP\winwidget-render.png" }`
Expected: `ALL PASSED (30)`, `rendered ...png`. PNG를 열어 확인:
- CPU 값 `95%` 빨강, 그래프가 왼쪽 아래에서 오른쪽 위로 상승
- MEM `x.x/32.0GB  xx%` 청록, 물결 모양 그래프
- DISK `R 2.0 MB/s   W 512.0 KB/s`, 파랑/보라 두 선에 스파이크
- 글자가 잘리거나 겹치지 않음

- [ ] **Step 9: 실제 실행 확인**

Run: `Start-Process .\bin\WinWidget.exe; Start-Sleep 3; Get-Process WinWidget | Select-Object Id, CPU, WorkingSet64`
Expected: 프로세스 1개, 주 모니터 우상단에 위젯 표시(이 단계에서는 아직 일반 창이라 다른 창 위에 보일 수 있음). 확인 후 `Stop-Process -Name WinWidget`.

- [ ] **Step 10: Commit**

```bash
git add src tests
git commit -m "feat: render CPU, memory and disk sparklines in widget window"
```

---

### Task 5: 바탕화면 고정 + 드래그 + 위치 저장 + 트레이 메뉴

**Files:**
- Create: `src/WidgetForm.Desktop.cs`

**Interfaces:**
- Consumes: `WidgetForm`의 partial 메서드 `InitDesktop`, `OnTickDesktop`, `DisposeDesktop`; `Settings`, `Placement`, `AutoStart`
- Produces: 없음 (최종 기능)

- [ ] **Step 1: 구현**

`src/WidgetForm.Desktop.cs`:
```csharp
using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WinWidget
{
    // Desktop-pinning behaviour: stays behind other windows, survives Win+D, drag to move, tray menu.
    public sealed partial class WidgetForm
    {
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WM_WINDOWPOSCHANGING = 0x0046;
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int HTCAPTION = 2;
        private const int GWLP_HWNDPARENT = -8;
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        private Settings _settings;
        private NotifyIcon _tray;
        private ContextMenuStrip _menu;
        private ToolStripMenuItem _lockItem;
        private ToolStripMenuItem _autoStartItem;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        partial void InitDesktop()
        {
            _settings = Settings.Load(Settings.DefaultPath);
            Rectangle[] areas = Screen.AllScreens.Select(s => s.WorkingArea).ToArray();
            Location = Placement.Resolve(_settings.X, _settings.Y, Size, areas, Screen.PrimaryScreen.WorkingArea);

            _lockItem = new ToolStripMenuItem("위치 잠금");
            _lockItem.Checked = _settings.Locked;
            _lockItem.Click += delegate
            {
                _settings.Locked = !_settings.Locked;
                _lockItem.Checked = _settings.Locked;
                SaveSettings();
            };

            _autoStartItem = new ToolStripMenuItem("Windows 시작 시 실행");
            _autoStartItem.Checked = AutoStart.IsEnabled(AutoStart.RunKey, AutoStart.AppName);
            _autoStartItem.Click += delegate { ToggleAutoStart(); };

            var exitItem = new ToolStripMenuItem("종료");
            exitItem.Click += delegate { Close(); };

            _menu = new ContextMenuStrip();
            _menu.Items.Add(_lockItem);
            _menu.Items.Add(_autoStartItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(exitItem);
            ContextMenuStrip = _menu;

            _tray = new NotifyIcon();
            _tray.Icon = SystemIcons.Application;
            _tray.Text = "WinWidget";
            _tray.ContextMenuStrip = _menu;
            _tray.Visible = true;
        }

        // Fallback in case "show desktop" or another tool still hid or minimized us.
        partial void OnTickDesktop()
        {
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            if (!Visible) Show();
        }

        partial void DisposeDesktop()
        {
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
            if (_menu != null) _menu.Dispose();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Owning the window by the desktop (Progman) keeps it visible after Win+D.
            IntPtr progman = FindWindow("Progman", null);
            if (progman != IntPtr.Zero) SetOwner(Handle, progman);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_WINDOWPOSCHANGING)
            {
                var pos = (WINDOWPOS)Marshal.PtrToStructure(m.LParam, typeof(WINDOWPOS));
                pos.hwndInsertAfter = HWND_BOTTOM;
                Marshal.StructureToPtr(pos, m.LParam, false);
            }
            base.WndProc(ref m);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || _settings.Locked) return;

            // Hand the drag to Windows as a caption drag; returns when the mouse is released.
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
            SaveSettings();
        }

        private void SaveSettings()
        {
            _settings.X = Location.X;
            _settings.Y = Location.Y;
            _settings.Save(Settings.DefaultPath);
        }

        private void ToggleAutoStart()
        {
            bool enable = !AutoStart.IsEnabled(AutoStart.RunKey, AutoStart.AppName);
            try
            {
                AutoStart.Set(AutoStart.RunKey, AutoStart.AppName, Application.ExecutablePath, enable);
            }
            catch (Exception ex)
            {
                MessageBox.Show("자동 실행 설정을 변경하지 못했습니다.\n" + ex.Message, "WinWidget",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            _autoStartItem.Checked = AutoStart.IsEnabled(AutoStart.RunKey, AutoStart.AppName);
        }

        private static void SetOwner(IntPtr hwnd, IntPtr owner)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr64(hwnd, GWLP_HWNDPARENT, owner);
            else SetWindowLong32(hwnd, GWLP_HWNDPARENT, owner.ToInt32());
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong32(IntPtr hwnd, int index, int value);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
```

- [ ] **Step 2: 빌드 + 기존 테스트 회귀 확인**

Run: `powershell -ExecutionPolicy Bypass -File .\build.ps1; if ($?) { .\bin\WinWidget.Tests.exe }`
Expected: `Build OK`, `ALL PASSED (30)`

- [ ] **Step 3: 렌더 회귀 확인**

Run: `.\bin\WinWidget.Tests.exe render "$env:TEMP\winwidget-render.png"`
Expected: Task 4 Step 8과 같은 그림. (이 모드에서도 트레이 아이콘이 잠깐 생겼다 사라지는 것은 정상)

- [ ] **Step 4: 위치 복원 확인**

Run:
```powershell
New-Item -ItemType Directory -Force "$env:APPDATA\WinWidget" | Out-Null
Set-Content "$env:APPDATA\WinWidget\settings.ini" "X=300`r`nY=300`r`nLocked=false" -Encoding ascii
Start-Process .\bin\WinWidget.exe; Start-Sleep 2
```
Expected: 위젯이 화면 (300,300) 근처에 표시. 이어서 `X=99999`로 바꿔 재실행하면 우상단에 표시.

- [ ] **Step 5: 수동 점검 (사용자와 함께)**

위젯 실행 상태에서:
- 다른 창을 클릭해도 위젯이 그 창 **뒤로** 간다.
- 작업표시줄과 Alt+Tab 목록에 위젯이 없다.
- Win+D 후에도 위젯이 바탕화면에 보인다.
- 왼쪽 드래그로 이동되고, 종료 후 재실행하면 옮긴 위치에 나타난다.
- 트레이(또는 위젯) 우클릭 → "위치 잠금" 체크 후 드래그가 안 된다.
- "Windows 시작 시 실행" 체크 → `reg query HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v WinWidget`에 exe 경로가 보인다. 체크 해제 → 값이 사라진다.
- "종료" → 프로세스와 트레이 아이콘이 모두 사라진다.

- [ ] **Step 6: 중복 실행 방지 확인**

Run: `Start-Process .\bin\WinWidget.exe; Start-Sleep 2; Start-Process .\bin\WinWidget.exe; Start-Sleep 2; (Get-Process WinWidget).Count`
Expected: `1`

- [ ] **Step 7: Commit**

```bash
git add src/WidgetForm.Desktop.cs
git commit -m "feat: pin widget to desktop with drag, saved position and tray menu"
```

---

### Task 6: 성능·정확도 최종 검증 + README

**Files:**
- Create: `README.md`

- [ ] **Step 1: 정확도 비교**

Run: `.\bin\WinWidget.Tests.exe dump` 실행과 동시에 작업 관리자 > 성능 탭 확인.
Expected: CPU ±5%p, 메모리 ±0.2GB, 디스크 같은 자릿수.

- [ ] **Step 2: 자체 부하 측정**

Run:
```powershell
Start-Process .\bin\WinWidget.exe; Start-Sleep 5
$p = Get-Process WinWidget; $c0 = $p.TotalProcessorTime; Start-Sleep 30; $p.Refresh()
'{0:N2}% CPU, {1:N1} MB' -f (($p.TotalProcessorTime - $c0).TotalSeconds / 30 / [Environment]::ProcessorCount * 100), ($p.WorkingSet64 / 1MB)
```
Expected: CPU `1%` 미만, Working Set `40 MB` 미만.

- [ ] **Step 3: 고DPI 점검 (Review Focus 5)**

설정 > 디스플레이 > 배율을 125% 또는 150%로 바꾼 뒤 로그아웃/로그인 후 위젯 확인: 글자가 선명하고 패널 안에 잘리지 않고 그래프 비율이 유지된다. (자동화 불가, 사용자 확인)

- [ ] **Step 4: README 작성**

`README.md`:
````markdown
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

## 테스트

```powershell
.\bin\WinWidget.Tests.exe              # 단위·통합 테스트
.\bin\WinWidget.Tests.exe dump         # 5초간 실측값 출력
.\bin\WinWidget.Tests.exe render a.png # 합성 데이터로 위젯 이미지 생성
```
````

- [ ] **Step 5: Commit**

```bash
git add README.md
git commit -m "docs: add README with build and usage"
```
