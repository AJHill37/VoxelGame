using Godot;
using System;
using System.Collections.Generic;

// Assembles a raider character from raider.vox.
// Each object in the .vox scene graph becomes a separate pivot + mesh,
// enabling per-part animation exactly like PlayerBody.
//
// MagicaVoxel is Z-up; axes are remapped on load:
//   MV (x, y, z)  →  Godot (x, z, y)
public partial class RaiderBody : Node3D
{
    // 1 MagicaVoxel voxel = this many Godot metres
    [Export] public float VoxelScale = 0.05f;

    // Set each frame by the owning entity to drive walk animation
    public Vector3 Velocity { get; set; }

    // VoxelMesherCubes padding is always 1 (static const PADDING = 1)
    private const int MeshPad = 1;

    // ── Body-part pivots ─────────────────────────────────────────────────────

    private Node3D _head;
    private Node3D _neck;
    private Node3D _torso;
    private Node3D _legs;
    private Node3D _handL, _handR;
    private Node3D _footL, _footR;

    private Vector3 _footLRest, _footRRest;
    private Vector3 _handLRest, _handRRest;

    private float _walkPhase;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private Player _playerSource;

    public override void _Ready()
    {
        var voxFile = VoxReader.Load("res://src/Assets/raider.vox");

        var palette = new VoxelColorPalette();
        for (int i = 0; i < 256; i++)
            palette.SetColor(i, voxFile.Palette[i]);

        var sharedMat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 0.9f,
        };

        float groundY = FindGroundY(voxFile);

        // Build all pivots first so we can assign body parts by position
        var built = new List<(Node3D pivot, VoxModel model)>();

        foreach (var model in voxFile.Models)
        {
            var mesh = BuildMesh(model, palette, sharedMat);
            if (mesh == null) continue;

            // Scene translation is the model centre in MV world space (Z=up).
            // Remap to Godot coords and subtract ground so feet sit at y=0.
            var pivot = new Node3D
            {
                Name = $"Part{built.Count}",
                Position = new Vector3(
                    model.SceneTranslation.X * VoxelScale,
                    model.SceneTranslation.Z * VoxelScale - groundY,
                    model.SceneTranslation.Y * VoxelScale),
            };

            // The mesh from BuildMesh is in voxel units (1 unit = 1 voxel).
            // Scale converts to Godot metres; Position centres it around the pivot.
            // Voxel data in the buffer starts at offset MeshPad in each axis,
            // so the centre in buffer local space = MeshPad + size/2.
            pivot.AddChild(new MeshInstance3D
            {
                Mesh = mesh,
                Scale = Vector3.One * VoxelScale,
                Position = new Vector3(
                    -(MeshPad + model.SizeX * 0.5f) * VoxelScale,
                    -(MeshPad + model.SizeZ * 0.5f) * VoxelScale,
                    -(MeshPad + model.SizeY * 0.5f) * VoxelScale),
            });

            AddChild(pivot);
            built.Add((pivot, model));
        }

        AssignBodyParts(built);
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

    // ── Part assignment ───────────────────────────────────────────────────────

    // Assigns body-part refs purely from spatial position since MagicaVoxel
    // exports no names by default. Sort by Godot Y (= MV Z, the up-axis):
    //   top → head, neck, torso
    //   mid pairs (symmetric X) → hands
    //   lower single → legs
    //   bottom pairs → feet
    private void AssignBodyParts(List<(Node3D pivot, VoxModel model)> parts)
    {
        // Sort descending by Y (highest first = head)
        parts.Sort((a, b) => b.pivot.Position.Y.CompareTo(a.pivot.Position.Y));

        // Collect singles and pairs (parts that share nearly the same Y but have mirrored X)
        var singles = new List<(Node3D pivot, VoxModel model)>();
        var pairs   = new List<(Node3D pivotA, Node3D pivotB)>();
        var used    = new bool[parts.Count];

        for (int i = 0; i < parts.Count; i++)
        {
            if (used[i]) continue;
            bool foundPair = false;
            for (int j = i + 1; j < parts.Count; j++)
            {
                if (used[j]) continue;
                float dy = Mathf.Abs(parts[i].pivot.Position.Y - parts[j].pivot.Position.Y);
                float dx = Mathf.Abs(parts[i].pivot.Position.X + parts[j].pivot.Position.X); // should be ~0 if mirrored
                if (dy < 0.2f && dx < 0.2f && Mathf.Abs(parts[i].pivot.Position.X) > 0.1f)
                {
                    pairs.Add((parts[i].pivot, parts[j].pivot));
                    used[i] = used[j] = true;
                    foundPair = true;
                    break;
                }
            }
            if (!foundPair) { singles.Add(parts[i]); used[i] = true; }
        }

        // Singles (still sorted high→low): head, neck, torso, legs, …
        if (singles.Count > 0) _head  = singles[0].pivot;
        if (singles.Count > 1) _neck  = singles[1].pivot;
        if (singles.Count > 2) _torso = singles[2].pivot;
        if (singles.Count > 3) _legs  = singles[3].pivot;

        // Pairs (sorted by average Y, high pair → hands, low pair → feet)
        pairs.Sort((a, b) =>
            b.pivotA.Position.Y.CompareTo(a.pivotA.Position.Y));

        if (pairs.Count > 0)
        {
            // Within each pair: positive X → right side
            var (pA, pB) = pairs[0];
            (_handR, _handL) = pA.Position.X >= pB.Position.X ? (pA, pB) : (pB, pA);
        }
        if (pairs.Count > 1)
        {
            var (pA, pB) = pairs[1];
            (_footR, _footL) = pA.Position.X >= pB.Position.X ? (pA, pB) : (pB, pA);
        }
    }

    private void CaptureRestPositions()
    {
        if (_handL != null) _handLRest = _handL.Position;
        if (_handR != null) _handRRest = _handR.Position;
        if (_footL != null) _footLRest = _footL.Position;
        if (_footR != null) _footRRest = _footR.Position;
    }

    // ── Mesh building ─────────────────────────────────────────────────────────

    private Mesh BuildMesh(VoxModel model, VoxelColorPalette palette, StandardMaterial3D mat)
    {
        if (model.Voxels == null || model.Voxels.Length == 0) return null;

        var mesher = new VoxelMesherCubes();
        mesher.ColorMode      = VoxelMesherCubes.ColorModeEnum.MesherPalette;
        mesher.Palette        = palette;
        mesher.OpaqueMaterial = mat;

        uint pad = mesher.GetMinimumPadding(); // always 1 for VoxelMesherCubes

        // MV axes: X=right, Y=depth, Z=up
        // Remap to Godot buffer: bufX=MV.x, bufY=MV.z (up→Y), bufZ=MV.y (depth→Z)
        var buffer = new VoxelBuffer();
        buffer.Create(
            model.SizeX + (int)pad * 2,
            model.SizeZ + (int)pad * 2,
            model.SizeY + (int)pad * 2);

        const uint colorChannel = (uint)VoxelBuffer.ChannelId.ChannelColor;
        buffer.SetChannelDepth(colorChannel, VoxelBuffer.Depth.Depth8Bit);

        foreach (var (vx, vy, vz, ci) in model.Voxels)
        {
            if (ci == 0) continue;
            buffer.SetVoxel(ci,
                vx + (int)pad,
                vz + (int)pad,
                vy + (int)pad,
                colorChannel);
        }

        return mesher.BuildMesh(buffer, new Godot.Collections.Array<Material>(), new Godot.Collections.Dictionary());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private float FindGroundY(VoxFile voxFile)
    {
        float min = float.MaxValue;
        foreach (var m in voxFile.Models)
        {
            float bottom = (m.SceneTranslation.Z - m.SizeZ * 0.5f) * VoxelScale;
            if (bottom < min) min = bottom;
        }
        return min == float.MaxValue ? 0f : min;
    }
}
