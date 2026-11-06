namespace Enzeun.Runtime
{
    /// <summary>
    /// 월드 오브젝트 요청이 거부되거나 실패한 이유입니다.
    /// </summary>
    public enum WorldObjectDenyReason
    {
        None,
        Invalid,        // 깨졌거나 말이 안 되는 요청
        NotFound,       // 모르는 오브젝트 번호
        Depleted,       // 이미 소진됨
        NoEffect,       // 이 타격은 이 오브젝트에 효과가 없음 (타입 불일치 등)
        Timeout,        // 제한 시간 안에 답이 없음
        NotInRoom,      // 방에 들어와 있지 않음
    }

    public enum WorldObjectApplyStatus
    {
        Applied,        // 적용함
        Ignored,        // 이미 반영한 버전이라 무시
        VersionGap,     // 중간 메시지를 놓침 (전체 목록 필요)
        Inconsistent,   // 사본이 마스터와 어긋남 (전체 목록 필요)
    }

    /// <summary>
    /// 소진으로 바닥에 떨어진 재료 한 건입니다. DropId 는 마스터만 발급하며, 다음 단계(줍기 동기화)에서 "누가 먼저 주웠나"를 가리는 데 씁니다.
    /// </summary>
    public struct WorldDrop
    {
        public int DropId;
        public int MaterialId;
        public int Count;
    }

    /// <summary>
    /// 월드 오브젝트 요청 / 적용 메시지의 내용입니다. 요청에서는 앞쪽 필드, 적용에서는 마스터가 확정한 값까지 씁니다.
    /// </summary>
    public struct WorldObjectCommand
    {
        public int RequestId;
        public int RequesterActor;
        public int Version;             // 적용 : 적용 후 사본 버전

        public int NetworkId;           // 월드 오브젝트 번호 (씬 안에서 고유)
        public WorldObjectHitKind Kind;
        public int Damage;              // 포션 대미지 수치
        public int TargetMask;          // 포션 대미지의 대상 종류 마스크

        // 적용(마스터가 확정)
        public int Previous;            // 적용 전 남은 횟수
        public int Remaining;           // 적용 후 남은 횟수
        public WorldDrop[] Drops;       // 소진되었을 때만 값이 있음
    }

    /// <summary>
    /// 요청한 사람이 돌려받는 결과입니다.
    /// </summary>
    public readonly struct WorldObjectResult
    {
        public readonly bool Success;
        public readonly WorldObjectDenyReason Reason;
        public readonly int Remaining;
        public readonly WorldDrop[] Drops;

        private WorldObjectResult(bool success, WorldObjectDenyReason reason, int remaining, WorldDrop[] drops)
        {
            Success = success;
            Reason = reason;
            Remaining = remaining;
            Drops = drops;
        }

        public static WorldObjectResult Ok(int remaining, WorldDrop[] drops)
        {
            return new WorldObjectResult(true, WorldObjectDenyReason.None, remaining, drops);
        }

        public static WorldObjectResult Fail(WorldObjectDenyReason reason)
        {
            return new WorldObjectResult(false, reason, 0, null);
        }
    }
}
