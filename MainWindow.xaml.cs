using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using Flux.Services;
using Flux.Utils;
using Flux.Views;

namespace Flux;

public sealed partial class MainWindow : Window
{
    private readonly EndSessionHook? _endSessionHook;
    private MainShell _shell;
    private bool _closed;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    public MainWindow()
    {
        InitializeComponent();
        ((FrameworkElement)Content).Loaded += (_, _) => Program.Trace("mainwindow content loaded");
        Title = "Flux";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SystemBackdrop = new MicaBackdrop()
        {
            Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt
        };
        _shell = new MainShell();
        ShellHost.Content = _shell;

        // 关闭窗口 = 隐藏到托盘；仅轻量模式或退出时真正关闭。
        AppWindow.Closing += (_, e) =>
        {
            if (App.AllowWindowClose)
            {
                App.AllowWindowClose = false;
                return;
            }
            e.Cancel = true;
            AppWindow.Hide();
            LightweightManager.OnWindowHidden();
        };
        Closed += (_, _) =>
        {
            _closed = true;
            AppServices.Core.RestartStateChanged -= OnCoreRestartStateChanged;
            _endSessionHook?.Dispose();
            ShellHost.Content = null;
            _shell.Dispose();
            SystemBackdrop = null;
            Content = null;
            if (ReferenceEquals(App.MainWindow, this)) App.MainWindow = null;
        };

        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        if (scale <= 0) scale = 1.0;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(
            (int)(1080 * scale), (int)(720 * scale)));
        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
        _endSessionHook = new EndSessionHook(WinRT.Interop.WindowNative.GetWindowHandle(this));
        AppServices.Core.RestartStateChanged += OnCoreRestartStateChanged;
    }

    /// <summary>保留原生窗口和标题栏，仅替换语言相关的内容树。</summary>
    internal void ReloadContentForLanguage()
    {
        if (_closed) return;
        var previous = _shell;
        var replacement = new MainShell(previous.CaptureState());
        try
        {
            ShellHost.Content = replacement;
            _shell = replacement;
            UpdateStartupState();
        }
        catch
        {
            _shell = previous;
            ShellHost.Content = previous;
            replacement.Dispose();
            throw;
        }
        previous.Dispose();
    }

    public void NavigateTo(string tag) => _shell.NavigateTo(tag);

    public void UpdateStartupState()
    {
        if (_closed) return;
        var starting = AppBootstrapper.IsStarting || AppServices.Core.IsRestarting;
        _shell.UpdateStartupState(starting);
        AppTitleSub.Text = starting ? L10n.T("Msg_StartingCore") : "";
        StartupProgress.IsActive = starting;
        StartupProgress.Visibility = starting ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnCoreRestartStateChanged()
    {
        if (DispatcherQueue.HasThreadAccess) UpdateStartupState();
        else DispatcherQueue.TryEnqueue(UpdateStartupState);
    }

    /// <summary>
    /// 主窗口 WndProc 子类化，监听 WM_ENDSESSION：系统关机/注销时把系统代理恢复为关闭。
    /// </summary>
    private sealed class EndSessionHook : IDisposable
    {
        private const int GWL_WNDPROC = -4;
        private const uint WM_ENDSESSION = 0x0016;

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProcW(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private readonly IntPtr _hwnd;
        private readonly IntPtr _oldProc;
        // 委托必须保活，否则原生回调指向已回收的委托
        private readonly WndProcDelegate _newProc;

        public EndSessionHook(IntPtr hwnd)
        {
            _hwnd = hwnd;
            _newProc = WndProc;
            _oldProc = GetWindowLongPtrW(hwnd, GWL_WNDPROC);
            SetWindowLongPtrW(hwnd, GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newProc));
        }

        private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_ENDSESSION && wParam != IntPtr.Zero)
            {
                try { AppServices.SysProxy.Reset(); }
                catch { }
            }
            return CallWindowProcW(_oldProc, hwnd, msg, wParam, lParam);
        }

        public void Dispose() => SetWindowLongPtrW(_hwnd, GWL_WNDPROC, _oldProc);
    }

}
