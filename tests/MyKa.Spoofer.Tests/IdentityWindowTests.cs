using System.Diagnostics;
using System.Runtime.ExceptionServices;
using MyKa.Spoofer.Identity;
using Xunit;

namespace MyKa.Spoofer.Tests;

[CollectionDefinition("Windows UI", DisableParallelization = true)]
public sealed class WindowsUiCollection;

[Collection("Windows UI")]
public sealed class IdentityWindowTests
{
    static IdentityWindowTests() => Application.SetCompatibleTextRenderingDefault(false);

    private static IdentitySnapshot Snapshot(string value = "before") => new(1, DateTimeOffset.UtcNow, "fixture-computer",
        [new("fixture-id", "Synthetic fixture identifier", value, "Test fixture; no real hardware data")]);

    private static T Find<T>(Control parent, string name) where T : Control => Assert.IsAssignableFrom<T>(Assert.Single(parent.Controls.Find(name, true)));

    private static void Sta(Action test)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException, threadScope: true);
                Application.EnableVisualStyles();
                Control.CheckForIllegalCrossThreadCalls = true;
                SynchronizationContext.SetSynchronizationContext(null);
                using var context = new ApplicationContext();
                using var start = new System.Windows.Forms.Timer { Interval = 1 };
                start.Tick += (_, _) =>
                {
                    start.Stop();
                    try { test(); }
                    catch (Exception e) { failure = ExceptionDispatchInfo.Capture(e); }
                    finally { context.ExitThread(); }
                };
                start.Start();
                Application.Run(context);
            }
            catch (Exception e) { failure = ExceptionDispatchInfo.Capture(e); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "The native UI test did not complete.");
        failure?.Throw();
    }

    private static void ShowHidden(IdentityWindow window)
    {
        window.ShowInTaskbar = false;
        window.StartPosition = FormStartPosition.Manual;
        window.Location = new Point(-30000, -30000);
        window.Opacity = 0;
        window.Show();
        Application.DoEvents();
    }

    private static void PumpUntil(Func<bool> done)
    {
        var timer = Stopwatch.StartNew();
        while (!done())
        {
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(8), "The window did not return to its ready state.");
            Application.DoEvents();
            Thread.Sleep(5);
        }
        Application.DoEvents();
    }

    private static void SaveScreenshot(IdentityWindow window, string name)
    {
        var artifact = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.artifacts/identity-ui"));
        Directory.CreateDirectory(artifact);
        using var bitmap = new Bitmap(window.Width, window.Height);
        window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, window.Size));
        bitmap.Save(Path.Combine(artifact, name + ".png"));
    }

    private static void AssertTabsFit(TabControl tabs)
    {
        for (var index = 0; index < tabs.TabCount; index++)
        {
            var bounds = tabs.GetTabRect(index);
            var textSize = TextRenderer.MeasureText(tabs.TabPages[index].Text, tabs.Font);
            Assert.True(textSize.Width <= bounds.Width && textSize.Height <= bounds.Height,
                tabs.TabPages[index].Text + " does not fit its tab");
        }
    }

    [Fact]
    public void NativeWindowUsesTheSharedVisualLanguageAndClearTabs() => Sta(() =>
    {
        using var window = new IdentityWindow(CancellationToken.None, _ => Task.FromResult(Snapshot()), false);
        ShowHidden(window);
        Assert.Equal(IdentityTheme.Background, window.BackColor);
        var tabs = Find<TabControl>(window, "identityTabs");
        Assert.Equal(["Snapshots", "Operations", "Results"], tabs.TabPages.Cast<TabPage>().Select(p => p.Text));
        AssertTabsFit(tabs);
        var grid = Find<DataGridView>(window, "identityValues");
        Assert.False(grid.EnableHeadersVisualStyles);
        Assert.Equal(IdentityTheme.Accent, grid.DefaultCellStyle.SelectionBackColor);
        Assert.False(Find<Button>(window, "compareSnapshots").Enabled);
        Assert.False(Find<Button>(window, "exportSnapshot").Enabled);
        Assert.False(Find<Button>(window, "applyOperations").Enabled);
        grid.Rows.Add("Fixture UUID", "00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000002", "Changed", "Synthetic data; inspect only");
        SaveScreenshot(window, "snapshots");
        tabs.SelectedTab = Find<TabPage>(window, "operationsTab");
        Application.DoEvents();
        Assert.False(Find<CheckBox>(window, "machineGuid").Enabled);
        Assert.True(Find<CheckBox>(window, "cleanCache").Enabled);
        foreach (var name in new[] { "machineGuid", "profileGuid", "hardwareId", "adapterAddresses", "flushDns", "renewDhcp" })
        {
            var box = Find<CheckBox>(window, name);
            using var graphics = box.CreateGraphics();
            var glyph = CheckBoxRenderer.GetGlyphSize(graphics, System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedDisabled);
            var caption = TextRenderer.MeasureText(graphics, box.Text, box.Font, Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            Assert.True(box.Width >= glyph.Width + (int)Math.Ceiling(3 * box.DeviceDpi / 96f) + caption.Width,
                name + " has a clipped disabled caption");
        }
        SaveScreenshot(window, "operations");
        tabs.SelectedTab = Find<TabPage>(window, "resultsTab");
        Application.DoEvents();
        SaveScreenshot(window, "results");
    });

    [Theory]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void ScaledLayoutsKeepTabsAndFooterReachable(float scale) => Sta(() =>
    {
        using var window = new IdentityWindow(CancellationToken.None, _ => Task.FromResult(Snapshot()), true);
        ShowHidden(window);
        window.Size = window.MinimumSize;
        window.Scale(new SizeF(scale, scale));
        Application.DoEvents();
        var tabs = Find<TabControl>(window, "identityTabs");
        AssertTabsFit(tabs);
        tabs.SelectedTab = Find<TabPage>(window, "operationsTab");
        Application.DoEvents();
        var choices = Find<FlowLayoutPanel>(window, "operationChoices");
        Assert.True(choices.Width > 0 && choices.Height > 0);
        var last = Find<CheckBox>(window, "cleanRegistry");
        choices.ScrollControlIntoView(last);
        Application.DoEvents();
        Assert.True(choices.ClientRectangle.IntersectsWith(last.Bounds));
        var apply = Find<Button>(window, "applyOperations");
        Assert.True(apply.Parent!.ClientRectangle.Contains(apply.Bounds));
        var footer = Find<TableLayoutPanel>(window, "identityFooter");
        foreach (var name in new[] { "cancelOperation", "closeIdentity", "viewResults" })
            Assert.True(footer.ClientRectangle.Contains(Find<Button>(window, name).Bounds), name + " is clipped after scaling");
    });

    [Theory]
    [InlineData(12f)]
    [InlineData(16f)]
    public void LargerFontsKeepActionsAndLongValuesAccessible(float points) => Sta(() =>
    {
        using var window = new IdentityWindow(CancellationToken.None, _ => Task.FromResult(Snapshot(new string('x', 8192))), true);
        using var font = new Font("Consolas", points);
        ShowHidden(window);
        Find<Button>(window, "captureBefore").PerformClick();
        PumpUntil(() => window.PendingWork.IsCompleted);
        window.Font = font;
        window.Size = window.MinimumSize;
        Application.DoEvents();
        AssertTabsFit(Find<TabControl>(window, "identityTabs"));
        var grid = Find<DataGridView>(window, "identityValues");
        Assert.Equal(8192, Assert.IsType<string>(grid.Rows[0].Cells[1].Value).Length);
        Assert.Equal(points, grid.Rows[0].Cells[0].InheritedStyle.Font!.SizeInPoints);
        Assert.True(grid.Rows[0].Height >= window.Font.Height + 8);
        SaveScreenshot(window, "large-font-" + (int)points);
        Assert.True(grid.Height >= window.Font.Height * 2, $"Large text pushed the data out of view (grid: {grid.Height}, text: {window.Font.Height})");
        foreach (var name in new[] { "captureBefore", "captureAfter", "compareSnapshots", "exportSnapshot", "loadSnapshot", "restoreChanges" })
        {
            var button = Find<Button>(window, name);
            Assert.True(button.Parent!.ClientRectangle.Contains(button.Bounds), name + " is clipped with large text");
        }
        var footer = Find<TableLayoutPanel>(window, "identityFooter");
        Assert.True(window.ClientRectangle.Contains(window.RectangleToClient(footer.RectangleToScreen(footer.ClientRectangle))));
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InvalidOrUnexpectedProbeFailurePreservesDataAndAllowsRetry(bool invalidSnapshot) => Sta(() =>
    {
        var call = 0;
        using var window = new IdentityWindow(CancellationToken.None, _ =>
        {
            if (++call == 2)
            {
                if (!invalidSnapshot) throw new NotSupportedException("private fixture detail");
                return Task.FromResult(Snapshot() with { SchemaVersion = 99 });
            }
            return Task.FromResult(Snapshot(call == 1 ? "before" : "replacement"));
        }, false);
        ShowHidden(window);
        Find<Button>(window, "captureBefore").PerformClick();
        PumpUntil(() => window.PendingWork.IsCompleted);
        Find<Button>(window, "captureBefore").PerformClick();
        PumpUntil(() => window.PendingWork.IsCompleted);
        var grid = Find<DataGridView>(window, "identityValues");
        Assert.Equal("before", grid.Rows[0].Cells[1].Value);
        Assert.True(Find<Button>(window, "exportSnapshot").Enabled);
        Assert.Equal("resultsTab", Find<TabControl>(window, "identityTabs").SelectedTab!.Name);
        Assert.DoesNotContain("private fixture detail", Find<TextBox>(window, "operationResults").Text);
        Find<TabControl>(window, "identityTabs").SelectedTab = Find<TabPage>(window, "snapshotsTab");
        Find<Button>(window, "captureBefore").PerformClick();
        PumpUntil(() => window.PendingWork.IsCompleted);
        Assert.Equal("replacement", grid.Rows[0].Cells[1].Value);
        Assert.True(window.PendingWork.IsCompletedSuccessfully);
    });

    [Fact]
    public void WindowsCanBeClosedDisposedAndReopenedRepeatedly() => Sta(() =>
    {
        for (var index = 0; index < 4; index++)
        {
            using var window = new IdentityWindow(CancellationToken.None, _ => Task.FromResult(Snapshot()), false);
            ShowHidden(window);
            var tabs = Find<TabControl>(window, "identityTabs");
            foreach (TabPage page in tabs.TabPages)
            {
                tabs.SelectedTab = page;
                Application.DoEvents();
                Assert.True(page.Visible);
            }
            window.Close();
            window.Dispose();
            Assert.True(window.IsDisposed);
        }
    });

    [Theory]
    [InlineData(760, 540)]
    [InlineData(900, 650)]
    [InlineData(1180, 800)]
    public void ButtonsAndDataRemainReachableAtEveryWindowSize(int width, int height) => Sta(() =>
    {
        using var window = new IdentityWindow(CancellationToken.None, _ => Task.FromResult(Snapshot()), true);
        ShowHidden(window);
        window.Size = new Size(width, height);
        Application.DoEvents();
        var tabs = Find<TabControl>(window, "identityTabs");
        var footer = Find<TableLayoutPanel>(window, "identityFooter");
        Assert.True(tabs.Height >= 180);
        Assert.True(footer.Bottom <= window.ClientSize.Height);
        foreach (var name in new[] { "captureBefore", "captureAfter", "compareSnapshots", "exportSnapshot", "loadSnapshot", "restoreChanges" })
        {
            var button = Find<Button>(window, name);
            Assert.True(button.Width > 100 && button.Height >= 30, name);
            Assert.True(button.Parent!.ClientRectangle.Contains(button.Bounds), name + " is clipped by its toolbar");
        }
        var grid = Find<DataGridView>(window, "identityValues");
        Assert.True(grid.Height >= 90, "The grid was pushed out by the controls.");
        tabs.SelectedTab = Find<TabPage>(window, "operationsTab");
        Application.DoEvents();
        var apply = Find<Button>(window, "applyOperations");
        Assert.True(apply.Parent!.ClientRectangle.Contains(apply.Bounds));
        Assert.True(Find<FlowLayoutPanel>(window, "operationChoices").AutoScroll);
        foreach (var name in new[] { "cancelOperation", "closeIdentity", "viewResults" })
        {
            var button = Find<Button>(window, name);
            Assert.True(footer.ClientRectangle.Contains(button.Bounds), name + " is clipped");
        }
    });

    [Fact]
    public void SynchronousProbeRunsOffTheUiThreadAndCanBeCancelledThenRetried() => Sta(() =>
    {
        var uiThread = Environment.CurrentManagedThreadId;
        var calls = 0;
        using var started = new ManualResetEventSlim();
        using var window = new IdentityWindow(CancellationToken.None, token =>
        {
            Assert.NotEqual(uiThread, Environment.CurrentManagedThreadId);
            if (Interlocked.Increment(ref calls) == 1)
            {
                started.Set();
                token.WaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                token.ThrowIfCancellationRequested();
            }
            return Task.FromResult(Snapshot());
        }, false);
        ShowHidden(window);
        Find<Button>(window, "captureBefore").PerformClick();
        PumpUntil(() => started.IsSet || window.PendingWork.IsCompleted);
        Assert.False(window.PendingWork.IsFaulted, window.PendingWork.Exception?.Message);
        Assert.True(window.IsWorking);
        Assert.False(Find<Button>(window, "captureAfter").Enabled);
        Assert.True(Find<Button>(window, "cancelOperation").Enabled);
        Assert.True(Find<Button>(window, "closeIdentity").Enabled);
        Find<Button>(window, "cancelOperation").PerformClick();
        PumpUntil(() => window.PendingWork.IsCompleted);
        Assert.False(window.IsWorking);
        Assert.False(window.IsDisposed);
        Find<Button>(window, "captureBefore").PerformClick();
        PumpUntil(() => window.PendingWork.IsCompleted);
        Assert.Equal(2, calls);
        Assert.True(Find<Button>(window, "exportSnapshot").Enabled);
    });

    [Fact]
    public void AFailedAfterCaptureKeepsTheBaselineAndCanBeRetriedWithoutRestart() => Sta(() =>
    {
        var call = 0;
        using var window = new IdentityWindow(CancellationToken.None, _ =>
        {
            call++;
            if (call == 2) throw new IOException("private fixture path");
            return Task.FromResult(Snapshot(call == 1 ? "before" : "after"));
        }, false);
        ShowHidden(window);
        Find<Button>(window, "captureBefore").PerformClick();
        PumpUntil(() => window.PendingWork.IsCompleted);
        Find<Button>(window, "captureAfter").PerformClick();
        PumpUntil(() => window.PendingWork.IsCompleted);
        var results = Find<TextBox>(window, "operationResults");
        Assert.False(results.InvokeRequired);
        Assert.DoesNotContain("private fixture path", results.Text);
        Assert.True(Find<Button>(window, "exportSnapshot").Enabled);
        Assert.False(Find<Button>(window, "compareSnapshots").Enabled);
        Find<TabControl>(window, "identityTabs").SelectedTab = Find<TabPage>(window, "snapshotsTab");
        Find<Button>(window, "captureAfter").PerformClick();
        PumpUntil(() => window.PendingWork.IsCompleted);
        var grid = Find<DataGridView>(window, "identityValues");
        Assert.Equal("before", grid.Rows[0].Cells[1].Value);
        Assert.Equal("after", grid.Rows[0].Cells[2].Value);
        Assert.Equal("Changed", grid.Rows[0].Cells[3].Value);
        Assert.True(Find<Button>(window, "compareSnapshots").Enabled);
        Assert.Contains("Before captured", results.Text);
        Assert.Contains("Operation did not complete", results.Text);
        Assert.Contains("After captured", results.Text);
    });

    [Fact]
    public void ClosingDuringWorkWaitsForCancellationAndReleasesTheWindow() => Sta(() =>
    {
        using var started = new ManualResetEventSlim();
        using var window = new IdentityWindow(CancellationToken.None, token =>
        {
            started.Set();
            token.WaitHandle.WaitOne(TimeSpan.FromSeconds(5));
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Snapshot());
        }, false);
        ShowHidden(window);
        Find<Button>(window, "captureBefore").PerformClick();
        PumpUntil(() => started.IsSet || window.PendingWork.IsCompleted);
        Assert.False(window.PendingWork.IsFaulted, window.PendingWork.Exception?.Message);
        window.Close();
        PumpUntil(() => window.IsDisposed);
        Assert.True(window.PendingWork.IsCompletedSuccessfully);
    });
}
