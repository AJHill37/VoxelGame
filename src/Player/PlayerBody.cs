using Godot;

public partial class PlayerBody : Node3D
{
    private Player _player;

    private Node3D _torso;
    private Node3D _armL;
    private Node3D _armR;
    private Node3D _legL;
    private Node3D _legR;

    private float _walkPhase;
    private float _bodyYaw;

    public override void _Ready()
    {
        _player = GetParent<Player>();
        Build();
    }

    private void Build()
    {
        // Torso — parent for head and arms so the whole upper body bobs together
        _torso = new Node3D { Position = new Vector3(0f, 1.2f, 0f) };
        AddChild(_torso);
        _torso.AddChild(Box(new Vector3(0.50f, 0.60f, 0.30f), new Color(0.35f, 0.45f, 0.75f)));

        // Head
        var head = new Node3D { Position = new Vector3(0f, 0.50f, 0f) };
        _torso.AddChild(head);
        head.AddChild(Box(new Vector3(0.42f, 0.42f, 0.42f), new Color(0.92f, 0.75f, 0.60f)));

        // Arms — pivot at shoulder, mesh hangs below
        _armL = new Node3D { Position = new Vector3(-0.35f, 0.30f, 0f) };
        _torso.AddChild(_armL);
        _armL.AddChild(Box(new Vector3(0.20f, 0.55f, 0.20f), new Color(0.92f, 0.75f, 0.60f),
            offset: new Vector3(0f, -0.275f, 0f)));

        _armR = new Node3D { Position = new Vector3(0.35f, 0.30f, 0f) };
        _torso.AddChild(_armR);
        _armR.AddChild(Box(new Vector3(0.20f, 0.55f, 0.20f), new Color(0.92f, 0.75f, 0.60f),
            offset: new Vector3(0f, -0.275f, 0f)));

        // Legs — pivot at hip, mesh hangs below; not children of torso so they don't bob
        _legL = new Node3D { Position = new Vector3(-0.13f, 0.90f, 0f) };
        AddChild(_legL);
        _legL.AddChild(Box(new Vector3(0.22f, 0.85f, 0.22f), new Color(0.25f, 0.25f, 0.45f),
            offset: new Vector3(0f, -0.425f, 0f)));

        _legR = new Node3D { Position = new Vector3(0.13f, 0.90f, 0f) };
        AddChild(_legR);
        _legR.AddChild(Box(new Vector3(0.22f, 0.85f, 0.22f), new Color(0.25f, 0.25f, 0.45f),
            offset: new Vector3(0f, -0.425f, 0f)));
    }

    public override void _Process(double delta)
    {
        var vel = _player.Velocity;
        var hSpeed = new Vector2(vel.X, vel.Z).Length();

        // Rotate body to face movement direction
        if (hSpeed > 0.5f)
        {
            // Atan2(-x, -z) gives yaw so the node's -Z axis aligns with velocity
            var targetYaw = Mathf.Atan2(-vel.X, -vel.Z);
            _bodyYaw = Mathf.LerpAngle(_bodyYaw, targetYaw, (float)delta * 10f);
        }
        Rotation = new Vector3(0f, _bodyYaw, 0f);

        // Walk phase advances with speed
        _walkPhase += hSpeed * (float)delta * 2.5f;

        // Swing scales 0→1 over the first few units of speed
        var swingScale = Mathf.Clamp(hSpeed / _player.Speed, 0f, 1f);
        var swing = Mathf.Sin(_walkPhase) * 0.5f * swingScale;

        // Legs opposite, arms counter to same-side leg (natural gait)
        _legL.Rotation = new Vector3( swing, 0f, 0f);
        _legR.Rotation = new Vector3(-swing, 0f, 0f);
        _armL.Rotation = new Vector3(-swing, 0f, 0f);
        _armR.Rotation = new Vector3( swing, 0f, 0f);

        // Upper body bobs twice per stride
        _torso.Position = new Vector3(0f, 1.2f + Mathf.Sin(_walkPhase * 2f) * 0.025f * swingScale, 0f);
    }

    private static MeshInstance3D Box(Vector3 size, Color color, Vector3 offset = default) =>
        new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.9f },
            Position = offset,
        };
}
