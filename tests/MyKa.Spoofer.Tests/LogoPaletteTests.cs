using MyKa.Spoofer.Branding;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class LogoPaletteTests
{
    private static Rgb Of(SurfaceKind kind, float light = 0.9f, float tint = 0.3f) => LogoPalette.Of(new Sample(kind, light, tint));

    private static int Brightness(Rgb color) => color.R + color.G + color.B;

    [Fact]
    public void FrontFacesAreBrighterThanTopsAndWalls()
    {
        Assert.True(Brightness(Of(SurfaceKind.Front)) > Brightness(Of(SurfaceKind.Top)));
        Assert.True(Brightness(Of(SurfaceKind.Top)) > Brightness(Of(SurfaceKind.Side)));
    }

    [Fact]
    public void RaisedEdgesAreBrighterThanFlatFaceAndRecessedOnesDarker()
    {
        Assert.True(Brightness(Of(SurfaceKind.FrontHigh)) > Brightness(Of(SurfaceKind.Front)));
        Assert.True(Brightness(Of(SurfaceKind.Front)) > Brightness(Of(SurfaceKind.FrontLow)));
    }

    [Fact]
    public void MoreLightMakesAFaceBrighter()
        => Assert.True(Brightness(Of(SurfaceKind.Side, 0.9f)) > Brightness(Of(SurfaceKind.Side, 0.2f)));

    [Fact]
    public void ColourRunsFromAquaAtTheTopToVioletAtTheBottom()
    {
        var top = Of(SurfaceKind.Front, tint: 0f);
        var bottom = Of(SurfaceKind.Front, tint: 1f);

        Assert.True(top.G > top.R, "the top should be aqua");
        Assert.True(bottom.R > bottom.G, "the bottom should be violet");
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void ShadowDoesNotDependOnLightOrPosition(float value)
        => Assert.Equal(LogoPalette.Shadow, Of(SurfaceKind.Shadow, value, value));

    [Fact]
    public void ScalingAndMixingClampToValidChannels()
    {
        Assert.Equal(new Rgb(255, 255, 255), LogoPalette.Scale(new Rgb(200, 200, 200), 5));
        Assert.Equal(new Rgb(0, 0, 0), LogoPalette.Scale(new Rgb(200, 200, 200), -1));
        Assert.Equal(new Rgb(10, 20, 30), LogoPalette.Mix(new Rgb(10, 20, 30), new Rgb(250, 250, 250), 0));
        Assert.Equal(new Rgb(250, 250, 250), LogoPalette.Mix(new Rgb(10, 20, 30), new Rgb(250, 250, 250), 1));
        Assert.Equal(new Rgb(130, 135, 140), LogoPalette.Mix(new Rgb(10, 20, 30), new Rgb(250, 250, 250), 0.5));
    }

    [Theory]
    [InlineData(-3f)]
    [InlineData(7f)]
    public void OutOfRangeTintStillGivesAColour(float tint)
        => Assert.True(Brightness(Of(SurfaceKind.Front, tint: tint)) > 0);
}
