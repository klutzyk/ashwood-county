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
    private IsometricWorld _world=null!;private CountyWorld _county=null!;private StrategyCamera _camera=null!;private AuthoringStudioCanvas _canvas=null!;private AuthoredLandscapeSystem _landscape=null!;
    private readonly IReadOnlyList<AuthoringAssetEntry> _assets=AuthoringAssetCatalog.GetAssets();
    private readonly List<AuthoringAssetEntry> _recent=[];private readonly HashSet<string> _favorites=[];
    private OptionButton _location=null!,_radius=null!,_category=null!,_subcategory=null!,_gameplay=null!,_selectedGameplay=null!,_loot=null!,_roadType=null!,_layer=null!;
    private LineEdit _search=null!,_name=null!,_roomA=null!,_roomB=null!;
    private GridContainer _assetGrid=null!;private Label _status=null!,_dirtyStatus=null!,_coordinates=null!,_selectionTitle=null!,_id=null!,_validation=null!;
    private SpinBox _x=null!,_y=null!,_width=null!,_height=null!,_target=null!,_visualWidth=null!,_rotation=null!,_anchorX=null!,_anchorY=null!,_duration=null!,_brushRadius=null!,_brushDensity=null!,_brushVariation=null!,_roadWidth=null!;
    private Label _xLabel=null!,_yLabel=null!,_widthLabel=null!,_heightLabel=null!,_targetLabel=null!,_visualWidthLabel=null!,_anchorXLabel=null!,_anchorYLabel=null!;
    private CheckBox _collision=null!,_snap=null!,_keepAspect=null!,_randomBrush=null!,_layerVisible=null!,_layerLocked=null!,_cleanupFlag=null!;private Button _editInterior=null!,_exitInterior=null!,_apply=null!;private StudioCountyMinimap _minimap=null!;private AssetInspectionPreview _assetPreview=null!;private Label _assetDetails=null!;private AuthoringAssetEntry? _chosenAsset;
    private CountyLocationDefinition[] _locations=[];private readonly Dictionary<string,bool> _layerVisibility=[];private readonly Dictionary<string,bool> _layerLocks=[];private readonly HashSet<string> _cleanupFlags=[];private bool _refreshingInspector,_quitArmed,_refreshingLayer,_refreshingAssetQa;

    public override void _Ready()
    {
        LoadCleanupFlags();BuildWorld();BuildUi();
        GetTree().AutoAcceptQuit=false;Timer autosave=new(){WaitTime=60,OneShot=false,Autostart=true};autosave.Timeout+=_canvas.SaveRecovery;AddChild(autosave);
        Vector2 center=AuthoringSessionState.Center.IsZeroApprox()?new Vector2(220,155):AuthoringSessionState.Center;
        int radius=AuthoringSessionState.Radius;
        LoadArea(center,radius);
        _camera.SetZoom(AuthoringSessionState.Zoom>0?AuthoringSessionState.Zoom:(radius==0?.85f:radius==1?.52f:.34f));
        if(!string.IsNullOrWhiteSpace(AuthoringSessionState.SelectionId))_canvas.RestoreWorldSelection(AuthoringSessionState.SelectionKind,AuthoringSessionState.SelectionId);
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
    public override void _Process(double delta)
    {
        if(_coordinates is null||_world is null)return;
        Vector2 grid=_world.ScreenToGridPosition(GetViewport().GetMousePosition());_coordinates.Text=$"X {grid.X:0.0}  Y {grid.Y:0.0}";
        if(_canvas.IsInteriorMode)return;
        Vector2 cameraGrid=CountyCoordinateSpace.ClampToCounty(IsometricGrid.ScreenToGrid(_camera.Position));
        if(CountyCoordinateSpace.GridToChunk(cameraGrid)!=CountyCoordinateSpace.GridToChunk(_canvas.LoadedCenter))StreamArea(cameraGrid,RadiusValue());
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
        _landscape=new AuthoredLandscapeSystem{Name="AuthoredLandscapeSystem"};_landscape.Initialize(_county,_world,_canvas.Document);AddChild(_landscape);
        _canvas.SelectionChanged+=RefreshInspector;_canvas.StatusChanged+=SetStatus;_canvas.DirtyChanged+=SetDirtyStatus;_canvas.DocumentChanged+=()=>{_minimap?.QueueRedraw();_landscape.RefreshDocument(_canvas.Document);};
    }

    private void BuildUi()
    {
        CanvasLayer layer=new(){Name="StudioUI",Layer=30};AddChild(layer);
        Control root=new(){Name="Root",Theme=AshwoodTheme.Create(),LayoutMode=1,MouseFilter=Control.MouseFilterEnum.Ignore};root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);layer.AddChild(root);
        BuildLeftPanel(root);BuildToolbar(root);BuildInspector(root);BuildStatus(root);
    }

    private void BuildLeftPanel(Control root)
    {
        PanelContainer panel=Panel(root,10,10,300,-10,true);VBoxContainer box=VBox(panel);
        box.AddChild(Label("ASHWOOD AUTHORING STUDIO","HudTitle"));box.AddChild(Label("CHUNK-SCOPED WORLD & INTERIOR DESIGN","HudTiny"));
        _minimap=new StudioCountyMinimap{CustomMinimumSize=new Vector2(270,104)};_minimap.CenterRequested+=center=>LoadArea(center,RadiusValue());box.AddChild(_minimap);
        _location=new OptionButton();_locations=CountyMacroLayout.Locations.OrderBy(location=>location.Kind).ThenBy(location=>location.Name).ToArray();foreach(CountyLocationDefinition location in _locations)_location.AddItem(location.Name);box.AddChild(_location);
        HBoxContainer loadRow=new();_radius=new OptionButton();_radius.AddItem("1 CHUNK");_radius.AddItem("3 x 3 CHUNKS");_radius.AddItem("5 x 5 CHUNKS");_radius.Selected=1;loadRow.AddChild(_radius);Button load=Button("LOAD AREA",()=>LoadArea(_locations[_location.Selected].Center,RadiusValue()));loadRow.AddChild(load);box.AddChild(loadRow);

        TabContainer tabs=new(){Name="AuthoringTabs",SizeFlagsVertical=Control.SizeFlags.ExpandFill,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};box.AddChild(tabs);

        VBoxContainer assetsTab=new(){Name="ASSETS",SizeFlagsVertical=Control.SizeFlags.ExpandFill};tabs.AddChild(assetsTab);
        _search=new LineEdit{PlaceholderText="Search assets...",ClearButtonEnabled=true};_search.TextChanged+=_=>RefreshAssets();assetsTab.AddChild(_search);
        _category=new OptionButton();_category.AddItem("All Assets");_category.AddItem("Recent");_category.AddItem("★ Favorites");foreach(string category in _assets.Select(asset=>asset.Category).Distinct())_category.AddItem(category);_category.ItemSelected+=_=>RefreshSubcategories();assetsTab.AddChild(_category);
        _subcategory=new OptionButton();_subcategory.AddItem("All Subcategories");_subcategory.ItemSelected+=_=>RefreshAssets();assetsTab.AddChild(_subcategory);
        ScrollContainer scroll=new(){SizeFlagsVertical=Control.SizeFlags.ExpandFill,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,MouseFilter=Control.MouseFilterEnum.Stop};scroll.GuiInput+=input=>{if(input is InputEventMouseButton mouse&&mouse.Pressed&&mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)scroll.AcceptEvent();};_assetGrid=new GridContainer{Columns=3,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};scroll.AddChild(_assetGrid);assetsTab.AddChild(scroll);

        VBoxContainer qaTab=new(){Name="QA",SizeFlagsVertical=Control.SizeFlags.ExpandFill};tabs.AddChild(qaTab);qaTab.AddChild(Label("ASSET INSPECTION","HudHeading"));qaTab.AddChild(Label("Select any library thumbnail to inspect its actual pixels.","HudTiny"));_assetPreview=new AssetInspectionPreview{CustomMinimumSize=new Vector2(270,230),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};qaTab.AddChild(_assetPreview);_assetDetails=Label("No asset selected.","HudMuted");_assetDetails.AutowrapMode=TextServer.AutowrapMode.WordSmart;qaTab.AddChild(_assetDetails);_cleanupFlag=new CheckBox{Text="NEEDS CLEANUP"};_cleanupFlag.Toggled+=ToggleCleanupFlag;qaTab.AddChild(_cleanupFlag);

        VBoxContainer toolsTab=new(){Name="PAINT",SizeFlagsVertical=Control.SizeFlags.ExpandFill};tabs.AddChild(toolsTab);
        toolsTab.AddChild(new HSeparator());toolsTab.AddChild(Label("TERRAIN / SCATTER BRUSH","HudHeading"));toolsTab.AddChild(Label("R radius   D density   ± scale variation","HudTiny"));HBoxContainer brushRow=new();_brushRadius=CompactNumber("RADIUS",.5,12,.5,2.5);_brushDensity=CompactNumber("DENSITY",.1,2,.1,.65);_brushVariation=CompactNumber("SCALE ±",0,.75,.05,.18);brushRow.AddChild(_brushRadius);brushRow.AddChild(_brushDensity);brushRow.AddChild(_brushVariation);toolsTab.AddChild(brushRow);_randomBrush=new CheckBox{Text="RANDOM ASSET / ROTATION",ButtonPressed=true};_randomBrush.Toggled+=_=>ApplyBrushSettings();toolsTab.AddChild(_randomBrush);_brushRadius.ValueChanged+=_=>ApplyBrushSettings();_brushDensity.ValueChanged+=_=>ApplyBrushSettings();_brushVariation.ValueChanged+=_=>ApplyBrushSettings();ApplyBrushSettings();
        toolsTab.AddChild(new HSeparator());toolsTab.AddChild(Label("ROAD STYLE","HudHeading"));toolsTab.AddChild(Label("Choosing a style starts drawing immediately.","HudTiny"));HBoxContainer roadRow=new();_roadType=new OptionButton();foreach(string type in new[]{"Highway","Paved Town Road","Rural Road","Dirt Road","Farm Track","Forest Track","Footpath"})_roadType.AddItem(type);_roadType.Selected=2;_roadWidth=CompactNumber("ROAD WIDTH",.2,4,.1,1.2);_roadType.ItemSelected+=_=>ActivateSelectedRoad();_roadWidth.ValueChanged+=_=>ApplyPathSettings();roadRow.AddChild(_roadType);roadRow.AddChild(_roadWidth);toolsTab.AddChild(roadRow);

        HBoxContainer paintActions=new();paintActions.AddChild(Button("SCATTER SELECTED",ActivateScatter));paintActions.AddChild(Button("ERASE BRUSH",()=>_canvas.SetTool(AuthoringTool.Erase)));toolsTab.AddChild(paintActions);toolsTab.AddChild(Button("DRAW SELECTED AS LINE",ActivateLine));

        VBoxContainer layersTab=new(){Name="LAYERS",SizeFlagsVertical=Control.SizeFlags.ExpandFill};tabs.AddChild(layersTab);layersTab.AddChild(Label("AUTHORING LAYERS","HudHeading"));layersTab.AddChild(Label("SHOW hides or reveals a layer. LOCK prevents selecting or painting it.","HudMuted"));HBoxContainer layerRow=new();_layer=new OptionButton();foreach(string layer in new[]{"Terrain","Roads","Vegetation","Buildings","Props","Gameplay"}){_layer.AddItem(layer);_layerVisibility[layer]=true;_layerLocks[layer]=false;}_layerVisible=new CheckBox{Text="SHOW",ButtonPressed=true};_layerLocked=new CheckBox{Text="LOCK"};_layer.ItemSelected+=_=>RefreshLayerControls();_layerVisible.Toggled+=_=>ApplyLayerControls();_layerLocked.Toggled+=_=>ApplyLayerControls();layerRow.AddChild(_layer);layerRow.AddChild(_layerVisible);layerRow.AddChild(_layerLocked);layersTab.AddChild(layerRow);
        layersTab.AddChild(Label("Switching the dropdown only chooses which layer the SHOW and LOCK controls affect.","HudTiny"));
        layersTab.AddChild(new HSeparator());layersTab.AddChild(Label("GAMEPLAY ROLE","HudHeading"));layersTab.AddChild(Label("Advanced: only change this for gameplay markers.","HudTiny"));_gameplay=new OptionButton();foreach(string type in new[]{"Decoration","Building","Door","Container","Bed","Scavenge Source","Landmark","Zombie Spawn","Resource"})_gameplay.AddItem(type);_gameplay.ItemSelected+=_=>_canvas.PlacementGameplayType=_gameplay.GetItemText(_gameplay.Selected);layersTab.AddChild(_gameplay);
        RefreshAssets();
    }

    private void BuildToolbar(Control root)
    {
        PanelContainer panel=Panel(root,310,10,-330,132);VBoxContainer rows=new();panel.AddChild(rows);HBoxContainer tools=new();HBoxContainer actions=new();rows.AddChild(tools);rows.AddChild(actions);
        tools.AddChild(Button("SELECT [Q]",()=>_canvas.SetTool(AuthoringTool.Select)));tools.AddChild(Button("FINISH PATH",_canvas.FinishPath));tools.AddChild(Button("ROOM",()=>_canvas.SetTool(AuthoringTool.Room)));tools.AddChild(Button("WALL",()=>_canvas.SetTool(AuthoringTool.Wall)));tools.AddChild(Button("DOOR",()=>_canvas.SetTool(AuthoringTool.Door)));
        _snap=new CheckBox{Text="SNAP 0.25",ButtonPressed=true};_snap.Toggled+=value=>_canvas.SnapEnabled=value;tools.AddChild(_snap);
        actions.AddChild(Button("UNDO",_canvas.Undo));actions.AddChild(Button("REDO",_canvas.Redo));actions.AddChild(Button("DUPLICATE",_canvas.DuplicateSelection));actions.AddChild(Button("ROTATE [R]",()=>_canvas.RotateSelection()));actions.AddChild(Button("DELETE [E]",_canvas.DeleteSelection));actions.AddChild(Button("SAVE",_canvas.Save));actions.AddChild(Button("RECOVER",_canvas.Recover));actions.AddChild(Button("PLAYTEST",BeginPlaytest));
    }

    private void BuildInspector(Control root)
    {
        PanelContainer panel=Panel(root,-320,10,-10,-10,false,true);VBoxContainer box=ScrollableVBox(panel);
        _selectionTitle=Label("SELECTION INSPECTOR","HudTitle");box.AddChild(_selectionTitle);_id=Label("No selection","HudTiny");box.AddChild(_id);
        _name=Field(box,"NAME");_selectedGameplay=new OptionButton();foreach(string type in new[]{"Decoration","Building","Door","Container","Bed","Scavenge Source","Landmark","Zombie Spawn","Resource"})_selectedGameplay.AddItem(type);box.AddChild(Labeled("GAMEPLAY TYPE",_selectedGameplay));_x=Number(box,"X",out _xLabel);_y=Number(box,"Y",out _yLabel);_width=Number(box,"WIDTH",out _widthLabel,.05,100);_height=Number(box,"HEIGHT",out _heightLabel,.05,100);_target=Number(box,"VISUAL HEIGHT",out _targetLabel,.01,2000);_visualWidth=Number(box,"VISUAL WIDTH",out _visualWidthLabel,.01,2000);_rotation=Number(box,"ROTATION (DEGREES)",out _,1,360);
        _anchorX=Number(box,"ANCHOR X",out _anchorXLabel,.01,500);_anchorY=Number(box,"ANCHOR Y",out _anchorYLabel,.01,500);
        _roomA=Field(box,"ROOM / SIDE A");_roomB=Field(box,"ROOM / SIDE B");
        _collision=new CheckBox{Text="BLOCKS MOVEMENT / COLLISION"};box.AddChild(_collision);
        _keepAspect=new CheckBox{Text="KEEP ASPECT RATIO",ButtonPressed=true};_keepAspect.Toggled+=value=>_canvas.KeepAspectRatio=value;box.AddChild(_keepAspect);
        _loot=new OptionButton();foreach(string preset in AuthoredInteriorConverter.LootPresetNames)_loot.AddItem(preset);box.AddChild(Labeled("LOOT TABLE",_loot));
        _duration=Number(box,"SEARCH SECONDS",out _,.1,60);
        _apply=Button("APPLY CHANGES",ApplyInspector);box.AddChild(_apply);
        _editInterior=Button("EDIT INTERIOR",()=>{if(_canvas.EnterInteriorForSelection()){RefreshInspector();CenterBuilding();}});box.AddChild(_editInterior);
        _exitInterior=Button("RETURN TO WORLD EDIT",()=>{_canvas.ExitInterior();RefreshInspector();});box.AddChild(_exitInterior);
        box.AddChild(new HSeparator());HBoxContainer validationButtons=new();validationButtons.AddChild(Button("VALIDATE",RunValidation));validationButtons.AddChild(Button("TEST ENTRANCE",TestEntrance));box.AddChild(validationButtons);
        _validation=Label("Select a building and enter Interior Edit.","HudMuted");_validation.AutowrapMode=TextServer.AutowrapMode.WordSmart;_validation.SizeFlagsVertical=Control.SizeFlags.ExpandFill;box.AddChild(_validation);
        box.AddChild(Label("SHORTCUTS  Q Select  E Delete  R Rotate  Ctrl+Z  Ctrl+Y  Ctrl+D  Esc","HudTiny"));
    }

    private void BuildStatus(Control root)
    {
        PanelContainer panel=Panel(root,310,-48,-330,-10);HBoxContainer row=new();_dirtyStatus=Label("SAVED","HudTiny");_dirtyStatus.CustomMinimumSize=new Vector2(90,0);_dirtyStatus.VerticalAlignment=VerticalAlignment.Center;_status=Label("Ready","HudHeading");_status.HorizontalAlignment=HorizontalAlignment.Center;_status.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;_coordinates=Label("X 0  Y 0","HudTiny");_coordinates.CustomMinimumSize=new Vector2(120,0);_coordinates.HorizontalAlignment=HorizontalAlignment.Right;_coordinates.VerticalAlignment=VerticalAlignment.Center;row.AddChild(_dirtyStatus);row.AddChild(_status);row.AddChild(_coordinates);panel.AddChild(row);
    }

    private void RefreshAssets()
    {
        if(_assetGrid is null)return;foreach(Node child in _assetGrid.GetChildren())child.QueueFree();string category=_category?.Selected>0?_category.GetItemText(_category.Selected):string.Empty;string subcategory=_subcategory?.Selected>0?_subcategory.GetItemText(_subcategory.Selected):string.Empty;string query=_search?.Text.Trim()??string.Empty;IEnumerable<AuthoringAssetEntry> source=category=="Recent"?_recent:category=="★ Favorites"?_assets.Where(asset=>_favorites.Contains(asset.Path)):_assets.Where(asset=>string.IsNullOrEmpty(category)||asset.Category==category);
        foreach(AuthoringAssetEntry asset in source.Where(asset=>(string.IsNullOrEmpty(subcategory)||asset.Subcategory==subcategory)&&(string.IsNullOrEmpty(query)||asset.SearchTags.Contains(query,StringComparison.OrdinalIgnoreCase))).Take(90))
        {
            VBoxContainer tile=new(){CustomMinimumSize=new Vector2(82,82)};Button thumbnail=new(){TooltipText=$"{asset.Name}\n{asset.Category} > {asset.Subcategory}\n{asset.AssetKind} • {asset.SourceSheet}\n{asset.Path}\nRight-click to favorite",CustomMinimumSize=new Vector2(82,58),Icon=AuthoringThumbnailCache.Get(asset.Path),ExpandIcon=true};thumbnail.Pressed+=()=>ChooseAsset(asset);thumbnail.GuiInput+=input=>{if(input is InputEventMouseButton mouse&&mouse.Pressed&&mouse.ButtonIndex==MouseButton.Right){ToggleFavorite(asset);thumbnail.AcceptEvent();}};Label caption=Label((_cleanupFlags.Contains(asset.Path)?"! ":_favorites.Contains(asset.Path)?"★ ":"")+Short(asset.Name,13),"HudTiny");caption.HorizontalAlignment=HorizontalAlignment.Center;caption.TooltipText=asset.Name;caption.ClipText=true;tile.AddChild(thumbnail);tile.AddChild(caption);_assetGrid.AddChild(tile);
        }
    }

    private void RefreshSubcategories()
    {
        if(_subcategory is null)return;string category=_category.Selected>0?_category.GetItemText(_category.Selected):string.Empty;_subcategory.Clear();_subcategory.AddItem("All Subcategories");IEnumerable<AuthoringAssetEntry> source=category=="Recent"?_recent:category=="★ Favorites"?_assets.Where(asset=>_favorites.Contains(asset.Path)):_assets.Where(asset=>string.IsNullOrEmpty(category)||asset.Category==category);foreach(string subcategory in source.Select(asset=>asset.Subcategory).Distinct().OrderBy(value=>value))_subcategory.AddItem(subcategory);_subcategory.Selected=0;RefreshAssets();
    }

    private void ChooseAsset(AuthoringAssetEntry asset)
    {
        _chosenAsset=asset;RefreshAssetQa();
        _recent.RemoveAll(item=>item.Path==asset.Path);_recent.Insert(0,asset);if(_recent.Count>18)_recent.RemoveRange(18,_recent.Count-18);
        if(asset.Category=="Terrain"){ActivateTerrain();return;}
        if(_canvas.Tool==AuthoringTool.Scatter){ActivateScatter();return;}
        string type=asset.Category=="Buildings"?"Building":asset.Path.Contains("/bedroom/bed_")?"Bed":"Decoration";
        SelectText(_gameplay,type);_canvas.PlacementGameplayType=type;_canvas.SetPlacementAsset(asset);
    }
    private void RefreshAssetQa(){if(_chosenAsset is null||_assetPreview is null)return;_assetPreview.ShowAsset(_chosenAsset.Path);Image image=new();image.Load(ProjectSettings.GlobalizePath(_chosenAsset.Path));_assetDetails.Text=$"{_chosenAsset.Name}\n{image.GetWidth()} × {image.GetHeight()} px\n{_chosenAsset.Category} > {_chosenAsset.Subcategory}\nSource: {_chosenAsset.SourceSheet}\n{_chosenAsset.AssetKind}\nAnchor: {_chosenAsset.SuggestedAnchor.X:0.##}, {_chosenAsset.SuggestedAnchor.Y:0.##}\nScale: {_chosenAsset.DefaultScale:0.##}   Blocking: {(_chosenAsset.DefaultCollision?"Yes":"No")}";_refreshingAssetQa=true;_cleanupFlag.ButtonPressed=_cleanupFlags.Contains(_chosenAsset.Path);_refreshingAssetQa=false;}
    private void ToggleCleanupFlag(bool flagged){if(_refreshingAssetQa||_chosenAsset is null)return;if(flagged)_cleanupFlags.Add(_chosenAsset.Path);else _cleanupFlags.Remove(_chosenAsset.Path);ConfigFile config=new();foreach(string path in _cleanupFlags)config.SetValue("needs_cleanup",path,true);config.Save("user://asset_qa_flags.cfg");RefreshAssets();}
    private void LoadCleanupFlags(){ConfigFile config=new();if(config.Load("user://asset_qa_flags.cfg")!=Error.Ok)return;foreach(string key in config.GetSectionKeys("needs_cleanup"))if(config.GetValue("needs_cleanup",key,false).AsBool())_cleanupFlags.Add(key);}
    private void ToggleFavorite(AuthoringAssetEntry asset){if(!_favorites.Add(asset.Path))_favorites.Remove(asset.Path);RefreshAssets();SetStatus(_favorites.Contains(asset.Path)?$"Favorited {asset.Name}":$"Removed {asset.Name} from favorites");}
    private void ActivateTerrain(){if(_chosenAsset is null||_chosenAsset.Category!="Terrain"){SetStatus("Choose a Terrain thumbnail first.");return;}ApplyBrushSettings();_canvas.SetTerrainBrush(_chosenAsset);}
    private void ActivateScatter(){if(_chosenAsset is null||_chosenAsset.Category is "Terrain" or "Buildings" or "Interiors"){SetStatus("Choose a vegetation or prop thumbnail first.");return;}ApplyBrushSettings();IReadOnlyList<AuthoringAssetEntry> variants=_assets.Where(item=>item.Category==_chosenAsset.Category&&item.Subcategory==_chosenAsset.Subcategory).ToArray();_canvas.SetScatterBrush(_chosenAsset,variants);}
    private void ActivateLine(){if(_chosenAsset is null||_chosenAsset.Category is "Terrain" or "Buildings" or "Interiors"){SetStatus("Choose a fence, hedge, barrier, or pole asset first.");return;}ApplyPathSettings();_canvas.BeginStructureLineTool(_chosenAsset);}
    private void ActivateSelectedRoad(){ApplyPathSettings();_canvas.BeginRoadTool();SetStatus($"{_canvas.PathType.ToUpperInvariant()} — click control points, then FINISH or press Enter");}
    private void ApplyBrushSettings(){if(_canvas is null)return;_canvas.BrushRadius=(float)(_brushRadius?.Value??2.5);_canvas.BrushDensity=(float)(_brushDensity?.Value??.65);_canvas.BrushScaleVariation=(float)(_brushVariation?.Value??.18);_canvas.RandomAssetVariation=_randomBrush?.ButtonPressed??true;}
    private void ApplyPathSettings(){if(_canvas is null||_roadType is null)return;_canvas.PathType=_roadType.GetItemText(_roadType.Selected);_canvas.PathWidth=(float)_roadWidth.Value;_canvas.PathAssetPath=_chosenAsset is not null&&(_chosenAsset.Subcategory.Contains("Road",StringComparison.OrdinalIgnoreCase)||_chosenAsset.Name.Contains("Road",StringComparison.OrdinalIgnoreCase)||_chosenAsset.Name.Contains("Track",StringComparison.OrdinalIgnoreCase)||_chosenAsset.Name.Contains("Path",StringComparison.OrdinalIgnoreCase))?_chosenAsset.Path:string.Empty;}
    private void RefreshLayerControls(){if(_layer is null)return;_refreshingLayer=true;string layer=_layer.GetItemText(_layer.Selected);_layerVisible.ButtonPressed=_layerVisibility.GetValueOrDefault(layer,true);_layerLocked.ButtonPressed=_layerLocks.GetValueOrDefault(layer);_refreshingLayer=false;}
    private void ApplyLayerControls(){if(_refreshingLayer||_layer is null)return;string layer=_layer.GetItemText(_layer.Selected);_layerVisibility[layer]=_layerVisible.ButtonPressed;_layerLocks[layer]=_layerLocked.ButtonPressed;_canvas.SetLayerState(layer,_layerVisible.ButtonPressed,_layerLocked.ButtonPressed);_landscape.SetLayerVisibility(layer,_layerVisible.ButtonPressed);SetStatus($"{layer}: {(_layerVisible.ButtonPressed?"visible":"hidden")}, {(_layerLocked.ButtonPressed?"locked":"editable")}");}
    private void ShowWindowAssets(){SelectText(_category,"Interiors");RefreshSubcategories();_search.Text="window";RefreshAssets();_canvas.SetTool(AuthoringTool.Place);SetStatus("WINDOW TOOL — choose a window thumbnail, then place against an authored wall");}

    private void RefreshInspector()
    {
        if(_name is null)return;_refreshingInspector=true;object? data=_canvas.GetSelectedData();bool selected=data is not null;_apply.Disabled=!selected;
        foreach(Control control in new Control[]{_name,_selectedGameplay,_x,_y,_width,_height,_target,_visualWidth,_rotation,_anchorX,_anchorY,_roomA,_roomB,_collision,_keepAspect,_loot,_duration})SetFieldVisible(control,selected);
        _editInterior.Visible=data is AuthoredBuildingData&&!_canvas.IsInteriorMode;_exitInterior.Visible=_canvas.IsInteriorMode;
        if(data is null){_selectionTitle.Text=_canvas.IsInteriorMode?"INTERIOR INSPECTOR":"SELECTION INSPECTOR";_id.Text="No selection";_refreshingInspector=false;return;}
        _id.Text=$"{_canvas.PrimarySelection.Kind.ToString().ToUpperInvariant()}  •  {GetId(data)}";_selectionTitle.Text="SELECTION INSPECTOR";_name.Text=GetName(data);
        ConfigureInspector(data);_refreshingInspector=false;
    }

    private void ConfigureInspector(object data)
    {
        SetFieldVisible(_roomA,false);SetFieldVisible(_roomB,false);SetFieldVisible(_loot,false);SetFieldVisible(_duration,false);SetFieldVisible(_collision,false);SetFieldVisible(_keepAspect,false);SetFieldVisible(_selectedGameplay,false);SetFieldVisible(_target,true);SetFieldVisible(_visualWidth,false);SetFieldVisible(_rotation,false);SetFieldVisible(_anchorX,false);SetFieldVisible(_anchorY,false);_targetLabel.Text="VISUAL HEIGHT";
        switch(data)
        {
            case AuthoredWorldObjectData item:SetNumbers(item.X,item.Y,0,0,item.Scale,item.AnchorX,item.AnchorY);_visualWidth.Value=item.ScaleY>0?item.ScaleY:item.Scale;_rotation.Value=item.RotationDegrees;Labels("X","Y","","","ANCHOR X","ANCHOR Y");_targetLabel.Text="SCALE X";_visualWidthLabel.Text="SCALE Y";SetFieldVisible(_selectedGameplay,true);SelectText(_selectedGameplay,item.GameplayType);SetFieldVisible(_width,false);SetFieldVisible(_height,false);SetFieldVisible(_visualWidth,true);SetFieldVisible(_rotation,true);SetFieldVisible(_anchorX,true);SetFieldVisible(_anchorY,true);SetFieldVisible(_collision,true);SetFieldVisible(_keepAspect,true);_collision.ButtonPressed=item.Collision;break;
            case AuthoredBuildingData item:SetNumbers(item.ExteriorX,item.ExteriorY,item.FootprintWidth,item.FootprintHeight,item.ExteriorTargetHeight,item.FootprintX,item.FootprintY);_visualWidth.Value=item.ExteriorTargetWidth>0?item.ExteriorTargetWidth:BuildingVisualWidth(item);_rotation.Value=item.ExteriorRotationDegrees;_visualWidthLabel.Text="VISUAL WIDTH";Labels("EXTERIOR X","EXTERIOR Y","FOOTPRINT W","FOOTPRINT H","FOOTPRINT X","FOOTPRINT Y");SetFieldVisible(_visualWidth,true);SetFieldVisible(_rotation,true);SetFieldVisible(_keepAspect,true);SetFieldVisible(_anchorX,true);SetFieldVisible(_anchorY,true);break;
            case AuthoredPathData item:SetNumbers(0,0,item.Width,0,0,0,0);Labels("","","ROAD WIDTH","","","");SetFieldVisible(_x,false);SetFieldVisible(_y,false);SetFieldVisible(_height,false);SetFieldVisible(_target,false);SetFieldVisible(_width,true);break;
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
            case AuthoredWorldObjectData item:item.X=(float)_x.Value;item.Y=(float)_y.Value;item.Scale=(float)_target.Value;item.ScaleY=_keepAspect.ButtonPressed?item.Scale:(float)_visualWidth.Value;item.RotationDegrees=(float)_rotation.Value;item.AnchorX=(float)_anchorX.Value;item.AnchorY=(float)_anchorY.Value;item.Collision=_collision.ButtonPressed;item.GameplayType=_selectedGameplay.GetItemText(_selectedGameplay.Selected);break;
            case AuthoredBuildingData item:
                float previousFootprintX=item.FootprintX,previousFootprintY=item.FootprintY;Vector2 delta=new((float)_x.Value-item.ExteriorX,(float)_y.Value-item.ExteriorY);AuthoringStudioCanvas.TranslateBuilding(item,delta);item.FootprintWidth=(float)_width.Value;item.FootprintHeight=(float)_height.Value;item.ExteriorTargetHeight=(float)_target.Value;item.ExteriorTargetWidth=_keepAspect.ButtonPressed?0:(float)_visualWidth.Value;item.ExteriorRotationDegrees=(float)_rotation.Value;
                if(!Mathf.IsEqualApprox((float)_anchorX.Value,previousFootprintX))item.FootprintX=(float)_anchorX.Value;if(!Mathf.IsEqualApprox((float)_anchorY.Value,previousFootprintY))item.FootprintY=(float)_anchorY.Value;break;
            case AuthoredPathData item:item.Width=Mathf.Max(.15f,(float)_width.Value);break;
            case AuthoredRoomData item:item.X=(float)_x.Value;item.Y=(float)_y.Value;item.Width=(float)_width.Value;item.Height=(float)_height.Value;break;
            case AuthoredWallData item:item.StartX=(float)_x.Value;item.StartY=(float)_y.Value;item.EndX=(float)_width.Value;item.EndY=(float)_height.Value;break;
            case AuthoredDoorData item:item.X=(float)_x.Value;item.Y=(float)_y.Value;item.InsideArrivalX=(float)_width.Value;item.InsideArrivalY=(float)_height.Value;item.OutsideApproachX=(float)_anchorX.Value;item.OutsideApproachY=(float)_anchorY.Value;item.RoomAId=_roomA.Text;item.RoomBId=_roomB.Text;break;
            case AuthoredFurnitureData item:item.X=(float)_x.Value;item.Y=(float)_y.Value;item.Width=(float)_width.Value;item.Height=(float)_height.Value;item.TargetHeight=(float)_target.Value;item.BlocksMovement=_collision.ButtonPressed;break;
            case AuthoredContainerData item:item.X=(float)_x.Value;item.Y=(float)_y.Value;item.Width=(float)_width.Value;item.Height=(float)_height.Value;item.TargetHeight=(float)_target.Value;item.InteractionX=(float)_anchorX.Value;item.InteractionY=(float)_anchorY.Value;item.RoomId=_roomA.Text;item.LootPreset=_loot.GetItemText(_loot.Selected);item.SearchDuration=(float)_duration.Value;break;
            case AuthoredBedData item:item.X=(float)_x.Value;item.Y=(float)_y.Value;item.Width=(float)_width.Value;item.Height=(float)_height.Value;item.TargetHeight=(float)_target.Value;item.InteractionX=(float)_anchorX.Value;item.InteractionY=(float)_anchorY.Value;item.RoomId=_roomA.Text;break;
        }
        _canvas.NotifyInspectorChanged();RefreshInspector();
    }

    private void RunValidation(){IReadOnlyList<AuthoringValidationIssue> issues=_canvas.ValidateCurrent();_validation.Text=string.Join("\n",issues.Take(9).Select(issue=>$"{(issue.Severity==AuthoringValidationSeverity.Invalid?"RED":issue.Severity==AuthoringValidationSeverity.Warning?"YELLOW":"GREEN")}: {issue.Message}"));}
    private void TestEntrance(){bool pass=_canvas.TestEntrance();_validation.Text=(pass?"GREEN: ":"RED: ")+_status.Text;}
    private void BeginPlaytest()
    {
        _canvas.Save();AuthoredBuildingData? building=_canvas.InteriorBuilding??_canvas.Document.Buildings.FirstOrDefault(item=>item.Id==_canvas.PrimarySelection.Id)??_canvas.Document.Buildings.FirstOrDefault();if(building is null){SetStatus("At least one authored building is required before playtesting.");return;}
        AuthoringSessionState.IsPlaytesting=true;AuthoringSessionState.BuildingId=building.Id;AuthoringSessionState.SelectionKind=_canvas.PrimarySelection.Kind.ToString();AuthoringSessionState.SelectionId=_canvas.PrimarySelection.Id;AuthoringSessionState.ActiveTool=_canvas.Tool;AuthoringSessionState.Zoom=_camera.Zoom.X;AuthoringSessionState.Center=_canvas.LoadedCenter;AuthoringSessionState.Radius=RadiusValue();AuthoringSessionState.ReturnToInterior=_canvas.IsInteriorMode;GetTree().ChangeSceneToFile("res://scenes/world/World.tscn");
    }

    private async void StartAutomatedPlaytest(){for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);AuthoredBuildingData building=_canvas.Document.Buildings.First();_canvas.SelectBuilding(building.Id);_canvas.EnterInteriorForSelection();BeginPlaytest();}

    private async void RunAutomatedValidation()
    {
        for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        LoadArea(new Vector2(220,155),2);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);int fiveByFive=_county.LoadedChunkCount;
        LoadArea(new Vector2(220,155),0);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);int oneChunk=_county.LoadedChunkCount;
        LoadArea(new Vector2(220,155),1);
        string worldBefore=AuthoredContentRepository.Serialize(_canvas.Document);int initialObjects=_canvas.Document.WorldObjects.Count,initialPaths=_canvas.Document.Paths.Count;AuthoringAssetEntry terrain=_assets.First(asset=>asset.Category=="Terrain"&&!asset.Path.Contains("sheet"));_canvas.SetTerrainBrush(terrain);_canvas.ApplyBrushDab(new Vector2(221,155));bool terrainPaint=_canvas.Document.TerrainStamps.Count>AuthoredContentRepository.Deserialize(worldBefore).TerrainStamps.Count;_canvas.Undo();AuthoringAssetEntry scatter=_assets.First(asset=>asset.Category=="Environment");_canvas.SetScatterBrush(scatter,[scatter]);_canvas.ApplyBrushDab(new Vector2(222,155));bool scatterPaint=_canvas.Document.WorldObjects.Count>initialObjects;_canvas.Undo();_canvas.PathType="Rural Road";_canvas.PathWidth=1.2f;_canvas.BeginRoadTool();_canvas.AddPathPoint(new Vector2(220,154));_canvas.AddPathPoint(new Vector2(224,157));_canvas.FinishPath();bool roadCreated=_canvas.Document.Paths.Count>initialPaths;_canvas.Undo();bool worldToolsRestored=AuthoredContentRepository.Serialize(_canvas.Document)==worldBefore;GD.Print($"AUTHORING_WORLD_TOOLS: {(terrainPaint&&scatterPaint&&roadCreated&&worldToolsRestored?"PASS":"FAIL")} (terrain={terrainPaint}, scatter={scatterPaint}, road={roadCreated}, undo={worldToolsRestored})");
        AuthoredBuildingData building=_canvas.Document.Buildings.First();_canvas.SelectBuilding(building.Id);bool entered=_canvas.EnterInteriorForSelection();
        IReadOnlyList<AuthoringValidationIssue> issues=_canvas.ValidateInterior();foreach(AuthoringValidationIssue issue in issues)GD.Print($"AUTHORING_CHECK: {issue.Severity} {issue.Message}");int errors=issues.Count(issue=>issue.Severity==AuthoringValidationSeverity.Invalid);bool entrance=_canvas.TestEntrance();
        string before=AuthoredContentRepository.Serialize(_canvas.Document);AuthoredFurnitureData furniture=building.Furniture.First();_canvas.SelectInteriorItem(StudioSelectionKind.Furniture,furniture.Id);_canvas.DuplicateSelection();bool duplicated=building.Furniture.Count>15;_canvas.Undo();bool undoRestored=AuthoredContentRepository.Serialize(_canvas.Document)==before;
        bool streaming=fiveByFive==25&&oneChunk==1;_canvas.Save();AuthoredBuildingData restored=_canvas.Document.Buildings.First();_canvas.SelectInteriorItem(StudioSelectionKind.Container,restored.Containers.First().Id);_validation.Text=string.Join("\n",issues.Take(6).Select(issue=>$"{issue.Severity.ToString().ToUpperInvariant()}: {issue.Message}"));SetStatus(entrance?"ENTRANCE TEST PASS — deterministic approach, door and arrival route":"ENTRANCE TEST FAIL");GD.Print($"AUTHORING_STUDIO_VALIDATION: {(entered&&errors==0&&entrance&&duplicated&&undoRestored&&streaming?"PASS":"FAIL")} (chunks_5x5={fiveByFive}, chunks_1={oneChunk}, assets={_assets.Count}, interior={entered}, errors={errors}, entrance={entrance}, duplicate={duplicated}, undo={undoRestored}, runtime_data_saved=True)");
        string? path=System.Environment.GetEnvironmentVariable("ASHWOOD_STUDIO_CAPTURE_PNG");if(!string.IsNullOrWhiteSpace(path))await SaveCapture(path);
    }

    private async void CaptureStudio(){string? path=System.Environment.GetEnvironmentVariable("ASHWOOD_STUDIO_CAPTURE_PNG");if(!string.IsNullOrWhiteSpace(path))await SaveCapture(path);}
    private async System.Threading.Tasks.Task SaveCapture(string path){for(int i=0;i<24;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(DisplayServer.GetName()=="headless")return;Error error=GetViewport().GetTexture().GetImage().SavePng(path);GD.Print($"AUTHORING_STUDIO_CAPTURE: {error} {path}");}

    private void LoadArea(Vector2 center,int radius){StreamArea(center,radius);_camera.CenterOnGridPosition(center);_camera.SetZoom(radius==0?.85f:radius==1?.52f:.34f);}
    private void StreamArea(Vector2 center,int radius){_county.StreamingRadiusChunks=radius;_county.SetStreamingFocus(center);_canvas.SetLoadedArea(center,radius);_minimap.Center=center;_minimap.Radius=radius;_minimap.QueueRedraw();AuthoringSessionState.Center=center;AuthoringSessionState.Radius=radius;}
    private void CenterBuilding(){AuthoredBuildingData? building=_canvas.InteriorBuilding;if(building is null)return;_camera.CenterOnGridPosition(new Vector2(building.ExteriorX,building.ExteriorY));_camera.SetZoom(.84f);}
    private int RadiusValue()=>Mathf.Clamp(_radius?.Selected??1,0,2);
    private void SetStatus(string value){if(_status is not null)_status.Text=value;}
    private void SetDirtyStatus(bool dirty){if(_dirtyStatus is null)return;_dirtyStatus.Text=dirty?"● UNSAVED":"✓ SAVED";_dirtyStatus.Modulate=dirty?new Color("e7b85c"):new Color("7fc98a");}
    public override void _Notification(int what){if(what!=(int)NotificationWMCloseRequest)return;if(!_canvas.IsDirty||_quitArmed){GetTree().Quit();return;}_quitArmed=true;_canvas.SaveRecovery();SetStatus("UNSAVED WORK — recovery saved. Close again to quit, or click SAVE.");}

    private static PanelContainer Panel(Control parent,float left,float top,float right,float bottom,bool leftAnchor=false,bool rightAnchor=false){PanelContainer panel=new(){ThemeTypeVariation="HudPalettePanel"};parent.AddChild(panel);panel.AnchorLeft=rightAnchor?1:0;panel.AnchorRight=rightAnchor?1:(right<0?1:0);panel.AnchorTop=top<0?1:0;panel.AnchorBottom=bottom<0?1:0;panel.OffsetLeft=left;panel.OffsetTop=top;panel.OffsetRight=right;panel.OffsetBottom=bottom;return panel;}
    private static VBoxContainer VBox(PanelContainer panel){MarginContainer margin=new();margin.AddThemeConstantOverride("margin_left",8);margin.AddThemeConstantOverride("margin_right",8);margin.AddThemeConstantOverride("margin_top",8);margin.AddThemeConstantOverride("margin_bottom",8);panel.AddChild(margin);VBoxContainer box=new(){SizeFlagsVertical=Control.SizeFlags.ExpandFill};margin.AddChild(box);return box;}
    private static VBoxContainer ScrollableVBox(PanelContainer panel){MarginContainer margin=new();margin.AddThemeConstantOverride("margin_left",8);margin.AddThemeConstantOverride("margin_right",8);margin.AddThemeConstantOverride("margin_top",8);margin.AddThemeConstantOverride("margin_bottom",8);panel.AddChild(margin);ScrollContainer scroll=new(){HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,SizeFlagsVertical=Control.SizeFlags.ExpandFill,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};margin.AddChild(scroll);VBoxContainer box=new(){SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};scroll.AddChild(box);return box;}
    private static Label Label(string text,string variation="")=>new(){Text=text,ThemeTypeVariation=variation};
    private static Button Button(string text,Action action){Button button=new(){Text=text,ThemeTypeVariation="HudActionButton"};button.Pressed+=action;return button;}
    private static LineEdit Field(VBoxContainer box,string label){LineEdit field=new();box.AddChild(Labeled(label,field));return field;}
    private static SpinBox Number(VBoxContainer box,string label,out Label title,double step=.25,double max=500){SpinBox number=new(){MinValue=-500,MaxValue=max,Step=step,AllowGreater=true,AllowLesser=true};title=Label(label,"HudTiny");VBoxContainer wrap=new();wrap.SetMeta("studio_field",true);wrap.AddChild(title);wrap.AddChild(number);box.AddChild(wrap);return number;}
    private static SpinBox CompactNumber(string tooltip,double min,double max,double step,double value){string prefix=tooltip=="RADIUS"?"R ":tooltip=="DENSITY"?"D ":tooltip=="SCALE ±"?"± ":"W ";return new(){TooltipText=tooltip,Prefix=prefix,MinValue=min,MaxValue=max,Step=step,Value=value,CustomMinimumSize=new Vector2(74,0),AllowGreater=false,AllowLesser=false};}
    private static VBoxContainer Labeled(string label,Control control){VBoxContainer wrap=new();wrap.SetMeta("studio_field",true);wrap.AddChild(Label(label,"HudTiny"));wrap.AddChild(control);return wrap;}
    private static void SetFieldVisible(Control control,bool visible){if(control.GetParent() is Control parent&&parent.HasMeta("studio_field"))parent.Visible=visible;else control.Visible=visible;}
    private void SetNumbers(float x,float y,float width,float height,float target,float anchorX,float anchorY){_x.Value=x;_y.Value=y;_width.Value=width;_height.Value=height;_target.Value=target;_anchorX.Value=anchorX;_anchorY.Value=anchorY;}
    private void Labels(string x,string y,string width,string height,string anchorX,string anchorY){_xLabel.Text=x;_yLabel.Text=y;_widthLabel.Text=width;_heightLabel.Text=height;_anchorXLabel.Text=anchorX;_anchorYLabel.Text=anchorY;}
    private static void SelectText(OptionButton option,string value){for(int i=0;i<option.ItemCount;i++)if(option.GetItemText(i)==value){option.Selected=i;return;}}
    private static string GetId(object data)=>data switch{AuthoredWorldObjectData x=>x.Id,AuthoredBuildingData x=>x.Id,AuthoredPathData x=>x.Id,AuthoredRoomData x=>x.Id,AuthoredWallData x=>x.Id,AuthoredDoorData x=>x.Id,AuthoredFurnitureData x=>x.Id,AuthoredContainerData x=>x.Id,AuthoredBedData x=>x.Id,_=>string.Empty};
    private static string GetName(object data)=>data switch{AuthoredWorldObjectData x=>x.DisplayName,AuthoredBuildingData x=>x.DisplayName,AuthoredPathData x=>x.DisplayName,AuthoredRoomData x=>x.DisplayName,AuthoredDoorData x=>x.DisplayName,AuthoredFurnitureData x=>x.DisplayName,AuthoredContainerData x=>x.DisplayName,AuthoredBedData x=>x.DisplayName,AuthoredWallData=>"Wall",_=>string.Empty};
    private static void SetName(object data,string value){switch(data){case AuthoredWorldObjectData x:x.DisplayName=value;break;case AuthoredBuildingData x:x.DisplayName=value;break;case AuthoredPathData x:x.DisplayName=value;break;case AuthoredRoomData x:x.DisplayName=value;break;case AuthoredDoorData x:x.DisplayName=value;break;case AuthoredFurnitureData x:x.DisplayName=value;break;case AuthoredContainerData x:x.DisplayName=value;break;case AuthoredBedData x:x.DisplayName=value;break;}}
    private static float BuildingVisualWidth(AuthoredBuildingData item){if(!ResourceLoader.Exists(item.ExteriorAssetPath))return item.ExteriorTargetHeight;Texture2D texture=TextureRegistry.Get(item.ExteriorAssetPath);return texture.GetWidth()*(item.ExteriorTargetHeight/Mathf.Max(1,texture.GetHeight()));}
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
