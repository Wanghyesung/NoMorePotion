namespace Enzeun.Runtime
{
    /// <summary>
    /// 포션 인스턴스 ID 를 발급하는 정적 도우미입니다. ID 는 1부터 순서대로 발급합니다.
    /// 멀티플레이에서는 권한을 가진 쪽(마스터)만 발급해야 ID 가 겹치지 않습니다.
    /// 마스터가 아닌 클라이언트는 발급하지 않고, 마스터가 정해 준 ID 를 그대로 사용합니다.
    /// </summary>
    public static class PotionInstanceIds
    {
        private static int _next = 1;

        /// <summary>
        /// 다음 인스턴스 ID 를 발급합니다.
        /// </summary>
        public static int Next()
        {
            return _next++;
        }

        /// <summary>
        /// 발급 번호를 처음(1)으로 되돌립니다. 새 게임을 시작할 때와 시험에서 사용합니다.
        /// </summary>
        public static void Reset()
        {
            _next = 1;
        }
    }
}
