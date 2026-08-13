using System;
using System.Collections.Generic;
using AshwoodCounty.Resources;
using AshwoodCounty.Systems;
using AshwoodCounty.Units;
using AshwoodCounty.Buildings;
using Godot;

namespace AshwoodCounty.UI;

public partial class GameHud : CanvasLayer
{
    public const string GroupName="game_hud";
    private SettlementInventory _inventory = null!;
    private GameClock _clock = null!;
    private SimulationController _simulation = null!;
    private SurvivorSelectionController _selection = null!;
    private BuildingPlacementController _placement = null!;
    private ChopDesignationController _designation = null!;
    private Label _resources = null!, _time = null!, _context = null!;
    private PanelContainer _contextPanel = null!;
    private readonly Dictionary<WorkCategory,Button> _workButtons=[];
    private Label _toast=null!; private double _toastRemaining;
    private double _refresh;

    public override void _Ready()
    {
        ProcessMode=ProcessModeEnum.Always;AddToGroup(GroupName);
        _inventory=GetNode<SettlementInventory>("../SettlementInventory"); _clock=GetNode<GameClock>("../GameClock");
        _simulation=GetNode<SimulationController>("../SimulationController"); _selection=GetNode<SurvivorSelectionController>("../SelectionController");
        _placement=GetNode<BuildingPlacementController>("../BuildingPlacementController"); _designation=GetNode<ChopDesignationController>("../ChopDesignationController");
        Theme theme=AshwoodTheme.Create();
        MarginContainer safe=new(){AnchorsPreset=(int)Control.LayoutPreset.FullRect, MouseFilter=Control.MouseFilterEnum.Ignore, Theme=theme}; AddChild(safe);
        safe.AddThemeConstantOverride("margin_left",16); safe.AddThemeConstantOverride("margin_top",14); safe.AddThemeConstantOverride("margin_right",16); safe.AddThemeConstantOverride("margin_bottom",14);
        VBoxContainer layout=new(){MouseFilter=Control.MouseFilterEnum.Ignore}; safe.AddChild(layout);
        PanelContainer top=new(){MouseFilter=Control.MouseFilterEnum.Ignore}; layout.AddChild(top);
        HBoxContainer bar=new(){MouseFilter=Control.MouseFilterEnum.Ignore}; top.AddChild(bar);
        Label title=new(){Text="ASHWOOD COUNTY",TooltipText="Your settlement and county reclamation overview.",MouseFilter=Control.MouseFilterEnum.Ignore}; title.AddThemeFontSizeOverride("font_size",18); bar.AddChild(title);
        bar.AddChild(new VSeparator(){MouseFilter=Control.MouseFilterEnum.Ignore}); _resources=new Label(){MouseFilter=Control.MouseFilterEnum.Ignore}; _resources.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill; bar.AddChild(_resources);
        _time=new Label(){MouseFilter=Control.MouseFilterEnum.Ignore}; bar.AddChild(_time); bar.AddChild(new VSeparator(){MouseFilter=Control.MouseFilterEnum.Ignore});
        AddSpeedButton(bar,"❚❚",0,"Pause or resume [Space]"); AddSpeedButton(bar,"1×",1,"Normal speed [1]"); AddSpeedButton(bar,"2×",2,"Fast speed [2]"); AddSpeedButton(bar,"3×",3,"Very fast speed [3]");
        Control spacer=new(){SizeFlagsVertical=Control.SizeFlags.ExpandFill,MouseFilter=Control.MouseFilterEnum.Ignore}; layout.AddChild(spacer);
        HBoxContainer bottom=new(){MouseFilter=Control.MouseFilterEnum.Ignore}; layout.AddChild(bottom);
        _contextPanel=new PanelContainer(){CustomMinimumSize=new Vector2(340,0),MouseFilter=Control.MouseFilterEnum.Stop}; bottom.AddChild(_contextPanel);
        VBoxContainer contextRows=new(){MouseFilter=Control.MouseFilterEnum.Ignore};_contextPanel.AddChild(contextRows);_context=new Label(){AutowrapMode=TextServer.AutowrapMode.WordSmart,MouseFilter=Control.MouseFilterEnum.Ignore}; contextRows.AddChild(_context);HBoxContainer workBar=new(){MouseFilter=Control.MouseFilterEnum.Ignore};contextRows.AddChild(workBar);foreach(WorkCategory category in Enum.GetValues<WorkCategory>()){Button work=new(){TooltipText=$"Cycle {category} between Allowed, Preferred and Disabled.",MouseFilter=Control.MouseFilterEnum.Stop};WorkCategory captured=category;work.Pressed+=()=>CycleWork(captured);workBar.AddChild(work);_workButtons[category]=work;}
        Control bottomSpacer=new(){SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,MouseFilter=Control.MouseFilterEnum.Ignore}; bottom.AddChild(bottomSpacer);
        PanelContainer actions=new(){MouseFilter=Control.MouseFilterEnum.Ignore}; bottom.AddChild(actions); HBoxContainer tools=new(){MouseFilter=Control.MouseFilterEnum.Ignore}; actions.AddChild(tools);
        AddAction(tools,"SHELTER",()=>_placement.BeginPlacement(BuildingCatalog.Shelter),"Build sleeping shelter • 30 Wood");
        AddAction(tools,"STORAGE",()=>_placement.BeginPlacement(BuildingCatalog.ProvisionsShed),"Build provisions storage • 20 Wood");
        AddAction(tools,"OUTPOST",()=>_placement.BeginPlacement(BuildingCatalog.Outpost),"Establish county control • 12 Materials");
        AddAction(tools,"CHOP",_designation.ToggleDesignation,"Designate timber harvesting"); AddAction(tools,"FORAGE",_designation.ToggleForageDesignation,"Designate food gathering"); AddAction(tools,"SCAVENGE",_designation.ToggleScavengeDesignation,"Search abandoned locations for salvage");
        AddAction(tools,"TRAVEL",()=>GetNode<TravelPanel>("../TravelPanel").TogglePanel(),"Travel to an adjacent playable region [T]");
        _toast=new Label(){Visible=false,HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore};_toast.AddThemeColorOverride("font_color",new Color("f2d78f"));layout.AddChild(_toast);layout.MoveChild(_toast,1);
    }
    private static void AddAction(Container parent,string text,Action action,string tooltip){Button b=new(){Text=text,TooltipText=tooltip,MouseFilter=Control.MouseFilterEnum.Stop};b.Pressed+=action;parent.AddChild(b);}
    private void CycleWork(WorkCategory category){if(_selection.SelectedCount!=1)return;SurvivorProfile p=_selection.SelectedSurvivors[0].Profile;WorkPriority next=p.Priority(category) switch{WorkPriority.Allowed=>WorkPriority.Preferred,WorkPriority.Preferred=>WorkPriority.Disabled,_=>WorkPriority.Allowed};p.WorkPriorities[category]=next;RefreshWork(p);}
    private void RefreshWork(SurvivorProfile profile){foreach((WorkCategory category,Button button) in _workButtons){WorkPriority priority=profile.Priority(category);string shortName=category.ToString()[..Mathf.Min(4,category.ToString().Length)].ToUpperInvariant();button.Text=$"{shortName} {(priority==WorkPriority.Preferred?"★":priority==WorkPriority.Disabled?"×":"•")}";button.Disabled=false;}}
    private void AddSpeedButton(Container bar,string text,int speed,string tooltip){Button b=new(){Text=text,TooltipText=tooltip,CustomMinimumSize=new Vector2(44,34)}; b.Pressed+=()=>{if(speed==0)_simulation.TogglePause();else _simulation.SetSpeed(speed);};bar.AddChild(b);}
    public void Notify(string message){_toast.Text=message;_toast.Visible=true;_toastRemaining=6;}
    public override void _Process(double delta){if(_toastRemaining>0){_toastRemaining-=delta;if(_toastRemaining<=0)_toast.Visible=false;}_refresh-=delta;if(_refresh>0)return;_refresh=.15;string Amount(ResourceType r)=>_inventory.DevUnlimitedResources?"∞":_inventory.GetAmount(r).ToString();
        _resources.Text=$"WOOD  {Amount(ResourceType.Wood)}     FOOD  {Amount(ResourceType.Food)}     MATERIALS  {Amount(ResourceType.Materials)}     MEDICINE  {Amount(ResourceType.Medicine)}";
        _time.Text=$"{_clock.DisplayTime}   {(_simulation.IsPaused?"PAUSED":$"{_simulation.Speed}×")}";
        _contextPanel.Visible=_selection.SelectedCount>0;if(_selection.SelectedCount==1){Survivor s=_selection.SelectedSurvivors[0];SurvivorProfile p=s.Profile;_context.Text=$"{p.DisplayName.ToUpperInvariant()}\n{p.Occupation}  •  {p.Trait}\n{p.HomeRegion}  •  {p.ImportantLocation}\n\n{s.Activity}\nHealth  {s.Health:0}/{s.MaxHealth:0}   Hunger  {s.Hunger:0}%\nEnergy  {s.Energy:0}%   Morale  {s.Morale:0}%\n\nLabor {p.Skill(SurvivorSkill.Labor)}  Scavenge {p.Skill(SurvivorSkill.Scavenging)}  Combat {p.Skill(SurvivorSkill.Combat)}  Medical {p.Skill(SurvivorSkill.Medical)}";RefreshWork(p);}else if(_selection.SelectedCount>1){_context.Text=$"{_selection.SelectedCount} SURVIVORS SELECTED\nIssue a move or combat order to the group.";foreach(Button b in _workButtons.Values)b.Disabled=true;}}
}
