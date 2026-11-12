using Hashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;

public static class PlayerProps
{
    public const string READY = "rd";
    public const string LOADED = "ld";
    // TODO(멀티) :  플레이어 색 연동
    //public const string COLOR = "cl";


    /// <summary>
    /// 방을 나가면 의미가 없어지는(다음 방으로 넘어가면 안 되는) 키들의 초기값.
    /// 방 범위 키를 새로 만들면 여기에도 추가할 것.
    /// 호출: 방 퇴장·연결 끊김 시 LocalPlayer.SetCustomProperties(...)
    /// </summary>
    public static Hashtable CreateRoomScopedReset()
    {
        // 공유 객체로 두면 누군가 내용을 바꿨을 때 모든 초기화에 섞여 들어감 → 매번 새로 생성 (방 퇴장 시 1회라 GC 부담 없음)
        return new Hashtable { { READY, false }, { LOADED, false } };
    }

    public static bool IsReady(PhotonPlayer player) => GetBool(player, READY);
    public static bool IsLoaded(PhotonPlayer player) => GetBool(player, LOADED);

    // 키가 없거나 bool이 아니면 false (강제 형변환은 타입이 다르면 예외)
    private static bool GetBool(PhotonPlayer player, string key)
    {
        return player != null
            && player.CustomProperties.TryGetValue(key, out object value)
            && value is bool b
            && b;
    }
}
