using R3;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using static JJH_Enums;
using Photon.Realtime;

public class AreaBossProgress : MonoBehaviourPunCallbacks
{
    public static AreaBossProgress Instance {  get; private set; }

    private readonly Subject<Unit> changed = new Subject<Unit>();
    public Observable<Unit> Changed => changed;
    public static readonly SpawnAreaType[] RegionBossAreas = { SpawnAreaType.Plain, SpawnAreaType.Forest, SpawnAreaType.Desert, SpawnAreaType.Glacier, SpawnAreaType.Volcano };


    private void Awake()
    {
        Instance = this;
    }

    public override void OnEnable()
    {
        base.OnEnable();
        BossEvents.OnDefeated += HandleBossDefeated;
    }
    

    public override void OnDisable()
    {
        base.OnDisable();
        BossEvents.OnDefeated -= HandleBossDefeated;
    }


    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        changed.Dispose();
    }

    public bool IsCleared(SpawnAreaType area)
    {
        return RoomProps.IsAreaBossCleared(PhotonNetwork.CurrentRoom, area);
    }

    public static bool IsAllCleared()
    {
        Room room = PhotonNetwork.CurrentRoom;
        
        if(room == null)
            return false;
    
        foreach(SpawnAreaType area in RegionBossAreas)
        {
            if (!RoomProps.IsAreaBossCleared(room, area))
                return false;
        }

        return true;
    }

    private void HandleBossDefeated(BossMonster boss)
    {
        if (boss == null)
            return;

        MarkCleared(boss.SpawnArea);
    }

    public static void MarkCleared(SpawnAreaType area)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        if (RoomProps.IsAreaBossCleared(PhotonNetwork.CurrentRoom, area))
            return;

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { RoomProps.AreaBossKey(area), true } });
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        foreach(object key in propertiesThatChanged.Keys)
        {
            if(key is string s && s.StartsWith(RoomProps.AREA_BOSS_PREFIX))
            {
                changed.OnNext(Unit.Default);
                return;
            }
        }
    }

#if UNITY_EDITOR
    #region 디버그 (세이브 테스트용)
    // 플레이 중 컴포넌트 ⋮ 메뉴에서 실행. 보스를 잡지 않고 클리어 상태만 만든다
    [UnityEngine.ContextMenu("디버그: 평원 보스 클리어")]
    private void DebugClearPlain() => DebugClear(SpawnAreaType.Plain);

    [UnityEngine.ContextMenu("디버그: 숲 보스 클리어")]
    private void DebugClearForest() => DebugClear(SpawnAreaType.Forest);

    // 마왕성(Castle)을 제외한 지역 보스 전부. 방장에서만 반영됨 (MarkCleared가 방장만 처리)
    [UnityEngine.ContextMenu("디버그: 지역 보스 전부 클리어")]
    private void DebugClearAllRegions()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            UnityEngine.Debug.LogWarning("[AreaBossProgress] 방장이 아니라 클리어 표시 불가 (방장 클라이언트에서 실행)");
            return;
        }

        foreach (SpawnAreaType area in RegionBossAreas)
            DebugClear(area);
    }

    [UnityEngine.ContextMenu("디버그: 클리어 상태 출력")]
    private void DebugLogCleared()
    {
        foreach (SpawnAreaType area in (SpawnAreaType[])System.Enum.GetValues(typeof(SpawnAreaType)))
            UnityEngine.Debug.Log($"[AreaBossProgress] {area} : {(IsCleared(area) ? "클리어" : "-")}");

        UnityEngine.Debug.Log($"[AreaBossProgress] 지역 보스 전부 처치 : {(IsAllCleared() ? "예" : "아니오")}");
    }

    private void DebugClear(SpawnAreaType area)
    {
        if (PhotonNetwork.CurrentRoom == null)
        {
            UnityEngine.Debug.LogWarning("[AreaBossProgress] 방 밖이라 클리어 표시 불가 (플레이 중에 실행)");
            return;
        }

        MarkCleared(area);
        UnityEngine.Debug.Log($"[AreaBossProgress] 디버그 클리어 : {area}");
    }
    #endregion
#endif
}
