using R3;
using System;
using UnityEngine;

public class NightWaveSpawner : MonoBehaviour
{
    [SerializeField] private WaveTable waveTable;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform spawnRoot;
    [SerializeField] private GameObject defendTarget;   // 수정 구슬
    private CompositeDisposable pendingSpawns = new CompositeDisposable(); // 예약 중 묶음
    private TimeManager time;
    private int aliveCount;
    private void Start()
    {
        time = TimeManager.Instance;
        if (time == null)
        {
            Debug.LogWarning("[NightWaveSpawner] TimeManager 없음");
            return;
        }

        if (waveTable == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[NightWaveSpawner] waveTable 또는 spawnPoints 확인불가");
            return;
        }

        pendingSpawns.AddTo(this);
        time.Phase
            .Where(p => p == TimePhase.Night)
            .Subscribe(_ => StartWave())
            .AddTo(this);

        time.Phase
            .Where(p => p == TimePhase.Day)
            .Subscribe(_ => CancelPendingSpawns())
            .AddTo(this);
    }

    private void StartWave()
    {
        if (!GameAuthority.HasAuthority)
            return;

        // TODO(세이브): 밤에 불러오기 시 웨이브 재시작 여부
        CancelPendingSpawns();

        int day = time.DayCount.CurrentValue;
        WaveData wave = waveTable.GetWave(day);
        if(wave == null)
        {
            Debug.LogWarning("[NightWaveSpawner] waveTable에서 가져올 웨이브가 없음");
            return;
        }

        int order = 0;

        foreach(WaveEntry entry in wave.Entries)
        {
            if(entry.Prefab == null)
            {
                Debug.LogWarning("[NightWaveSpawner] entry 프리팹 없음");
                continue;
            }

            PoolManager.Instance.Prewarm(entry.Prefab, entry.Count);

            for (int i = 0; i < entry.Count; i++)
            {
                float delay = order * wave.SpawnInterval;
                IDisposable handle = time.Schedule(delay, () => SpawnOne(entry.Prefab));
                pendingSpawns.Add(handle);
                order++;
            }
        }

        Debug.Log($"[NightWaveSpawner] {day}일차 웨이브 시작, {order}마리 예약");
    }

    // TODO(전멸 알림): aliveCount가 0이 될 경우 뭘 할지 정하기
    private void SpawnOne(GameObject prefab)
    {
        Transform point = PickSpawnPoint();
        GameObject go = PoolManager.Instance.Get(prefab, point.position, point.rotation, spawnRoot);
        if (go == null) return;

        // 웨이브 몹은 기본적으로 수정 구슬을 노림
        if (go.TryGetComponent(out MonsterMove mover))
            mover.SetDefaultTarget(defendTarget);

        if (go.TryGetComponent(out MonsterCombat combat))
        {
            combat.Died += OnMonsterDied;
            aliveCount++;
        }
        // TODO(멀티): Resources 이동 후 PhotonNetwork.InstantiateRoomObject(prefab.name, ...)
    }

    private Transform PickSpawnPoint()
    {
        return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
    }

    private void CancelPendingSpawns()
    {
        pendingSpawns.Clear();
    }

    private void OnMonsterDied(MonsterCombat combat)
    {
        combat.Died -= OnMonsterDied;
        aliveCount--;
        // aliveCount == 0이면 전멸 알림
    }
}
