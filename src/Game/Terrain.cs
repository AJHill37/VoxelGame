using Godot;

public partial class Game
{
    private void SetupTerrain()
    {
        _terrain = new VoxelTerrain { Name = "VoxelTerrain" };

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

        _terrain.Mesher = new VoxelMesherBlocky { Library = library };

        _terrain.Generator = new VoxelGeneratorFlat
        {
            // VoxelBuffer.CHANNEL_TYPE (0) — blocky meshes read TYPE, not the default SDF channel
            Channel = 0,
            VoxelType = BlockIds.GrassId,
            Height = 4,
        };

        _terrain.GenerateCollisions = true;
        _terrain.MaxViewDistance = 128;

        AddChild(_terrain);
    }
}
