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
    /// </summary>
    public static class WorldSeed
    {
        /// <summary>
        /// 방 프로퍼티 키입니다. 다른 시스템(예: 야간 웨이브)이 쓰는 키와 겹치지 않아야 합니다.
        /// </summary>
        public const string RoomKey = "worldObjectSeed";

        /// <summary>
        /// 방에 정해진 시드를 읽습니다. 방 밖이거나 아직 정해지지 않았으면 false 입니다.
        /// </summary>
        public static bool TryGet(out int seed)
        {
            seed = 0;

            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom.CustomProperties == null)
            {
                return false;
            }

            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomKey, out object value) && value is int number)
            {
                seed = number;
                return true;
            }

            return false;
        }

        /// <summary>
        /// [방장] 시드를 새로 정해 방 프로퍼티에 넣고 그 값을 돌려줍니다. fixedSeed 가 0 이 아니면 그 값을 씁니다. (개발용)
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

            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { RoomKey, seed } });
            return seed;
        }
    }
}
