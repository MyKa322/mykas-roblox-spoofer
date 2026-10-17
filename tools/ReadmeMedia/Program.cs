using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MyKa.Spoofer.Accounts;
using MyKa.Spoofer.Branding;
using MyKa.Spoofer.Diagnostics;
using MyKa.Spoofer.Identity;
using MyKa.Spoofer.Terminal.Tabs;
using Spectre.Console;
using Spectre.Console.Rendering;
using Color = System.Drawing.Color;
using RenderOptions = Spectre.Console.Rendering.RenderOptions;
using Size = System.Drawing.Size;

namespace MyKa.Spoofer.ReadmeMedia;

/// <summary>Renders the real views with fixtures. Never opens a store, probes the host, or applies system operations.</summary>
internal static class Program
{
    private static readonly DateTimeOffset Captured = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1) { System.Console.Error.WriteLine("Usage: ReadmeMedia OUTPUT_DIRECTORY"); return 2; }
        var output = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(output);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        CaptureTerminal(output);
        CaptureIdentity(output);
        System.Console.WriteLine("Fixture media written to " + output);
        return 0;
    }

    private static void CaptureTerminal(string output)
    {
        var accounts = new AccountsPage(new FixtureAccounts());
        var pages = new ITabPage[]
        {
            accounts,
            new DiagnosticsPage(() => throw new InvalidOperationException("No host probes in media capture."), () => "fixture"),
            new AdaptersPage(() => new AdapterReport([], null), () => { }),
            new IdentityPage(() => { }),
        };
        var model = new TabsModel(pages);
        accounts.Activate();
        using var consoleWriter = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes, ColorSystem = ColorSystemSupport.TrueColor,
            Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(consoleWriter),
        });
        console.Profile.Width = 112;
        console.Profile.Height = 43;
        var options = RenderOptions.Create(console);
        var layout = TabsLayout.For(112, 43);
        var status = Line.Of("Documentation preview | fictional accounts and values", Looks.Dim);
        using (var still = DrawTerminal(TabsView.Build(model, layout, LogoMotion.Still, status), options))
            still.Save(Path.Combine(output, "main-menu.png"), ImageFormat.Png);
        using var animation = new FileStream(Path.Combine(output, "main-menu.gif"), FileMode.Create);
        var encoder = new GifBitmapEncoder();
        const int frames = 48;
        for (var index = 0; index < frames; index++)
        {
            using var frame = DrawTerminal(TabsView.Build(model, layout, LogoMotion.At(index * LogoMotion.PeriodSeconds / frames), status), options);
            AddGifFrame(encoder, frame);
        }
        GifAnimationWriter.Save(encoder, animation, 19);
    }

    private static Bitmap DrawTerminal(IRenderable renderable, RenderOptions options)
    {
        const int cellWidth = 9, cellHeight = 18, margin = 20, titleHeight = 32;
        var bitmap = new Bitmap(112 * cellWidth + margin * 2, 42 * cellHeight + margin * 2 + titleHeight);
        using var graphics = Graphics.FromImage(bitmap);
        var background = Color.FromArgb(12, 15, 20);
        graphics.Clear(background);
        using var normal = new Font("Consolas", 15, FontStyle.Regular, GraphicsUnit.Pixel);
        using var bold = new Font("Consolas", 15, FontStyle.Bold, GraphicsUnit.Pixel);
        TextRenderer.DrawText(graphics, "myka.exe  |  documentation preview", normal,
            new Point(margin, 10), Color.FromArgb(167, 179, 195), TextFormatFlags.NoPadding);
        var x = margin;
        var y = titleHeight + margin;
        foreach (var segment in renderable.Render(options, 112))
        {
            if (segment.IsLineBreak) { x = margin; y += cellHeight; continue; }
            if (segment.IsControlCode) continue;
            var foreground = segment.Style.Foreground;
            var fill = segment.Style.Background;
            var ink = foreground == Spectre.Console.Color.Default ? Color.FromArgb(225, 231, 239) : Color.FromArgb(foreground.R, foreground.G, foreground.B);
            var paper = fill == Spectre.Console.Color.Default ? background : Color.FromArgb(fill.R, fill.G, fill.B);
            var width = Cells.Width(segment.Text) * cellWidth;
            using (var brush = new SolidBrush(paper)) graphics.FillRectangle(brush, x, y, width, cellHeight);
            var font = (segment.Style.Decoration & Decoration.Bold) != 0 ? bold : normal;
            using (var brush = new SolidBrush(ink))
                graphics.DrawString(segment.Text, font, brush, x, y, StringFormat.GenericTypographic);
            x += width;
        }
        return bitmap;
    }

    private static void CaptureIdentity(string output)
    {
        var reads = 0;
        using var window = new IdentityWindow(CancellationToken.None,
            _ => Task.FromResult(Snapshot(Interlocked.Increment(ref reads) > 1)), false)
        {
            ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
            Location = new Point(-30000, -30000), Opacity = 0,
        };
        Exception? error = null;
        window.Shown += async (_, _) =>
        {
            try
            {
                Find<Button>(window, "captureBefore").PerformClick();
                await window.PendingWork;
                Find<Button>(window, "captureAfter").PerformClick();
                await window.PendingWork;
                if (Find<DataGridView>(window, "identityValues").Rows.Count != Snapshot(false).Values.Length)
                    throw new InvalidOperationException("Fixture capture did not complete.");
                var encoder = new GifBitmapEncoder();
                var tabs = Find<TabControl>(window, "identityTabs");
                foreach (var name in new[] { "snapshots", "operations", "results" })
                {
                    tabs.SelectedTab = Find<TabPage>(window, name + "Tab");
                    window.PerformLayout();
                    window.Refresh();
                    using var bitmap = new Bitmap(window.Width, window.Height);
                    window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, window.Size));
                    bitmap.Save(Path.Combine(output, "identity-" + name + ".png"), ImageFormat.Png);
                    AddGifFrame(encoder, bitmap);
                }
                using var animation = File.Create(Path.Combine(output, "identity-workflow.gif"));
                GifAnimationWriter.Save(encoder, animation, 180);
            }
            catch (Exception e) { error = e; }
            finally { window.Close(); }
        };
        Application.Run(window);
        if (error is not null) throw new InvalidOperationException("Identity media capture failed.", error);
    }

    private static T Find<T>(Control parent, string name) where T : Control
        => parent.Controls.Find(name, true).OfType<T>().Single();

    private static IdentitySnapshot Snapshot(bool after) => new(1, Captured.AddMinutes(after ? 2 : 0), "documentation-fixture", [
        new("machine-guid", "MachineGuid", after ? "22222222-2222-4222-8222-222222222222" : "11111111-1111-4111-8111-111111111111", "Synthetic stored GUID; not a firmware identifier"),
        new("system-uuid", "System UUID (WMI)", "AAAAAAAA-BBBB-4CCC-8DDD-EEEEEEEEEEEE", "Synthetic firmware observation; inspect only"),
        new("baseboard", "Baseboard serial", "DEMO-BOARD-0001", "Synthetic observation; inspect only"),
        new("configured-mac", "Configured MAC override", after ? "02AABBCCDD02" : "02AABBCCDD01", "Synthetic configured value; no adapter was changed"),
        new("effective-mac", "Effective adapter MAC", "02AABBCCDD01", "Synthetic observation; effective and configured values differ"),
        new("public-ip", "Public IP routing", null, "Outside the current tools", "Not implemented"),
    ]);

    private static void AddGifFrame(GifBitmapEncoder encoder, Bitmap bitmap)
    {
        using var png = new MemoryStream();
        bitmap.Save(png, ImageFormat.Png);
        png.Position = 0;
        var source = BitmapFrame.Create(png, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var scale = Math.Min(1, 900d / source.PixelWidth);
        BitmapSource resized = scale < 1 ? new TransformedBitmap(source, new ScaleTransform(scale, scale)) : source;
        var colors = new List<System.Windows.Media.Color>
        {
            System.Windows.Media.Color.FromRgb(12, 15, 20), System.Windows.Media.Color.FromRgb(23, 29, 38),
            System.Windows.Media.Color.FromRgb(225, 231, 239), System.Windows.Media.Color.FromRgb(167, 179, 195),
            System.Windows.Media.Colors.Cyan, System.Windows.Media.Colors.Black,
        };
        colors.AddRange(new BitmapPalette(resized, 120).Colors);
        var indexed = new FormatConvertedBitmap(resized, PixelFormats.Indexed8, new BitmapPalette(colors), 0);
        encoder.Frames.Add(BitmapFrame.Create(indexed));
    }

    private sealed class FixtureAccounts : IAccountOperations
    {
        public AccountListing List() => new(Enumerable.Range(1, 3).Select(index => new SavedAccount(
            new string((char)('a' + index), 32), new string((char)('d' + index), 32), (1000000 + index).ToString(),
            new[] { "demo_main", "demo_build", "demo_test" }[index - 1], "Documentation fixture", Captured)).ToArray(), 0);
        public int Add(Action<string> report) => throw Disabled();
        public int SignInAgain(string accountId, Action<string> report) => throw Disabled();
        public int OpenExperience(string accountId, string? placeId, Action<string> report) => throw Disabled();
        public int Remove(string accountId, Func<SavedAccount, bool> confirm, Action<string> report) => throw Disabled();
        public int CleanUp(Action<string> report) => throw Disabled();
        private static InvalidOperationException Disabled() => new("Media fixtures cannot perform account operations.");
    }
}
