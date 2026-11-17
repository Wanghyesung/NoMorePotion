using ExitGames.Client.Photon;
using Photon.Pun;
using R3;
using System;
using UnityEngine;
using static JJH_Constants;

public class NightWaveSpawner : MonoBehaviourPunCallbacks
{
    [SerializeField] private WaveTable waveTable;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private GameObject defendTarget;   // 수정 구슬
    [SerializeField] private PhotonView defendTargetView;
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

        if (PhotonNetwork.InRoom)
        {
            Hashtable data = new()
            {
                [SPAWNER_DAY] = day,
                [SPAWNER_WAVE_START_TIME] = PhotonNetwork.Time,
                [SPAWNER_ORDER] = -1
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(data);
        }

        int count = ScheduleWave(wave);
        Debug.Log($"[NightWaveSpawner] {day}일차 웨이브 시작, {count}마리 예약");
    }

    // TODO(전멸 알림): aliveCount가 0이 될 경우 뭘 할지 정하기
    private void SpawnOne(GameObject prefab,int order)
    {
        Transform point = PickSpawnPoint();
        // 방 안: 방장이 포톤으로 생성 → 모든 클라이언트에 생성됨 / 방 밖: 로컬 풀에서 생성
        // 포톤 생성은 부모 지정 불가 → 씬 루트에 생성
        GameObject go = NetPool.Spawn(prefab, point.position, point.rotation);
        if (go == null) return;

        // 웨이브 몹은 기본적으로 수정 구슬을 노림
        if (go.TryGetComponent(out WaveMonsterMember wave))
            wave.MarkAsWave(defendTarget);
        else if (go.TryGetComponent(out MonsterMove mover))
            mover.SetDefaultTarget(defendTarget);

        if (go.TryGetComponent(out MonsterCombat combat))
        {
            combat.Died += OnMonsterDied;
            aliveCount++;
        }

        if (PhotonNetwork.InRoom)
        {
            Hashtable data = new()
            {
                [SPAWNER_ORDER] = order
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(data);
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


    /// <summary>
    /// 방장 이탈 시 살아 있는 웨이브 몬스터를 다시 세고 사망을 구독시키는 함수
    /// </summary>

    private void RecountAliveWaveMonsters()
    {
        aliveCount = 0;

        foreach (WaveMonsterMember m in WaveMonsterRegistry.Monsters)
        {
            if (!m.IsAlive) continue;   // 가라앉는 중인 시체 제외

            m.Combat.Died -= OnMonsterDied;   // 혹시 모를 중복 구독 방지
            m.Combat.Died += OnMonsterDied;
            aliveCount++;
        }
    }

    #region 방장 변경 시 처리
    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        RecountAliveWaveMonsters();
        if (time.Phase.CurrentValue == TimePhase.Day) return;

        Hashtable table = PhotonNetwork.CurrentRoom.CustomProperties;
        int dayNum = 0;
        int orderNum = 0;
        double waveTime = 0;

        CancelPendingSpawns();

        if (table.TryGetValue(SPAWNER_DAY, out object day))
        {
            dayNum = (int)day;
            if (dayNum <= 0) return;
        }
        else return;

        if (table.TryGetValue(SPAWNER_ORDER, out object order))
            orderNum = (int)order;
        else return;

        if (table.TryGetValue(SPAWNER_WAVE_START_TIME, out object waveStartTime))
            waveTime = (double)waveStartTime;
        else return;

        WaveData wave = waveTable.GetWave(dayNum);

        if (wave == null)
        {
            Debug.LogWarning("[NightWaveSpawner] waveTable에서 가져올 웨이브가 없음");
            return;
        }
        float beforeTime = (float)(PhotonNetwork.Time - waveTime);
        int count = ScheduleWave(wave,orderNum,beforeTime);

        Debug.Log($"[NightWaveSpawner] {day}일차 웨이브 이어받음, {orderNum + 1} 번부터 {count}마리 생성");

    }
    #endregion

   
    /// <summary>
    /// 발행한 웨이브 계획대로 몬스터를 생성 예약하는 함수
    /// </summary>
    /// <param name="wave">웨이브 데이터</param>
    /// <param name="lastSpawned">건너뛸 순번 기준</param>
    /// <param name="elapsed">웨이브 후 지난 시간</param>
    /// <returns>총 예약된 몬스터 수</returns>
    private int ScheduleWave(WaveData wave, int lastSpawned = -1, float elapsed = 0)
    {
        int order = 0;
        int scheduled = 0;

        foreach (WaveEntry entry in wave.Entries)
        {
            if (entry.Prefab == null) continue;

            for (int i = 0; i < entry.Count; i++)
            {
                int spawnOrder = order;
                order++;

                if (spawnOrder <= lastSpawned) continue;

                float delay = Mathf.Max(0f, spawnOrder * wave.SpawnInterval - elapsed);
                GameObject prefab = entry.Prefab;
                pendingSpawns.Add(time.Schedule(delay, () => SpawnOne(prefab, spawnOrder)));
                scheduled++;
            }
        }
        return scheduled;
    }

}
 