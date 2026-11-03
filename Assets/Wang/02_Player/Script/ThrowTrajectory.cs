using UnityEngine;

public readonly struct ThrowTrajectory
{
    public Vector3 Origin { get; }
    public Vector3 Velocity { get; }
    public Vector3 Gravity { get; }

    public ThrowTrajectory(Vector3 origin, Vector3 velocity, Vector3 gravity)
    {
        Origin = origin;
        Velocity = velocity;
        Gravity = gravity;
    }

    public Vector3 GetPosition(float time)
    {
        return Origin + Velocity * time + Gravity * (0.5f * time * time);
    }
}
