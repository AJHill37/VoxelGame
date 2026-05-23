using Godot;

public partial class Game
{
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
}
