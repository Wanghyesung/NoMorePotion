using UnityEngine;

public class ParticleRelease : MonoBehaviour
{
    public void OnParticleSystemStopped()
    {
        PoolManager.Instance.Release(gameObject);
    }
}
