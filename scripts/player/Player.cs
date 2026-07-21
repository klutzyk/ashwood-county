using Godot;

public partial class Player : CharacterBody2D
{
	[Export]
	public float MoveSpeed { get; set; } = 200.0f;

	public override void _PhysicsProcess(double delta)
	{
		Vector2 inputDirection = Input.GetVector(
			"move_left",
			"move_right",
			"move_up",
			"move_down");

		Velocity = inputDirection * MoveSpeed;
		MoveAndSlide();
	}
}
