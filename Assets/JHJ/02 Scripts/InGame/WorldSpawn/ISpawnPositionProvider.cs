using UnityEngine;

public interface ISpawnPositionProvider
{
    public bool TryGetPosition(out Vector3 position, out Quaternion rotation);
}
