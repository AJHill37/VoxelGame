using Godot;

public partial class Game : Node3D
{
    private const float VoxelScale = 0.05f;
    private const int   MeshPad    = 1;

    public override void _Ready()
    {
        var player = GetNode<Player>("Player");
        player.Terrain = GetNode<GameTerrain>("GameTerrain").Terrain;
        Input.MouseMode = Input.MouseModeEnum.Captured;

        SpawnStaticRaider(new Vector3(3f, 12f, 0f));
    }

    private void SpawnStaticRaider(Vector3 worldBase)
    {
        // MV scene-graph translations that were stripped when splitting the file.
        // MV(tx, ty, tz) → Godot position(tx, tz, ty) * VoxelScale
        var parts = new (string file, int mvTx, int mvTy, int mvTz)[]
        {
            ("res://src/Assets/raider_head.vox",    0,  -5, 99),
            ("res://src/Assets/raider_neck.vox",    0,  -2, 79),
            ("res://src/Assets/raider_torso.vox",   0,  -3, 61),
            ("res://src/Assets/raider_hand_r.vox",  30, -4, 41),
            ("res://src/Assets/raider_hand_l.vox", -30, -4, 41),
            ("res://src/Assets/raider_legs.vox",    0,  -3, 31),
            ("res://src/Assets/raider_foot_r.vox",  13, -7,  4),
            ("res://src/Assets/raider_foot_l.vox", -12, -7,  4),
        };

        var sharedMat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 0.9f,
        };

        var root = new Node3D { Name = "StaticRaider" };
        AddChild(root);
        root.GlobalPosition = worldBase;

        foreach (var (file, mvTx, mvTy, mvTz) in parts)
        {
            var voxFile = VoxReader.Load(file);
            if (voxFile.Models.Length == 0) continue;

            var model = voxFile.Models[0];

            var palette = new VoxelColorPalette();
            for (int i = 0; i < 256; i++)
                palette.SetColor(i, voxFile.Palette[i]);

            var mesh = BuildMesh(model, palette, sharedMat);
            if (mesh == null) continue;

            var pivot = new Node3D
            {
                Position = new Vector3(
                    mvTx * VoxelScale,
                    mvTz * VoxelScale,
                    mvTy * VoxelScale),
            };
            root.AddChild(pivot);

            pivot.AddChild(new MeshInstance3D
            {
                Mesh     = mesh,
                Scale    = Vector3.One * VoxelScale,
                Position = new Vector3(
                    -(MeshPad + model.SizeX * 0.5f) * VoxelScale,
                    -(MeshPad + model.SizeZ * 0.5f) * VoxelScale,
                    -(MeshPad + model.SizeY * 0.5f) * VoxelScale),
            });
        }
    }

    private static Mesh BuildMesh(VoxModel model, VoxelColorPalette palette, StandardMaterial3D mat)
    {
        if (model.Voxels == null || model.Voxels.Length == 0) return null;

        var mesher = new VoxelMesherCubes();
        mesher.ColorMode      = VoxelMesherCubes.ColorModeEnum.MesherPalette;
        mesher.Palette        = palette;
        mesher.OpaqueMaterial = mat;

        uint pad = mesher.GetMinimumPadding();

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

        return mesher.BuildMesh(buffer,
            new Godot.Collections.Array<Material>(),
            new Godot.Collections.Dictionary());
    }
}
