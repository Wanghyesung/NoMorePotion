namespace Enzeun.Runtime
{
    /// <summary>
    /// 포션 인스턴스 ID 를 발급하는 정적 도우미입니다. ID 는 1부터 순서대로 발급합니다.
    /// 마스터(오프라인 모드에서는 나 자신)만 발급하고, 다른 클라이언트는 마스터가 정해 준 ID 를 그대로 씁니다.
    ///
    /// 주의 : 발급 번호는 이 클라이언트의 메모리에만 있어서 마스터 교체를 지원하지 않습니다.
    /// 새 마스터가 1부터 다시 발급하면 기존 포션 ID 와 겹치므로, 마스터가 나가면 방을 닫는 것(방폭)을 전제로 합니다.
    /// </summary>
    public static class PotionInstanceIds
    {
        private static int _next = 1;

        /// <summary>
        /// 다음 인스턴스 ID 를 발급합니다. 마스터만 호출합니다. 호출할 때마다 번호가 소모됩니다.
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
