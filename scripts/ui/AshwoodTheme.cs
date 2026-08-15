using Godot;

namespace AshwoodCounty.UI;

/// <summary>
/// Shared, code-owned HUD theme. It deliberately uses Godot's native/system font
/// so the interface stays portable and does not depend on a bundled typeface.
///
/// The persistent HUD is built from bars rather than boxes. A masthead across
/// the top and a command bar along the bottom each carry a single hairline on
/// the edge that faces the world; nothing else gets a full outline. Buttons in
/// those bars are transparent until touched and mark their active state with a
/// brass underline instead of another filled rectangle. Panels that genuinely
/// float over the world (survivor, loot, toasts) keep their outline, because
/// there they are doing real work separating themselves from the terrain.
/// </summary>
public static class AshwoodTheme
{
    // One palette, named by role rather than by where it happens to be used.
    private static readonly Color Ink = new("0d110ff5");
    private static readonly Color Card = new("151a16f2");
    private static readonly Color Raised = new("222a21ff");
    private static readonly Color Brass = new("c2a35fff");
    private static readonly Color BrassDim = new("6b5d3c99");
    private static readonly Color Hairline = new("8a7a4ca8");
    private static readonly Color Parchment = new("e5dcc4ff");
    private static readonly Color Muted = new("9c9683ff");
    private static readonly Color Faint = new("7d7a6cff");
    private static readonly Color HoverParchment = new("fff0c2ff");

    // A single spacing scale keeps every bar, panel and row on the same rhythm.
    private const int GapTight = 4;
    private const int GapBase = 8;
    private const int GapWide = 12;

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
        theme.SetColor("font_disabled_color", "Button", new Color("6b6d63ff"));
        theme.SetColor("icon_normal_color", "Button", new Color("bda468ff"));
        theme.SetColor("icon_hover_color", "Button", new Color("f0cd7dff"));
        theme.SetColor("icon_pressed_color", "Button", new Color("ffe09aff"));
        theme.SetColor("icon_disabled_color", "Button", new Color("686b61ff"));
        theme.SetStylebox("normal", "Button", Box(Raised, BrassDim, 1, 9, 5));
        theme.SetStylebox("hover", "Button", Box(new Color("2e3729ff"), Brass, 1, 9, 5));
        theme.SetStylebox("pressed", "Button", Box(new Color("463d25ff"), new Color("e0b75fff"), 1, 9, 5));
        theme.SetStylebox("hover_pressed", "Button", Box(new Color("524628ff"), new Color("f0c66bff"), 1, 9, 5));
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());

        theme.SetStylebox("panel", "PanelContainer", Box(Card, BrassDim, 1, GapWide, GapBase));
        theme.SetStylebox("separator", "HSeparator", Line(new Color("6d6448a0"), 1));
        theme.SetStylebox("separator", "VSeparator", Line(new Color("6d644880"), 1));
        theme.SetConstant("separation", "HBoxContainer", GapBase);
        theme.SetConstant("separation", "VBoxContainer", GapTight);

        theme.SetStylebox("background", "ProgressBar", Box(new Color("0a0e0bf0"), new Color("2f342c00"), 0, 0, 0));
        theme.SetStylebox("fill", "ProgressBar", Box(new Color("77934fff"), new Color("00000000"), 0, 0, 0));
        theme.SetColor("font_color", "ProgressBar", new Color("00000000"));
        theme.SetColor("font_outline_color", "ProgressBar", new Color("00000000"));

        // Typography: three weights of emphasis and one muted caption size.
        // Anything that needs a fourth is usually a layout problem instead.
        AddLabelVariation(theme, "HudTitle", new Color("dfc481ff"), 16);
        AddLabelVariation(theme, "HudHeading", new Color("d8bc7aff"), 13);
        AddLabelVariation(theme, "HudMuted", Muted, 12);
        AddLabelVariation(theme, "HudResourceName", Faint, 9);
        AddLabelVariation(theme, "HudResourceValue", Parchment, 17);
        AddLabelVariation(theme, "HudTiny", Faint, 9);
        AddLabelVariation(theme, "HudSurvivorName", new Color("f0dfb5ff"), 16);
        AddLabelVariation(theme, "HudMapTitle", new Color("e8ce8cff"), 18);
        AddLabelVariation(theme, "HudMapSubtitle", new Color("91866dff"), 9);
        AddLabelVariation(theme, "HudMapDetail", Parchment, 13);

        // Bars: one hairline on the world-facing edge, nothing else.
        theme.SetTypeVariation("HudTopPanel", "PanelContainer");
        theme.SetStylebox("panel", "HudTopPanel",
            Bar(Ink, Hairline, bottom: 1, horizontal: 20, vertical: 9));
        theme.SetTypeVariation("HudToolbarPanel", "PanelContainer");
        theme.SetStylebox("panel", "HudToolbarPanel",
            Bar(Ink, Hairline, top: 1, horizontal: 14, vertical: GapTight));

        AddPanelVariation(theme, "HudPalettePanel", new Color("141a15ee"), new Color("60563d9e"), 1, GapWide, GapBase);
        AddPanelVariation(theme, "HudSurvivorPanel", new Color("111611f7"), new Color("7d6b429e"), 1, GapWide, GapWide);
        AddPanelVariation(theme, "HudLootRowPanel", new Color("1a211bf2"), new Color("53482c8c"), 1, GapBase, GapTight + 1);
        AddPanelVariation(theme, "HudToastPanel", new Color("141a15f4"), new Color("9c8349c4"), 1, GapWide, GapBase);
        AddPanelVariation(theme, "HudMapDetailPanel", new Color("101510ea"), new Color("6d5f3d9e"), 1, GapWide, GapBase);

        // Bar buttons: no chrome until hovered, brass underline when active.
        AddBarButtonVariation(theme, "HudCategoryButton", 11, horizontal: 15);
        // Tempo controls are single glyphs, so they get tight padding of their
        // own; the command bar's generous padding would clip them to nothing.
        AddBarButtonVariation(theme, "HudSpeedButton", 12, horizontal: 7);
        // Survivor tabs are the same idea one level down: a row of labels with
        // a rule under the active one, rather than four more filled pills.
        AddBarButtonVariation(theme, "HudTabButton", 10, horizontal: 9, vertical: GapTight + 1);

        // Content buttons still read as buttons; they sit inside outlined panels
        // where a flat label would be ambiguous.
        AddButtonVariation(theme, "HudActionButton", 11, new Color("1c231dff"));
        AddButtonVariation(theme, "HudPriorityButton", 10, new Color("181f19ff"));

        theme.SetStylebox("panel", "TooltipPanel", Box(new Color("0f140ffc"), new Color("bd9d57ff"), 1, GapWide, GapBase));
        theme.SetColor("font_color", "TooltipLabel", Parchment);
        theme.SetColor("font_shadow_color", "TooltipLabel", new Color("000000c0"));
        theme.SetConstant("shadow_offset_x", "TooltipLabel", 1);
        theme.SetConstant("shadow_offset_y", "TooltipLabel", 1);
        theme.SetFontSize("font_size", "TooltipLabel", 12);

        AddButtonVariation(theme, "HudMapMarker", 10, new Color("101510ee"));
        AddButtonVariation(theme, "HudMapCloseButton", 11, new Color("161c17ff"));
        theme.SetStylebox("panel", "HudRuleSeparator", Bar(new Color("00000000"), Hairline, horizontal: 0, vertical: 0, top: 1));

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
        theme.SetStylebox("normal", name, Box(normal, BrassDim, 1, GapWide, GapTight + 1));
        theme.SetStylebox("hover", name, Box(new Color("2e3729ff"), Brass, 1, GapWide, GapTight + 1));
        theme.SetStylebox("pressed", name, Box(new Color("4a4025ff"), new Color("d9ae51ff"), 1, GapWide, GapTight + 1));
        theme.SetStylebox("hover_pressed", name, Box(new Color("544728ff"), new Color("edc26bff"), 1, GapWide, GapTight + 1));
        theme.SetStylebox("focus", name, new StyleBoxEmpty());
    }

    /// <summary>
    /// A button that lives inside a bar: invisible at rest, a soft wash on
    /// hover, and a brass underline when it is the active choice. Removing the
    /// per-button rectangle is most of what separates a command bar from a row
    /// of dashboard tiles.
    /// </summary>
    private static void AddBarButtonVariation(Theme theme, string name, int fontSize, int horizontal, int vertical = GapBase)
    {
        theme.SetTypeVariation(name, "Button");
        theme.SetFontSize("font_size", name, fontSize);
        theme.SetStylebox("normal", name, Bar(new Color("00000000"), new Color("00000000"), bottom: 2, horizontal: horizontal, vertical: vertical));
        theme.SetStylebox("hover", name, Bar(new Color("ffffff12"), new Color("bda46877"), bottom: 2, horizontal: horizontal, vertical: vertical));
        theme.SetStylebox("pressed", name, Bar(new Color("d9ae511a"), Brass, bottom: 2, horizontal: horizontal, vertical: vertical));
        theme.SetStylebox("hover_pressed", name, Bar(new Color("d9ae5128"), new Color("f0c66bff"), bottom: 2, horizontal: horizontal, vertical: vertical));
        theme.SetStylebox("disabled", name, Bar(new Color("00000000"), new Color("00000000"), bottom: 2, horizontal: horizontal, vertical: vertical));
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

    /// <summary>A square-cornered fill with a hairline on selected edges only.</summary>
    private static StyleBoxFlat Bar(Color fill, Color border, int horizontal, int vertical,
        int top = 0, int bottom = 0, int left = 0, int right = 0)
    {
        return new StyleBoxFlat
        {
            BgColor = fill,
            BorderColor = border,
            BorderWidthLeft = left,
            BorderWidthTop = top,
            BorderWidthRight = right,
            BorderWidthBottom = bottom,
            ContentMarginLeft = horizontal,
            ContentMarginRight = horizontal,
            ContentMarginTop = vertical,
            ContentMarginBottom = vertical
        };
    }

    private static StyleBoxLine Line(Color color, int thickness)
    {
        return new StyleBoxLine { Color = color, Thickness = thickness };
    }
}
