using Godot;

public partial class Game : Node3D
{
    public override void _Ready()
    {
        var player = GetNode<Player>("Player");
        player.Terrain = GetNode<GameTerrain>("GameTerrain").Terrain;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }
}
