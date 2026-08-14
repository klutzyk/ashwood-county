#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.Buildings.Interiors;
using AshwoodCounty.World;
using AshwoodCounty.World.County;
using Godot;

namespace AshwoodCounty.Authoring;

public enum AuthoringTool { Select,Place,Terrain,Scatter,Erase,Road,Room,Wall,Door }
public enum StudioSelectionKind { None,WorldObject,Building,Road,Room,Wall,Door,Furniture,Container,Bed }
public enum StudioResizeHandle { None,Left,Right,Top,Bottom,TopLeft,TopRight,BottomLeft,BottomRight }
public readonly record struct StudioSelection(StudioSelectionKind Kind,string Id);

/// <summary>Interactive canvas for both chunk-scoped county objects and one building interior.</summary>
public partial class AuthoringStudioCanvas : Node2D
{
    public event Action? SelectionChanged;
    public event Action<string>? StatusChanged;
    public event Action? DocumentChanged;
    public event Action<bool>? DirtyChanged;

    private readonly List<Node> _visuals=[];
    private readonly Stack<string> _undo=[];
    private readonly Stack<string> _redo=[];
    private readonly HashSet<StudioSelection> _selection=[];
    private IsometricWorld _world=null!;
    private AuthoredCountyDocument _document=new();
    private AuthoredBuildingData? _interiorBuilding;
    private AuthoringAssetEntry? _placementAsset;
    private Vector2 _loadedCenter=new(203,157);
    private int _loadedRadius=1;
    private bool _snap=true;
    private Vector2? _dragStart;
    private Vector2? _boxStart;
    private string? _dragSnapshot;
    private Vector2? _toolStart;
    private IReadOnlyList<AuthoringValidationIssue> _issues=[];
    private IReadOnlyList<Vector2> _testRoute=[];
    private StudioResizeHandle _resizeHandle;
    private Vector2 _resizeStartWorld;
    private Vector2 _resizeOriginalScale;
    private Rect2 _resizeStartRect;
    private string? _resizeSnapshot;
    private bool _pointerDown;
    private string? _strokeSnapshot;
    private Vector2? _lastBrushPoint;
    private AuthoringAssetEntry? _brushAsset;
    private IReadOnlyList<AuthoringAssetEntry> _scatterAssets=[];
    private readonly RandomNumberGenerator _brushRandom=new();
    private readonly List<Vector2> _pathPoints=[];
    private int _roadPointIndex=-1;
    private string? _roadSnapshot;
    private bool _structureLine;
    private AuthoringAssetEntry? _lineAsset;
    private string _savedSnapshot=string.Empty;
    private bool _dirty;
    private readonly HashSet<string> _hiddenLayers=[];
    private readonly HashSet<string> _lockedLayers=[];

    public AuthoringTool Tool { get; private set; }=AuthoringTool.Select;
    public string PlacementGameplayType { get; set; }="Decoration";
    public AuthoredCountyDocument Document=>_document;
    public Vector2 LoadedCenter=>_loadedCenter;
    public AuthoredBuildingData? InteriorBuilding=>_interiorBuilding;
    public IReadOnlyCollection<StudioSelection> Selection=>_selection;
    public StudioSelection PrimarySelection=>_selection.LastOrDefault();
    public bool IsInteriorMode=>_interiorBuilding is not null;
    public bool IsDirty=>_dirty;
    public bool KeepAspectRatio { get; set; }=true;
    public float BrushRadius { get; set; }=2.5f;
    public float BrushDensity { get; set; }=.65f;
    public float BrushScaleVariation { get; set; }=.18f;
    public bool RandomAssetVariation { get; set; }=true;
    public string PathType { get; set; }="Rural Road";
    public float PathWidth { get; set; }=1.2f;
    public bool SnapEnabled { get=>_snap; set{_snap=value;QueueRedraw();} }

    public void Initialize(IsometricWorld world,AuthoredCountyDocument document)
    {
        _world=world;_document=document;_savedSnapshot=AuthoredContentRepository.Serialize(document);ZAsRelative=false;ZIndex=20;YSortEnabled=true;
    }

    public override void _Ready(){RebuildVisuals();SetProcess(true);}
    public override void _Process(double delta){QueueRedraw();}

    public void SetLoadedArea(Vector2 center,int radius)
    {
        _loadedCenter=CountyCoordinateSpace.ClampToCounty(center);_loadedRadius=Mathf.Clamp(radius,0,2);
        ExitInterior();RebuildVisuals();StatusChanged?.Invoke($"Loaded {(_loadedRadius*2+1)}x{(_loadedRadius*2+1)} chunks around {_loadedCenter.X:0}, {_loadedCenter.Y:0}");
    }

    public void SetTool(AuthoringTool tool)
    {
        Tool=tool;_toolStart=null;_boxStart=null;StatusChanged?.Invoke(tool==AuthoringTool.Place&&_placementAsset is null?"Choose an asset from the library.":$"{tool} tool active");QueueRedraw();
    }
    public void SetPlacementAsset(AuthoringAssetEntry asset){_placementAsset=asset;SetTool(AuthoringTool.Place);StatusChanged?.Invoke($"Placing {asset.Name} — click world to place, Esc to cancel");}
    public void SetTerrainBrush(AuthoringAssetEntry asset){_brushAsset=asset;SetTool(AuthoringTool.Terrain);StatusChanged?.Invoke($"TERRAIN BRUSH — {asset.Name}");}
    public void SetScatterBrush(AuthoringAssetEntry asset,IReadOnlyList<AuthoringAssetEntry> variants){_brushAsset=asset;_scatterAssets=variants;SetTool(AuthoringTool.Scatter);StatusChanged?.Invoke($"SCATTER BRUSH — {asset.Name}");}
    public void SetLayerState(string layer,bool visible,bool locked){if(visible)_hiddenLayers.Remove(layer);else _hiddenLayers.Add(layer);if(locked)_lockedLayers.Add(layer);else _lockedLayers.Remove(layer);RebuildVisuals();QueueRedraw();}
    public void BeginRoadTool(){if(_lockedLayers.Contains("Roads")){StatusChanged?.Invoke("Roads layer is locked.");return;}_structureLine=false;_lineAsset=null;_pathPoints.Clear();SetTool(AuthoringTool.Road);StatusChanged?.Invoke("ROAD TOOL — click control points, press Enter to finish");}
    public void BeginStructureLineTool(AuthoringAssetEntry asset){if(_lockedLayers.Contains("Props")){StatusChanged?.Invoke("Props layer is locked.");return;}_structureLine=true;_lineAsset=asset;_pathPoints.Clear();SetTool(AuthoringTool.Road);StatusChanged?.Invoke($"LINE TOOL — {asset.Name}; click points, Enter to finish");}
    public void FinishPath()
    {
        if(_pathPoints.Count<2){_pathPoints.Clear();QueueRedraw();return;}Checkpoint();_document.Paths.Add(new AuthoredPathData{Id=StableId(_structureLine?"line":"road"),DisplayName=_structureLine?_lineAsset?.Name??"Structure Line":PathType,PathType=PathType,LineKind=_structureLine?"Structure":"Road",AssetPath=_lineAsset?.Path??string.Empty,Width=Mathf.Max(.15f,PathWidth),SegmentSpacing=Mathf.Max(.35f,PathWidth),SegmentScale=_lineAsset?.DefaultScale??.35f,Points=_pathPoints.Select(AuthoredPointData.From).ToList()});_pathPoints.Clear();Changed(_structureLine?"Created structure line":"Created road/path");
    }
    public void AddPathPoint(Vector2 point){_pathPoints.Add(Snap(point));QueueRedraw();}
    public void ApplyBrushDab(Vector2 point){BeginBrush(point);EndPointer(point,false);}
    public void SelectBuilding(string id){if(_document.Buildings.All(building=>building.Id!=id))return;_selection.Clear();_selection.Add(new(StudioSelectionKind.Building,id));RebuildVisuals();SelectionChanged?.Invoke();}
    public void RestoreWorldSelection(string kind,string id){if(Enum.TryParse(kind,true,out StudioSelectionKind parsed)&&GetData(new(parsed,id)) is not null){_selection.Clear();_selection.Add(new(parsed,id));RebuildVisuals();SelectionChanged?.Invoke();}}
    public void SelectInteriorItem(StudioSelectionKind kind,string id){_selection.Clear();_selection.Add(new(kind,id));RebuildVisuals();SelectionChanged?.Invoke();}

    public bool EnterInteriorForSelection()
    {
        StudioSelection selection=PrimarySelection;
        if(selection.Kind!=StudioSelectionKind.Building)return false;
        _interiorBuilding=_document.Buildings.FirstOrDefault(building=>building.Id==selection.Id);
        if(_interiorBuilding is null)return false;
        _selection.Clear();_issues=[];_testRoute=[];RebuildVisuals();SelectionChanged?.Invoke();StatusChanged?.Invoke($"INTERIOR EDIT — {_interiorBuilding.DisplayName}");return true;
    }
    public void ExitInterior(){if(_interiorBuilding is null)return;_interiorBuilding=null;_issues=[];_testRoute=[];_selection.Clear();RebuildVisuals();SelectionChanged?.Invoke();}

    public void Save(){AuthoredContentRepository.Save(_document);_savedSnapshot=AuthoredContentRepository.Serialize(_document);SetDirty(false);StatusChanged?.Invoke("Saved authored county data used by normal runtime.");}
    public void SaveRecovery(){if(!_dirty)return;AuthoredContentRepository.SaveRecovery(_document);StatusChanged?.Invoke("Recovery autosave updated — explicit Save is still authoritative");}
    public void Recover(){AuthoredCountyDocument? recovery=AuthoredContentRepository.LoadRecovery();if(recovery is null){StatusChanged?.Invoke("No recovery autosave exists.");return;}Checkpoint();_document=recovery;_interiorBuilding=null;_selection.Clear();RebuildVisuals();SetDirty(true);DocumentChanged?.Invoke();SelectionChanged?.Invoke();StatusChanged?.Invoke("Recovered autosave — review and click SAVE to make it authoritative");}
    public void Undo(){if(_undo.Count==0)return;_redo.Push(AuthoredContentRepository.Serialize(_document));Restore(_undo.Pop());StatusChanged?.Invoke("Undo");}
    public void Redo(){if(_redo.Count==0)return;_undo.Push(AuthoredContentRepository.Serialize(_document));Restore(_redo.Pop());StatusChanged?.Invoke("Redo");}

    public void DeleteSelection()
    {
        if(_selection.Count==0)return;Checkpoint();
        foreach(StudioSelection selection in _selection.ToArray())Remove(selection);
        _selection.Clear();Changed("Deleted selection");
    }

    public void DuplicateSelection()
    {
        if(_selection.Count==0)return;Checkpoint();List<StudioSelection> replacements=[];
        foreach(StudioSelection selection in _selection.ToArray())
        {
            if(selection.Kind==StudioSelectionKind.WorldObject)
            {
                AuthoredWorldObjectData? source=_document.WorldObjects.FirstOrDefault(item=>item.Id==selection.Id);if(source is null)continue;
                AuthoredWorldObjectData copy=AuthoredContentRepository.Deserialize(AuthoredContentRepository.Serialize(new AuthoredCountyDocument{WorldObjects=[source]})).WorldObjects[0];
                copy.Id=StableId("object");copy.DisplayName+=" Copy";copy.X+=.5f;copy.Y+=.5f;_document.WorldObjects.Add(copy);replacements.Add(new(StudioSelectionKind.WorldObject,copy.Id));
            }
            else if(selection.Kind==StudioSelectionKind.Building)
            {
                AuthoredBuildingData? source=_document.Buildings.FirstOrDefault(item=>item.Id==selection.Id);if(source is null)continue;
                AuthoredBuildingData copy=AuthoredContentRepository.Deserialize(AuthoredContentRepository.Serialize(new AuthoredCountyDocument{Buildings=[source]})).Buildings[0];copy.Id=StableId("building");copy.DisplayName+=" Copy";TranslateBuilding(copy,new Vector2(1,1));_document.Buildings.Add(copy);replacements.Add(new(StudioSelectionKind.Building,copy.Id));
            }
            else if(selection.Kind==StudioSelectionKind.Road)
            {
                AuthoredPathData? source=_document.Paths.FirstOrDefault(item=>item.Id==selection.Id);if(source is null)continue;AuthoredCountyDocument shell=new(){Paths=[source]};AuthoredPathData copy=AuthoredContentRepository.Deserialize(AuthoredContentRepository.Serialize(shell)).Paths[0];copy.Id=StableId("road");copy.DisplayName+=" Copy";foreach(AuthoredPointData point in copy.Points){point.X+=1;point.Y+=1;}_document.Paths.Add(copy);replacements.Add(new(StudioSelectionKind.Road,copy.Id));
            }
            else if(IsInteriorMode)DuplicateInterior(selection,replacements);
        }
        _selection.Clear();foreach(StudioSelection selection in replacements)_selection.Add(selection);Changed("Duplicated selection");
    }

    public void RotateSelection(float degrees=90)
    {
        if(GetSelectedData() is not AuthoredWorldObjectData&&GetSelectedData() is not AuthoredBuildingData)return;Checkpoint();
        if(GetSelectedData() is AuthoredWorldObjectData item)item.RotationDegrees=Mathf.PosMod(item.RotationDegrees+degrees,360);
        else if(GetSelectedData() is AuthoredBuildingData building)building.ExteriorRotationDegrees=Mathf.PosMod(building.ExteriorRotationDegrees+degrees,360);
        Changed("Rotated selection");
    }

    public IReadOnlyList<AuthoringValidationIssue> ValidateInterior()
    {
        if(_interiorBuilding is null)return [];
        _issues=AuthoringValidation.Validate(_interiorBuilding);_testRoute=[];QueueRedraw();
        int errors=_issues.Count(issue=>issue.Severity==AuthoringValidationSeverity.Invalid);int warnings=_issues.Count(issue=>issue.Severity==AuthoringValidationSeverity.Warning);
        StatusChanged?.Invoke(errors==0?$"VALID — {warnings} warning(s)":$"INVALID — {errors} error(s), {warnings} warning(s)");return _issues;
    }
    public IReadOnlyList<AuthoringValidationIssue> ValidateCurrent()
    {
        if(IsInteriorMode)return ValidateInterior();_issues=AuthoringValidation.ValidateWorld(_document);_testRoute=[];QueueRedraw();int errors=_issues.Count(issue=>issue.Severity==AuthoringValidationSeverity.Invalid);int warnings=_issues.Count(issue=>issue.Severity==AuthoringValidationSeverity.Warning);StatusChanged?.Invoke(errors==0?$"WORLD VALID — {warnings} warning(s)":$"WORLD INVALID — {errors} error(s), {warnings} warning(s)");return _issues;
    }

    public bool TestEntrance()
    {
        if(_interiorBuilding is null)return false;
        bool pass=AuthoringValidation.TestEntrance(_interiorBuilding,out _testRoute,out string detail);StatusChanged?.Invoke(detail);QueueRedraw();return pass;
    }

    public object? GetSelectedData()
    {
        StudioSelection s=PrimarySelection;if(s.Kind==StudioSelectionKind.None)return null;
        if(s.Kind==StudioSelectionKind.WorldObject)return _document.WorldObjects.FirstOrDefault(item=>item.Id==s.Id);
        if(s.Kind==StudioSelectionKind.Building)return _document.Buildings.FirstOrDefault(item=>item.Id==s.Id);
        if(s.Kind==StudioSelectionKind.Road)return _document.Paths.FirstOrDefault(item=>item.Id==s.Id);
        if(_interiorBuilding is null)return null;
        return s.Kind switch
        {
            StudioSelectionKind.Room=>_interiorBuilding.Rooms.FirstOrDefault(item=>item.Id==s.Id),
            StudioSelectionKind.Wall=>_interiorBuilding.Walls.FirstOrDefault(item=>item.Id==s.Id),
            StudioSelectionKind.Door=>_interiorBuilding.Doors.FirstOrDefault(item=>item.Id==s.Id),
            StudioSelectionKind.Furniture=>_interiorBuilding.Furniture.FirstOrDefault(item=>item.Id==s.Id),
            StudioSelectionKind.Container=>_interiorBuilding.Containers.FirstOrDefault(item=>item.Id==s.Id),
            StudioSelectionKind.Bed=>_interiorBuilding.Beds.FirstOrDefault(item=>item.Id==s.Id),
            _=>null
        };
    }

    public void NotifyInspectorChanged(){Changed("Updated selection");}
    public void BeginInspectorMutation()=>Checkpoint();

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if(inputEvent is InputEventKey key&&key.Pressed&&!key.Echo){HandleShortcut(key);return;}
        Vector2 rawGrid=_world.ScreenToGridPosition(GetViewport().GetMousePosition());
        Vector2 grid=Snap(rawGrid);
        if(inputEvent is InputEventMouseButton mouse&&mouse.ButtonIndex==MouseButton.Left)
        {
            _pointerDown=mouse.Pressed;
            if(mouse.Pressed)BeginPointer(grid,IsometricGrid.GridToScreen(rawGrid),mouse.CtrlPressed||mouse.ShiftPressed);
            else EndPointer(grid,mouse.CtrlPressed||mouse.ShiftPressed);
            GetViewport().SetInputAsHandled();
        }
        else if(inputEvent is InputEventMouseMotion&&Tool==AuthoringTool.Select)
        {
            if(_roadPointIndex>=0)MoveRoadPoint(grid);
            else if(_resizeHandle!=StudioResizeHandle.None)ResizeSelection(IsometricGrid.GridToScreen(rawGrid));
            else if(_dragStart is not null)MoveSelection(grid-_dragStart.Value);
        }
        else if(inputEvent is InputEventMouseMotion&&_pointerDown&&Tool is AuthoringTool.Terrain or AuthoringTool.Scatter or AuthoringTool.Erase)ContinueBrush(rawGrid);
    }

    private void HandleShortcut(InputEventKey key)
    {
        if(key.Keycode==Key.Q){SetTool(AuthoringTool.Select);return;}
        if(key.Keycode==Key.E){DeleteSelection();return;}
        if(key.Keycode==Key.R){RotateSelection();return;}
        if(key.Keycode==Key.Enter&&Tool==AuthoringTool.Road){FinishPath();return;}
        if(key.Keycode==Key.Escape){if(_pathPoints.Count>0){_pathPoints.Clear();QueueRedraw();}else SetTool(AuthoringTool.Select);return;}
        if(key.Keycode==Key.Delete){DeleteSelection();return;}
        if(key.CtrlPressed&&key.Keycode==Key.Z){Undo();return;}
        if(key.CtrlPressed&&key.Keycode==Key.Y){Redo();return;}
        if(key.CtrlPressed&&key.Keycode==Key.D){DuplicateSelection();return;}
    }

    private void BeginPointer(Vector2 grid,Vector2 worldPoint,bool additive)
    {
        switch(Tool)
        {
            case AuthoringTool.Place:Place(grid);break;
            case AuthoringTool.Terrain:case AuthoringTool.Scatter:case AuthoringTool.Erase:BeginBrush(grid);break;
            case AuthoringTool.Road:AddPathPoint(grid);StatusChanged?.Invoke($"ROAD TOOL — {_pathPoints.Count} points; Enter to finish");break;
            case AuthoringTool.Room:case AuthoringTool.Wall:_toolStart=grid;break;
            case AuthoringTool.Door:PlaceDoor(grid);break;
            default:
                int roadPoint=RoadPointAt(grid);if(roadPoint>=0){_roadPointIndex=roadPoint;_roadSnapshot=AuthoredContentRepository.Serialize(_document);return;}
                StudioResizeHandle resize=ResizeHandleAt(worldPoint);
                if(resize!=StudioResizeHandle.None)
                {
                    _resizeHandle=resize;_resizeStartWorld=worldPoint;_resizeStartRect=SelectedResizeRect();_resizeOriginalScale=GetSelectedData() switch{AuthoredWorldObjectData selectedItem=>new(selectedItem.Scale,selectedItem.ScaleY>0?selectedItem.ScaleY:selectedItem.Scale),AuthoredBuildingData selectedBuilding=>new(selectedBuilding.ExteriorTargetWidth>0?selectedBuilding.ExteriorTargetWidth:_resizeStartRect.Size.X,selectedBuilding.ExteriorTargetHeight),_=>Vector2.One};_resizeSnapshot=AuthoredContentRepository.Serialize(_document);return;
                }
                StudioSelection hit=HitTest(grid,worldPoint);
                if(hit.Kind!=StudioSelectionKind.None)
                {
                    if(!additive&&!_selection.Contains(hit))_selection.Clear();_selection.Add(hit);SelectionChanged?.Invoke();
                    _dragStart=grid;_dragSnapshot=AuthoredContentRepository.Serialize(_document);
                }
                else{if(!additive)_selection.Clear();_boxStart=grid;SelectionChanged?.Invoke();}
                break;
        }
    }

    private void EndPointer(Vector2 grid,bool additive)
    {
        if(_roadPointIndex>=0){if(_roadSnapshot is not null&&_roadSnapshot!=AuthoredContentRepository.Serialize(_document)){_undo.Push(_roadSnapshot);_redo.Clear();Changed("Moved road control point",false);}_roadPointIndex=-1;_roadSnapshot=null;return;}
        if(_strokeSnapshot is not null)
        {
            if(_strokeSnapshot!=AuthoredContentRepository.Serialize(_document)){_undo.Push(_strokeSnapshot);_redo.Clear();StatusChanged?.Invoke("Brush stroke complete");}
            _strokeSnapshot=null;_lastBrushPoint=null;return;
        }
        if(_resizeHandle!=StudioResizeHandle.None)
        {
            if(_resizeSnapshot is not null&&_resizeSnapshot!=AuthoredContentRepository.Serialize(_document)){_undo.Push(_resizeSnapshot);_redo.Clear();Changed("Scaled selection",false);}
            _resizeHandle=StudioResizeHandle.None;_resizeSnapshot=null;return;
        }
        if(Tool==AuthoringTool.Room&&_toolStart is Vector2 roomStart){CreateRoom(roomStart,grid);_toolStart=null;return;}
        if(Tool==AuthoringTool.Wall&&_toolStart is Vector2 wallStart){CreateWall(wallStart,grid);_toolStart=null;return;}
        if(_dragStart is not null)
        {
            if(_dragSnapshot is not null&&_dragSnapshot!=AuthoredContentRepository.Serialize(_document)){_undo.Push(_dragSnapshot);_redo.Clear();Changed("Moved selection",false);}
            _dragStart=null;_dragSnapshot=null;return;
        }
        if(_boxStart is Vector2 box){BoxSelect(new Rect2(Min(box,grid),(grid-box).Abs()),additive);_boxStart=null;}
    }

    private int RoadPointAt(Vector2 grid)
    {
        if(GetSelectedData() is not AuthoredPathData path)return -1;for(int i=0;i<path.Points.Count;i++)if(path.Points[i].Vector.DistanceTo(grid)<.55f)return i;return -1;
    }
    private void MoveRoadPoint(Vector2 grid){if(GetSelectedData() is not AuthoredPathData path||_roadPointIndex>=path.Points.Count)return;path.Points[_roadPointIndex]=AuthoredPointData.From(grid);DocumentChanged?.Invoke();QueueRedraw();SelectionChanged?.Invoke();}

    private void BeginBrush(Vector2 grid){_strokeSnapshot=AuthoredContentRepository.Serialize(_document);_lastBrushPoint=null;ApplyBrush(grid);}
    private void ContinueBrush(Vector2 grid){if(_lastBrushPoint is null||_lastBrushPoint.Value.DistanceTo(grid)>=Mathf.Max(.35f,BrushRadius*.28f))ApplyBrush(grid);}
    private void ApplyBrush(Vector2 center)
    {
        if(!CountyCoordinateSpace.GridBounds.HasPoint(center))return;_lastBrushPoint=center;
        string brushLayer=Tool==AuthoringTool.Terrain?"Terrain":Tool==AuthoringTool.Scatter&&_brushAsset is not null?LayerFor(_brushAsset.Category,"Decoration"):"";if(_lockedLayers.Contains(brushLayer)){StatusChanged?.Invoke($"{brushLayer} layer is locked.");return;}
        if(Tool==AuthoringTool.Terrain&&_brushAsset is not null)
        {
            Vector2 point=center+RandomOffset(BrushRadius*.18f);_document.TerrainStamps.Add(new AuthoredTerrainStampData{Id=StableId("terrain"),AssetPath=_brushAsset.Path,X=point.X,Y=point.Y,Radius=Mathf.Max(.65f,BrushRadius*(.82f+_brushRandom.RandfRange(-.08f,.08f))),Opacity=Mathf.Clamp(.42f+BrushDensity*.42f,.2f,.95f),RotationDegrees=RandomAssetVariation?_brushRandom.RandfRange(-12,12):0});
        }
        else if(Tool==AuthoringTool.Scatter&&_brushAsset is not null)
        {
            int count=Mathf.Clamp(Mathf.CeilToInt(BrushRadius*BrushRadius*BrushDensity*.34f),1,40);IReadOnlyList<AuthoringAssetEntry> choices=RandomAssetVariation&&_scatterAssets.Count>0?_scatterAssets:[_brushAsset];
            for(int i=0;i<count;i++)
            {
                Vector2 point=center+RandomOffset(BrushRadius);if(!CountyCoordinateSpace.GridBounds.HasPoint(point)||_document.WorldObjects.Any(item=>item.BrushGenerated&&new Vector2(item.X,item.Y).DistanceTo(point)<.34f))continue;AuthoringAssetEntry asset=choices[_brushRandom.RandiRange(0,choices.Count-1)];float scale=asset.DefaultScale*(1+_brushRandom.RandfRange(-BrushScaleVariation,BrushScaleVariation));
                _document.WorldObjects.Add(new AuthoredWorldObjectData{Id=StableId("scatter"),DisplayName=Title(asset.Name),AssetPath=asset.Path,Category=asset.Category,GameplayType="Decoration",X=point.X,Y=point.Y,Scale=Mathf.Max(.04f,scale),ScaleY=Mathf.Max(.04f,scale),AnchorX=.5f,AnchorY=1,Collision=asset.DefaultCollision,BrushGenerated=true});
            }
        }
        else if(Tool==AuthoringTool.Erase)
        {
            _document.TerrainStamps.RemoveAll(item=>new Vector2(item.X,item.Y).DistanceTo(center)<=BrushRadius);_document.WorldObjects.RemoveAll(item=>item.BrushGenerated&&new Vector2(item.X,item.Y).DistanceTo(center)<=BrushRadius);
        }
        RebuildVisuals();SetDirty(true);DocumentChanged?.Invoke();QueueRedraw();
    }
    private Vector2 RandomOffset(float radius){float angle=_brushRandom.RandfRange(0,Mathf.Tau),distance=radius*Mathf.Sqrt(_brushRandom.Randf());return Vector2.FromAngle(angle)*distance;}

    private void ResizeSelection(Vector2 worldPoint)
    {
        object? selected=GetSelectedData();if(selected is not AuthoredWorldObjectData&&selected is not AuthoredBuildingData)return;Vector2 delta=(worldPoint-_resizeStartWorld).Rotated(-Mathf.DegToRad(SelectedRotation()));float fx=1,fy=1;
        if(_resizeHandle is StudioResizeHandle.Left or StudioResizeHandle.TopLeft or StudioResizeHandle.BottomLeft)fx=1-delta.X/Mathf.Max(1,_resizeStartRect.Size.X);
        if(_resizeHandle is StudioResizeHandle.Right or StudioResizeHandle.TopRight or StudioResizeHandle.BottomRight)fx=1+delta.X/Mathf.Max(1,_resizeStartRect.Size.X);
        if(_resizeHandle is StudioResizeHandle.Top or StudioResizeHandle.TopLeft or StudioResizeHandle.TopRight)fy=1-delta.Y/Mathf.Max(1,_resizeStartRect.Size.Y);
        if(_resizeHandle is StudioResizeHandle.Bottom or StudioResizeHandle.BottomLeft or StudioResizeHandle.BottomRight)fy=1+delta.Y/Mathf.Max(1,_resizeStartRect.Size.Y);
        if(KeepAspectRatio)
        {
            float factor=_resizeHandle is StudioResizeHandle.Left or StudioResizeHandle.Right?fx:_resizeHandle is StudioResizeHandle.Top or StudioResizeHandle.Bottom?fy:Mathf.Abs(fx-1)>Mathf.Abs(fy-1)?fx:fy;
            fx=factor;fy=factor;
        }
        if(selected is AuthoredWorldObjectData item){item.Scale=Mathf.Max(.02f,_resizeOriginalScale.X*fx);item.ScaleY=Mathf.Max(.02f,_resizeOriginalScale.Y*fy);}
        else if(selected is AuthoredBuildingData building){building.ExteriorTargetWidth=Mathf.Max(8,_resizeOriginalScale.X*fx);building.ExteriorTargetHeight=Mathf.Max(8,_resizeOriginalScale.Y*fy);}
        RebuildVisuals();SelectionChanged?.Invoke();
    }

    private void Place(Vector2 grid)
    {
        if(_placementAsset is null)return;Checkpoint();
        if(IsInteriorMode)PlaceInteriorAsset(grid,_placementAsset);
        else if(PlacementGameplayType=="Building"||_placementAsset.Category=="Buildings")
        {
            Texture2D texture=TextureRegistry.Get(_placementAsset.Path);
            AuthoredBuildingData building=new(){Id=StableId("building"),DisplayName=Title(_placementAsset.Name),ExteriorAssetPath=_placementAsset.Path,ExteriorX=grid.X,ExteriorY=grid.Y,ExteriorTargetHeight=texture.GetHeight()*_placementAsset.DefaultScale,FootprintX=grid.X-3,FootprintY=grid.Y-2.5f,FootprintWidth=6,FootprintHeight=5};
            _document.Buildings.Add(building);_selection.Clear();_selection.Add(new(StudioSelectionKind.Building,building.Id));
        }
        else
        {
            AuthoredWorldObjectData item=new(){Id=StableId("object"),DisplayName=Title(_placementAsset.Name),AssetPath=_placementAsset.Path,Category=_placementAsset.Category,GameplayType=PlacementGameplayType,X=grid.X,Y=grid.Y,Scale=_placementAsset.DefaultScale,Collision=_placementAsset.DefaultCollision};
            _document.WorldObjects.Add(item);_selection.Clear();_selection.Add(new(StudioSelectionKind.WorldObject,item.Id));
        }
        Changed($"Placed {_placementAsset.Name}");
    }

    private void PlaceInteriorAsset(Vector2 grid,AuthoringAssetEntry asset)
    {
        if(_interiorBuilding is null)return;string room=RoomAt(grid)?.Id??string.Empty;
        if(asset.Path.Contains("/surfaces/floor_"))
        {
            AuthoredRoomData? floorRoom=RoomAt(grid);if(floorRoom is null){StatusChanged?.Invoke("Place floor material inside an authored room.");return;}floorRoom.FloorTexturePath=asset.Path;_selection.Clear();_selection.Add(new(StudioSelectionKind.Room,floorRoom.Id));return;
        }
        if(PlacementGameplayType=="Container")
        {
            AuthoredContainerData item=new(){Id=StableId("container"),DisplayName=Title(asset.Name),RoomId=room,AssetPath=asset.Path,LootPreset="Bedroom Storage",X=grid.X,Y=grid.Y,InteractionX=grid.X+.65f,InteractionY=grid.Y+.45f,Width=.6f,Height=.55f,TargetHeight=AssetHeight(asset.Path),SearchDuration=3.5f};_interiorBuilding.Containers.Add(item);_selection.Clear();_selection.Add(new(StudioSelectionKind.Container,item.Id));
        }
        else if(PlacementGameplayType=="Bed")
        {
            AuthoredBedData item=new(){Id=StableId("bed"),DisplayName=Title(asset.Name),RoomId=room,AssetPath=asset.Path,X=grid.X,Y=grid.Y,InteractionX=grid.X+1f,InteractionY=grid.Y+.7f,Width=1.35f,Height=.82f,TargetHeight=AssetHeight(asset.Path)};_interiorBuilding.Beds.Add(item);_selection.Clear();_selection.Add(new(StudioSelectionKind.Bed,item.Id));
        }
        else
        {
            AuthoredFurnitureData item=new(){Id=StableId("furniture"),DisplayName=Title(asset.Name),RoomId=room,AssetPath=asset.Path,X=grid.X,Y=grid.Y,Width=asset.DefaultCollision?.72f:.12f,Height=asset.DefaultCollision?.58f:.12f,TargetHeight=AssetHeight(asset.Path),BlocksMovement=asset.DefaultCollision};_interiorBuilding.Furniture.Add(item);_selection.Clear();_selection.Add(new(StudioSelectionKind.Furniture,item.Id));
        }
    }

    private void CreateRoom(Vector2 a,Vector2 b)
    {
        if(_interiorBuilding is null||a.DistanceTo(b)<.5f)return;Checkpoint();Vector2 min=Min(a,b),size=(b-a).Abs();
        AuthoredRoomData room=new(){Id=StableId("room"),DisplayName=$"Room {_interiorBuilding.Rooms.Count+1}",X=min.X,Y=min.Y,Width=size.X,Height=size.Y,FloorTexturePath="res://assets/art/interiors/residential/surfaces/floor_wood_light_01.png",FloorTint="b5a17d"};
        _interiorBuilding.Rooms.Add(room);_selection.Clear();_selection.Add(new(StudioSelectionKind.Room,room.Id));Changed("Created room");
    }

    private void CreateWall(Vector2 a,Vector2 b)
    {
        if(_interiorBuilding is null||a.DistanceTo(b)<.25f)return;Checkpoint();
        Vector2 delta=b-a;if(Mathf.Abs(delta.X)>Mathf.Abs(delta.Y))b.Y=a.Y;else b.X=a.X;
        AuthoredWallData wall=new(){Id=StableId("wall"),StartX=a.X,StartY=a.Y,EndX=b.X,EndY=b.Y,TexturePath="res://assets/art/interiors/residential/structure/wall_plain_cream_01.png",FlipVisual=Mathf.IsEqualApprox(a.X,b.X)};
        _interiorBuilding.Walls.Add(wall);_selection.Clear();_selection.Add(new(StudioSelectionKind.Wall,wall.Id));Changed("Drew wall with navigation blocker");
    }

    private void PlaceDoor(Vector2 grid)
    {
        if(_interiorBuilding is null)return;AuthoredWallData? wall=_interiorBuilding.Walls.MinBy(item=>DistanceToSegment(grid,new(item.StartX,item.StartY),new(item.EndX,item.EndY)));
        if(wall is null||DistanceToSegment(grid,new(wall.StartX,wall.StartY),new(wall.EndX,wall.EndY))>.5f){StatusChanged?.Invoke("INVALID — doors must attach to a wall segment");return;}
        Vector2 start=new(wall.StartX,wall.StartY),end=new(wall.EndX,wall.EndY);Vector2 line=end-start;float t=Mathf.Clamp((grid-start).Dot(line)/Mathf.Max(.001f,line.LengthSquared()),.12f,.88f);Vector2 point=Snap(start+line*t);Vector2 normal=Mathf.IsEqualApprox(start.Y,end.Y)?Vector2.Down:Vector2.Right;
        Checkpoint();SplitWallForDoor(wall,point);
        Vector2 minus=point-normal*.75f,plus=point+normal*.75f;AuthoredRoomData? minusRoom=RoomAt(minus),plusRoom=RoomAt(plus);bool exterior=minusRoom is null||plusRoom is null;
        Vector2 approach=exterior?(minusRoom is null?minus:plus):minus;Vector2 arrival=exterior?(minusRoom is null?plus:minus):plus;
        AuthoredDoorData door=new(){Id=StableId("door"),DisplayName=exterior?"Exterior Door":"Interior Door",WallId=wall.Id,RoomAId=minusRoom?.Id??"outside",RoomBId=plusRoom?.Id??"outside",Exterior=exterior,X=point.X,Y=point.Y,OutsideApproachX=approach.X,OutsideApproachY=approach.Y,InsideArrivalX=arrival.X,InsideArrivalY=arrival.Y,ClosedTexturePath="res://assets/art/interiors/residential/structure/door_closed_brown_01.png",OpenTexturePath="res://assets/art/interiors/residential/structure/door_frame_open_01.png",InitialState="Closed"};
        _interiorBuilding.Doors.Add(door);_selection.Clear();_selection.Add(new(StudioSelectionKind.Door,door.Id));Changed("Placed deterministic door and navigation opening");
    }

    private void SplitWallForDoor(AuthoredWallData wall,Vector2 point)
    {
        if(_interiorBuilding is null)return;Vector2 start=new(wall.StartX,wall.StartY),end=new(wall.EndX,wall.EndY),direction=(end-start).Normalized();const float halfGap=.48f;
        _interiorBuilding.Walls.Remove(wall);
        AddWallSegment(wall,start,point-direction*halfGap,"a");AddWallSegment(wall,point+direction*halfGap,end,"b");
    }
    private void AddWallSegment(AuthoredWallData source,Vector2 start,Vector2 end,string suffix){if(_interiorBuilding is null||start.DistanceTo(end)<.15f)return;_interiorBuilding.Walls.Add(new AuthoredWallData{Id=source.Id+"_"+suffix,StartX=start.X,StartY=start.Y,EndX=end.X,EndY=end.Y,TexturePath=source.TexturePath,FlipVisual=source.FlipVisual});}

    private void MoveSelection(Vector2 delta)
    {
        if(delta.IsZeroApprox())return;foreach(StudioSelection selection in _selection)Move(selection,delta);_dragStart=(_dragStart??Vector2.Zero)+delta;RebuildVisuals();SelectionChanged?.Invoke();
    }

    private void Move(StudioSelection selection,Vector2 delta)
    {
        object? data=GetData(selection);switch(data)
        {
            case AuthoredWorldObjectData item:item.X+=delta.X;item.Y+=delta.Y;break;
            case AuthoredBuildingData item:TranslateBuilding(item,delta);break;
            case AuthoredPathData item:foreach(AuthoredPointData point in item.Points){point.X+=delta.X;point.Y+=delta.Y;}break;
            case AuthoredRoomData item:item.X+=delta.X;item.Y+=delta.Y;break;
            case AuthoredWallData item:item.StartX+=delta.X;item.StartY+=delta.Y;item.EndX+=delta.X;item.EndY+=delta.Y;break;
            case AuthoredDoorData item:item.X+=delta.X;item.Y+=delta.Y;item.OutsideApproachX+=delta.X;item.OutsideApproachY+=delta.Y;item.InsideArrivalX+=delta.X;item.InsideArrivalY+=delta.Y;break;
            case AuthoredFurnitureData item:item.X+=delta.X;item.Y+=delta.Y;break;
            case AuthoredContainerData item:item.X+=delta.X;item.Y+=delta.Y;item.InteractionX+=delta.X;item.InteractionY+=delta.Y;break;
            case AuthoredBedData item:item.X+=delta.X;item.Y+=delta.Y;item.InteractionX+=delta.X;item.InteractionY+=delta.Y;break;
        }
    }

    private void BoxSelect(Rect2 area,bool additive)
    {
        if(!additive)_selection.Clear();
        if(IsInteriorMode&&_interiorBuilding is not null)
        {
            foreach(AuthoredFurnitureData item in _interiorBuilding.Furniture.Where(item=>area.HasPoint(new(item.X,item.Y))))_selection.Add(new(StudioSelectionKind.Furniture,item.Id));
            foreach(AuthoredContainerData item in _interiorBuilding.Containers.Where(item=>area.HasPoint(new(item.X,item.Y))))_selection.Add(new(StudioSelectionKind.Container,item.Id));
            foreach(AuthoredBedData item in _interiorBuilding.Beds.Where(item=>area.HasPoint(new(item.X,item.Y))))_selection.Add(new(StudioSelectionKind.Bed,item.Id));
        }
        else{foreach(AuthoredWorldObjectData item in _document.WorldObjects.Where(item=>area.HasPoint(new(item.X,item.Y))))_selection.Add(new(StudioSelectionKind.WorldObject,item.Id));foreach(AuthoredPathData path in _document.Paths.Where(path=>path.Points.Any(point=>area.HasPoint(point.Vector))))_selection.Add(new(StudioSelectionKind.Road,path.Id));}
        SelectionChanged?.Invoke();RebuildVisuals();
    }

    private StudioSelection HitTest(Vector2 grid,Vector2 worldPoint)
    {
        if(IsInteriorMode&&_interiorBuilding is not null)
        {
            foreach(AuthoredDoorData item in _interiorBuilding.Doors.OrderByDescending(item=>item.X+item.Y))if(SpriteContains(item.ClosedTexturePath,new(item.X,item.Y),84,0,new(.5f,1),worldPoint))return new(StudioSelectionKind.Door,item.Id);
            foreach(AuthoredContainerData item in _interiorBuilding.Containers.OrderByDescending(item=>item.X+item.Y))if(SpriteContains(item.AssetPath,new(item.X,item.Y),item.TargetHeight,0,new(.5f,1),worldPoint))return new(StudioSelectionKind.Container,item.Id);
            foreach(AuthoredBedData item in _interiorBuilding.Beds.OrderByDescending(item=>item.X+item.Y))if(SpriteContains(item.AssetPath,new(item.X,item.Y),item.TargetHeight,0,new(.5f,1),worldPoint))return new(StudioSelectionKind.Bed,item.Id);
            foreach(AuthoredFurnitureData item in _interiorBuilding.Furniture.OrderByDescending(item=>item.X+item.Y))if(SpriteContains(item.AssetPath,new(item.X,item.Y),item.TargetHeight,0,new(.5f,1),worldPoint))return new(StudioSelectionKind.Furniture,item.Id);
            foreach(AuthoredDoorData item in _interiorBuilding.Doors.OrderByDescending(item=>item.Y))if(new Vector2(item.X,item.Y).DistanceTo(grid)<.55f)return new(StudioSelectionKind.Door,item.Id);
            foreach(AuthoredContainerData item in _interiorBuilding.Containers.OrderByDescending(item=>item.Y))if(new Rect2(item.X-item.Width*.5f,item.Y-item.Height*.5f,item.Width,item.Height).Grow(.18f).HasPoint(grid))return new(StudioSelectionKind.Container,item.Id);
            foreach(AuthoredBedData item in _interiorBuilding.Beds.OrderByDescending(item=>item.Y))if(new Rect2(item.X-item.Width*.5f,item.Y-item.Height*.5f,item.Width,item.Height).Grow(.18f).HasPoint(grid))return new(StudioSelectionKind.Bed,item.Id);
            foreach(AuthoredFurnitureData item in _interiorBuilding.Furniture.OrderByDescending(item=>item.Y))if(new Rect2(item.X-item.Width*.5f,item.Y-item.Height*.5f,Mathf.Max(.25f,item.Width),Mathf.Max(.25f,item.Height)).Grow(.18f).HasPoint(grid))return new(StudioSelectionKind.Furniture,item.Id);
            AuthoredWallData? wall=_interiorBuilding.Walls.FirstOrDefault(item=>DistanceToSegment(grid,new(item.StartX,item.StartY),new(item.EndX,item.EndY))<.18f);if(wall is not null)return new(StudioSelectionKind.Wall,wall.Id);
            AuthoredRoomData? room=_interiorBuilding.Rooms.LastOrDefault(item=>new Rect2(item.X,item.Y,item.Width,item.Height).HasPoint(grid));if(room is not null)return new(StudioSelectionKind.Room,room.Id);
        }
        else
        {
            foreach(AuthoredWorldObjectData visualItem in _document.WorldObjects.Where(InLoadedArea).Where(item=>LayerEditable(item)).OrderByDescending(item=>item.X+item.Y))if(SpriteContains(visualItem.AssetPath,new(visualItem.X,visualItem.Y),0,visualItem.Scale,new(visualItem.AnchorX,visualItem.AnchorY),worldPoint,visualItem.ScaleY,0,visualItem.RotationDegrees))return new(StudioSelectionKind.WorldObject,visualItem.Id);
            if(!_hiddenLayers.Contains("Buildings")&&!_lockedLayers.Contains("Buildings"))foreach(AuthoredBuildingData visualBuilding in _document.Buildings.Where(InLoadedArea).OrderByDescending(item=>item.ExteriorX+item.ExteriorY))if(SpriteContains(visualBuilding.ExteriorAssetPath,new(visualBuilding.ExteriorX,visualBuilding.ExteriorY),visualBuilding.ExteriorTargetHeight,0,new(.5f,1),worldPoint,0,visualBuilding.ExteriorTargetWidth,visualBuilding.ExteriorRotationDegrees))return new(StudioSelectionKind.Building,visualBuilding.Id);
            AuthoredWorldObjectData? item=_document.WorldObjects.Where(InLoadedArea).Where(item=>LayerEditable(item)).OrderByDescending(item=>item.X+item.Y).FirstOrDefault(item=>new Vector2(item.X,item.Y).DistanceTo(grid)<.75f);if(item is not null)return new(StudioSelectionKind.WorldObject,item.Id);
            if(!_hiddenLayers.Contains("Buildings")&&!_lockedLayers.Contains("Buildings")){AuthoredBuildingData? building=_document.Buildings.Where(InLoadedArea).OrderByDescending(item=>item.ExteriorX+item.ExteriorY).FirstOrDefault(item=>new Rect2(item.FootprintX,item.FootprintY,item.FootprintWidth,item.FootprintHeight).HasPoint(grid));if(building is not null)return new(StudioSelectionKind.Building,building.Id);}
            if(!_hiddenLayers.Contains("Roads")&&!_lockedLayers.Contains("Roads")){AuthoredPathData? path=_document.Paths.LastOrDefault(path=>path.Points.Zip(path.Points.Skip(1),(a,b)=>DistanceToSegment(grid,a.Vector,b.Vector)).Any(distance=>distance<Mathf.Max(.35f,path.Width*.35f)));if(path is not null)return new(StudioSelectionKind.Road,path.Id);}
        }
        return default;
    }

    private StudioResizeHandle ResizeHandleAt(Vector2 worldPoint)
    {
        if(GetSelectedData() is not AuthoredWorldObjectData&&GetSelectedData() is not AuthoredBuildingData)return StudioResizeHandle.None;Rect2 rect=SelectedResizeRect();Vector2 center=rect.GetCenter(),pivot=SelectedPivot();float rotation=Mathf.DegToRad(SelectedRotation());
        (StudioResizeHandle Handle,Vector2 Point)[] handles=[(StudioResizeHandle.TopLeft,rect.Position),(StudioResizeHandle.Top,new(center.X,rect.Position.Y)),(StudioResizeHandle.TopRight,new(rect.End.X,rect.Position.Y)),(StudioResizeHandle.Left,new(rect.Position.X,center.Y)),(StudioResizeHandle.Right,new(rect.End.X,center.Y)),(StudioResizeHandle.BottomLeft,new(rect.Position.X,rect.End.Y)),(StudioResizeHandle.Bottom,new(center.X,rect.End.Y)),(StudioResizeHandle.BottomRight,rect.End)];
        foreach((StudioResizeHandle handle,Vector2 point) in handles)if(worldPoint.DistanceTo(pivot+(point-pivot).Rotated(rotation))<=12)return handle;return StudioResizeHandle.None;
    }

    private Rect2 SelectedResizeRect()=>GetSelectedData() switch
    {
        AuthoredWorldObjectData item=>SpriteRect(item.AssetPath,new(item.X,item.Y),0,item.Scale,new(item.AnchorX,item.AnchorY),item.ScaleY),
        AuthoredBuildingData building=>SpriteRect(building.ExteriorAssetPath,new(building.ExteriorX,building.ExteriorY),building.ExteriorTargetHeight,0,new(.5f,1),0,building.ExteriorTargetWidth),
        _=>new Rect2()
    };
    private float SelectedRotation()=>GetSelectedData() switch{AuthoredWorldObjectData item=>item.RotationDegrees,AuthoredBuildingData building=>building.ExteriorRotationDegrees,_=>0};
    private Vector2 SelectedPivot()=>GetSelectedData() switch{AuthoredWorldObjectData item=>IsometricGrid.GridToScreen(new(item.X,item.Y)),AuthoredBuildingData building=>IsometricGrid.GridToScreen(new(building.ExteriorX,building.ExteriorY)),_=>Vector2.Zero};

    private static bool SpriteContains(string path,Vector2 grid,float targetHeight,float scale,Vector2 anchor,Vector2 worldPoint,float scaleY=0,float targetWidth=0,float rotationDegrees=0){Vector2 pivot=IsometricGrid.GridToScreen(grid);Vector2 unrotated=pivot+(worldPoint-pivot).Rotated(-Mathf.DegToRad(rotationDegrees));return SpriteRect(path,grid,targetHeight,scale,anchor,scaleY,targetWidth).Grow(3).HasPoint(unrotated);}
    private static Rect2 SpriteRect(string path,Vector2 grid,float targetHeight,float scale,Vector2 anchor,float scaleY=0,float targetWidth=0)
    {
        if(string.IsNullOrWhiteSpace(path)||!ResourceLoader.Exists(path))return new Rect2();Texture2D texture=TextureRegistry.Get(path);Vector2 visualScale=targetHeight>0?new Vector2(targetWidth>0?targetWidth/Mathf.Max(1,texture.GetWidth()):targetHeight/Mathf.Max(1,texture.GetHeight()),targetHeight/Mathf.Max(1,texture.GetHeight())):new Vector2(Mathf.Max(.02f,scale),Mathf.Max(.02f,scaleY>0?scaleY:scale));Vector2 size=texture.GetSize()*visualScale;
        return new Rect2(IsometricGrid.GridToScreen(grid)-size*anchor,size);
    }

    private object? GetData(StudioSelection selection)
    {
        if(selection.Kind==StudioSelectionKind.WorldObject)return _document.WorldObjects.FirstOrDefault(item=>item.Id==selection.Id);
        if(selection.Kind==StudioSelectionKind.Building)return _document.Buildings.FirstOrDefault(item=>item.Id==selection.Id);
        if(selection.Kind==StudioSelectionKind.Road)return _document.Paths.FirstOrDefault(item=>item.Id==selection.Id);
        if(_interiorBuilding is null)return null;
        return selection.Kind switch
        {
            StudioSelectionKind.Room=>_interiorBuilding.Rooms.FirstOrDefault(item=>item.Id==selection.Id),
            StudioSelectionKind.Wall=>_interiorBuilding.Walls.FirstOrDefault(item=>item.Id==selection.Id),
            StudioSelectionKind.Door=>_interiorBuilding.Doors.FirstOrDefault(item=>item.Id==selection.Id),
            StudioSelectionKind.Furniture=>_interiorBuilding.Furniture.FirstOrDefault(item=>item.Id==selection.Id),
            StudioSelectionKind.Container=>_interiorBuilding.Containers.FirstOrDefault(item=>item.Id==selection.Id),
            StudioSelectionKind.Bed=>_interiorBuilding.Beds.FirstOrDefault(item=>item.Id==selection.Id),
            _=>null
        };
    }

    private void Remove(StudioSelection selection)
    {
        switch(selection.Kind)
        {
            case StudioSelectionKind.WorldObject:_document.WorldObjects.RemoveAll(item=>item.Id==selection.Id);break;
            case StudioSelectionKind.Building:_document.Buildings.RemoveAll(item=>item.Id==selection.Id);break;
            case StudioSelectionKind.Road:_document.Paths.RemoveAll(item=>item.Id==selection.Id);break;
            case StudioSelectionKind.Room:_interiorBuilding?.Rooms.RemoveAll(item=>item.Id==selection.Id);break;
            case StudioSelectionKind.Wall:_interiorBuilding?.Walls.RemoveAll(item=>item.Id==selection.Id);break;
            case StudioSelectionKind.Door:_interiorBuilding?.Doors.RemoveAll(item=>item.Id==selection.Id);break;
            case StudioSelectionKind.Furniture:_interiorBuilding?.Furniture.RemoveAll(item=>item.Id==selection.Id);break;
            case StudioSelectionKind.Container:_interiorBuilding?.Containers.RemoveAll(item=>item.Id==selection.Id);break;
            case StudioSelectionKind.Bed:_interiorBuilding?.Beds.RemoveAll(item=>item.Id==selection.Id);break;
        }
    }

    private void DuplicateInterior(StudioSelection selection,List<StudioSelection> replacements)
    {
        if(_interiorBuilding is null)return;object? source=GetData(selection);if(source is null)return;
        AuthoredBuildingData shell=new();
        switch(source)
        {
            case AuthoredFurnitureData item:shell.Furniture=[item];AuthoredFurnitureData f=AuthoredContentRepository.Deserialize(AuthoredContentRepository.Serialize(new AuthoredCountyDocument{Buildings=[shell]})).Buildings[0].Furniture[0];f.Id=StableId("furniture");f.X+=.5f;f.Y+=.5f;_interiorBuilding.Furniture.Add(f);replacements.Add(new(StudioSelectionKind.Furniture,f.Id));break;
            case AuthoredContainerData item:shell.Containers=[item];AuthoredContainerData c=AuthoredContentRepository.Deserialize(AuthoredContentRepository.Serialize(new AuthoredCountyDocument{Buildings=[shell]})).Buildings[0].Containers[0];c.Id=StableId("container");c.X+=.5f;c.Y+=.5f;c.InteractionX+=.5f;c.InteractionY+=.5f;_interiorBuilding.Containers.Add(c);replacements.Add(new(StudioSelectionKind.Container,c.Id));break;
            case AuthoredBedData item:shell.Beds=[item];AuthoredBedData b=AuthoredContentRepository.Deserialize(AuthoredContentRepository.Serialize(new AuthoredCountyDocument{Buildings=[shell]})).Buildings[0].Beds[0];b.Id=StableId("bed");b.X+=.5f;b.Y+=.5f;b.InteractionX+=.5f;b.InteractionY+=.5f;_interiorBuilding.Beds.Add(b);replacements.Add(new(StudioSelectionKind.Bed,b.Id));break;
        }
    }

    private void Checkpoint(){_undo.Push(AuthoredContentRepository.Serialize(_document));_redo.Clear();}
    private void Restore(string json){string? buildingId=_interiorBuilding?.Id;_document=AuthoredContentRepository.Deserialize(json);_interiorBuilding=buildingId is null?null:_document.Buildings.FirstOrDefault(building=>building.Id==buildingId);_selection.Clear();RebuildVisuals();SetDirty(AuthoredContentRepository.Serialize(_document)!=_savedSnapshot);SelectionChanged?.Invoke();DocumentChanged?.Invoke();}
    private void Changed(string status,bool checkpointAlready=true){RebuildVisuals();SetDirty(AuthoredContentRepository.Serialize(_document)!=_savedSnapshot);SelectionChanged?.Invoke();DocumentChanged?.Invoke();StatusChanged?.Invoke(status);}
    private void SetDirty(bool value){if(_dirty==value)return;_dirty=value;DirtyChanged?.Invoke(value);}

    private void RebuildVisuals()
    {
        foreach(Node node in _visuals)if(GodotObject.IsInstanceValid(node))node.QueueFree();_visuals.Clear();
        if(IsInteriorMode&&_interiorBuilding is not null)BuildInteriorVisuals(_interiorBuilding);else BuildWorldVisuals();QueueRedraw();
    }

    private void BuildWorldVisuals()
    {
        foreach(AuthoredWorldObjectData item in _document.WorldObjects.Where(InLoadedArea).Where(item=>LayerEditable(item,true)))AddSprite(item.AssetPath,new(item.X,item.Y),0,item.Scale,new(item.AnchorX,item.AnchorY),new(StudioSelectionKind.WorldObject,item.Id),Colors.White,item.ScaleY,0,item.RotationDegrees);
        if(!_hiddenLayers.Contains("Buildings"))foreach(AuthoredBuildingData building in _document.Buildings.Where(InLoadedArea))AddSprite(building.ExteriorAssetPath,new(building.ExteriorX,building.ExteriorY),building.ExteriorTargetHeight,0,new(.5f,1),new(StudioSelectionKind.Building,building.Id),Colors.White,0,building.ExteriorTargetWidth,building.ExteriorRotationDegrees);
    }

    private void BuildInteriorVisuals(AuthoredBuildingData building)
    {
        AddSprite(building.ExteriorAssetPath,new(building.ExteriorX,building.ExteriorY),building.ExteriorTargetHeight,0,new(.5f,1),new(StudioSelectionKind.Building,building.Id),new Color(1,1,1,.18f),0,building.ExteriorTargetWidth,building.ExteriorRotationDegrees);
        InteriorBuildingDefinition definition=AuthoredInteriorConverter.Convert(building);
        foreach(RoomDefinition room in definition.Rooms){InteriorFloorVisual floor=new();floor.Initialize(room);AddVisual(floor);}
        foreach(WallDefinition wall in definition.Walls){InteriorWallVisual visual=new();visual.Initialize(wall);AddVisual(visual);}
        foreach(AuthoredFurnitureData item in building.Furniture)AddSprite(item.AssetPath,new(item.X,item.Y),item.TargetHeight,0,new(.5f,1),new(StudioSelectionKind.Furniture,item.Id));
        foreach(AuthoredContainerData item in building.Containers)AddSprite(item.AssetPath,new(item.X,item.Y),item.TargetHeight,0,new(.5f,1),new(StudioSelectionKind.Container,item.Id));
        foreach(AuthoredBedData item in building.Beds)AddSprite(item.AssetPath,new(item.X,item.Y),item.TargetHeight,0,new(.5f,1),new(StudioSelectionKind.Bed,item.Id));
        foreach(AuthoredDoorData item in building.Doors)AddSprite(item.ClosedTexturePath,new(item.X,item.Y),84,0,new(.5f,1),new(StudioSelectionKind.Door,item.Id));
    }

    private void AddSprite(string path,Vector2 grid,float targetHeight,float scale,Vector2 anchor,StudioSelection selection,Color? tint=null,float scaleY=0,float targetWidth=0,float rotationDegrees=0)
    {
        if(string.IsNullOrWhiteSpace(path)||!ResourceLoader.Exists(path))return;StudioSpriteVisual visual=new();visual.Initialize(path,grid,targetHeight,targetWidth,scale,scaleY,anchor,tint??Colors.White,_selection.Contains(selection),selection.Kind is StudioSelectionKind.WorldObject or StudioSelectionKind.Building,rotationDegrees);AddVisual(visual);
    }
    private void AddVisual(Node node){AddChild(node);_visuals.Add(node);}

    public override void _Draw()
    {
        Rect2 loaded=LoadedGridBounds();DrawGridRect(loaded,new Color("d1ad6060"),3);
        if(_interiorBuilding is not null)
        {
            Rect2 footprint=new(_interiorBuilding.FootprintX,_interiorBuilding.FootprintY,_interiorBuilding.FootprintWidth,_interiorBuilding.FootprintHeight);DrawGridRect(footprint,new Color("e3c875d0"),3);
            DrawScaleReference(footprint.Position+new Vector2(.7f,footprint.Size.Y-.7f));
        }
        if(_boxStart is Vector2 box){Vector2 mouse=Snap(_world.ScreenToGridPosition(GetViewport().GetMousePosition()));DrawGridRect(new Rect2(Min(box,mouse),(mouse-box).Abs()),new Color("88b7e5c0"),2);}
        if(_toolStart is Vector2 start){Vector2 mouse=Snap(_world.ScreenToGridPosition(GetViewport().GetMousePosition()));if(Tool==AuthoringTool.Wall)DrawLine(IsometricGrid.GridToScreen(start),IsometricGrid.GridToScreen(mouse),new Color("e5c46cff"),5,true);else DrawGridRect(new Rect2(Min(start,mouse),(mouse-start).Abs()),new Color("7fcf83b0"),2);}
        foreach(AuthoringValidationIssue issue in _issues){Color color=issue.Severity==AuthoringValidationSeverity.Invalid?new Color("e34b45"):issue.Severity==AuthoringValidationSeverity.Warning?new Color("e5b84f"):new Color("65c878");Vector2 point=IsometricGrid.GridToScreen(issue.Position);DrawCircle(point,12,color);DrawCircle(point,5,new Color("151a16"));}
        if(!IsInteriorMode&&!_hiddenLayers.Contains("Gameplay"))foreach(AuthoredWorldObjectData item in _document.WorldObjects.Where(InLoadedArea).Where(item=>item.GameplayType!="Decoration")){Vector2 point=IsometricGrid.GridToScreen(new(item.X,item.Y));Color color=item.GameplayType.Contains("Zombie")?new Color("d85b51"):item.GameplayType.Contains("Landmark")?new Color("e0bc62"):new Color("70b8dc");DrawPolyline([point+new Vector2(0,-17),point+new Vector2(17,0),point+new Vector2(0,17),point+new Vector2(-17,0),point+new Vector2(0,-17)],color,3,true);DrawString(ThemeDB.FallbackFont,point+new Vector2(22,5),item.GameplayType,HorizontalAlignment.Left,160,12,color);}
        if(_testRoute.Count>1)DrawPolyline(_testRoute.Select(IsometricGrid.GridToScreen).ToArray(),new Color("66dd82"),5,true);
        if(GetSelectedData() is AuthoredPathData selectedPath){Vector2[] pathPoints=selectedPath.Points.Select(point=>IsometricGrid.GridToScreen(point.Vector)).ToArray();if(pathPoints.Length>1)DrawPolyline(pathPoints,new Color("f0c96d"),5,true);foreach(Vector2 point in pathPoints){DrawCircle(point,9,new Color("f0c96d"));DrawCircle(point,4,new Color("263027"));}}
        if(Tool==AuthoringTool.Road&&_pathPoints.Count>0){List<Vector2> preview=_pathPoints.Select(IsometricGrid.GridToScreen).ToList();preview.Add(IsometricGrid.GridToScreen(Snap(_world.ScreenToGridPosition(GetViewport().GetMousePosition()))));DrawPolyline(preview.ToArray(),new Color("e1bd69d8"),Mathf.Max(4,PathWidth*IsometricGrid.TileHeight),true);foreach(Vector2 point in preview.Take(preview.Count-1))DrawCircle(point,7,new Color("f0c96d"));}
        if(Tool==AuthoringTool.Place&&_placementAsset is not null){Vector2 grid=Snap(_world.ScreenToGridPosition(GetViewport().GetMousePosition()));Vector2 point=IsometricGrid.GridToScreen(grid);DrawCircle(point,10,new Color("e5c46caa"));}
        if(Tool is AuthoringTool.Terrain or AuthoringTool.Scatter or AuthoringTool.Erase)DrawBrushPreview(_world.ScreenToGridPosition(GetViewport().GetMousePosition()));
    }

    private void DrawBrushPreview(Vector2 center)
    {
        Vector2[] points=new Vector2[33];for(int i=0;i<points.Length;i++){float angle=Mathf.Tau*i/(points.Length-1);points[i]=IsometricGrid.GridToScreen(center+Vector2.FromAngle(angle)*BrushRadius);}Color color=Tool==AuthoringTool.Erase?new Color("e85b55d8"):Tool==AuthoringTool.Terrain?new Color("d7b65ed8"):new Color("76c986d8");DrawPolyline(points,color,3,true);DrawCircle(IsometricGrid.GridToScreen(center),5,color);
    }

    private void DrawGridRect(Rect2 rect,Color color,float width){Vector2[] points=IsometricGrid.ProjectRectangle(rect.Position,rect.Size);DrawPolyline([points[0],points[1],points[2],points[3],points[0]],color,width,true);}
    private void DrawScaleReference(Vector2 grid){Vector2 feet=IsometricGrid.GridToScreen(grid);DrawLine(feet,feet+new Vector2(0,-78),new Color("8bd098d0"),12,true);DrawCircle(feet+new Vector2(0,-92),14,new Color("8bd098d0"));DrawString(ThemeDB.FallbackFont,feet+new Vector2(22,-58),"SURVIVOR SCALE",HorizontalAlignment.Left,150,12,new Color("d9e6d6"));}

    private bool InLoadedArea(AuthoredWorldObjectData item)=>LoadedGridBounds().HasPoint(new(item.X,item.Y));
    private bool InLoadedArea(AuthoredBuildingData item)=>LoadedGridBounds().HasPoint(new(item.ExteriorX,item.ExteriorY));
    private bool LayerEditable(AuthoredWorldObjectData item,bool visibilityOnly=false){string layer=LayerFor(item.Category,item.GameplayType);return !_hiddenLayers.Contains(layer)&&(visibilityOnly||!_lockedLayers.Contains(layer));}
    private static string LayerFor(string category,string gameplay)=>gameplay!="Decoration"?"Gameplay":category=="Environment"?"Vegetation":category=="Buildings"?"Buildings":"Props";
    private Rect2 LoadedGridBounds(){Vector2I center=CountyCoordinateSpace.GridToChunk(_loadedCenter);Vector2 start=new((center.X-_loadedRadius)*CountyCoordinateSpace.ChunkSize,(center.Y-_loadedRadius)*CountyCoordinateSpace.ChunkSize);float size=(_loadedRadius*2+1)*CountyCoordinateSpace.ChunkSize;return new Rect2(start,new Vector2(size,size)).Intersection(CountyCoordinateSpace.GridBounds);}
    private AuthoredRoomData? RoomAt(Vector2 grid)=>_interiorBuilding?.Rooms.FirstOrDefault(room=>new Rect2(room.X,room.Y,room.Width,room.Height).HasPoint(grid));
    private Vector2 Snap(Vector2 point)=>_snap?new Vector2(Mathf.Round(point.X*4)/4,Mathf.Round(point.Y*4)/4):point;
    private static string StableId(string prefix)=>prefix+"_"+Guid.NewGuid().ToString("N")[..10];
    private static string Title(string value)=>string.Join(' ',value.Split(' ').Select(word=>word.Length==0?word:char.ToUpperInvariant(word[0])+word[1..]));
    private static float AssetHeight(string path)=>path.Contains("bed_")?92:path.Contains("shelf")||path.Contains("fridge")||path.Contains("refrigerator")?90:74;
    public static void TranslateBuilding(AuthoredBuildingData building,Vector2 delta)
    {
        building.ExteriorX+=delta.X;building.ExteriorY+=delta.Y;building.FootprintX+=delta.X;building.FootprintY+=delta.Y;
        foreach(AuthoredRoomData item in building.Rooms){item.X+=delta.X;item.Y+=delta.Y;}
        foreach(AuthoredWallData item in building.Walls){item.StartX+=delta.X;item.StartY+=delta.Y;item.EndX+=delta.X;item.EndY+=delta.Y;}
        foreach(AuthoredDoorData item in building.Doors){item.X+=delta.X;item.Y+=delta.Y;item.OutsideApproachX+=delta.X;item.OutsideApproachY+=delta.Y;item.InsideArrivalX+=delta.X;item.InsideArrivalY+=delta.Y;}
        foreach(AuthoredFurnitureData item in building.Furniture){item.X+=delta.X;item.Y+=delta.Y;}
        foreach(AuthoredContainerData item in building.Containers){item.X+=delta.X;item.Y+=delta.Y;item.InteractionX+=delta.X;item.InteractionY+=delta.Y;}
        foreach(AuthoredBedData item in building.Beds){item.X+=delta.X;item.Y+=delta.Y;item.InteractionX+=delta.X;item.InteractionY+=delta.Y;}
    }
    private static Vector2 Min(Vector2 a,Vector2 b)=>new(Mathf.Min(a.X,b.X),Mathf.Min(a.Y,b.Y));
    private static float DistanceToSegment(Vector2 point,Vector2 start,Vector2 end){Vector2 line=end-start;float length=line.LengthSquared();if(length<.0001f)return point.DistanceTo(start);float t=Mathf.Clamp((point-start).Dot(line)/length,0,1);return point.DistanceTo(start+line*t);}
}

public partial class StudioSpriteVisual:Node2D
{
    private Texture2D _texture=null!;private float _height;private float _targetWidth;private float _scale;private float _scaleY;private Vector2 _anchor;private Color _tint;private bool _selected;private bool _resizable;
    public void Initialize(string path,Vector2 grid,float targetHeight,float targetWidth,float scale,float scaleY,Vector2 anchor,Color tint,bool selected,bool resizable,float rotationDegrees){_texture=TextureRegistry.Get(path);_height=targetHeight;_targetWidth=targetWidth;_scale=scale;_scaleY=scaleY;_anchor=anchor;_tint=tint;_selected=selected;_resizable=resizable;Position=IsometricGrid.GridToScreen(grid);RotationDegrees=rotationDegrees;ZIndex=0;}
    public override void _Ready()=>QueueRedraw();
    public override void _Draw(){Vector2 scale=_height>0?new Vector2(_targetWidth>0?_targetWidth/Mathf.Max(1,_texture.GetWidth()):_height/Mathf.Max(1,_texture.GetHeight()),_height/Mathf.Max(1,_texture.GetHeight())):new Vector2(Mathf.Max(.02f,_scale),Mathf.Max(.02f,_scaleY>0?_scaleY:_scale));Vector2 size=_texture.GetSize()*scale;Rect2 rect=new(-size.X*_anchor.X,-size.Y*_anchor.Y,size.X,size.Y);DrawTextureRect(_texture,rect,false,_tint);if(_selected){Color color=new("f0c96d");DrawRect(rect,color,false,3);if(_resizable){Vector2 center=rect.GetCenter();foreach(Vector2 point in new[]{rect.Position,new Vector2(center.X,rect.Position.Y),new Vector2(rect.End.X,rect.Position.Y),new Vector2(rect.Position.X,center.Y),new Vector2(rect.End.X,center.Y),new Vector2(rect.Position.X,rect.End.Y),new Vector2(center.X,rect.End.Y),rect.End})DrawRect(new Rect2(point-new Vector2(5,5),new Vector2(10,10)),color,true);}}}
}
