using Enzeun.Runtime;
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

        // 타이틀 안거치고 씬 바로 실행한 경우에만
        if (enableDebug && !SaveSession.HasSlot())
            DebugBeginSession();

        ApplySavedSeed();       // 월드 시드
    }

    private void Start()
    {
        if (GameSessionManager.Instance == null)
            return;

        GameSessionManager.Instance.Phase
            .Where(p => p == SessionPhase.Playing)
            .Take(1)                            // 1번만
            .Subscribe(_ => RestoreLoaded())
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
    /// </summary>
    public bool SaveNow()
    {
        if(!SaveSession.HasSlot())
        {
            Debug.LogWarning("[SaveManager] 슬롯 없음, 저장 안 함");
            return false;
        }

        if (!GameAuthority.HasAuthority)
            return false;

        if(GameSessionManager.Instance == null || GameSessionManager.Instance.Phase.CurrentValue != SessionPhase.Playing)
        {
            Debug.Log("[SaveManager] 진행 중이 아님, 저장 안 함");
            return false;
        }

        SaveData data = Capture();
        if (data == null)
            return false;

        bool ok = SaveStore.Save(SaveSession.Slot, data);

        Debug.Log($"[SaveManager] {SaveSession.Slot}번 슬롯 저장 {(ok ? "성공" : "실패")} : {data.Clock.DayCount}일차 {data.Clock.Phase}");
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

        return new SaveData { Clock = clock, WorldSeed = seed, WorldObjects = worldObjects, Inventory = inventory};
    }

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

        // TODO: 지역보스 진행도, 수정구슬 hp
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

    private void DebugBeginSession()
    {
        SaveData loaded = null;
        if (debugLoadOnStart)
            SaveStore.TryLoad(debugSlot, out loaded);

        SaveSession.Begin(debugSlot, loaded);
        Debug.Log($"[SaveManager] 디버그 슬롯 {debugSlot} 사용, {(loaded != null ? "불러오기" : "새 게임")}");
    }
}
