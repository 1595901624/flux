using System.ComponentModel;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Flux.Core.Contracts;
using Flux.Core.Hotkeys;

namespace Flux.Services;

/// <summary>
/// 全局热键服务：Win32 RegisterHotKey + 隐藏消息窗口（独立 STA 消息循环线程）。
/// 冲突（注册失败）时拒绝保存并报告冲突组合；触发事件封送回 UI 线程队列。
/// </summary>
public sealed class HotkeyService : IHotkeyService, IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int WmInvoke = 0x8001;

    private readonly object _sync = new();
    private readonly List<(int Id, HotkeyCombo Combo, string Action)> _registered = [];
    private readonly ConcurrentQueue<Action> _commands = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _disposed;
    private Thread? _thread;
    private IntPtr _hwnd;
    private WndProcDelegate? _wndProc; // 保持委托存活防止 GC 回收
    private int _nextId = 1;

    public event Action<string>? HotkeyPressed;

    /// <summary>触发时投递给 UI 的动作（由启动器赋值为 dispatcher.TryEnqueue）。</summary>
    public Action<Action>? Dispatcher { get; set; }

    public OperationResult<IReadOnlyList<string>> ApplyHotkeys(IReadOnlyDictionary<string, string> hotkeys)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureThread();
            return Invoke(() => ApplyHotkeysOnThread(hotkeys));
        }
    }

    private OperationResult<IReadOnlyList<string>> ApplyHotkeysOnThread(IReadOnlyDictionary<string, string> hotkeys)
    {
        var failures = new List<string>();
        // 所有输入先通过校验，不能因为输入错误注销现有热键。
        var parsed = new List<(string Action, HotkeyCombo Combo)>();
        foreach (var (action, comboText) in hotkeys)
        {
            if (string.IsNullOrWhiteSpace(comboText)) continue;
            var combo = HotkeyComboParser.Parse(comboText);
            if (combo is null)
            {
                failures.Add(L10n.F("Hotkey_UnknownCombo", action, comboText));
                continue;
            }

            var duplicate = parsed.FirstOrDefault(p => HotkeyComboParser.Conflicts(p.Combo, combo));
            if (duplicate.Combo is not null)
            {
                failures.Add(L10n.F("Hotkey_Duplicate", action, duplicate.Action, comboText));
                continue;
            }

            parsed.Add((action, combo));
        }

        if (failures.Count > 0)
            return OperationResult<IReadOnlyList<string>>.Ok(failures);

        var staged = new List<(int Id, HotkeyCombo Combo, string Action)>();
        var desired = new List<(int Id, HotkeyCombo Combo, string Action)>();
        foreach (var (action, combo) in parsed)
        {
            // 同一物理组合可重新分配动作，无需释放后再抢占。
            var existing = _registered.FirstOrDefault(r => HotkeyComboParser.Conflicts(r.Combo, combo));
            if (existing.Combo is not null)
            {
                desired.Add((existing.Id, combo, action));
                continue;
            }
            var id = AllocateId(staged);
            // MOD_NOREPEAT 保证按住不重复触发（Win7+）
            var modifiers = combo.Modifiers | HotkeyComboParser.ModNoRepeat;
            if (RegisterHotKey(_hwnd, id, modifiers, combo.VirtualKey))
            {
                staged.Add((id, combo, action));
                desired.Add((id, combo, action));
            }
            else
            {
                failures.Add(L10n.F("Hotkey_InUse", action, combo.Display));
            }
        }

        if (failures.Count > 0)
        {
            foreach (var item in staged) UnregisterHotKey(_hwnd, item.Id);
            return OperationResult<IReadOnlyList<string>>.Ok(failures);
        }

        foreach (var item in _registered)
            if (!desired.Any(r => r.Id == item.Id)) UnregisterHotKey(_hwnd, item.Id);
        _registered.Clear();
        _registered.AddRange(desired);
        _idToAction.Clear();
        foreach (var item in desired) _idToAction[item.Id] = item.Action;
        return OperationResult<IReadOnlyList<string>>.Ok(failures);
    }

    private int AllocateId(List<(int Id, HotkeyCombo Combo, string Action)> staged)
    {
        // Win32 应用热键 ID 必须处于 0x0000..0xBFFF。
        while (true)
        {
            if (_nextId > 0xBFFF) _nextId = 1;
            var id = _nextId++;
            if (!_registered.Any(r => r.Id == id) && !staged.Any(r => r.Id == id)) return id;
        }
    }

    private readonly Dictionary<int, string> _idToAction = new();

    public void Clear()
    {
        lock (_sync)
        {
            if (_disposed || _hwnd == IntPtr.Zero) return;
            Invoke(() => { UnregisterAllUnsafe(); return true; });
        }
    }

    private void UnregisterAllUnsafe()
    {
        foreach (var (id, _, _) in _registered)
            UnregisterHotKey(_hwnd, id);
        _registered.Clear();
        _idToAction.Clear();
    }

    // ---------- 消息窗口线程 ----------

    private void EnsureThread()
    {
        if (_thread is null)
        {
            _thread = new Thread(RunMessageLoop)
            {
                Name = "FluxHotkeyThread",
                IsBackground = true,
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }
        _ready.Task.GetAwaiter().GetResult();
    }

    private T Invoke<T>(Func<T> action)
    {
        if (Thread.CurrentThread == _thread) return action();
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _commands.Enqueue(() =>
        {
            try { completion.SetResult(action()); }
            catch (Exception ex) { completion.SetException(ex); }
        });
        if (!PostMessage(_hwnd, WmInvoke, IntPtr.Zero, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return completion.Task.GetAwaiter().GetResult();
    }

    private void RunMessageLoop()
    {
        _wndProc = WndProc;
        var className = "FluxHotkeyWindow_" + Guid.NewGuid().ToString("N");
        var hInstance = GetModuleHandle(null);

        var wc = new WndClassEx
        {
            Size = (uint)Marshal.SizeOf<WndClassEx>(),
            Style = 0,
            WndProc = _wndProc,
            Instance = hInstance,
            ClassName = className,
        };
        if (RegisterClassEx(ref wc) == 0)
        {
            _ready.SetException(new Win32Exception(Marshal.GetLastWin32Error(), "RegisterClassEx failed"));
            return;
        }

        _hwnd = CreateWindowEx(
            0, className, "FluxHotkey", 0,
            0, 0, 0, 0,
            (IntPtr)(-3), // HWND_MESSAGE：仅消息窗口
            IntPtr.Zero, hInstance, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            _ready.SetException(new Win32Exception(Marshal.GetLastWin32Error(), "CreateWindowEx failed"));
            UnregisterClass(className, hInstance);
            return;
        }
        _ready.SetResult();
        try
        {
            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        finally
        {
            UnregisterAllUnsafe();
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
            UnregisterClass(className, hInstance);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WmInvoke)
        {
            while (_commands.TryDequeue(out var command)) command();
            return IntPtr.Zero;
        }
        if (msg == WmHotkey)
        {
            var id = wParam.ToInt32();
            if (_idToAction.TryGetValue(id, out var action))
            {
                if (Dispatcher is { } d) d(() => HotkeyPressed?.Invoke(action));
                else HotkeyPressed?.Invoke(action);
            }
            return IntPtr.Zero;
        }
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            if (_hwnd != IntPtr.Zero)
                Invoke(() => { UnregisterAllUnsafe(); PostQuitMessage(0); return true; });
            _disposed = true;
        }
        if (Thread.CurrentThread != _thread) _thread?.Join();
    }

    // ---------- Win32 ----------

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public uint Size;
        public uint Style;
        public WndProcDelegate? WndProc;
        public int ClsExtra;
        public int WndExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string ClassName;
        public IntPtr IconSm;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WndClassEx lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint styleEx, string className, string windowName, uint style,
        int x, int y, int width, int height,
        IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out Msg lpMsg, IntPtr hwnd, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Msg lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref Msg lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int exitCode);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClass(string className, IntPtr instance);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public IntPtr Hwnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int PointX;
        public int PointY;
    }
}
