using R3;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using static JJH_Enums;

public class AreaBossProgress : MonoBehaviourPunCallbacks
{

    public static AreaBossProgress Instance {  get; private set; }

    private readonly Subject<Unit> changed = new Subject<Unit>();
    public Observable<Unit> Changed => changed;

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

    private void HandleBossDefeated(BossMonster boss)
    {
        if (boss == null)
            return;

        MarkCleared(boss.SpawnArea);
    }

    public void MarkCleared(SpawnAreaType area)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        if (IsCleared(area))
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
}
