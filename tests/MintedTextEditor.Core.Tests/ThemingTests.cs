using MintedTextEditor.Core.Rendering;
using MintedTextEditor.Core.Theming;

namespace MintedTextEditor.Core.Tests;

public class ThemingTests
{
    // ──────────────────────────────────────────────────────────────────
    // EditorTheme.Create dispatch
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_Light_ReturnsSameAsCreateLight()
    {
        var fromEnum  = EditorTheme.Create(EditorThemeMode.Light);
        var fromMethod = EditorTheme.CreateLight();

        Assert.Equal(fromMethod.Background,         fromEnum.Background);
        Assert.Equal(fromMethod.DefaultTextColor,   fromEnum.DefaultTextColor);
        Assert.Equal(fromMethod.DefaultFontFamily,  fromEnum.DefaultFontFamily);
    }

    [Fact]
    public void Create_Dark_ReturnsSameAsCreateDark()
    {
        var fromEnum   = EditorTheme.Create(EditorThemeMode.Dark);
        var fromMethod = EditorTheme.CreateDark();

        Assert.Equal(fromMethod.Background,       fromEnum.Background);
        Assert.Equal(fromMethod.DefaultTextColor, fromEnum.DefaultTextColor);
    }

    [Fact]
    public void Create_HighContrast_ReturnsSameAsCreateHighContrast()
    {
        var fromEnum   = EditorTheme.Create(EditorThemeMode.HighContrast);
        var fromMethod = EditorTheme.CreateHighContrast();

        Assert.Equal(fromMethod.Background,       fromEnum.Background);
        Assert.Equal(fromMethod.DefaultTextColor, fromEnum.DefaultTextColor);
    }

    // ──────────────────────────────────────────────────────────────────
    // Light theme verifications
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void LightTheme_Background_IsWhite()
    {
        var style = EditorTheme.CreateLight();
        Assert.Equal(EditorColor.White, style.Background);
    }

    [Fact]
    public void LightTheme_DefaultTextColor_IsBlack()
    {
        var style = EditorTheme.CreateLight();
        Assert.Equal(EditorColor.Black, style.DefaultTextColor);
    }

    [Fact]
    public void LightTheme_CaretColor_IsBlack()
    {
        var style = EditorTheme.CreateLight();
        Assert.Equal(EditorColor.Black, style.CaretColor);
    }

    [Fact]
    public void LightTheme_DefaultFontSize_IsPositive()
    {
        var style = EditorTheme.CreateLight();
        Assert.True(style.DefaultFontSize > 0);
    }

    [Fact]
    public void LightTheme_DefaultFontFamily_IsNotEmpty()
    {
        var style = EditorTheme.CreateLight();
        Assert.False(string.IsNullOrEmpty(style.DefaultFontFamily));
    }

    [Fact]
    public void LightTheme_BorderWidth_IsPositive()
    {
        var style = EditorTheme.CreateLight();
        Assert.True(style.BorderWidth > 0);
    }

    [Fact]
    public void LightTheme_CaretWidth_IsPositive()
    {
        var style = EditorTheme.CreateLight();
        Assert.True(style.CaretWidth > 0);
    }

    [Fact]
    public void LightTheme_SelectionHighlightColor_IsOpaque()
    {
        var style = EditorTheme.CreateLight();
        Assert.Equal(255, style.SelectionHighlightColor.A);
    }

    [Fact]
    public void LightTheme_PaddingAllSides_ArePositive()
    {
        var style = EditorTheme.CreateLight();
        Assert.True(style.Padding.Top    > 0);
        Assert.True(style.Padding.Right  > 0);
        Assert.True(style.Padding.Bottom > 0);
        Assert.True(style.Padding.Left   > 0);
    }

    // ──────────────────────────────────────────────────────────────────
    // Dark theme verifications
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void DarkTheme_Background_IsNotWhite()
    {
        var style = EditorTheme.CreateDark();
        Assert.NotEqual(EditorColor.White, style.Background);
    }

    [Fact]
    public void DarkTheme_DefaultTextColor_IsLight()
    {
        var style = EditorTheme.CreateDark();
        // Light text for dark background: all channels >= 128
        Assert.True(style.DefaultTextColor.R >= 128 ||
                    style.DefaultTextColor.G >= 128 ||
                    style.DefaultTextColor.B >= 128);
    }

    [Fact]
    public void DarkTheme_Background_IsDark()
    {
        var style = EditorTheme.CreateDark();
        // Dark background: all channels < 128
        Assert.True(style.Background.R < 128 &&
                    style.Background.G < 128 &&
                    style.Background.B < 128);
    }

    // ──────────────────────────────────────────────────────────────────
    // High-contrast theme verifications
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void HighContrastTheme_Background_IsBlack()
    {
        var style = EditorTheme.CreateHighContrast();
        Assert.Equal(EditorColor.Black, style.Background);
    }

    [Fact]
    public void HighContrastTheme_DefaultTextColor_IsWhite()
    {
        var style = EditorTheme.CreateHighContrast();
        Assert.Equal(EditorColor.White, style.DefaultTextColor);
    }

    [Fact]
    public void HighContrastTheme_BorderWidth_IsGreaterThanLight()
    {
        var hc    = EditorTheme.CreateHighContrast();
        var light = EditorTheme.CreateLight();
        Assert.True(hc.BorderWidth >= light.BorderWidth);
    }

    [Fact]
    public void HighContrastTheme_SelectionTextColor_IsBlack()
    {
        var style = EditorTheme.CreateHighContrast();
        // White-on-yellow selection in HC
        Assert.Equal(EditorColor.Black, style.SelectionTextColor);
    }

    [Fact]
    public void HighContrastTheme_FocusRingColor_IsYellow()
    {
        var style = EditorTheme.CreateHighContrast();
        Assert.Equal(EditorColor.Yellow, style.FocusRingColor);
    }

    // ──────────────────────────────────────────────────────────────────
    // Custom style overrides
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void CustomStyle_OverridesBackgroundColor()
    {
        var custom = EditorTheme.CreateLight() with { Background = EditorColor.Red };

        Assert.Equal(EditorColor.Red, custom.Background);
        // Other properties remain from the light theme
        Assert.Equal(EditorColor.Black, custom.DefaultTextColor);
    }

    [Fact]
    public void CustomStyle_OverridesFontSize()
    {
        var custom = EditorTheme.CreateLight() with { DefaultFontSize = 24f };

        Assert.Equal(24f, custom.DefaultFontSize);
    }

    [Fact]
    public void CustomStyle_OverridesPadding()
    {
        var padding = new EditorPadding(10, 20, 10, 20);
        var custom  = EditorTheme.CreateLight() with { Padding = padding };

        Assert.Equal(10f, custom.Padding.Top);
        Assert.Equal(20f, custom.Padding.Right);
    }

    // ──────────────────────────────────────────────────────────────────
    // EditorPadding helpers
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void EditorPadding_All_SetsSidesUniformly()
    {
        var padding = EditorPadding.All(12f);

        Assert.Equal(12f, padding.Top);
        Assert.Equal(12f, padding.Right);
        Assert.Equal(12f, padding.Bottom);
        Assert.Equal(12f, padding.Left);
    }
}
