using Godot;

namespace AshwoodCounty.UI;

/// <summary>
/// Shared, code-owned HUD theme. It deliberately uses Godot's native/system font
/// so the interface stays portable and does not depend on a bundled typeface.
/// </summary>
public static class AshwoodTheme
{
    private static readonly Color Card = new("151a16f2");
    private static readonly Color Brass = new("c2a35fff");
    private static readonly Color BrassDim = new("65593dcc");
    private static readonly Color Parchment = new("e5dcc4ff");
    private static readonly Color Muted = new("a9a38fff");
    private static readonly Color HoverParchment = new("fff0c2ff");

    public static Theme Create()
    {
        Theme theme = new() { DefaultFontSize = 13 };

        theme.SetColor("font_color", "Label", Parchment);
        theme.SetColor("font_shadow_color", "Label", new Color("00000080"));
        theme.SetConstant("shadow_offset_x", "Label", 1);
        theme.SetConstant("shadow_offset_y", "Label", 1);

        theme.SetColor("font_color", "Button", Parchment);
        theme.SetColor("font_hover_color", "Button", HoverParchment);
        theme.SetColor("font_pressed_color", "Button", new Color("ffe09aff"));
        theme.SetColor("font_disabled_color", "Button", new Color("77796fff"));
        theme.SetColor("icon_normal_color", "Button", new Color("c8ad70ff"));
        theme.SetColor("icon_hover_color", "Button", new Color("f0cd7dff"));
        theme.SetColor("icon_pressed_color", "Button", new Color("ffe09aff"));
        theme.SetColor("icon_disabled_color", "Button", new Color("686b61ff"));
        theme.SetStylebox("normal", "Button", Box(new Color("1b211cf2"), BrassDim, 4, 7, 4));
        theme.SetStylebox("hover", "Button", Box(new Color("30372df7"), Brass, 4, 7, 4));
        theme.SetStylebox("pressed", "Button", Box(new Color("4a4027fa"), new Color("e0b75fff"), 4, 7, 4));
        theme.SetStylebox("hover_pressed", "Button", Box(new Color("584b2afa"), new Color("f0c66bff"), 4, 7, 4));
        theme.SetStylebox("disabled", "Button", Box(new Color("171b17c8"), new Color("41443b99"), 4, 7, 4));
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());

        theme.SetStylebox("panel", "PanelContainer", Box(Card, BrassDim, 6, 10, 8));
        theme.SetStylebox("separator", "HSeparator", Line(new Color("766b4eb0"), 1));
        theme.SetStylebox("separator", "VSeparator", Line(new Color("766b4e90"), 1));
        theme.SetConstant("separation", "HBoxContainer", 5);
        theme.SetConstant("separation", "VBoxContainer", 5);

        theme.SetStylebox("background", "ProgressBar", Box(new Color("0d110ef0"), new Color("353a32cc"), 3, 0, 0));
        theme.SetStylebox("fill", "ProgressBar", Box(new Color("77934fff"), new Color("9cae6cff"), 3, 0, 0));
        theme.SetColor("font_color", "ProgressBar", new Color("00000000"));
        theme.SetColor("font_outline_color", "ProgressBar", new Color("00000000"));

        AddLabelVariation(theme, "HudTitle", new Color("d8bc7aff"), 15);
        AddLabelVariation(theme, "HudHeading", new Color("d8bc7aff"), 13);
        AddLabelVariation(theme, "HudMuted", Muted, 12);
        AddLabelVariation(theme, "HudResourceName", Muted, 10);
        AddLabelVariation(theme, "HudResourceValue", Parchment, 14);
        AddLabelVariation(theme, "HudTiny", Muted, 10);
        AddLabelVariation(theme, "HudSurvivorName", new Color("f0dfb5ff"), 16);
        AddLabelVariation(theme, "HudMapTitle", new Color("e8ce8cff"), 18);
        AddLabelVariation(theme, "HudMapSubtitle", new Color("91866dff"), 9);
        AddLabelVariation(theme, "HudMapDetail", Parchment, 13);

        AddPanelVariation(theme, "HudTopPanel", new Color("101510f2"), new Color("71613dc7"), 6, 11, 6);
        AddPanelVariation(theme, "HudToolbarPanel", new Color("101510f1"), new Color("7c6a43d1"), 6, 5, 4);
        AddPanelVariation(theme, "HudPalettePanel", new Color("151a16ed"), new Color("665b42ba"), 5, 6, 5);
        AddPanelVariation(theme, "HudSurvivorPanel", new Color("121713f5"), new Color("877246d9"), 7, 10, 9);
        AddPanelVariation(theme, "HudToastPanel", new Color("151a16f2"), new Color("a1864dd9"), 5, 8, 6);
        AddPanelVariation(theme, "HudMapDetailPanel", new Color("101510e6"), new Color("756541a8"), 5, 8, 7);

        AddButtonVariation(theme, "HudCategoryButton", 10, new Color("151a16ee"));
        AddButtonVariation(theme, "HudActionButton", 10, new Color("1b211cf0"));
        AddButtonVariation(theme, "HudTabButton", 10, new Color("141914e5"));
        AddButtonVariation(theme, "HudPriorityButton", 9, new Color("171c18e9"));
        AddButtonVariation(theme, "HudSpeedButton", 10, new Color("161b17eb"));

        theme.SetStylebox("panel", "TooltipPanel", Box(new Color("111611fc"), new Color("d1ad60ff"), 5, 10, 7));
        theme.SetColor("font_color", "TooltipLabel", Parchment);
        theme.SetColor("font_shadow_color", "TooltipLabel", new Color("000000c0"));
        theme.SetConstant("shadow_offset_x", "TooltipLabel", 1);
        theme.SetConstant("shadow_offset_y", "TooltipLabel", 1);
        theme.SetFontSize("font_size", "TooltipLabel", 12);
        AddButtonVariation(theme, "HudMapMarker", 10, new Color("101510e8"));
        theme.SetStylebox("normal", "HudMapMarker", Box(new Color("101510e8"), new Color("61583fc4"), 4, 9, 4));
        theme.SetStylebox("hover", "HudMapMarker", Box(new Color("293126f4"), new Color("c5a45fff"), 4, 9, 4));
        theme.SetStylebox("pressed", "HudMapMarker", Box(new Color("4a4028f7"), new Color("e0b85fff"), 4, 9, 4));
        theme.SetStylebox("hover_pressed", "HudMapMarker", Box(new Color("574a2cf8"), new Color("f0ca73ff"), 4, 9, 4));
        AddButtonVariation(theme, "HudMapCloseButton", 10, new Color("151a16ec"));

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
        theme.SetStylebox("hover_pressed", name, Box(new Color("5b4d2bfa"), new Color("edc26bff"), 4, 8, 5));
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
