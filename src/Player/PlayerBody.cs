using Godot;

public partial class PlayerBody : Node3D
{
    [Export] public float VoxelScale = 0.0174f;
    [Export] public float AnimationSpeed = 8.0f;

    private Node3D _head, _neck, _torso, _legs;
    private Node3D _handL, _handR, _footL, _footR;
    private Vector3 _handLRest, _handRRest, _footLRest, _footRRest;
    private float _walkPhase;
    private Player _player;

    private static readonly (string File, int Tx, int Ty, int Tz, int Sx, int Sy, int Sz, string Part)[] PartDefs =
    {
        ("res://src/Assets/raider_head.vox",    0,  -5, 99, 40, 36, 32, "head"),
        ("res://src/Assets/raider_neck.vox",    0,  -2, 79, 16, 16,  8, "neck"),
        ("res://src/Assets/raider_torso.vox",   0,  -3, 61, 56, 32, 32, "torso"),
        ("res://src/Assets/raider_hand_r.vox",  30, -4, 41, 12, 12, 16, "hand_r"),
        ("res://src/Assets/raider_hand_l.vox", -30, -4, 41, 12, 12, 16, "hand_l"),
        ("res://src/Assets/raider_legs.vox",    0,  -3, 31, 40, 32, 28, "legs"),
        ("res://src/Assets/raider_foot_r.vox",  13, -7,  4, 16, 32,  8, "foot_r"),
        ("res://src/Assets/raider_foot_l.vox", -12, -7,  4, 16, 32,  8, "foot_l"),
    };

    public override void _Ready()
    {
        _player = GetParent<Player>();

        foreach (var (file, tx, ty, tz, sx, sy, sz, part) in PartDefs)
        {
            var scene    = ResourceLoader.Load<PackedScene>(file);
            var instance = scene.Instantiate<Node3D>();

            instance.Scale           = new Vector3(VoxelScale, VoxelScale, -VoxelScale);
            instance.RotationDegrees = new Vector3(0f, -90f, 0f);
            instance.Position = new Vector3(
                -(sx / 2f) * VoxelScale,
                -(sz / 2f) * VoxelScale,
                -(sy / 2f) * VoxelScale);

            var pivot = new Node3D
            {
                Name     = part,
                Position = new Vector3(tx * VoxelScale, tz * VoxelScale, ty * VoxelScale),
            };

            AddChild(pivot);
            pivot.AddChild(instance);

            switch (part)
            {
                case "head":   _head  = pivot; break;
                case "neck":   _neck  = pivot; break;
                case "torso":  _torso = pivot; break;
                case "hand_r": _handR = pivot; break;
                case "hand_l": _handL = pivot; break;
                case "legs":   _legs  = pivot; break;
                case "foot_r": _footR = pivot; break;
                case "foot_l": _footL = pivot; break;
            }
        }

        CaptureRestPositions();
    }

    public override void _Process(double delta)
    {
        var vel      = _player.Velocity;
        var hSpeed   = new Vector2(vel.X, vel.Z).Length();
        var swingScale = Mathf.Clamp(hSpeed / _player.Speed, 0f, 1f);

        float targetYaw = hSpeed > 0.5f ? Mathf.Atan2(-vel.X, -vel.Z) : Rotation.Y;
        float yaw       = Mathf.LerpAngle(Rotation.Y, targetYaw, (float)delta * 10f);
        float lean      = Mathf.Lerp(Rotation.X, -swingScale * 0.25f, (float)delta * 8f);
        Rotation = new Vector3(lean, yaw, 0f);

        if (hSpeed > 0.1f)
            _walkPhase += AnimationSpeed * (float)delta;

        float swing = Mathf.Sin(_walkPhase) * swingScale;

        if (_handL != null)
            _handL.Position = _handLRest with { Z = _handLRest.Z - swing * 0.28f };
        if (_handR != null)
            _handR.Position = _handRRest with { Z = _handRRest.Z + swing * 0.28f };

        if (_footL != null)
            _footL.Position = _footLRest with { Z = _footLRest.Z + swing * 0.28f };
        if (_footR != null)
            _footR.Position = _footRRest with { Z = _footRRest.Z - swing * 0.28f };
    }

    private void CaptureRestPositions()
    {
        if (_handL != null) _handLRest = _handL.Position;
        if (_handR != null) _handRRest = _handR.Position;
        if (_footL != null) _footLRest = _footL.Position;
        if (_footR != null) _footRRest = _footR.Position;
    }
}
