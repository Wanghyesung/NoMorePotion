using PhotonPlayer = Photon.Realtime.Player;

public static class PlayerProps
{
    public const string READY = "rd";

    public static bool IsReady(PhotonPlayer player)
    {
        if (player.CustomProperties.TryGetValue(READY, out object value) && value is bool)
            return (bool)value;

        return false;
    }
}
