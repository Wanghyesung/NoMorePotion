using R3;
using System;
using UnityEngine;

public class NightWaveSpawner : MonoBehaviour
{
    [SerializeField] private WaveTable waveTable;
    [SerializeField] private Transform[] spawnPoints;
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

        RegisterAllPrefabs();

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

    // 웨이브 테이블의 모든 몬스터를 풀 대상으로 등록 (모든 클라이언트)
    // 방장이 생성 주문을 보냈을 때 각자 PhotonPoolAdapter 장부에서 찾을 수 있어야 함
    private void RegisterAllPrefabs()
    {
        foreach (WaveData wave in waveTable.Waves)
        {
            if (wave == null)
                continue;

            foreach (WaveEntry entry in wave.Entries)
            {
                if (entry != null && entry.Prefab != null)
                    PhotonPoolAdapter.RegisterPooled(entry.Prefab);
            }
        }
    }

    private void StartWave()
    {
        // TODO(세이브): 밤에 불러오기 시 웨이브 재시작 여부
        CancelPendingSpawns();

        int day = time.DayCount.CurrentValue;
        WaveData wave = waveTable.GetWave(day);
        if(wave == null)
        {
            Debug.LogWarning("[NightWaveSpawner] waveTable에서 가져올 웨이브가 없음");
            return;
        }

        // Prewarm은 모든 클라이언트: 방장의 생성 주문이 오면 각자 자기 풀에서 꺼냄
        foreach (WaveEntry entry in wave.Entries)
        {
            if (entry.Prefab != null)
                NetPool.Prepare(entry.Prefab, entry.Count);
        }

        // 생성 예약은 방장만 (다른 클라이언트는 포톤 생성 이벤트로 받음)
        if (!GameAuthority.HasAuthority)
            return;

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

    // TODO(전멸 알림): aliveCount가 0이 될 경우 뭘 할지 정하기
    private void SpawnOne(GameObject prefab)
    {
        Transform point = PickSpawnPoint();
        // 방 안: 방장이 포톤으로 생성 → 모든 클라이언트에 생성됨 / 방 밖: 로컬 풀에서 생성
        // 포톤 생성은 부모 지정 불가 → 씬 루트에 생성
        GameObject go = NetPool.Spawn(prefab, point.position, point.rotation);
        if (go == null) return;

        // 웨이브 몹은 기본적으로 수정 구슬을 노림
        if (go.TryGetComponent(out MonsterMove mover))
            mover.SetDefaultTarget(defendTarget);

        if (go.TryGetComponent(out MonsterCombat combat))
        {
            combat.Died += OnMonsterDied;
            aliveCount++;
        }
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
