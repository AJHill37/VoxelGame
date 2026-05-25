using Godot;

public partial class PlayerBody : Node3D
{
    private Player _player;

    private Node3D _torso;
    private Node3D _head;
    private Node3D _handL, _handR;
    private Node3D _legPiece;
    private Node3D _footL, _footR;

    private float _walkPhase;
    private float _bodyYaw;

    // Dwarf proportions: squat, barrel-chested, almost no legs.
    // Torso width (0.76) nearly matches head width (0.64) — both dominate the silhouette.
    // Total visual height ~1.35 units inside the 1.8-unit collision capsule.
    private static readonly Vector3 HeadBase     = new( 0.00f, 1.32f,  0.00f);
    private static readonly Vector3 TorsoBase    = new( 0.00f, 0.78f,  0.00f);
    private static readonly Vector3 HandLBase    = new(-0.64f, 0.65f,  0.00f);
    private static readonly Vector3 HandRBase    = new( 0.64f, 0.65f,  0.00f);
    private static readonly Vector3 LegPieceBase = new( 0.00f, 0.40f,  0.00f);
    private static readonly Vector3 FootLBase    = new(-0.22f, 0.10f,  0.00f);
    private static readonly Vector3 FootRBase    = new( 0.22f, 0.10f,  0.00f);

    public override void _Ready()
    {
        _player = GetParent<Player>();
        Build();
    }

    private void Build()
    {
        // Head — large and wide, nearly as wide as the torso
        _head = Pivot(HeadBase);
        AddChild(_head);
        _head.AddChild(Box(new Vector3(0.64f, 0.58f, 0.58f), new Color(0.92f, 0.75f, 0.60f)));

        // Torso — barrel chest, the dominant feature; very wide and deep
        _torso = Pivot(TorsoBase);
        AddChild(_torso);
        _torso.AddChild(Box(new Vector3(0.76f, 0.48f, 0.44f), new Color(0.35f, 0.45f, 0.75f)));

        // Hands — large floating fists, pulled wide and low (dwarf arms are short but hands are big)
        _handL = Pivot(HandLBase);
        AddChild(_handL);
        _handL.AddChild(Box(new Vector3(0.28f, 0.28f, 0.28f), new Color(0.92f, 0.75f, 0.60f)));

        _handR = Pivot(HandRBase);
        AddChild(_handR);
        _handR.AddChild(Box(new Vector3(0.28f, 0.28f, 0.28f), new Color(0.92f, 0.75f, 0.60f)));

        // Leg piece — almost vestigial; very wide and flat, barely there
        _legPiece = Pivot(LegPieceBase);
        AddChild(_legPiece);
        _legPiece.AddChild(Box(new Vector3(0.66f, 0.20f, 0.38f), new Color(0.22f, 0.22f, 0.42f)));

        // Feet — wide, flat boots; spread to match the leg piece width
        _footL = Pivot(FootLBase);
        AddChild(_footL);
        _footL.AddChild(Box(new Vector3(0.32f, 0.15f, 0.44f), new Color(0.18f, 0.14f, 0.11f)));

        _footR = Pivot(FootRBase);
        AddChild(_footR);
        _footR.AddChild(Box(new Vector3(0.32f, 0.15f, 0.44f), new Color(0.18f, 0.14f, 0.11f)));
    }

    public override void _Process(double delta)
    {
        var vel = _player.Velocity;
        var hSpeed = new Vector2(vel.X, vel.Z).Length();

        if (hSpeed > 0.5f)
        {
            var targetYaw = Mathf.Atan2(-vel.X, -vel.Z);
            _bodyYaw = Mathf.LerpAngle(_bodyYaw, targetYaw, (float)delta * 10f);
        }
        Rotation = new Vector3(0f, _bodyYaw, 0f);

        _walkPhase += hSpeed * (float)delta * 2.5f;

        var swingScale = Mathf.Clamp(hSpeed / _player.Speed, 0f, 1f);
        var swing = Mathf.Sin(_walkPhase) * swingScale;

        // Counter-twist: heavy torso rotates opposite the leg piece
        _torso.Rotation    = new Vector3(0f, -swing * 0.20f, 0f);
        _legPiece.Rotation = new Vector3(0f,  swing * 0.35f, 0f);

        // Bob is subtle — dwarves are heavy
        var bob = Mathf.Sin(_walkPhase * 2f) * 0.018f * swingScale;
        _torso.Position = TorsoBase with { Y = TorsoBase.Y + bob };
        _head.Position  = HeadBase  with { Y = HeadBase.Y  + bob };

        // Feet waddle — short stride, exaggerated tilt
        _footL.Position = FootLBase with { Z =  swing * 0.22f };
        _footR.Position = FootRBase with { Z = -swing * 0.22f };
        _footL.Rotation = new Vector3( swing * 0.50f, 0f, 0f);
        _footR.Rotation = new Vector3(-swing * 0.50f, 0f, 0f);

        // Hands swing low and wide
        _handL.Position = HandLBase with { Z = -swing * 0.20f };
        _handR.Position = HandRBase with { Z =  swing * 0.20f };
        _handL.Rotation = new Vector3(-swing * 0.40f, 0f, 0f);
        _handR.Rotation = new Vector3( swing * 0.40f, 0f, 0f);
    }

    private static Node3D Pivot(Vector3 position) => new Node3D { Position = position };

    private static MeshInstance3D Box(Vector3 size, Color color) =>
        new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.9f },
        };
}
