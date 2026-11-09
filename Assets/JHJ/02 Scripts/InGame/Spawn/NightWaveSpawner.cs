using R3;
using System;
using UnityEngine;

public class NightWaveSpawner : MonoBehaviour
{
    [SerializeField] private WaveTable waveTable;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform spawnRoot;

    private CompositeDisposable pendingSpawns = new CompositeDisposable(); // 예약 중 묶음
    private TimeManager time;

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

    // TODO(전멸 알림): 생성한 몬스터 OnDead 구독 → 살아 있는 수 관리 (밤 채집금지 기획 확정 시)
    private void SpawnOne(GameObject prefab)
    {
        Transform point = PickSpawnPoint();
        Instantiate(prefab, point.position, point.rotation, spawnRoot);

        // TODO(멀티): Resources 이동 후 PhotonNetwork.InstantiateRoomObject(prefab.name, ...)

        // TODO(풀): 풀 매니저에서 꺼내기
    }

    private Transform PickSpawnPoint()
    {
        return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
    }

    private void CancelPendingSpawns()
    {
        pendingSpawns.Clear();
    }
}
