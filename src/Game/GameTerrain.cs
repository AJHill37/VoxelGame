using Godot;

public partial class GameTerrain : Node3D
{
    public VoxelTerrain Terrain { get; private set; }

    public override void _Ready()
    {
        var library = new VoxelBlockyLibrary();
        var fullCube = new Godot.Collections.Array<Aabb> { new Aabb(Vector3.Zero, Vector3.One) };

        var sharedMat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 0.95f,
        };

        library.AddModel(new VoxelBlockyModelEmpty());

        var grass = new VoxelBlockyModelCube
        {
            Color = new Color(0.35f, 0.70f, 0.25f),
            CollisionAabbs = fullCube,
        };
        grass.SetMaterialOverride(0, sharedMat);
        library.AddModel(grass);

        var stone = new VoxelBlockyModelCube
        {
            Color = new Color(0.55f, 0.55f, 0.58f),
            CollisionAabbs = fullCube,
        };
        stone.SetMaterialOverride(0, sharedMat);
        library.AddModel(stone);

        Terrain = new VoxelTerrain
        {
            Name = "VoxelTerrain",
            Mesher = new VoxelMesherBlocky { Library = library },
            Generator = new VoxelGeneratorFlat
            {
                // VoxelBuffer.CHANNEL_TYPE (0) — blocky meshes read TYPE, not the default SDF channel
                Channel = 0,
                VoxelType = BlockIds.GrassId,
                Height = 4,
            },
            GenerateCollisions = true,
            MaxViewDistance = 128,
        };

        AddChild(Terrain);
    }
}
