using Godot;

public partial class Game : Node3D
{
    public const int AirId = 0;
    public const int GrassId = 1;
    public const int StoneId = 2;

    private VoxelTerrain _terrain;

    public override void _Ready()
    {
        SetupEnvironment();
        SetupTerrain();
        SetupPlayer();
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void SetupEnvironment()
    {
        var skyMat = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.40f, 0.60f, 1.00f),
            SkyHorizonColor = new Color(0.80f, 0.90f, 1.00f),
            GroundHorizonColor = new Color(0.60f, 0.60f, 0.65f),
            GroundBottomColor = new Color(0.20f, 0.22f, 0.25f),
        };
        var sky = new Sky { SkyMaterial = skyMat };
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = sky,
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.6f,
        };
        AddChild(new WorldEnvironment { Environment = env });

        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-55f, 40f, 0f),
            ShadowEnabled = true,
            LightEnergy = 1.2f,
        };
        AddChild(sun);
    }

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

        // Id 0 - air
        library.AddModel(new VoxelBlockyModelEmpty());

        // Id 1 - grass
        var grass = new VoxelBlockyModelCube
        {
            Color = new Color(0.35f, 0.70f, 0.25f),
            CollisionAabbs = fullCube,
        };
        grass.SetMaterialOverride(0, sharedMat);
        library.AddModel(grass);

        // Id 2 - stone
        var stone = new VoxelBlockyModelCube
        {
            Color = new Color(0.55f, 0.55f, 0.58f),
            CollisionAabbs = fullCube,
        };
        stone.SetMaterialOverride(0, sharedMat);
        library.AddModel(stone);

        var mesher = new VoxelMesherBlocky { Library = library };
        _terrain.Mesher = mesher;

        _terrain.Generator = new VoxelGeneratorFlat
        {
            // VoxelBuffer.CHANNEL_TYPE (0) — blocky meshes read TYPE, not the default SDF channel
            Channel = 0,
            VoxelType = GrassId,
            Height = 4,
        };

        _terrain.GenerateCollisions = true;
        _terrain.MaxViewDistance = 128;

        AddChild(_terrain);
    }

    private void SetupPlayer()
    {
        var player = new Player
        {
            Name = "Player",
            Position = new Vector3(0f, 12f, 0f),
            Terrain = _terrain,
            PlaceBlockId = StoneId,
        };
        AddChild(player);
    }
}
