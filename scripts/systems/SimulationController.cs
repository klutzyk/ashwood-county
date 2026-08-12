using Godot;
namespace AshwoodCounty.Systems;
public partial class SimulationController : Node
{
    public int Speed{get;private set;}=1; public bool IsPaused=>GetTree().Paused;
    public override void _Ready()=>ProcessMode=ProcessModeEnum.Always;
    public override void _UnhandledInput(InputEvent e){if(e is not InputEventKey k||!k.Pressed||k.Echo)return;if(k.Keycode==Key.Space){GetTree().Paused=!GetTree().Paused;GetViewport().SetInputAsHandled();}else if(k.Keycode is Key.Key1 or Key.Key2 or Key.Key3){Speed=k.Keycode==Key.Key1?1:k.Keycode==Key.Key2?2:3;Engine.TimeScale=Speed;GetTree().Paused=false;GetViewport().SetInputAsHandled();}}
}
