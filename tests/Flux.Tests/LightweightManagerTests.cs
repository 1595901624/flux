using System.Reflection;
using Flux.Services;
using Xunit;

namespace Flux.Tests
{
    public sealed class LightweightManagerTests : IDisposable
    {
        private readonly Queue<Action> _queue = new();
        private readonly bool _previousEnabled = AppServices.Config.Settings.EnableLightweightMode;

        public LightweightManagerTests()
        {
            LightweightManager.OnWindowShown();
            LightweightManager.RunOnUiThread = action => _queue.Enqueue(action);
            AppServices.Config.Settings.EnableLightweightMode = true;
            App.MainWindow = new LightweightTestWindow();
            App.ShowFailure = null;
        }

        [Fact]
        public void EnterReleasesWindowAndExitRecreatesIt()
        {
            var original = App.MainWindow!;
            LightweightManager.Enter();
            Assert.False(LightweightManager.IsLightweight);
            _queue.Dequeue()();
            Assert.True(original.Closed);
            Assert.Null(App.MainWindow);
            Assert.True(LightweightManager.IsLightweight);
            Assert.False(App.AllowWindowClose);

            LightweightManager.Exit();
            _queue.Dequeue()();
            Assert.NotNull(App.MainWindow);
            Assert.NotSame(original, App.MainWindow);
            Assert.True(App.MainWindow.AppWindow.IsVisible);
            Assert.False(LightweightManager.IsLightweight);
        }

        [Fact]
        public void FailedRecreationKeepsLightweightStateAndCanBeRetried()
        {
            LightweightManager.Enter();
            _queue.Dequeue()();
            App.ShowFailure = new InvalidOperationException("window creation failed");
            LightweightManager.Exit();
            Assert.Throws<InvalidOperationException>(_queue.Dequeue());
            Assert.True(LightweightManager.IsLightweight);
            App.ShowFailure = null;
            LightweightManager.Exit();
            _queue.Dequeue()();
            Assert.False(LightweightManager.IsLightweight);
            Assert.NotNull(App.MainWindow);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void QueuedAutomaticEntryDoesNotCloseReopenedWindow(bool cancel)
        {
            using var timer = new CancellationTokenSource();
            var window = App.MainWindow!;
            window.AppWindow.IsVisible = false;
            typeof(LightweightManager).GetMethod("Enter", BindingFlags.Static | BindingFlags.NonPublic,
                [typeof(CancellationToken?)])!.Invoke(null, [timer.Token]);
            if (cancel) timer.Cancel();
            else window.AppWindow.IsVisible = true;
            _queue.Dequeue()();
            Assert.False(window.Closed);
            Assert.Same(window, App.MainWindow);
            Assert.False(LightweightManager.IsLightweight);
        }

        [Fact]
        public void AutomaticEntryNotifiesCheckedStateAndRestoringWindowClearsIt()
        {
            using var timer = new CancellationTokenSource();
            App.MainWindow!.AppWindow.IsVisible = false;
            var observed = new List<bool>();
            void OnStateChanged() => observed.Add(LightweightManager.IsLightweight);
            LightweightManager.StateChanged += OnStateChanged;
            try
            {
                typeof(LightweightManager).GetMethod("Enter", BindingFlags.Static | BindingFlags.NonPublic,
                    [typeof(CancellationToken?)])!.Invoke(null, [timer.Token]);
                Assert.Empty(observed);
                _queue.Dequeue()();
                Assert.Equal(new[] { true }, observed);
                App.ShowMainWindow();
                Assert.Equal(new[] { true, false }, observed);
            }
            finally { LightweightManager.StateChanged -= OnStateChanged; }
        }

        [Fact]
        public void FailedCloseRetainsWindowAndRestoresClosePermission()
        {
            var window = App.MainWindow!;
            window.CloseFailure = new InvalidOperationException("close failed");
            LightweightManager.Enter();
            Assert.Throws<InvalidOperationException>(_queue.Dequeue());
            Assert.Same(window, App.MainWindow);
            Assert.False(App.AllowWindowClose);
            Assert.False(LightweightManager.IsLightweight);
        }

        public void Dispose()
        {
            LightweightManager.OnWindowShown();
            LightweightManager.RunOnUiThread = null;
            AppServices.Config.Settings.EnableLightweightMode = _previousEnabled;
            App.MainWindow = null;
            App.ShowFailure = null;
        }
    }
}

// 窗口替身只验证管理器的状态与恢复调用，不启动 WinUI、不修改系统代理。
namespace Flux
{
    public static class App
    {
        public static LightweightTestWindow? MainWindow { get; set; }
        public static bool AllowWindowClose { get; set; }
        public static Exception? ShowFailure { get; set; }
        public static void ShowMainWindow()
        {
            if (ShowFailure is { } failure) throw failure;
            MainWindow ??= new LightweightTestWindow();
            MainWindow.AppWindow.IsVisible = true;
            LightweightManager.OnWindowShown();
        }
    }

    public sealed class LightweightTestWindow
    {
        public sealed class WindowVisibility { public bool IsVisible { get; set; } = true; }
        public WindowVisibility AppWindow { get; } = new();
        public bool Closed { get; private set; }
        public Exception? CloseFailure { get; set; }
        public void Close()
        {
            Assert.True(App.AllowWindowClose);
            if (CloseFailure is { } failure) throw failure;
            Closed = true;
            AppWindow.IsVisible = false;
        }
    }
}
