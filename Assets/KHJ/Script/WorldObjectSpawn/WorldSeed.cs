using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 월드 오브젝트 자동 배치의 시드를 방(룸) 커스텀 프로퍼티로 공유하는 도우미입니다.
    /// 방장이 한 번 정하면 방의 모든 사람이 같은 값을 읽습니다. 늦게 들어온 사람도 입장할 때 받습니다.
    ///
    /// 새 판을 시작할 때 방장이 Roll() 을 호출해서 시드를 새로 정해야 판마다 배치가 바뀝니다.
    /// (방이 유지되는데 Roll 을 안 부르면 이전 판의 시드가 그대로 남아 같은 배치가 나옵니다)
    /// 호출 시점은 다른 사람이 게임 씬을 불러오기 전이어야 합니다.
    ///
    /// 주의 : 온라인에서는 SetCustomProperties 가 서버를 거쳐 돌아온 뒤에야 방장 자신의 방 정보에도 반영됩니다. (오프라인은 즉시 반영)
    /// Roll 직후 LoadLevel 을 하면 방장의 게임 씬 Start 시점에는 아직 반영 전이라, 방장만 이전 값(또는 없음)을 읽어 클라이언트와 배치가 달라졌습니다.
    /// 그래서 방장은 자기가 정한 값을 기억해 두고, 같은 방 안에서는 그 값을 먼저 돌려줍니다.
    /// </summary>
    public static class WorldSeed
    {
        #region Config
        /// <summary>
        /// 방 프로퍼티 키입니다. 다른 시스템(예: 야간 웨이브)이 쓰는 키와 겹치지 않아야 합니다.
        /// </summary>
        public const string RoomKey = "worldObjectSeed";
        #endregion

        #region LocalRoll
        // 이 기기가 방장으로서 마지막으로 정한 시드와 그 방 이름입니다. (서버 반영을 기다리지 않고 바로 쓰기 위함)
        // 방 이름이 다르면 이전 방의 값이므로 쓰지 않습니다.
        private static int s_rolledSeed;
        private static string s_rolledRoom;

        private static bool TryGetLocalRoll(out int seed)
        {
            seed = 0;

            if (s_rolledSeed == 0 || !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom.Name != s_rolledRoom)
            {
                return false;
            }

            seed = s_rolledSeed;
            return true;
        }
        #endregion

        #region Seed
        /// <summary>
        /// 방에 정해진 시드를 읽습니다. 방 밖이거나 아직 정해지지 않았으면 false 입니다.
        /// 방장은 이 방에서 직접 정한 값이 있으면 서버 반영 전이라도 그 값을 돌려줍니다.
        /// </summary>
        public static bool TryGet(out int seed)
        {
            seed = 0;

            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom.CustomProperties == null)
            {
                return false;
            }

            if (TryGetLocalRoll(out seed))
            {
                return true;
            }

            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomKey, out object value) && value is int number)
            {
                seed = number;
                return true;
            }

            return false;
        }

        /// <summary>
        /// [방장] 시드를 새로 정해 방 프로퍼티에 넣고 그 값을 돌려줍니다. fixedSeed 가 0 이 아니면 그 값을 씁니다. (개발용, 저장된 시드)
        /// 방장이 아니거나 방 밖이면 아무것도 하지 않고 0 을 돌려줍니다.
        /// </summary>
        public static int Roll(int fixedSeed = 0)
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            {
                return 0;
            }

            int seed = fixedSeed != 0 ? fixedSeed : System.Guid.NewGuid().GetHashCode();

            // 0 은 "정해지지 않음"으로 쓰는 값과 헷갈리지 않도록 피합니다.
            if (seed == 0)
            {
                seed = 1;
            }

            // 프로퍼티를 보내기 전에 기억합니다. (오프라인에서는 SetCustomProperties 안에서 바로 OnRoomPropertiesUpdate → TryGet 이 불림)
            s_rolledSeed = seed;
            s_rolledRoom = PhotonNetwork.CurrentRoom.Name;

            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { RoomKey, seed } });
            return seed;
        }
        #endregion
    }
}
