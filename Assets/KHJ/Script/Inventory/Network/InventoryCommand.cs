using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 팀 공유 인벤토리(재료, 포션)와 시체 상자에 대한 요청의 종류입니다.
    /// </summary>
    public enum InventoryOp : byte
    {
        None = 0,
        AddMaterial = 1,
        RemoveMaterial = 2,
        AddPotion = 3,
        TakePotion = 4,
        CreateCorpse = 5,
        TakeCorpseSlot = 6,
        PutCorpseSlot = 7,
    }

    /// <summary>
    /// 요청이 거부된 이유입니다. Timeout, NotInRoom 은 네트워크로 오지 않고 요청한 쪽에서 직접 만듭니다.
    /// </summary>
    public enum InventoryDenyReason : byte
    {
        None = 0,
        Invalid = 1,     // 잘못된 값 (모르는 ID, 0 이하의 개수 등)
        Full = 2,        // 한도가 가득 참
        NotEnough = 3,   // 개수가 모자람
        NotFound = 4,    // 대상(포션, 시체 상자)이 없음
        SlotEmpty = 5,   // 꺼내려는 칸이 비어 있음
        Blocked = 6,     // 놓으려는 칸에 놓을 수 없음
        Timeout = 7,     // 제한 시간 안에 답이 오지 않음
        NotInRoom = 8,   // 방에 들어가 있지 않음
    }

    /// <summary>
    /// 적용(Apply) 결과의 상태입니다.
    /// </summary>
    public enum InventoryApplyStatus : byte
    {
        Applied = 0,        // 정상 적용
        Ignored = 1,        // 이미 적용한 오래된 메시지라 무시
        VersionGap = 2,     // 중간 메시지를 놓쳤음 (전체 목록을 다시 받아야 함)
        Inconsistent = 3,   // 내 사본이 마스터와 어긋났음 (전체 목록을 다시 받아야 함)
    }

    /// <summary>
    /// 요청(클라이언트 → 마스터)과 적용 메시지(마스터 → 전원)가 함께 쓰는 명령입니다.
    /// 종류(Op)마다 쓰는 필드가 다르며, 쓰지 않는 필드는 기본값입니다.
    /// 요청에는 요청한 쪽이 아는 값만 들어 있고, 적용 메시지에는 마스터가 확정한 값(실제 개수, 부여한 ID 등)이 채워집니다.
    /// </summary>
    public struct InventoryCommand
    {
        public InventoryOp Op;

        // 요청한 쪽이 정하는 요청 번호입니다. 결과를 요청과 짝지을 때 사용합니다.
        public int RequestId;

        // 적용 메시지에서만 사용합니다. 요청한 사람(ActorNumber)과, 이 적용으로 도달하는 사본의 버전입니다.
        public int RequesterActor;
        public int Version;

        // 재료. 요청의 Count 는 넣거나 빼려는 개수, 적용의 Count 는 실제로 넣거나 뺀 개수이고 NewTotal 은 적용 뒤 보관 개수입니다.
        public int MaterialId;
        public int Count;
        public int NewTotal;

        // 포션. 요청에는 포션 ID 와 키워드 ID 만 있고, 적용에는 마스터가 인스턴스 ID 를 부여해 만든 Potion 이 있습니다.
        public int PotionId;
        public int KeywordId;
        public int InstanceId;
        public PotionInstance Potion;

        // 시체 상자
        public int CorpseId;
        public int SlotIndex;
        public Vector3 Position;

        // Slot : PutCorpseSlot 요청에서는 놓으려는 내용, 적용에서는 실제로 놓이는 내용 / Leftover : 놓지 못하고 남은 내용
        public InventorySlot Slot;
        public InventorySlot Leftover;

        // CreateCorpse : 시체 상자에 들어갈 칸 전체
        public InventorySlot[] Slots;
    }

    /// <summary>
    /// 적용을 마친 뒤, 요청한 쪽이 결과로 받아 가는 값입니다.
    /// </summary>
    public struct InventoryOutcome
    {
        public int Value;               // 재료 : 실제로 넣거나 뺀 개수
        public PotionInstance Potion;   // AddPotion : 부여된 ID 가 있는 포션 / TakePotion : 꺼낸 포션
        public InventorySlot Slot;      // TakeCorpseSlot : 꺼낸 내용 / PutCorpseSlot : 실제로 놓인 내용
        public InventorySlot Leftover;  // PutCorpseSlot : 놓지 못하고 남은 내용 (요청한 쪽이 되돌려 받아야 함)
        public int CorpseId;            // CreateCorpse : 부여된 시체 상자 ID
    }

    /// <summary>
    /// 요청 하나의 최종 결과입니다. 성공이면 Success, 실패면 Reason 에 이유가 들어 있습니다.
    /// </summary>
    public struct InventoryResult
    {
        public bool Success;
        public InventoryDenyReason Reason;
        public InventoryOutcome Outcome;

        public static InventoryResult Fail(InventoryDenyReason reason)
        {
            return new InventoryResult { Success = false, Reason = reason };
        }

        public static InventoryResult Ok(InventoryOutcome outcome)
        {
            return new InventoryResult { Success = true, Reason = InventoryDenyReason.None, Outcome = outcome };
        }
    }
}
