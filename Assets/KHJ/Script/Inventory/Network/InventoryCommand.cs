using System.Collections.Generic;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 인벤토리 요청의 종류입니다.
    /// Grant : 개발용으로 아이템을 만들어 넣음 / Transfer : 장소 사이(또는 칸 사이) 이동 / Craft : 조합 /
    /// Die : 죽어서 개인 인벤토리를 시체 상자로 / TakeOut : 포션을 꺼내 손에 듦(인벤토리에서는 사라지고 결과로 포션을 받음) /
    /// Consume : 내 개인 인벤토리의 첫 번째 아이템 한 개를 소모함(허기. 마스터가 첫 번째 칸을 정함) /
    /// SpawnGround, Leave : 마스터만 만드는 변경 (클라이언트가 요청하지 않음)
    /// </summary>
    public enum InventoryOp : byte
    {
        None = 0,
        Grant = 1,
        Transfer = 2,
        Craft = 3,
        Die = 4,
        SpawnGround = 5,
        Leave = 6,
        TakeOut = 7,
        Consume = 8,
    }

    /// <summary>
    /// 요청이 거부된 이유입니다. Timeout, NotInRoom, OutOfSync 는 네트워크로 오지 않고 요청한 쪽에서 직접 만듭니다.
    /// </summary>
    public enum InventoryDenyReason : byte
    {
        None = 0,
        Invalid = 1,     // 잘못된 값 (모르는 ID, 0 이하의 개수, 허용되지 않는 조합 등)
        Full = 2,        // 넣을 곳이 가득 참
        NotEnough = 3,   // 개수가 모자람
        NotFound = 4,    // 대상(포션, 시체 상자, 바닥 주머니)이 없음
        SlotEmpty = 5,   // 꺼내려는 칸이 비어 있음
        Blocked = 6,     // 놓으려는 칸에 놓을 수 없음
        Timeout = 7,     // 제한 시간 안에 답이 오지 않음
        NotInRoom = 8,   // 방에 들어가 있지 않음
        OutOfSync = 9,   // 사본이 어긋나 요청의 결과를 알 수 없음 (전체 목록을 다시 받는 중. 결과는 사본을 확인할 것)
        NoAccess = 10,   // 접근 권한이 없음 (남의 개인 인벤토리, 베이스캠프 밖의 창고 등)
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
    /// 아이템을 보관하는 장소의 종류입니다.
    /// Pack : 공용 인벤(배낭, 어디서든) / Personal : 플레이어별 개인 인벤(칸) / Storage : 공용 창고(베이스캠프에서만) /
    /// Corpse : 시체 상자(칸) / Ground : 바닥에 떨어진 재료 주머니
    /// </summary>
    public enum ContainerKind : byte
    {
        None = 0,
        Pack = 1,
        Personal = 2,
        Storage = 3,
        Corpse = 4,
        Ground = 5,
    }

    /// <summary>
    /// 보관 장소 하나를 가리키는 값입니다. Id 는 개인 인벤토리면 플레이어 번호(ActorNumber), 시체 상자면 상자 ID, 바닥이면 주머니 ID 입니다.
    /// Slot 은 칸 구조(개인, 시체 상자)에서만 의미가 있고, -1 은 "알아서 고르기"(요청의 도착지)입니다.
    /// </summary>
    public struct ContainerRef
    {
        public ContainerKind Kind;
        public int Id;
        public int Slot;

        public static ContainerRef PackRef => new ContainerRef { Kind = ContainerKind.Pack, Slot = -1 };
        public static ContainerRef StorageRef => new ContainerRef { Kind = ContainerKind.Storage, Slot = -1 };
        public static ContainerRef Personal(int actor, int slot = -1) => new ContainerRef { Kind = ContainerKind.Personal, Id = actor, Slot = slot };
        public static ContainerRef Corpse(int corpseId, int slot = -1) => new ContainerRef { Kind = ContainerKind.Corpse, Id = corpseId, Slot = slot };
        public static ContainerRef Ground(int dropId) => new ContainerRef { Kind = ContainerKind.Ground, Id = dropId, Slot = -1 };

        /// <summary>
        /// 칸 구조(개인 인벤토리, 시체 상자)인지 여부입니다.
        /// </summary>
        public bool IsSlotBased => Kind == ContainerKind.Personal || Kind == ContainerKind.Corpse;

        /// <summary>
        /// 재료 개수와 포션 목록 구조(공용 인벤, 공용 창고)인지 여부입니다.
        /// </summary>
        public bool IsStore => Kind == ContainerKind.Pack || Kind == ContainerKind.Storage;

        /// <summary>
        /// 칸 위치와 상관없이 같은 장소인지 비교합니다.
        /// </summary>
        public bool IsSameContainer(ContainerRef other)
        {
            return Kind == other.Kind && Id == other.Id;
        }
    }

    /// <summary>
    /// 조합 재료 한 개를 가리킵니다. 출처가 공용 인벤이면 재료 ID 또는 포션 인스턴스 ID 로, 개인 인벤이면 칸 위치로 가리킵니다.
    /// </summary>
    public struct Ingredient
    {
        public ContainerRef Source;
        public int MaterialId;
        public int InstanceId;
    }

    /// <summary>
    /// 마스터가 확정한 변경 한 건의 종류입니다. 모든 클라이언트가 같은 값을 그대로(절대값) 적용하므로 같은 변경은 같은 결과를 냅니다.
    /// </summary>
    public enum InventoryChangeType : byte
    {
        None = 0,
        SetMaterial = 1,     // 공용 인벤/창고의 재료 총량을 정함
        AddPotion = 2,       // 공용 인벤/창고에 포션 추가
        RemovePotion = 3,    // 공용 인벤/창고에서 포션 제거
        SetSlot = 4,         // 개인 인벤/시체 상자의 칸 내용을 정함 (빈 칸이면 비움)
        CreateCorpse = 5,    // 빈 시체 상자 생성 (내용은 이어지는 SetSlot 이 채움)
        RemoveCorpse = 6,    // 시체 상자 제거
        RemovePersonal = 7,  // 플레이어의 개인 인벤토리 제거 (방을 나감)
        CreateGround = 8,    // 바닥 재료 주머니 생성
        SetGround = 9,       // 바닥 주머니의 개수를 정함 (0 이면 제거)
    }

    /// <summary>
    /// 마스터가 확정한 변경 한 건입니다. 종류(Type)마다 쓰는 필드가 다르며, 쓰지 않는 필드는 기본값입니다.
    /// </summary>
    public struct InventoryChange
    {
        public InventoryChangeType Type;

        // 변경 대상 장소 (SetMaterial, AddPotion, RemovePotion, SetSlot)
        public ContainerRef Target;

        public int MaterialId;      // SetMaterial, CreateGround
        public int Count;           // SetMaterial : 변경 후 총량 / CreateGround, SetGround : 주머니 개수
        public int InstanceId;      // RemovePotion
        public PotionInstance Potion;   // AddPotion
        public InventorySlot Slot;      // SetSlot : 칸의 새 내용
        public int Id;              // CreateCorpse, RemoveCorpse, RemovePersonal(플레이어 번호), CreateGround, SetGround
        public Vector3 Position;    // CreateCorpse, CreateGround
        public int Capacity;        // CreateCorpse : 칸 수

        public static InventoryChange SetMaterial(ContainerRef target, int materialId, int newTotal)
        {
            return new InventoryChange { Type = InventoryChangeType.SetMaterial, Target = target, MaterialId = materialId, Count = newTotal };
        }

        public static InventoryChange AddPotion(ContainerRef target, PotionInstance potion)
        {
            return new InventoryChange { Type = InventoryChangeType.AddPotion, Target = target, Potion = potion };
        }

        public static InventoryChange RemovePotion(ContainerRef target, int instanceId)
        {
            return new InventoryChange { Type = InventoryChangeType.RemovePotion, Target = target, InstanceId = instanceId };
        }

        public static InventoryChange SetSlot(ContainerRef target, InventorySlot slot)
        {
            return new InventoryChange { Type = InventoryChangeType.SetSlot, Target = target, Slot = slot };
        }

        public static InventoryChange CreateCorpse(int corpseId, Vector3 position, int capacity)
        {
            return new InventoryChange { Type = InventoryChangeType.CreateCorpse, Id = corpseId, Position = position, Capacity = capacity };
        }

        public static InventoryChange RemoveCorpse(int corpseId)
        {
            return new InventoryChange { Type = InventoryChangeType.RemoveCorpse, Id = corpseId };
        }

        public static InventoryChange RemovePersonal(int actor)
        {
            return new InventoryChange { Type = InventoryChangeType.RemovePersonal, Id = actor };
        }

        public static InventoryChange CreateGround(int dropId, int materialId, int count, Vector3 position)
        {
            return new InventoryChange { Type = InventoryChangeType.CreateGround, Id = dropId, MaterialId = materialId, Count = count, Position = position };
        }

        public static InventoryChange SetGround(int dropId, int newCount)
        {
            return new InventoryChange { Type = InventoryChangeType.SetGround, Id = dropId, Count = newCount };
        }
    }

    /// <summary>
    /// 요청(클라이언트 → 마스터)과 적용 메시지(마스터 → 전원)가 함께 쓰는 명령입니다.
    /// 요청에는 요청한 쪽이 아는 의도(From, To, 재료, 개수 등)만 들어 있고,
    /// 적용 메시지에는 마스터가 확정한 변경 목록(Changes)과 결과 값(Count, InstanceId, CorpseId)이 채워집니다.
    /// </summary>
    public struct InventoryCommand
    {
        public InventoryOp Op;

        // 요청한 쪽이 정하는 요청 번호입니다. 결과를 요청과 짝지을 때 사용합니다.
        public int RequestId;

        // 적용 메시지에서만 사용합니다. 요청한 사람(ActorNumber)과, 이 적용으로 도달하는 사본의 버전입니다.
        public int RequesterActor;
        public int Version;

        // Transfer : 옮길 곳과 받는 곳 / Grant : To 가 받는 곳
        public ContainerRef From;
        public ContainerRef To;

        // 옮길 재료(공용 인벤/창고에서 꺼낼 때)와 개수(0 = 전부). Grant 에서는 만들 재료.
        public int MaterialId;
        public int Count;

        // Transfer : 공용 인벤/창고에서 꺼낼 포션 / 적용 : 새로 만들어진 포션의 인스턴스 ID
        public int InstanceId;

        // Grant, Craft : 만들 포션(Grant)과 함정 키워드
        public int PotionId;
        public int KeywordId;

        // Die : 죽은 위치
        public Vector3 Position;

        // Craft : 조합 재료 두 개
        public Ingredient IngredientA;
        public Ingredient IngredientB;

        // 적용 : 시체 상자를 만들었으면 그 ID
        public int CorpseId;

        // 적용 : 마스터가 확정한 변경 목록
        public List<InventoryChange> Changes;
    }

    /// <summary>
    /// 적용을 마친 뒤, 요청한 쪽이 결과로 받아 가는 값입니다.
    /// </summary>
    public struct InventoryOutcome
    {
        public int Value;               // 실제로 옮기거나 만든 개수
        public PotionInstance Potion;   // Craft, Grant : 새로 만들어진 포션 (ID 가 부여됨)
        public IReadOnlyList<PotionInstance> Potions;   // TakeOut : 인벤토리에서 꺼낸 포션들 (꺼낸 순서, 못 꺼냈으면 비어 있음)
        public int CorpseId;            // Die : 부여된 시체 상자 ID
        public IReadOnlyList<InventoryChange> Changes;   // 이번 요청으로 바뀐 내용 전체
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
