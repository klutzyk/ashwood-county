using System;
using AshwoodCounty.Resources;
using AshwoodCounty.Systems;
using AshwoodCounty.Units;
using Godot;

namespace AshwoodCounty.UI;

public partial class GameHud : CanvasLayer
{
    private SettlementInventory _inventory = null!;
    private GameClock _clock = null!;
    private SimulationController _simulation = null!;
    private SurvivorSelectionController _selection = null!;
    private Label _resources = null!, _time = null!, _context = null!;
    private PanelContainer _contextPanel = null!;
    private double _refresh;

    public override void _Ready()
    {
        ProcessMode=ProcessModeEnum.Always;
        _inventory=GetNode<SettlementInventory>("../SettlementInventory"); _clock=GetNode<GameClock>("../GameClock");
        _simulation=GetNode<SimulationController>("../SimulationController"); _selection=GetNode<SurvivorSelectionController>("../SelectionController");
        Theme theme=AshwoodTheme.Create();
        MarginContainer safe=new(){AnchorsPreset=(int)Control.LayoutPreset.FullRect, MouseFilter=Control.MouseFilterEnum.Ignore, Theme=theme}; AddChild(safe);
        safe.AddThemeConstantOverride("margin_left",16); safe.AddThemeConstantOverride("margin_top",14); safe.AddThemeConstantOverride("margin_right",16); safe.AddThemeConstantOverride("margin_bottom",14);
        VBoxContainer layout=new(){MouseFilter=Control.MouseFilterEnum.Ignore}; safe.AddChild(layout);
        PanelContainer top=new(){MouseFilter=Control.MouseFilterEnum.Stop}; layout.AddChild(top);
        HBoxContainer bar=new(); top.AddChild(bar);
        Label title=new(){Text="ASHWOOD COUNTY",TooltipText="Your settlement and county reclamation overview."}; title.AddThemeFontSizeOverride("font_size",18); bar.AddChild(title);
        bar.AddChild(new VSeparator()); _resources=new Label(); _resources.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill; bar.AddChild(_resources);
        _time=new Label(); bar.AddChild(_time); bar.AddChild(new VSeparator());
        AddSpeedButton(bar,"❚❚",0,"Pause or resume [Space]"); AddSpeedButton(bar,"1×",1,"Normal speed [1]"); AddSpeedButton(bar,"2×",2,"Fast speed [2]"); AddSpeedButton(bar,"3×",3,"Very fast speed [3]");
        Control spacer=new(){SizeFlagsVertical=Control.SizeFlags.ExpandFill,MouseFilter=Control.MouseFilterEnum.Ignore}; layout.AddChild(spacer);
        HBoxContainer bottom=new(){MouseFilter=Control.MouseFilterEnum.Ignore}; layout.AddChild(bottom);
        _contextPanel=new PanelContainer(){CustomMinimumSize=new Vector2(340,0),MouseFilter=Control.MouseFilterEnum.Stop}; bottom.AddChild(_contextPanel);
        _context=new Label(){AutowrapMode=TextServer.AutowrapMode.WordSmart}; _contextPanel.AddChild(_context);
        Control bottomSpacer=new(){SizeFlagsHorizontal=Control.SizeFlags.ExpandFill}; bottom.AddChild(bottomSpacer);
    }
    private void AddSpeedButton(Container bar,string text,int speed,string tooltip){Button b=new(){Text=text,TooltipText=tooltip,CustomMinimumSize=new Vector2(44,34)}; b.Pressed+=()=>{if(speed==0)_simulation.TogglePause();else _simulation.SetSpeed(speed);};bar.AddChild(b);}
    public override void _Process(double delta){_refresh-=delta;if(_refresh>0)return;_refresh=.15;string Amount(ResourceType r)=>_inventory.DevUnlimitedResources?"∞":_inventory.GetAmount(r).ToString();
        _resources.Text=$"WOOD  {Amount(ResourceType.Wood)}     FOOD  {Amount(ResourceType.Food)}     MATERIALS  {Amount(ResourceType.Materials)}     MEDICINE  {Amount(ResourceType.Medicine)}";
        _time.Text=$"{_clock.DisplayTime}   {(_simulation.IsPaused?"PAUSED":$"{_simulation.Speed}×")}";
        _contextPanel.Visible=_selection.SelectedCount>0;if(_selection.SelectedCount==1){Survivor s=_selection.SelectedSurvivors[0];SurvivorProfile p=s.Profile;_context.Text=$"{p.DisplayName.ToUpperInvariant()}\n{p.Occupation}  •  {p.Trait}\n{p.HomeRegion}  •  {p.ImportantLocation}\n\n{s.Activity}\nHealth  {s.Health:0}/{s.MaxHealth:0}   Hunger  {s.Hunger:0}%\nEnergy  {s.Energy:0}%   Morale  {s.Morale:0}%\n\nLabor {p.Skill(SurvivorSkill.Labor)}  Scavenge {p.Skill(SurvivorSkill.Scavenging)}  Combat {p.Skill(SurvivorSkill.Combat)}  Medical {p.Skill(SurvivorSkill.Medical)}";}else if(_selection.SelectedCount>1)_context.Text=$"{_selection.SelectedCount} SURVIVORS SELECTED\nIssue a move or combat order to the group.";}
}
