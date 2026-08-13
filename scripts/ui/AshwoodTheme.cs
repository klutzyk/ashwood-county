using Godot;

namespace AshwoodCounty.UI;

public static class AshwoodTheme
{
    public static Theme Create()
    {
        Theme theme = new() { DefaultFontSize = 16 };
        theme.SetColor("font_color", "Label", new Color("e7e0cc"));
        theme.SetColor("font_color", "Button", new Color("e7e0cc"));
        theme.SetColor("font_hover_color", "Button", new Color("fff2c7"));
        theme.SetConstant("separation", "HBoxContainer", 8);
        theme.SetConstant("separation", "VBoxContainer", 7);
        theme.SetStylebox("panel", "PanelContainer", Box(new Color("171a16e8"), new Color("8a7448b8"), 8));
        theme.SetStylebox("normal", "Button", Box(new Color("292d25f2"), new Color("6d674fbb"), 6));
        theme.SetStylebox("hover", "Button", Box(new Color("3b402ff8"), new Color("b49a5de6"), 6));
        theme.SetStylebox("pressed", "Button", Box(new Color("4d492ff8"), new Color("d1ad63ff"), 6));
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        return theme;
    }

    private static StyleBoxFlat Box(Color fill, Color border, int radius)
    {
        StyleBoxFlat box = new() { BgColor=fill, BorderColor=border, BorderWidthLeft=1, BorderWidthTop=1, BorderWidthRight=1, BorderWidthBottom=1,
            CornerRadiusTopLeft=radius, CornerRadiusTopRight=radius, CornerRadiusBottomLeft=radius, CornerRadiusBottomRight=radius,
            ContentMarginLeft=12, ContentMarginRight=12, ContentMarginTop=8, ContentMarginBottom=8 };
        return box;
    }
}
