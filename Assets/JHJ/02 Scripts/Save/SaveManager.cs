using Enzeun.Runtime;
using static JJH_Enums;
using System.Collections.Generic;
using Photon.Pun;
using R3;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using SF = UnityEngine.SerializeField;

/// <summary>
/// 현 상태를 모아 현재 슬롯에 저장함. 인 게임씬에 하나
/// State가 Playing 진입 시 복원
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("저장")]
    [Tooltip("밤 몇 초전에 자동 저장할지")]
    [SF] private int checkpointSecBeforeNight = 10;

    private CrystalCore crystal;
    private int lastSavedDay;       // 마지막으로 저장된 일차 0 = 기록없음

    // [임시] 테스트용으로만 사용
    [Header("디버그")]
    [SF] private bool enableDebug = true;
    [SF] private int debugSlot = 0;
    [SF] private bool debugLoadOnStart = true;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[SaveManager] 인스턴스 중복, 삭제");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 현재 멀티는 세이브 없음
        // TODO: 추후 수정
        if (!PhotonNetwork.OfflineMode)
            SaveSession.Clear();

        // 타이틀 안거치고 씬 바로 실행한 경우에만
        if (enableDebug && !SaveSession.HasSlot() && PhotonNetwork.OfflineMode)
            DebugBeginSession();

        if (SaveSession.Loaded != null && SaveSession.Loaded.Clock != null)
            lastSavedDay = SaveSession.Loaded.Clock.DayCount;

        ApplySavedSeed();       // 월드 시드
        ApplySavedBossProgress(); // 지역보스 진행도
    }

    private void Start()
    {
        if (GameSessionManager.Instance == null)
            return;

        crystal = FindAnyObjectByType<CrystalCore>();

        GameSessionManager.Instance.Phase
            .Where(p => p == SessionPhase.Playing)
            .Take(1)                            // 1번만
            .Subscribe(_ => RestoreLoaded())
            .AddTo(this);

        if (TimeManager.Instance != null)
            TimeManager.Instance.RemainSec
                .Where(p => p == checkpointSecBeforeNight && TimeManager.Instance.Phase.CurrentValue == TimePhase.Day)
                .Subscribe(_ => SaveCheckpoint())
                .AddTo(this);

        GameSessionManager.Instance.Phase
            .Where(p => p == SessionPhase.Ended)
            .Take(1)
            .Subscribe(_ => OnSessionEnded())
            .AddTo(this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            SaveSession.Clear();
        }
    }

    private void Update()
    {
        if (!enableDebug || Keyboard.current == null)
            return;

        if (Keyboard.current.f5Key.wasPressedThisFrame)
            SaveNow();
    }

    /// <summary>
    /// 현 상태를 현재 슬롯에 저장(덮어쓰기) 성공 시 true
    /// 수동 저장
    /// </summary>
    public bool SaveNow()
    {
        if(TimeManager.Instance != null && TimeManager.Instance.Phase.CurrentValue == TimePhase.Night)
        {
            string note = lastSavedDay > 0 ?
                $"밤에는 저장할 수 없습니다. 지금 나가면 마지막 저장({lastSavedDay}일차 낮)부터 이어집니다."
                : "밤에는 저장할 수 없습니다.";

            NotificationUI.Show(note);
            return false;
        }

        return SaveInternal();
    }

    // 실제 저장
    private bool SaveInternal()
    {
        if (!SaveSession.HasSlot())
        {
            Debug.LogWarning("[SaveManager] 슬롯 없음, 저장 안 함");
            return false;
        }

        if (!GameAuthority.HasAuthority)
            return false;

        if (GameSessionManager.Instance == null || GameSessionManager.Instance.Phase.CurrentValue != SessionPhase.Playing)
        {
            Debug.Log("[SaveManager] 진행 중이 아님, 저장 안 함");
            return false;
        }

        SaveData data = Capture();
        if (data == null)
            return false;

        bool ok = SaveStore.Save(SaveSession.Slot, data);

        Debug.Log($"[SaveManager] {SaveSession.Slot}번 슬롯 저장 {(ok ? "성공" : "실패")} : {data.Clock.DayCount}일차 {data.Clock.Phase}");

        if (ok)
            lastSavedDay = data.Clock.DayCount;

        return ok;
    }


    private SaveData Capture()
    {
        // 시간
        TimeState clock = TimeManager.Instance != null ? TimeManager.Instance.CaptureState() : null;

        if(clock == null)
        {
            Debug.LogWarning("[SaveManager] 시계가 시작 전, 저장 안 함");
            return null;
        }

        // 월드 시드
        WorldSeed.TryGet(out int seed);

        // 월드오브젝트
        // WorldObjectNetworkSyncManager가 있으면 Base64 string으로 변환 / 없으면 null
        string worldObjects = WorldObjectNetworkSyncManager.Instance != null ? Convert.ToBase64String(WorldObjectNetworkSyncManager.Instance.State.CreateSnapshot()) : null;

        // 인벤토리
        string inventory = CaptureInventory();

        // 지역보스 클리어 진행도
        List<SpawnAreaType> cleared = new List<SpawnAreaType>();

        foreach (SpawnAreaType area in (SpawnAreaType[])Enum.GetValues(typeof(SpawnAreaType)))
        {
            if (RoomProps.IsAreaBossCleared(PhotonNetwork.CurrentRoom, area))
                cleared.Add(area);
        }

        // 수정구슬
        int maxHp = 0;
        int hp = 0;
        if(crystal != null)
        {
            maxHp = crystal.MaxHP;
            hp = crystal.HP;
        }

        return new SaveData { Clock = clock, WorldSeed = seed, WorldObjects = worldObjects, Inventory = inventory, ClearedBossArea = cleared, CrystalMaxHP = maxHp, CrystalHP = hp};
    }

    // 지역보스 진행도 불러오기
    private void ApplySavedBossProgress()
    {
        SaveData data = SaveSession.Loaded;

        if (data == null || data.ClearedBossArea == null)
            return;

        if (!PhotonNetwork.InRoom || !GameAuthority.HasAuthority)
            return;

        foreach (SpawnAreaType area in data.ClearedBossArea)
            AreaBossProgress.MarkCleared(area);
        
        Debug.Log($"[SaveManager] 저장된 지역보스 클리어 진행도 {data.ClearedBossArea.Count}개 적용");
    }

    // 월드 시드 불러오기
    private void ApplySavedSeed()
    {
        SaveData data = SaveSession.Loaded;

        if (data == null || data.WorldSeed == 0)
            return;

        if (!PhotonNetwork.InRoom || !GameAuthority.HasAuthority)
            return;

        WorldSeed.Roll(data.WorldSeed); // 0이 아니면 이 값으로 방 프로퍼티 설정
        Debug.Log($"[SaveManager] 저장된 시드 적용: {data.WorldSeed}");
    }

    // 불러온 값 적용
    private void RestoreLoaded()
    {
        SaveData data = SaveSession.Loaded;
        if (data == null || !GameAuthority.HasAuthority)
            return;

        RestoreWorldObjects(data.WorldObjects);
        RestoreInventory(data.Inventory);

        if (data.CrystalMaxHP > 0 && crystal != null)
        {
            crystal.LoadHP(data.CrystalMaxHP, data.CrystalHP);
            Debug.Log($"[SaveManager] 수정구슬 HP 복원 : {data.CrystalHP}/{data.CrystalMaxHP}");
        }
    }

    // 월드오브젝트 남은 횟수 복원
    private void RestoreWorldObjects(string worldObjects)
    {
        if (string.IsNullOrEmpty(worldObjects) || WorldObjectNetworkSyncManager.Instance == null)
            return;

        byte[] snapshot;

        try
        {
            snapshot = Convert.FromBase64String(worldObjects);
        }
        catch (FormatException e)
        {
            Debug.LogWarning($"[SaveManager] 스냅샷 문자열 깨짐, 변환 실패 : {e.Message}");
            return;
        }

        if (WorldObjectNetworkSyncManager.Instance.State.TryLoadSnapshot(snapshot))
            Debug.Log("[SaveManager] 월드오브젝트 남은횟수 복원 완료");
        else
            Debug.LogWarning("[SaveManager] 월드오브젝트 남은횟수 복원 실패, 기본 상태로 시작");
    }

    private void RestoreInventory(string inventory)
    {
        if(string.IsNullOrEmpty(inventory) || InventoryNetworkSyncManager.Instance == null) 
            return;

        byte[] snapshot;

        try
        {
            snapshot = Convert.FromBase64String(inventory);
        }
        catch (FormatException e)
        {
            Debug.LogWarning($"[SaveManager] 스냅샷 문자열 깨짐, 변환 실패 : {e.Message}");
            return;
        }

        if (InventoryNetworkSyncManager.Instance.State.TryLoadSnapshot(snapshot))
        {
            PotionInstanceIds.EnsureAbove(InventoryNetworkSyncManager.Instance.State.GetMaxPotionInstanceId());
            Debug.Log("[SaveManager] 인벤토리 복원 완료");
        }
        else
            Debug.LogWarning("[SaveManager] 인벤토리 복원 실패");
    }

    /// <summary>
    /// 인벤토리 스냅샷 만듦
    /// </summary>
    private string CaptureInventory()
    {
        if (InventoryNetworkSyncManager.Instance == null)
            return null;

        TeamInventoryState live = InventoryNetworkSyncManager.Instance.State;
        if (live == null)
            return null;

        TeamInventoryState copy = new TeamInventoryState();
        if(!copy.TryLoadSnapshot(live.CreateSnapshot()))
        {
            Debug.LogWarning("[SaveManager] 인벤토리 스냅샷 카피 불가");
            return null;
        }

        copy.MergePersonalsIntoPack(out int lost);
        if(lost > 0)
            Debug.LogWarning($"[SaveManager] 인벤토리 병합 중 개수가 넘쳐 {lost}개 버림");

        return Convert.ToBase64String(copy.CreateSnapshot());
    }

    private void SaveCheckpoint()
    {
        if (SaveInternal())
        {
            Debug.Log($"[SaveManager] 체크포인트 저장 : {lastSavedDay}일차");
            NotificationUI.Show("자동 저장되었습니다.");
        }
    }

    private void OnSessionEnded()
    {
        if (!SaveSession.HasSlot() || !GameAuthority.HasAuthority)
            return;

        if (GameSessionManager.Instance.Result == GameResult.Clear)
        {
            SaveStore.Delete(SaveSession.Slot);
            Debug.Log("[SaveManager] 게임 클리어로 인한 슬롯 삭제");
        }
        else
            Debug.Log("[SaveManager] 게임 미클리어로 마지막 저장 유지");
    }

    private void DebugBeginSession()
    {
        SaveData loaded = null;
        if (debugLoadOnStart)
            SaveStore.TryLoad(debugSlot, out loaded);

        SaveSession.Begin(debugSlot, loaded);
        Debug.Log($"[SaveManager] 디버그 슬롯 {debugSlot} 사용, {(loaded != null ? "불러오기" : "새 게임")}");
    }
}
