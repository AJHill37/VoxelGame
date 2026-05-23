using Godot;

public partial class Player
{
    private readonly float _gravity = (float)(double)ProjectSettings.GetSetting("physics/3d/default_gravity");

    public override void _PhysicsProcess(double delta)
    {
        var velocity = Velocity;

        if (!IsOnFloor())
            velocity.Y -= _gravity * (float)delta;

        if (Input.IsKeyPressed(Key.Space) && IsOnFloor())
            velocity.Y = JumpVelocity;

        var input = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W)) input.Y += 1f;
        if (Input.IsKeyPressed(Key.S)) input.Y -= 1f;
        if (Input.IsKeyPressed(Key.D)) input.X += 1f;
        if (Input.IsKeyPressed(Key.A)) input.X -= 1f;
        if (input != Vector2.Zero) input = input.Normalized();

        var basis = _yawPivot.GlobalTransform.Basis;
        var forward = -basis.Z; forward.Y = 0f;
        var right = basis.X; right.Y = 0f;
        if (forward.LengthSquared() > 0f) forward = forward.Normalized();
        if (right.LengthSquared() > 0f) right = right.Normalized();

        var dir = right * input.X + forward * input.Y;
        velocity.X = dir.X * Speed;
        velocity.Z = dir.Z * Speed;

        Velocity = velocity;
        MoveAndSlide();
    }
}
