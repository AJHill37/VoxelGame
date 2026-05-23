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

    public override void _Ready()
    {
        SetupCollision();
        SetupCameraRig();
        SetupFaceHighlight();
        AddChild(new VoxelViewer());
    }

    private void SetupCollision()
    {
        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.4f, Height = 1.8f },
            Position = new Vector3(0f, 0.9f, 0f),
        });

        AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.4f, Height = 1.8f },
            Position = new Vector3(0f, 0.9f, 0f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.95f, 0.55f, 0.20f),
                Roughness = 0.7f,
            },
        });
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mm && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            ApplyMouseLook(mm.Relative);
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
}
