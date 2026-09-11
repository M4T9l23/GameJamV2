using Godot;
using System;

public partial class Player : CharacterBody2D
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}
	
	public float Speed = 400f;
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		
	}
	public override void _PhysicsProcess(double delta)
	{
			Vector2 velocity = Velocity;
     
			float dir = Input.GetAxis("ui_left", "ui_right");
			velocity.X = dir * Speed;
         
			float dir_y = Input.GetAxis("ui_up", "ui_down");
			velocity.Y = dir_y * Speed;
         
			Velocity = velocity;
			MoveAndSlide();
     
			// Debug log to confirm movement
			if (velocity != Vector2.Zero)
			{
				GD.Print("Player Position: ", GlobalPosition);
			}
		}
}
