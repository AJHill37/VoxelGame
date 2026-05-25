using Godot;

// Assembles a raider character from individual per-part .vox files.
// Each part is imported by Voxel Tools' VoxelVoxSceneImporter (1 unit = 1 voxel);
// we scale the instance to VoxelScale m/voxel and shift it so the mesh centre
// aligns with the pivot (which sits at the original MV scene-graph translation).
// See CLAUDE.md for the full part table and coordinate conventions.
public partial class RaiderBody : Node3D
{
    [Export] public float VoxelScale = 0.05f;

    public Vector3 Velocity { get; set; }

    private Node3D _head, _neck, _torso, _legs;
    private Node3D _handL, _handR, _footL, _footR;
    private Vector3 _handLRest, _handRRest, _footLRest, _footRRest;
    private float _walkPhase;
    private Player _playerSource;

    // MV axes: X=right, Y=depth, Z=up.
    // Pivot position: MV(tx,ty,tz) → Godot(tx, tz, ty) * VoxelScale.
    // Mesh centering: importer origin is at model corner, not centre.
    //   Godot X offset = -(sx/2 + pad), Y offset = -(sz/2 + pad), Z offset = -(sy/2 + pad)
    // pad = 1 (VoxelMesherCubes minimum padding, used internally by the importer).
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
        foreach (var (file, tx, ty, tz, sx, sy, sz, part) in PartDefs)
        {
            var scene    = ResourceLoader.Load<PackedScene>(file);
            var instance = scene.Instantiate<Node3D>();

            // Importer maps MV(x,y,z) → Godot(y,z,x). Undo that swap with
            // RotationY(-90°) ∘ Scale(s,s,-s) so mesh aligns with pivot axes.
            instance.Scale          = new Vector3(VoxelScale, VoxelScale, -VoxelScale);
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
        _playerSource ??= GetParent() as Player;
        if (_playerSource != null)
            Velocity = _playerSource.Velocity;

        var hSpeed = new Vector2(Velocity.X, Velocity.Z).Length();

        if (hSpeed > 0.5f)
        {
            float targetYaw = Mathf.Atan2(-Velocity.X, -Velocity.Z);
            Rotation = new Vector3(0f, Mathf.LerpAngle(Rotation.Y, targetYaw, (float)delta * 10f), 0f);
        }

        _walkPhase += hSpeed * (float)delta * 2.5f;
        float swingScale = Mathf.Clamp(hSpeed / 5f, 0f, 1f);
        float swing      = Mathf.Sin(_walkPhase) * swingScale;

        if (_torso != null)
            _torso.Rotation = new Vector3(0f, -swing * 0.20f, 0f);

        if (_handL != null)
        {
            _handL.Rotation = new Vector3(-swing * 0.40f, 0f, 0f);
            _handL.Position = _handLRest with { Z = _handLRest.Z - swing * 0.20f };
        }
        if (_handR != null)
        {
            _handR.Rotation = new Vector3(swing * 0.40f, 0f, 0f);
            _handR.Position = _handRRest with { Z = _handRRest.Z + swing * 0.20f };
        }
        if (_footL != null)
        {
            _footL.Rotation = new Vector3(swing * 0.50f, 0f, 0f);
            _footL.Position = _footLRest with { Z = _footLRest.Z + swing * 0.22f };
        }
        if (_footR != null)
        {
            _footR.Rotation = new Vector3(-swing * 0.50f, 0f, 0f);
            _footR.Position = _footRRest with { Z = _footRRest.Z - swing * 0.22f };
        }
    }

    private void CaptureRestPositions()
    {
        if (_handL != null) _handLRest = _handL.Position;
        if (_handR != null) _handRRest = _handR.Position;
        if (_footL != null) _footLRest = _footL.Position;
        if (_footR != null) _footRRest = _footR.Position;
    }
}
