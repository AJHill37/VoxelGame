using Godot;

public partial class Player
{
    private Node3D _yawPivot;
    private Node3D _pitchPivot;
    private SpringArm3D _springArm;
    private Camera3D _camera;
    private float _yaw;
    private float _pitch;

    private void SetupCameraRig()
    {
        _yawPivot = new Node3D { Position = new Vector3(0f, 1.6f, 0f) };
        AddChild(_yawPivot);

        _pitchPivot = new Node3D();
        _yawPivot.AddChild(_pitchPivot);

        _springArm = new SpringArm3D
        {
            SpringLength = CameraDistance,
            CollisionMask = 1,
            Margin = 0.1f,
        };
        _pitchPivot.AddChild(_springArm);

        _camera = new Camera3D();
        _springArm.AddChild(_camera);
    }

    private void ApplyMouseLook(Vector2 relative)
    {
        _yaw -= relative.X * MouseSensitivity;
        _pitch -= relative.Y * MouseSensitivity;
        _pitch = Mathf.Clamp(_pitch, -1.4f, 1.4f);
        _yawPivot.Rotation = new Vector3(0f, _yaw, 0f);
        _pitchPivot.Rotation = new Vector3(_pitch, 0f, 0f);
    }
}
