using Godot;

public partial class Game : Node3D
{
    private VoxelTerrain _terrain;

    public override void _Ready()
    {
        SetupEnvironment();
        SetupTerrain();
        SetupPlayer();
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void SetupPlayer()
    {
        var player = new Player
        {
            Name = "Player",
            Position = new Vector3(0f, 12f, 0f),
            Terrain = _terrain,
            PlaceBlockId = BlockIds.StoneId,
        };
        AddChild(player);
    }
}
