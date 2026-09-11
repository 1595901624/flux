using System.ComponentModel;
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

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<(int Id, HotkeyCombo Combo)> _registered = [];
    private Thread? _thread;
    private IntPtr _hwnd;
    private WndProcDelegate? _wndProc; // 保持委托存活防止 GC 回收
    private int _nextId = 1;

    public event Action<string>? HotkeyPressed;

    /// <summary>触发时投递给 UI 的动作（由启动器赋值为 dispatcher.TryEnqueue）。</summary>
    public Action<Action>? Dispatcher { get; set; }

    public OperationResult<IReadOnlyList<string>> ApplyHotkeys(IReadOnlyDictionary<string, string> hotkeys)
    {
        EnsureThread();
        var failures = new List<string>();
        _gate.Wait();
        try
        {
            UnregisterAllUnsafe();

            // 先解析 + 配置内冲突检测，再逐个注册
            var parsed = new List<(string Action, HotkeyCombo Combo, int Id)>();
            foreach (var (action, comboText) in hotkeys)
            {
                if (string.IsNullOrWhiteSpace(comboText)) continue;
                var combo = HotkeyComboParser.Parse(comboText);
                if (combo is null)
                {
                    failures.Add($"{action}: 无法识别的组合键「{comboText}」");
                    continue;
                }

                var duplicate = parsed.FirstOrDefault(p => HotkeyComboParser.Conflicts(p.Combo, combo));
                if (duplicate.Combo is not null)
                {
                    failures.Add($"{action}: 与「{duplicate.Action}」的热键冲突（{comboText}）");
                    continue;
                }

                parsed.Add((action, combo, _nextId++));
            }

            foreach (var (action, combo, id) in parsed)
            {
                // MOD_NOREPEAT 保证按住不重复触发（Win7+）
                var modifiers = combo.Modifiers | HotkeyComboParser.ModNoRepeat;
                if (RegisterHotKey(_hwnd, id, modifiers, combo.VirtualKey))
                {
                    _registered.Add((id, combo));
                    _idToAction[id] = action;
                }
                else
                {
                    failures.Add($"{action}: 组合键「{combo.Display}」已被其他程序占用");
                }
            }

            return OperationResult<IReadOnlyList<string>>.Ok(failures);
        }
        finally
        {
            _gate.Release();
        }
    }

    private readonly Dictionary<int, string> _idToAction = new();

    public void Clear()
    {
        if (_hwnd == IntPtr.Zero) return;
        _gate.Wait();
        try { UnregisterAllUnsafe(); }
        finally { _gate.Release(); }
    }

    private void UnregisterAllUnsafe()
    {
        foreach (var (id, _) in _registered)
            UnregisterHotKey(_hwnd, id);
        _registered.Clear();
        _idToAction.Clear();
    }

    // ---------- 消息窗口线程 ----------

    private void EnsureThread()
    {
        if (_thread is not null) return;
        _thread = new Thread(RunMessageLoop)
        {
            Name = "FluxHotkeyThread",
            IsBackground = true,
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        // 等待窗口创建完成
        var deadline = Environment.TickCount64 + 5000;
        while (_hwnd == IntPtr.Zero && Environment.TickCount64 < deadline)
            Thread.Sleep(20);
    }

    private void RunMessageLoop()
    {
        _wndProc = WndProc;
        var className = "FluxHotkeyWindow";
        var hInstance = Marshal.GetHINSTANCE(typeof(HotkeyService).Module);

        var wc = new WndClassEx
        {
            Size = (uint)Marshal.SizeOf<WndClassEx>(),
            Style = 0,
            WndProc = _wndProc,
            Instance = hInstance,
            ClassName = className,
        };
        RegisterClassEx(ref wc);

        _hwnd = CreateWindowEx(
            0, className, "FluxHotkey", 0,
            0, 0, 0, 0,
            (IntPtr)(-3), // HWND_MESSAGE：仅消息窗口
            IntPtr.Zero, hInstance, IntPtr.Zero);

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey)
        {
            var id = wParam.ToInt32();
            if (_idToAction.TryGetValue(id, out var action))
            {
                if (Dispatcher is { } d) d(() => HotkeyPressed?.Invoke(action));
                else HotkeyPressed?.Invoke(action);
            }
            handled = true;
        }
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        Clear();
        if (_hwnd != IntPtr.Zero)
        {
            PostMessage(_hwnd, 0x0012 /*WM_QUIT*/, IntPtr.Zero, IntPtr.Zero);
            _hwnd = IntPtr.Zero;
        }
        _gate.Dispose();
    }

    // ---------- Win32 ----------

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class WndClassEx
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

    [DllImport("user32.dll")]
    private static extern void PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

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
