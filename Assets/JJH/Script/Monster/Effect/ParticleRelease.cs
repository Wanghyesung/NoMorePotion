using System.Runtime.InteropServices;
using UnityEngine;
using SF = UnityEngine.SerializeField;
public class ParticleRelease : MonoBehaviour
{
    [Header("풀에 등록된 오브젝트 (선택)")]
    [SF] private GameObject poolRoot;

    private ParticleSystem ps;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.stopAction = ParticleSystemStopAction.Callback;
    }

    private void OnEnable()
    {
        ps.Clear(true);
        ps.Play(true);
    }

    public void OnParticleSystemStopped()
    {
        PoolManager.Instance.Release(poolRoot != null ? poolRoot : gameObject);
    }
}
