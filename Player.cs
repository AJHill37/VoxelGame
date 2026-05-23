using Godot;

public partial class Player : CharacterBody3D
{
    [Export] public float Speed = 5.0f;
    [Export] public float JumpVelocity = 6.5f;
    [Export] public float MouseSensitivity = 0.003f;
    [Export] public float CameraDistance = 4.0f;
    [Export] public float Reach = 8.0f;

    public VoxelTerrain Terrain { get; set; }
    public int PlaceBlockId { get; set; } = 2;

    private Node3D _yawPivot;
    private Node3D _pitchPivot;
    private SpringArm3D _springArm;
    private Camera3D _camera;
    private VoxelTool _voxelTool;
    private MeshInstance3D _faceHighlight;

    private readonly float _gravity = (float)(double)ProjectSettings.GetSetting("physics/3d/default_gravity");
    private float _yaw;
    private float _pitch;

    public override void _Ready()
    {
        // Collision capsule (origin at feet)
        var col = new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.4f, Height = 1.8f },
            Position = new Vector3(0f, 0.9f, 0f),
        };
        AddChild(col);

        // Visible capsule
        var meshInst = new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.4f, Height = 1.8f },
            Position = new Vector3(0f, 0.9f, 0f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.95f, 0.55f, 0.20f),
                Roughness = 0.7f,
            },
        };
        AddChild(meshInst);

        // Camera rig: yaw -> pitch -> spring arm -> camera
        _yawPivot = new Node3D { Position = new Vector3(0f, 1.6f, 0f) };
        AddChild(_yawPivot);

        _pitchPivot = new Node3D();
        _yawPivot.AddChild(_pitchPivot);

        _springArm = new SpringArm3D
        {
            SpringLength = CameraDistance,
            CollisionMask = 1,
            Margin = 0.1f,
        };
        _pitchPivot.AddChild(_springArm);

        _camera = new Camera3D();
        _springArm.AddChild(_camera);

        // Tell the voxel terrain to stream chunks around us
        AddChild(new VoxelViewer());

        // Yellow face-highlight quad (positioned in world space each frame)
        _faceHighlight = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(1.002f, 1.002f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.95f, 0.15f, 0.55f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            TopLevel = true,
            Visible = false,
        };
        AddChild(_faceHighlight);
    }

    public override void _Process(double delta)
    {
        UpdateFaceHighlight();
    }

    private void UpdateFaceHighlight()
    {
        var tool = GetTool();
        if (tool == null) { _faceHighlight.Visible = false; return; }

        var origin = _camera.GlobalPosition;
        var dir = -_camera.GlobalTransform.Basis.Z;
        var hit = tool.Raycast(origin, dir, Reach);
        if (hit == null) { _faceHighlight.Visible = false; return; }

        var voxelPos = (Vector3I)hit.Get("position");
        var prevPos = (Vector3I)hit.Get("previous_position");
        var normal = new Vector3(prevPos.X - voxelPos.X, prevPos.Y - voxelPos.Y, prevPos.Z - voxelPos.Z);
        if (normal.LengthSquared() < 0.5f) { _faceHighlight.Visible = false; return; }

        var voxelCenter = new Vector3(voxelPos.X + 0.5f, voxelPos.Y + 0.5f, voxelPos.Z + 0.5f);
        var faceCenter = voxelCenter + normal * 0.5f;
        // Bias slightly outward so the quad doesn't z-fight with the cube face
        var pos = faceCenter + normal * 0.003f;

        // QuadMesh's front face points along +Z. LookingAt makes -Z point at the target,
        // so target = pos - normal puts +Z along the outward face normal.
        var up = Mathf.Abs(normal.Y) > 0.9f ? Vector3.Forward : Vector3.Up;
        _faceHighlight.GlobalTransform = new Transform3D(Basis.Identity, pos).LookingAt(pos - normal, up);
        _faceHighlight.Visible = true;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mm && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _yaw -= mm.Relative.X * MouseSensitivity;
            _pitch -= mm.Relative.Y * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, -1.4f, 1.4f);
            _yawPivot.Rotation = new Vector3(0f, _yaw, 0f);
            _pitchPivot.Rotation = new Vector3(_pitch, 0f, 0f);
        }
        else if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (Input.MouseMode != Input.MouseModeEnum.Captured)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
                return;
            }
            if (mb.ButtonIndex == MouseButton.Left) BreakBlock();
            else if (mb.ButtonIndex == MouseButton.Right) PlaceBlock();
        }
        else if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Escape)
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }
    }

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

    private VoxelTool GetTool()
    {
        if (_voxelTool == null && Terrain != null)
            _voxelTool = Terrain.GetVoxelTool();
        return _voxelTool;
    }

    private void BreakBlock()
    {
        var tool = GetTool();
        if (tool == null) return;
        var origin = _camera.GlobalPosition;
        var dir = -_camera.GlobalTransform.Basis.Z;
        var hit = tool.Raycast(origin, dir, Reach);
        if (hit == null) return;
        var pos = (Vector3I)hit.Get("position");
        tool.SetVoxel(pos, Game.AirId);
    }

    private void PlaceBlock()
    {
        var tool = GetTool();
        if (tool == null) return;
        var origin = _camera.GlobalPosition;
        var dir = -_camera.GlobalTransform.Basis.Z;
        var hit = tool.Raycast(origin, dir, Reach);
        if (hit == null) return;
        var placePos = (Vector3I)hit.Get("previous_position");

        // Avoid trapping the player inside the placed block
        var playerAabb = new Aabb(GlobalPosition - new Vector3(0.4f, 0f, 0.4f), new Vector3(0.8f, 1.8f, 0.8f));
        var blockAabb = new Aabb(new Vector3(placePos.X, placePos.Y, placePos.Z), Vector3.One);
        if (playerAabb.Intersects(blockAabb)) return;

        tool.SetVoxel(placePos, (ulong)PlaceBlockId);
    }
}
