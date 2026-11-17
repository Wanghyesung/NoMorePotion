using UnityEngine;

/// <summary>
/// 파티클 재생이 끝나면 풀로 자동 반납 (파티클 프리팹 루트에 부착)
/// 루트 파티클의 Looping은 꺼져 있어야 함
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class PooledParticle : MonoBehaviour
{
    private ParticleSystem ps;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.stopAction = ParticleSystemStopAction.Callback;   // 끝나면 OnParticleSystemStopped 호출
    }

    private void OnEnable()
    {
        ps.Clear(true);
        ps.Play(true);   // 풀에서 꺼낼 때마다 처음부터 재생
    }

    private void OnParticleSystemStopped()
    {
        PoolManager.Instance.Release(gameObject);
    }
}
