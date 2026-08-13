using Godot;

namespace AshwoodCounty.UI;

/// <summary>
/// Shared, code-owned HUD theme. It deliberately uses Godot's native/system font
/// so the interface stays portable and does not depend on a bundled typeface.
/// </summary>
public static class AshwoodTheme
{
    private static readonly Color Card = new("1a1f1aed");
    private static readonly Color Brass = new("b89b58ff");
    private static readonly Color BrassDim = new("6f6244d9");
    private static readonly Color Parchment = new("e5dcc4ff");
    private static readonly Color Muted = new("a9a38fff");

    public static Theme Create()
    {
        Theme theme = new() { DefaultFontSize = 14 };

        theme.SetColor("font_color", "Label", Parchment);
        theme.SetColor("font_shadow_color", "Label", new Color("00000080"));
        theme.SetConstant("shadow_offset_x", "Label", 1);
        theme.SetConstant("shadow_offset_y", "Label", 1);

        theme.SetColor("font_color", "Button", Parchment);
        theme.SetColor("font_hover_color", "Button", new Color("fff0c2ff"));
        theme.SetColor("font_pressed_color", "Button", new Color("ffe09aff"));
        theme.SetColor("font_disabled_color", "Button", new Color("77796fff"));
        theme.SetStylebox("normal", "Button", Box(new Color("20251ff2"), BrassDim, 4, 9, 6));
        theme.SetStylebox("hover", "Button", Box(new Color("31372bf7"), Brass, 4, 9, 6));
        theme.SetStylebox("pressed", "Button", Box(new Color("4a4027fa"), new Color("d6ae58ff"), 4, 9, 6));
        theme.SetStylebox("disabled", "Button", Box(new Color("181b18c8"), new Color("41443b99"), 4, 9, 6));
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());

        theme.SetStylebox("panel", "PanelContainer", Box(Card, BrassDim, 7, 12, 10));
        theme.SetStylebox("separator", "HSeparator", Line(new Color("766b4eb0"), 1));
        theme.SetStylebox("separator", "VSeparator", Line(new Color("766b4e90"), 1));
        theme.SetConstant("separation", "HBoxContainer", 7);
        theme.SetConstant("separation", "VBoxContainer", 7);

        theme.SetStylebox("background", "ProgressBar", Box(new Color("0d110ef0"), new Color("353a32cc"), 3, 0, 0));
        theme.SetStylebox("fill", "ProgressBar", Box(new Color("77934fff"), new Color("9cae6cff"), 3, 0, 0));
        theme.SetColor("font_color", "ProgressBar", new Color("00000000"));
        theme.SetColor("font_outline_color", "ProgressBar", new Color("00000000"));

        AddLabelVariation(theme, "HudTitle", new Color("d8bc7aff"), 17);
        AddLabelVariation(theme, "HudHeading", new Color("d8bc7aff"), 14);
        AddLabelVariation(theme, "HudMuted", Muted, 12);
        AddLabelVariation(theme, "HudResourceName", Muted, 10);
        AddLabelVariation(theme, "HudResourceValue", Parchment, 16);
        AddLabelVariation(theme, "HudTiny", Muted, 10);
        AddLabelVariation(theme, "HudSurvivorName", new Color("f0dfb5ff"), 18);

        AddPanelVariation(theme, "HudTopPanel", new Color("121712f5"), new Color("71613dd9"), 8, 14, 8);
        AddPanelVariation(theme, "HudToolbarPanel", new Color("121611f4"), new Color("7c6a43df"), 7, 7, 6);
        AddPanelVariation(theme, "HudPalettePanel", new Color("171b16f2"), new Color("665b42cc"), 6, 8, 7);
        AddPanelVariation(theme, "HudSurvivorPanel", new Color("151a16f7"), new Color("877246e8"), 8, 12, 11);
        AddPanelVariation(theme, "HudToastPanel", new Color("171c17f5"), new Color("a1864de6"), 6, 10, 8);

        AddButtonVariation(theme, "HudCategoryButton", 12, new Color("171b17f0"));
        AddButtonVariation(theme, "HudActionButton", 11, new Color("20251ff2"));
        AddButtonVariation(theme, "HudTabButton", 11, new Color("171b17e8"));
        AddButtonVariation(theme, "HudPriorityButton", 10, new Color("191d19eb"));
        AddButtonVariation(theme, "HudSpeedButton", 12, new Color("181d18ed"));

        return theme;
    }

    private static void AddLabelVariation(Theme theme, string name, Color color, int size)
    {
        theme.SetTypeVariation(name, "Label");
        theme.SetColor("font_color", name, color);
        theme.SetFontSize("font_size", name, size);
    }

    private static void AddPanelVariation(Theme theme, string name, Color fill, Color border, int radius, int horizontalMargin, int verticalMargin)
    {
        theme.SetTypeVariation(name, "PanelContainer");
        theme.SetStylebox("panel", name, Box(fill, border, radius, horizontalMargin, verticalMargin));
    }

    private static void AddButtonVariation(Theme theme, string name, int fontSize, Color normal)
    {
        theme.SetTypeVariation(name, "Button");
        theme.SetFontSize("font_size", name, fontSize);
        theme.SetStylebox("normal", name, Box(normal, BrassDim, 4, 8, 5));
        theme.SetStylebox("hover", name, Box(new Color("33382bf7"), Brass, 4, 8, 5));
        theme.SetStylebox("pressed", name, Box(new Color("514525fa"), new Color("d9ae51ff"), 4, 8, 5));
        theme.SetStylebox("focus", name, new StyleBoxEmpty());
    }

    private static StyleBoxFlat Box(Color fill, Color border, int radius, int horizontalMargin, int verticalMargin)
    {
        return new StyleBoxFlat
        {
            BgColor = fill,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = horizontalMargin,
            ContentMarginRight = horizontalMargin,
            ContentMarginTop = verticalMargin,
            ContentMarginBottom = verticalMargin
        };
    }

    private static StyleBoxLine Line(Color color, int thickness)
    {
        return new StyleBoxLine { Color = color, Thickness = thickness };
    }
}
