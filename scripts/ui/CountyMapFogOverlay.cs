using AshwoodCounty.World.Fog;
using Godot;

namespace AshwoodCounty.UI;

/// <summary>One filtered strategic fog texture; avoids visible tiles and per-frame draw spam.</summary>
public partial class CountyMapFogOverlay:Control
{
    private const int MaskWidth=96;
    private const int MaskHeight=80;
    public CountyFogOfWar Fog{get;set;}=null!;
    private ImageTexture _texture=null!;
    private double _refresh;
    public override void _Ready(){MouseFilter=MouseFilterEnum.Ignore;TextureFilter=TextureFilterEnum.Linear;RefreshMask();}
    public override void _Process(double delta){_refresh-=delta;if(_refresh<=0){_refresh=.3;RefreshMask();}}
    public override void _Draw(){if(_texture is not null)DrawTextureRect(_texture,new Rect2(Vector2.Zero,Size),false);}
    private void RefreshMask()
    {
        if(Fog is null)return;
        Image image=Image.CreateEmpty(MaskWidth,MaskHeight,false,Image.Format.Rgba8);
        for(int y=0;y<MaskHeight;y++)for(int x=0;x<MaskWidth;x++)
        {
            Vector2 county=new((x+.5f)/MaskWidth*384f,(y+.5f)/MaskHeight*320f);
            FogCellVisibility state=Fog.GetVisibilityAt(county);
            float alpha=state switch{FogCellVisibility.Visible=>.02f,FogCellVisibility.Explored=>.23f,_=>.62f};
            image.SetPixel(x,y,new Color(.025f,.032f,.024f,alpha));
        }
        if(_texture is null)_texture=ImageTexture.CreateFromImage(image);else _texture.Update(image);
        QueueRedraw();
    }
}
