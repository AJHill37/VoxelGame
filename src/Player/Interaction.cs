using Godot;

public partial class Player
{
    private VoxelTool _voxelTool;
    private MeshInstance3D _faceHighlight;

    public override void _Process(double delta)
    {
        UpdateFaceHighlight();
    }

    private void SetupFaceHighlight()
    {
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
        tool.SetVoxel((Vector3I)hit.Get("position"), BlockIds.AirId);
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
        var playerAabb = new Aabb(GlobalPosition - new Vector3(0.4f, 0f, 0.4f), new Vector3(0.8f, 2.0f, 0.8f));
        if (playerAabb.Intersects(new Aabb(new Vector3(placePos.X, placePos.Y, placePos.Z), Vector3.One))) return;

        tool.SetVoxel(placePos, (ulong)PlaceBlockId);
    }
}
