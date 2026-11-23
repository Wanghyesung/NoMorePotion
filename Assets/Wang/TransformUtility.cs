using UnityEngine;

public static class TransformUtility
{
    public static void MoveTo(Transform target, Vector3 position)
    {
        target.position = position;
    }

    public static void MoveTo(Transform target, Transform destination)
    {
        MoveTo(target, destination.position);
    }
}
