using MyKa.Spoofer.Branding;
using MyKa.Spoofer.Terminal.Tabs;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class TabsLayoutTests
{
    [Theory]
    [InlineData(120, 30, "compact", 14)]
    [InlineData(120, 28, "compact", 12)]
    [InlineData(120, 34, "compact", 18)]
    [InlineData(120, 37, "compact", 21)]
    [InlineData(120, 38, "large", 12)]
    [InlineData(120, 50, "large", 24)]
    [InlineData(100, 60, "large", 34)]
    public void ChoosesTheLargestLogoThatLeavesRoomForThePage(int width, int height, string logo, int content)
    {
        var layout = TabsLayout.For(width, height);

        Assert.False(layout.TooSmall);
        Assert.Equal(logo, layout.Logo?.Name);
        Assert.Equal(content, layout.ContentRows);
    }

    [Theory]
    [InlineData(120, 27)]
    [InlineData(120, 24)]
    [InlineData(120, 20)]
    [InlineData(120, 12)]
    public void ShortWindowsDropTheLogoAndKeepThePage(int width, int height)
    {
        var layout = TabsLayout.For(width, height);

        Assert.False(layout.TooSmall);
        Assert.Null(layout.Logo);
        Assert.Equal(height - TabsLayout.SpareRows - TabsLayout.FixedRows, layout.ContentRows);
    }

    [Theory]
    [InlineData(120, 11)]
    [InlineData(120, 5)]
    [InlineData(49, 40)]
    [InlineData(10, 10)]
    [InlineData(0, 0)]
    public void WindowsBelowTheMinimumAreTooSmall(int width, int height)
    {
        var layout = TabsLayout.For(width, height);

        Assert.True(layout.TooSmall);
        Assert.Null(layout.Logo);
    }

    [Theory]
    [InlineData(50, 40, null)]
    [InlineData(54, 40, null)]
    [InlineData(55, 40, "compact")]
    [InlineData(71, 40, "compact")]
    [InlineData(72, 40, "large")]
    public void WidthDecidesWhichLogoFits(int width, int height, string? logo)
        => Assert.Equal(logo, TabsLayout.For(width, height).Logo?.Name);

    [Fact]
    public void TheShortLogoIsGivenARowForItsSubtitle()
    {
        var compact = TabsLayout.For(120, 30);
        var large = TabsLayout.For(120, 50);

        Assert.True(compact.HasSubtitle);
        Assert.False(large.HasSubtitle);
        Assert.False(TabsLayout.For(120, 12).HasSubtitle);
    }

    [Fact]
    public void TheFrameAlwaysFillsEveryRowExceptTheSpareOne()
    {
        for (var width = 50; width <= 130; width += 5)
            for (var height = TabsLayout.MinHeight; height <= 70; height++)
            {
                var layout = TabsLayout.For(width, height);
                var logoRows = layout.Logo is null ? 0 : layout.Logo.Rows + (layout.HasSubtitle ? 1 : 0);

                Assert.Equal(height - TabsLayout.SpareRows, logoRows + TabsLayout.FixedRows + layout.ContentRows);
            }
    }

    [Fact]
    public void ThePageNeverGetsFewerRowsThanTheMinimum()
    {
        for (var height = TabsLayout.MinHeight; height <= 70; height++)
            Assert.True(TabsLayout.For(100, height).ContentRows >= TabsLayout.MinContentRows);
    }

    [Fact]
    public void ALogoIsOnlyShownWhilePageKeepsItsPreferredRows()
    {
        for (var height = TabsLayout.MinHeight; height <= 70; height++)
        {
            var layout = TabsLayout.For(120, height);
            if (layout.Logo is not null) Assert.True(layout.ContentRows >= TabsLayout.PreferredContentRows);
        }
    }

    [Fact]
    public void ThePageIsInsetByTheMarginOnBothSides()
        => Assert.Equal(96, TabsLayout.For(100, 40).ContentWidth);

    [Fact]
    public void TheDefaultTerminalShowsTheShortLogoWithAGenerousPage()
    {
        // 120 x 30 is the usual default size of a new terminal window.
        var layout = TabsLayout.For(120, 30);

        Assert.Same(LogoLayout.Compact, layout.Logo);
        Assert.True(layout.ContentRows >= 12);
    }
}
