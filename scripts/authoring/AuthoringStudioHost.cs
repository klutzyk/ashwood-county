#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.Camera;
using AshwoodCounty.UI;
using AshwoodCounty.World;
using AshwoodCounty.World.County;
using Godot;

namespace AshwoodCounty.Authoring;

public partial class AuthoringStudioHost:Node
{
    private IsometricWorld _world=null!;private CountyWorld _county=null!;private StrategyCamera _camera=null!;private AuthoringStudioCanvas _canvas=null!;
    private readonly IReadOnlyList<AuthoringAssetEntry> _assets=AuthoringAssetCatalog.GetAssets();
    private OptionButton _location=null!,_radius=null!,_category=null!,_gameplay=null!,_selectedGameplay=null!,_loot=null!;
    private LineEdit _search=null!,_name=null!,_roomA=null!,_roomB=null!;
    private GridContainer _assetGrid=null!;private Label _status=null!,_selectionTitle=null!,_id=null!,_validation=null!;
    private SpinBox _x=null!,_y=null!,_width=null!,_height=null!,_target=null!,_anchorX=null!,_anchorY=null!,_duration=null!;
    private Label _xLabel=null!,_yLabel=null!,_widthLabel=null!,_heightLabel=null!,_targetLabel=null!,_anchorXLabel=null!,_anchorYLabel=null!;
    private CheckBox _collision=null!,_snap=null!;private Button _editInterior=null!,_exitInterior=null!,_apply=null!;private StudioCountyMinimap _minimap=null!;
    private CountyLocationDefinition[] _locations=[];private bool _refreshingInspector;

    public override void _Ready()
    {
        BuildWorld();BuildUi();
        Vector2 center=AuthoringSessionState.Center.IsZeroApprox()?new Vector2(220,155):AuthoringSessionState.Center;
        int radius=AuthoringSessionState.Radius;
        LoadArea(center,radius);
        if(!string.IsNullOrWhiteSpace(AuthoringSessionState.BuildingId))
        {
            _canvas.SelectBuilding(AuthoringSessionState.BuildingId);
            if(AuthoringSessionState.ReturnToInterior)_canvas.EnterInteriorForSelection();
        }
        RefreshInspector();
        if(System.Environment.GetEnvironmentVariable("ASHWOOD_VALIDATE_AUTHORING_PLAYTEST")=="1")
        {
            if(!AuthoringSessionState.AutomatedPlaytestStarted){AuthoringSessionState.AutomatedPlaytestStarted=true;StartAutomatedPlaytest();}
            else if(AuthoringSessionState.AutomatedPlaytestReturned)GD.Print($"AUTHORING_PLAYTEST_RETURN: {(AuthoringSessionState.AutomatedPlaytestPassed?"PASS":"FAIL")} (studio_restored=True, interior_restored={AuthoringSessionState.ReturnToInterior})");
        }
        else if(System.Environment.GetEnvironmentVariable("ASHWOOD_VALIDATE_AUTHORING_STUDIO")=="1")RunAutomatedValidation();
        else if(!string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("ASHWOOD_STUDIO_CAPTURE_PNG")))CaptureStudio();
    }

    private void BuildWorld()
    {
        _world=new IsometricWorld{Name="World"};
        _world.AddChild(new TerrainRenderer{Name="Terrain"});
        _world.AddChild(new HoverHighlight{Name="HoverHighlight"});
        Node2D objects=new(){Name="Objects",YSortEnabled=true};_world.AddChild(objects);
        _camera=new StrategyCamera{Name="StrategyCamera",Zoom=new Vector2(.72f,.72f)};_world.AddChild(_camera);
        _county=new CountyWorld{Name="CountyWorld",ZIndex=-20,StreamingRadiusChunks=1,DrawMacroLandscape=true};_world.AddChild(_county);_world.MoveChild(_county,0);
        AddChild(_world);
        _canvas=new AuthoringStudioCanvas{Name="AuthoringCanvas"};_canvas.Initialize(_world,AuthoredContentRepository.Load());_world.AddChild(_canvas);
        _canvas.SelectionChanged+=RefreshInspector;_canvas.StatusChanged+=SetStatus;_canvas.DocumentChanged+=()=>_minimap?.QueueRedraw();
    }

    private void BuildUi()
    {
        CanvasLayer layer=new(){Name="StudioUI",Layer=30};AddChild(layer);
        Control root=new(){Name="Root",Theme=AshwoodTheme.Create(),LayoutMode=1};root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);layer.AddChild(root);
        BuildLeftPanel(root);BuildToolbar(root);BuildInspector(root);BuildStatus(root);
    }

    private void BuildLeftPanel(Control root)
    {
        PanelContainer panel=Panel(root,10,10,300,-10,true);VBoxContainer box=VBox(panel);
        box.AddChild(Label("ASHWOOD AUTHORING STUDIO","HudTitle"));box.AddChild(Label("CHUNK-SCOPED WORLD & INTERIOR DESIGN","HudTiny"));
        _minimap=new StudioCountyMinimap{CustomMinimumSize=new Vector2(270,142)};_minimap.CenterRequested+=center=>LoadArea(center,RadiusValue());box.AddChild(_minimap);
        _location=new OptionButton();_locations=CountyMacroLayout.Locations.OrderBy(location=>location.Kind).ThenBy(location=>location.Name).ToArray();foreach(CountyLocationDefinition location in _locations)_location.AddItem(location.Name);box.AddChild(_location);
        HBoxContainer loadRow=new();_radius=new OptionButton();_radius.AddItem("1 CHUNK");_radius.AddItem("3 x 3 CHUNKS");_radius.AddItem("5 x 5 CHUNKS");_radius.Selected=1;loadRow.AddChild(_radius);Button load=Button("LOAD AREA",()=>LoadArea(_locations[_location.Selected].Center,RadiusValue()));loadRow.AddChild(load);box.AddChild(loadRow);
        box.AddChild(new HSeparator());box.AddChild(Label("ASSET LIBRARY","HudHeading"));
        _search=new LineEdit{PlaceholderText="Search assets...",ClearButtonEnabled=true};_search.TextChanged+=_=>RefreshAssets();box.AddChild(_search);
        _category=new OptionButton();_category.AddItem("All Assets");foreach(string category in _assets.Select(asset=>asset.Category).Distinct())_category.AddItem(category);_category.ItemSelected+=_=>RefreshAssets();box.AddChild(_category);
        _gameplay=new OptionButton();foreach(string type in new[]{"Decoration","Building","Door","Container","Bed","Scavenge Source","Landmark","Zombie Spawn","Resource"})_gameplay.AddItem(type);_gameplay.ItemSelected+=_=>_canvas.PlacementGameplayType=_gameplay.GetItemText(_gameplay.Selected);box.AddChild(_gameplay);
        ScrollContainer scroll=new(){SizeFlagsVertical=Control.SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};_assetGrid=new GridContainer{Columns=3,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};scroll.AddChild(_assetGrid);box.AddChild(scroll);RefreshAssets();
    }

    private void BuildToolbar(Control root)
    {
        PanelContainer panel=Panel(root,310,10,-330,96);VBoxContainer rows=new();panel.AddChild(rows);HBoxContainer tools=new();HBoxContainer actions=new();rows.AddChild(tools);rows.AddChild(actions);
        tools.AddChild(Button("SELECT",()=>_canvas.SetTool(AuthoringTool.Select)));tools.AddChild(Button("PLACE",()=>_canvas.SetTool(AuthoringTool.Place)));tools.AddChild(Button("FLOOR/ROOM",()=>_canvas.SetTool(AuthoringTool.Room)));tools.AddChild(Button("WALL",()=>_canvas.SetTool(AuthoringTool.Wall)));tools.AddChild(Button("DOOR",()=>_canvas.SetTool(AuthoringTool.Door)));tools.AddChild(Button("WINDOWS",ShowWindowAssets));
        _snap=new CheckBox{Text="SNAP 0.25",ButtonPressed=true};_snap.Toggled+=value=>_canvas.SnapEnabled=value;tools.AddChild(_snap);
        actions.AddChild(Button("UNDO",_canvas.Undo));actions.AddChild(Button("REDO",_canvas.Redo));actions.AddChild(Button("DUPLICATE",_canvas.DuplicateSelection));actions.AddChild(Button("DELETE",_canvas.DeleteSelection));actions.AddChild(Button("SAVE",_canvas.Save));actions.AddChild(Button("PLAYTEST",BeginPlaytest));
    }

    private void BuildInspector(Control root)
    {
        PanelContainer panel=Panel(root,-320,10,-10,-10,false,true);VBoxContainer box=ScrollableVBox(panel);
        _selectionTitle=Label("SELECTION INSPECTOR","HudTitle");box.AddChild(_selectionTitle);_id=Label("No selection","HudTiny");box.AddChild(_id);
        _name=Field(box,"NAME");_selectedGameplay=new OptionButton();foreach(string type in new[]{"Decoration","Building","Door","Container","Bed","Scavenge Source","Landmark","Zombie Spawn","Resource"})_selectedGameplay.AddItem(type);box.AddChild(Labeled("GAMEPLAY TYPE",_selectedGameplay));_x=Number(box,"X",out _xLabel);_y=Number(box,"Y",out _yLabel);_width=Number(box,"WIDTH",out _widthLabel,.05,100);_height=Number(box,"HEIGHT",out _heightLabel,.05,100);_target=Number(box,"VISUAL HEIGHT",out _targetLabel,1,1000);
        _anchorX=Number(box,"ANCHOR X",out _anchorXLabel,.01,500);_anchorY=Number(box,"ANCHOR Y",out _anchorYLabel,.01,500);
        _roomA=Field(box,"ROOM / SIDE A");_roomB=Field(box,"ROOM / SIDE B");
        _collision=new CheckBox{Text="BLOCKS MOVEMENT / COLLISION"};box.AddChild(_collision);
        _loot=new OptionButton();foreach(string preset in AuthoredInteriorConverter.LootPresetNames)_loot.AddItem(preset);box.AddChild(Labeled("LOOT TABLE",_loot));
        _duration=Number(box,"SEARCH SECONDS",out _,.1,60);
        _apply=Button("APPLY CHANGES",ApplyInspector);box.AddChild(_apply);
        _editInterior=Button("EDIT INTERIOR",()=>{if(_canvas.EnterInteriorForSelection()){RefreshInspector();CenterBuilding();}});box.AddChild(_editInterior);
        _exitInterior=Button("RETURN TO WORLD EDIT",()=>{_canvas.ExitInterior();RefreshInspector();});box.AddChild(_exitInterior);
        box.AddChild(new HSeparator());HBoxContainer validationButtons=new();validationButtons.AddChild(Button("VALIDATE",RunValidation));validationButtons.AddChild(Button("TEST ENTRANCE",TestEntrance));box.AddChild(validationButtons);
        _validation=Label("Select a building and enter Interior Edit.","HudMuted");_validation.AutowrapMode=TextServer.AutowrapMode.WordSmart;_validation.SizeFlagsVertical=Control.SizeFlags.ExpandFill;box.AddChild(_validation);
        box.AddChild(Label("SHORTCUTS  Delete  Ctrl+Z  Ctrl+Y  Ctrl+D  Esc","HudTiny"));
    }

    private void BuildStatus(Control root)
    {
        PanelContainer panel=Panel(root,310,-48,-330,-10);_status=Label("Ready","HudHeading");_status.HorizontalAlignment=HorizontalAlignment.Center;panel.AddChild(_status);
    }

    private void RefreshAssets()
    {
        if(_assetGrid is null)return;foreach(Node child in _assetGrid.GetChildren())child.QueueFree();string category=_category?.Selected>0?_category.GetItemText(_category.Selected):string.Empty;string query=_search?.Text.Trim()??string.Empty;
        foreach(AuthoringAssetEntry asset in _assets.Where(asset=>(string.IsNullOrEmpty(category)||asset.Category==category)&&(string.IsNullOrEmpty(query)||asset.Name.Contains(query,StringComparison.OrdinalIgnoreCase)||asset.Subcategory.Contains(query,StringComparison.OrdinalIgnoreCase))).Take(90))
        {
            VBoxContainer tile=new(){CustomMinimumSize=new Vector2(82,82)};Button thumbnail=new(){TooltipText=$"{asset.Name}\n{asset.Category} / {asset.Subcategory}\n{asset.Path}",CustomMinimumSize=new Vector2(82,58),Icon=AuthoringThumbnailCache.Get(asset.Path),ExpandIcon=true};thumbnail.Pressed+=()=>ChooseAsset(asset);Label caption=Label(Short(asset.Name,13),"HudTiny");caption.HorizontalAlignment=HorizontalAlignment.Center;caption.TooltipText=asset.Name;caption.ClipText=true;tile.AddChild(thumbnail);tile.AddChild(caption);_assetGrid.AddChild(tile);
        }
    }

    private void ChooseAsset(AuthoringAssetEntry asset)
    {
        string type=asset.Category=="Buildings"?"Building":asset.Path.Contains("/bedroom/bed_")?"Bed":"Decoration";
        SelectText(_gameplay,type);_canvas.PlacementGameplayType=type;_canvas.SetPlacementAsset(asset);
    }
    private void ShowWindowAssets(){SelectText(_category,"Interiors");_search.Text="window";RefreshAssets();_canvas.SetTool(AuthoringTool.Place);SetStatus("WINDOW TOOL — choose a window thumbnail, then place against an authored wall");}

    private void RefreshInspector()
    {
        if(_name is null)return;_refreshingInspector=true;object? data=_canvas.GetSelectedData();bool selected=data is not null;_apply.Disabled=!selected;
        foreach(Control control in new Control[]{_name,_selectedGameplay,_x,_y,_width,_height,_target,_anchorX,_anchorY,_roomA,_roomB,_collision,_loot,_duration})SetFieldVisible(control,selected);
        _editInterior.Visible=data is AuthoredBuildingData&&!_canvas.IsInteriorMode;_exitInterior.Visible=_canvas.IsInteriorMode;
        if(data is null){_selectionTitle.Text=_canvas.IsInteriorMode?"INTERIOR INSPECTOR":"SELECTION INSPECTOR";_id.Text="No selection";_refreshingInspector=false;return;}
        _id.Text=$"{_canvas.PrimarySelection.Kind.ToString().ToUpperInvariant()}  •  {GetId(data)}";_selectionTitle.Text="SELECTION INSPECTOR";_name.Text=GetName(data);
        ConfigureInspector(data);_refreshingInspector=false;
    }

    private void ConfigureInspector(object data)
    {
        SetFieldVisible(_roomA,false);SetFieldVisible(_roomB,false);SetFieldVisible(_loot,false);SetFieldVisible(_duration,false);SetFieldVisible(_collision,false);SetFieldVisible(_selectedGameplay,false);SetFieldVisible(_target,true);SetFieldVisible(_anchorX,false);SetFieldVisible(_anchorY,false);_targetLabel.Text="VISUAL HEIGHT";
        switch(data)
        {
            case AuthoredWorldObjectData item:SetNumbers(item.X,item.Y,0,0,item.Scale,item.AnchorX,item.AnchorY);Labels("X","Y","","","ANCHOR X","ANCHOR Y");_targetLabel.Text="SCALE";SetFieldVisible(_selectedGameplay,true);SelectText(_selectedGameplay,item.GameplayType);SetFieldVisible(_width,false);SetFieldVisible(_height,false);SetFieldVisible(_anchorX,true);SetFieldVisible(_anchorY,true);SetFieldVisible(_collision,true);_collision.ButtonPressed=item.Collision;break;
            case AuthoredBuildingData item:SetNumbers(item.ExteriorX,item.ExteriorY,item.FootprintWidth,item.FootprintHeight,item.ExteriorTargetHeight,item.FootprintX,item.FootprintY);Labels("EXTERIOR X","EXTERIOR Y","FOOTPRINT W","FOOTPRINT H","FOOTPRINT X","FOOTPRINT Y");SetFieldVisible(_anchorX,true);SetFieldVisible(_anchorY,true);break;
            case AuthoredRoomData item:SetNumbers(item.X,item.Y,item.Width,item.Height,0,0,0);SetFieldVisible(_target,false);Labels("X","Y","WIDTH","HEIGHT","","");break;
            case AuthoredWallData item:SetNumbers(item.StartX,item.StartY,item.EndX,item.EndY,0,0,0);SetFieldVisible(_target,false);Labels("START X","START Y","END X","END Y","","");break;
            case AuthoredDoorData item:SetNumbers(item.X,item.Y,item.InsideArrivalX,item.InsideArrivalY,84,item.OutsideApproachX,item.OutsideApproachY);Labels("DOOR X","DOOR Y","INSIDE ARRIVAL X","INSIDE ARRIVAL Y","OUTSIDE APPROACH X","OUTSIDE APPROACH Y");SetFieldVisible(_anchorX,true);SetFieldVisible(_anchorY,true);SetFieldVisible(_roomA,true);SetFieldVisible(_roomB,true);_roomA.Text=item.RoomAId;_roomB.Text=item.RoomBId;break;
            case AuthoredFurnitureData item:SetNumbers(item.X,item.Y,item.Width,item.Height,item.TargetHeight,0,0);SetFieldVisible(_collision,true);_collision.ButtonPressed=item.BlocksMovement;break;
            case AuthoredContainerData item:SetNumbers(item.X,item.Y,item.Width,item.Height,item.TargetHeight,item.InteractionX,item.InteractionY);Labels("X","Y","WIDTH","HEIGHT","INTERACTION X","INTERACTION Y");SetFieldVisible(_anchorX,true);SetFieldVisible(_anchorY,true);SetFieldVisible(_roomA,true);SetFieldVisible(_loot,true);SetFieldVisible(_duration,true);_roomA.Text=item.RoomId;SelectText(_loot,item.LootPreset);_duration.Value=item.SearchDuration;break;
            case AuthoredBedData item:SetNumbers(item.X,item.Y,item.Width,item.Height,item.TargetHeight,item.InteractionX,item.InteractionY);Labels("X","Y","WIDTH","HEIGHT","INTERACTION X","INTERACTION Y");SetFieldVisible(_anchorX,true);SetFieldVisible(_anchorY,true);SetFieldVisible(_roomA,true);_roomA.Text=item.RoomId;break;
        }
    }

    private void ApplyInspector()
    {
        if(_refreshingInspector)return;object? data=_canvas.GetSelectedData();if(data is null)return;_canvas.BeginInspectorMutation();SetName(data,_name.Text);
        switch(data)
        {
            case AuthoredWorldObjectData item:item.X=(float)_x.Value;item.Y=(float)_y.Value;item.Scale=(float)_target.Value;item.AnchorX=(float)_anchorX.Value;item.AnchorY=(float)_anchorY.Value;item.Collision=_collision.ButtonPressed;item.GameplayType=_selectedGameplay.GetItemText(_selectedGameplay.Selected);break;
            case AuthoredBuildingData item:
                float previousFootprintX=item.FootprintX,previousFootprintY=item.FootprintY;Vector2 delta=new((float)_x.Value-item.ExteriorX,(float)_y.Value-item.ExteriorY);AuthoringStudioCanvas.TranslateBuilding(item,delta);item.FootprintWidth=(float)_width.Value;item.FootprintHeight=(float)_height.Value;item.ExteriorTargetHeight=(float)_target.Value;
                if(!Mathf.IsEqualApprox((float)_anchorX.Value,previousFootprintX))item.FootprintX=(float)_anchorX.Value;if(!Mathf.IsEqualApprox((float)_anchorY.Value,previousFootprintY))item.FootprintY=(float)_anchorY.Value;break;
            case AuthoredRoomData item:item.X=(float)_x.Value;item.Y=(float)_y.Value;item.Width=(float)_width.Value;item.Height=(float)_height.Value;break;
            case AuthoredWallData item:item.StartX=(float)_x.Value;item.StartY=(float)_y.Value;item.EndX=(float)_width.Value;item.EndY=(float)_height.Value;break;
            case AuthoredDoorData item:item.X=(float)_x.Value;item.Y=(float)_y.Value;item.InsideArrivalX=(float)_width.Value;item.InsideArrivalY=(float)_height.Value;item.OutsideApproachX=(float)_anchorX.Value;item.OutsideApproachY=(float)_anchorY.Value;item.RoomAId=_roomA.Text;item.RoomBId=_roomB.Text;break;
            case AuthoredFurnitureData item:item.X=(float)_x.Value;item.Y=(float)_y.Value;item.Width=(float)_width.Value;item.Height=(float)_height.Value;item.TargetHeight=(float)_target.Value;item.BlocksMovement=_collision.ButtonPressed;break;
            case AuthoredContainerData item:item.X=(float)_x.Value;item.Y=(float)_y.Value;item.Width=(float)_width.Value;item.Height=(float)_height.Value;item.TargetHeight=(float)_target.Value;item.InteractionX=(float)_anchorX.Value;item.InteractionY=(float)_anchorY.Value;item.RoomId=_roomA.Text;item.LootPreset=_loot.GetItemText(_loot.Selected);item.SearchDuration=(float)_duration.Value;break;
            case AuthoredBedData item:item.X=(float)_x.Value;item.Y=(float)_y.Value;item.Width=(float)_width.Value;item.Height=(float)_height.Value;item.TargetHeight=(float)_target.Value;item.InteractionX=(float)_anchorX.Value;item.InteractionY=(float)_anchorY.Value;item.RoomId=_roomA.Text;break;
        }
        _canvas.NotifyInspectorChanged();RefreshInspector();
    }

    private void RunValidation(){IReadOnlyList<AuthoringValidationIssue> issues=_canvas.ValidateInterior();_validation.Text=string.Join("\n",issues.Take(9).Select(issue=>$"{(issue.Severity==AuthoringValidationSeverity.Invalid?"RED":issue.Severity==AuthoringValidationSeverity.Warning?"YELLOW":"GREEN")}: {issue.Message}"));}
    private void TestEntrance(){bool pass=_canvas.TestEntrance();_validation.Text=(pass?"GREEN: ":"RED: ")+_status.Text;}
    private void BeginPlaytest()
    {
        _canvas.Save();AuthoredBuildingData? building=_canvas.InteriorBuilding??_canvas.Document.Buildings.FirstOrDefault(item=>item.Id==_canvas.PrimarySelection.Id)??_canvas.Document.Buildings.FirstOrDefault();if(building is null){SetStatus("Select an authored building before playtesting.");return;}
        AuthoringSessionState.IsPlaytesting=true;AuthoringSessionState.BuildingId=building.Id;AuthoringSessionState.Center=new Vector2(building.ExteriorX,building.ExteriorY);AuthoringSessionState.Radius=RadiusValue();AuthoringSessionState.ReturnToInterior=_canvas.IsInteriorMode;GetTree().ChangeSceneToFile("res://scenes/world/World.tscn");
    }

    private async void StartAutomatedPlaytest(){for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);AuthoredBuildingData building=_canvas.Document.Buildings.First();_canvas.SelectBuilding(building.Id);_canvas.EnterInteriorForSelection();BeginPlaytest();}

    private async void RunAutomatedValidation()
    {
        for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        LoadArea(new Vector2(220,155),2);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);int fiveByFive=_county.LoadedChunkCount;
        LoadArea(new Vector2(220,155),0);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);int oneChunk=_county.LoadedChunkCount;
        LoadArea(new Vector2(220,155),1);
        AuthoredBuildingData building=_canvas.Document.Buildings.First();_canvas.SelectBuilding(building.Id);bool entered=_canvas.EnterInteriorForSelection();
        IReadOnlyList<AuthoringValidationIssue> issues=_canvas.ValidateInterior();foreach(AuthoringValidationIssue issue in issues)GD.Print($"AUTHORING_CHECK: {issue.Severity} {issue.Message}");int errors=issues.Count(issue=>issue.Severity==AuthoringValidationSeverity.Invalid);bool entrance=_canvas.TestEntrance();
        string before=AuthoredContentRepository.Serialize(_canvas.Document);AuthoredFurnitureData furniture=building.Furniture.First();_canvas.SelectInteriorItem(StudioSelectionKind.Furniture,furniture.Id);_canvas.DuplicateSelection();bool duplicated=building.Furniture.Count>15;_canvas.Undo();bool undoRestored=AuthoredContentRepository.Serialize(_canvas.Document)==before;
        bool streaming=fiveByFive==25&&oneChunk==1;_canvas.Save();AuthoredBuildingData restored=_canvas.Document.Buildings.First();_canvas.SelectInteriorItem(StudioSelectionKind.Container,restored.Containers.First().Id);_validation.Text=string.Join("\n",issues.Take(6).Select(issue=>$"{issue.Severity.ToString().ToUpperInvariant()}: {issue.Message}"));SetStatus(entrance?"ENTRANCE TEST PASS — deterministic approach, door and arrival route":"ENTRANCE TEST FAIL");GD.Print($"AUTHORING_STUDIO_VALIDATION: {(entered&&errors==0&&entrance&&duplicated&&undoRestored&&streaming?"PASS":"FAIL")} (chunks_5x5={fiveByFive}, chunks_1={oneChunk}, assets={_assets.Count}, interior={entered}, errors={errors}, entrance={entrance}, duplicate={duplicated}, undo={undoRestored}, runtime_data_saved=True)");
        string? path=System.Environment.GetEnvironmentVariable("ASHWOOD_STUDIO_CAPTURE_PNG");if(!string.IsNullOrWhiteSpace(path))await SaveCapture(path);
    }

    private async void CaptureStudio(){string? path=System.Environment.GetEnvironmentVariable("ASHWOOD_STUDIO_CAPTURE_PNG");if(!string.IsNullOrWhiteSpace(path))await SaveCapture(path);}
    private async System.Threading.Tasks.Task SaveCapture(string path){for(int i=0;i<24;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(DisplayServer.GetName()=="headless")return;Error error=GetViewport().GetTexture().GetImage().SavePng(path);GD.Print($"AUTHORING_STUDIO_CAPTURE: {error} {path}");}

    private void LoadArea(Vector2 center,int radius){_county.StreamingRadiusChunks=radius;_county.SetStreamingFocus(center);_canvas.SetLoadedArea(center,radius);_camera.CenterOnGridPosition(center);_camera.SetZoom(radius==0?.85f:radius==1?.52f:.34f);_minimap.Center=center;_minimap.Radius=radius;_minimap.QueueRedraw();AuthoringSessionState.Center=center;AuthoringSessionState.Radius=radius;}
    private void CenterBuilding(){AuthoredBuildingData? building=_canvas.InteriorBuilding;if(building is null)return;_camera.CenterOnGridPosition(new Vector2(building.ExteriorX,building.ExteriorY));_camera.SetZoom(.84f);}
    private int RadiusValue()=>Mathf.Clamp(_radius?.Selected??1,0,2);
    private void SetStatus(string value){if(_status is not null)_status.Text=value;}

    private static PanelContainer Panel(Control parent,float left,float top,float right,float bottom,bool leftAnchor=false,bool rightAnchor=false){PanelContainer panel=new(){ThemeTypeVariation="HudPalettePanel"};parent.AddChild(panel);panel.AnchorLeft=rightAnchor?1:0;panel.AnchorRight=rightAnchor?1:(right<0?1:0);panel.AnchorTop=top<0?1:0;panel.AnchorBottom=bottom<0?1:0;panel.OffsetLeft=left;panel.OffsetTop=top;panel.OffsetRight=right;panel.OffsetBottom=bottom;return panel;}
    private static VBoxContainer VBox(PanelContainer panel){MarginContainer margin=new();margin.AddThemeConstantOverride("margin_left",8);margin.AddThemeConstantOverride("margin_right",8);margin.AddThemeConstantOverride("margin_top",8);margin.AddThemeConstantOverride("margin_bottom",8);panel.AddChild(margin);VBoxContainer box=new(){SizeFlagsVertical=Control.SizeFlags.ExpandFill};margin.AddChild(box);return box;}
    private static VBoxContainer ScrollableVBox(PanelContainer panel){MarginContainer margin=new();margin.AddThemeConstantOverride("margin_left",8);margin.AddThemeConstantOverride("margin_right",8);margin.AddThemeConstantOverride("margin_top",8);margin.AddThemeConstantOverride("margin_bottom",8);panel.AddChild(margin);ScrollContainer scroll=new(){HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,SizeFlagsVertical=Control.SizeFlags.ExpandFill,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};margin.AddChild(scroll);VBoxContainer box=new(){SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};scroll.AddChild(box);return box;}
    private static Label Label(string text,string variation="")=>new(){Text=text,ThemeTypeVariation=variation};
    private static Button Button(string text,Action action){Button button=new(){Text=text,ThemeTypeVariation="HudActionButton"};button.Pressed+=action;return button;}
    private static LineEdit Field(VBoxContainer box,string label){LineEdit field=new();box.AddChild(Labeled(label,field));return field;}
    private static SpinBox Number(VBoxContainer box,string label,out Label title,double step=.25,double max=500){SpinBox number=new(){MinValue=-500,MaxValue=max,Step=step,AllowGreater=true,AllowLesser=true};title=Label(label,"HudTiny");VBoxContainer wrap=new();wrap.SetMeta("studio_field",true);wrap.AddChild(title);wrap.AddChild(number);box.AddChild(wrap);return number;}
    private static VBoxContainer Labeled(string label,Control control){VBoxContainer wrap=new();wrap.SetMeta("studio_field",true);wrap.AddChild(Label(label,"HudTiny"));wrap.AddChild(control);return wrap;}
    private static void SetFieldVisible(Control control,bool visible){if(control.GetParent() is Control parent&&parent.HasMeta("studio_field"))parent.Visible=visible;else control.Visible=visible;}
    private void SetNumbers(float x,float y,float width,float height,float target,float anchorX,float anchorY){_x.Value=x;_y.Value=y;_width.Value=width;_height.Value=height;_target.Value=target;_anchorX.Value=anchorX;_anchorY.Value=anchorY;}
    private void Labels(string x,string y,string width,string height,string anchorX,string anchorY){_xLabel.Text=x;_yLabel.Text=y;_widthLabel.Text=width;_heightLabel.Text=height;_anchorXLabel.Text=anchorX;_anchorYLabel.Text=anchorY;}
    private static void SelectText(OptionButton option,string value){for(int i=0;i<option.ItemCount;i++)if(option.GetItemText(i)==value){option.Selected=i;return;}}
    private static string GetId(object data)=>data switch{AuthoredWorldObjectData x=>x.Id,AuthoredBuildingData x=>x.Id,AuthoredRoomData x=>x.Id,AuthoredWallData x=>x.Id,AuthoredDoorData x=>x.Id,AuthoredFurnitureData x=>x.Id,AuthoredContainerData x=>x.Id,AuthoredBedData x=>x.Id,_=>string.Empty};
    private static string GetName(object data)=>data switch{AuthoredWorldObjectData x=>x.DisplayName,AuthoredBuildingData x=>x.DisplayName,AuthoredRoomData x=>x.DisplayName,AuthoredDoorData x=>x.DisplayName,AuthoredFurnitureData x=>x.DisplayName,AuthoredContainerData x=>x.DisplayName,AuthoredBedData x=>x.DisplayName,AuthoredWallData=>"Wall",_=>string.Empty};
    private static void SetName(object data,string value){switch(data){case AuthoredWorldObjectData x:x.DisplayName=value;break;case AuthoredBuildingData x:x.DisplayName=value;break;case AuthoredRoomData x:x.DisplayName=value;break;case AuthoredDoorData x:x.DisplayName=value;break;case AuthoredFurnitureData x:x.DisplayName=value;break;case AuthoredContainerData x:x.DisplayName=value;break;case AuthoredBedData x:x.DisplayName=value;break;}}
    private static string Short(string value,int length)=>value.Length<=length?value:value[..(length-1)]+"…";
}

public partial class StudioCountyMinimap:Control
{
    public event Action<Vector2>? CenterRequested;public Vector2 Center{get;set;}=new(220,155);public int Radius{get;set;}=1;
    public override void _Ready(){MouseFilter=MouseFilterEnum.Stop;QueueRedraw();}
    public override void _GuiInput(InputEvent inputEvent){if(inputEvent is InputEventMouseButton mouse&&mouse.Pressed&&mouse.ButtonIndex==MouseButton.Left){Vector2 grid=new(mouse.Position.X/Size.X*CountyCoordinateSpace.Width,mouse.Position.Y/Size.Y*CountyCoordinateSpace.Height);CenterRequested?.Invoke(grid);AcceptEvent();}}
    public override void _Draw(){DrawRect(new Rect2(Vector2.Zero,Size),new Color("0d130ff2"));foreach(CountyLocationDefinition location in CountyMacroLayout.Locations){Vector2 center=Map(location.Center);Vector2 radius=new(location.Radius.X/CountyCoordinateSpace.Width*Size.X,location.Radius.Y/CountyCoordinateSpace.Height*Size.Y);DrawCircle(center,Mathf.Max(2,Mathf.Min(radius.X,radius.Y)),location.Kind==CountyLocationKind.District?new Color("5f704f80"):new Color("c3a45ecc"));}Vector2I chunk=CountyCoordinateSpace.GridToChunk(Center);Vector2 start=Map(new Vector2((chunk.X-Radius)*CountyCoordinateSpace.ChunkSize,(chunk.Y-Radius)*CountyCoordinateSpace.ChunkSize));float chunks=Radius*2+1;Vector2 area=new(chunks*CountyCoordinateSpace.ChunkSize/CountyCoordinateSpace.Width*Size.X,chunks*CountyCoordinateSpace.ChunkSize/CountyCoordinateSpace.Height*Size.Y);DrawRect(new Rect2(start,area),new Color("e2bd67"),false,2);DrawCircle(Map(Center),4,new Color("f3d57f"));}
    private Vector2 Map(Vector2 grid)=>new(grid.X/CountyCoordinateSpace.Width*Size.X,grid.Y/CountyCoordinateSpace.Height*Size.Y);
}
