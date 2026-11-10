using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Cysharp.Threading.Tasks;
using Enzeun.Runtime;
using Enzeun.SO;
using Photon.Pun;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 테스트 씬 전용. Photon 오프라인 모드로 방을 만들고, 매니저의 개발용 요청(Grant*)과 이동 요청(Transfer)으로 재료·포션을 넣고 옮깁니다.
/// Grant 요청은 TeamInventoryState.AllowGrant 가 켜져 있어야 통과합니다.
/// </summary>
public class InventoryTestGiver : MonoBehaviourPunCallbacks
{
    public enum Target
    {
        [LabelText("공용 인벤")] Pack,
        [LabelText("내 개인 인벤")] Personal,
    }

    [SerializeField, LabelText("시작 시 오프라인 모드로 방 만들기")]
    private bool startOffline = true;

    [SerializeField, LabelText("넣을 곳")]
    private Target target = Target.Pack;

    [SerializeField, LabelText("테스트 재료 목록")]
    private List<MaterialData> testMaterials = new();

    [SerializeField, LabelText("테스트 포션 목록")]
    private List<PotionData> testPotions = new();

    // 이번 테스트에서 공용 인벤으로 받은 포션의 인스턴스 ID (옮기기 테스트용)
    [ShowInInspector, ReadOnly, LabelText("공용 인벤으로 받은 포션 ID")]
    private readonly List<int> grantedPotionIds = new();

    private static InventoryNetworkSyncManager Manager => InventoryNetworkSyncManager.Instance;

    // ==================== 오프라인 모드 ====================

    private void Start()
    {
        if (!startOffline || PhotonNetwork.InRoom) return;

        if (PhotonNetwork.IsConnected && !PhotonNetwork.OfflineMode)
        {
            Debug.LogWarning("[테스트] 이미 온라인에 연결되어 있어 오프라인 모드를 켜지 않습니다.");
            return;
        }

        PhotonNetwork.OfflineMode = true; // → OnConnectedToMaster 호출됨
    }

    public override void OnConnectedToMaster()
    {
        if (PhotonNetwork.OfflineMode && !PhotonNetwork.InRoom)
            PhotonNetwork.CreateRoom(null);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("[테스트] 오프라인 방 입장 완료. 이제 재료/포션을 넣을 수 있습니다.");
    }

    // ==================== 재료 ====================

    [Button("재료 받기"), BoxGroup("재료")]
    private void GiveMaterial(MaterialData data, int count = 10) => GiveMaterialAsync(data, count).Forget();

    [Button("테스트 재료 전부 받기"), BoxGroup("재료")]
    private void GiveAllMaterials(int count = 10) => GiveAllMaterialsAsync(count).Forget();

    [Button("포션 레시피 재료 받기 (조합 테스트용)"), BoxGroup("재료")]
    private void GiveRecipeMaterials(PotionData potion, int sets = 1) => GiveRecipeAsync(potion, sets).Forget();

    private async UniTaskVoid GiveMaterialAsync(MaterialData data, int count)
    {
        if (!Has(data, "재료 데이터") || !Ready() || !Known(data)) return;
        await GrantMaterial(data, count);
    }

    private async UniTaskVoid GiveAllMaterialsAsync(int count)
    {
        if (!HasAny(testMaterials, "테스트 재료 목록") || !Ready()) return;
        foreach (MaterialData data in testMaterials)
        {
            if (data == null || !Known(data)) continue;
            await GrantMaterial(data, count);
        }
    }

    private async UniTaskVoid GiveRecipeAsync(PotionData potion, int sets)
    {
        if (!Has(potion, "포션 데이터") || !Ready()) return;
        if (potion.recipeMaterials == null || potion.recipeMaterials.Count == 0)
        {
            Debug.LogWarning($"[테스트] {potion.potionName} 은 재료 레시피가 없습니다. (포션 조합형일 수 있음)");
            return;
        }

        foreach (MaterialData data in potion.recipeMaterials)
        {
            if (data == null || !Known(data)) continue;
            await GrantMaterial(data, sets);
        }
    }

    private async UniTask GrantMaterial(MaterialData data, int count)
    {
        InventoryResult result = await Manager.GrantMaterialAsync(TargetRef(), data.materialID, count);
        Log($"재료 받기 {data.materialName} x{count} → {target}", result);
    }

    // ==================== 포션 ====================

    [Button("포션 받기"), BoxGroup("포션")]
    private void GivePotion(PotionData data, int count = 1, bool asTrap = false) => GivePotionAsync(data, count, asTrap).Forget();

    [Button("테스트 포션 무작위로 받기"), BoxGroup("포션")]
    private void GiveRandomPotions(int count = 10, int trapPercent = 0) => GiveRandomAsync(count, trapPercent).Forget();

    [Button("마지막으로 받은 포션을 공용 → 내 개인 인벤으로 옮기기"), BoxGroup("포션")]
    private void MoveLastPotionToPersonal() => MoveLastAsync().Forget();

    private async UniTaskVoid GivePotionAsync(PotionData data, int count, bool asTrap)
    {
        if (!Has(data, "포션 데이터") || !Ready() || !Known(data)) return;
        for (int i = 0; i < count; i++)
            await GrantPotion(data, asTrap);
    }

    private async UniTaskVoid GiveRandomAsync(int count, int trapPercent)
    {
        if (!HasAny(testPotions, "테스트 포션 목록") || !Ready()) return;
        for (int i = 0; i < count; i++)
        {
            PotionData data = testPotions[Random.Range(0, testPotions.Count)];
            if (data == null || !Known(data)) continue;
            await GrantPotion(data, Random.Range(0, 100) < trapPercent);
        }
    }

    private async UniTask GrantPotion(PotionData data, bool asTrap)
    {
        int keywordId = asTrap ? PickTrapKeywordId() : 0;
        InventoryResult result = await Manager.GrantPotionAsync(TargetRef(), data.potionID, keywordId);

        if (result.Success && target == Target.Pack && result.Outcome.Potion != null)
            grantedPotionIds.Add(result.Outcome.Potion.InstanceId);

        Log($"포션 받기 {data.potionName}{(keywordId != 0 ? " (함정)" : "")} → {target}", result);
    }

    private async UniTaskVoid MoveLastAsync()
    {
        if (!Ready()) return;
        if (grantedPotionIds.Count == 0)
        {
            Debug.Log("[테스트] 공용 인벤으로 받은 포션이 없습니다. '넣을 곳'을 공용 인벤으로 두고 포션을 먼저 받아 주세요.");
            return;
        }

        int instanceId = grantedPotionIds[grantedPotionIds.Count - 1];
        InventoryResult result = await Manager.TransferAsync(
            ContainerRef.PackRef,
            ContainerRef.Personal(Manager.LocalActor, -1), // -1 = 빈 칸 자동 선택
            instanceId: instanceId);

        if (result.Success) grantedPotionIds.RemoveAt(grantedPotionIds.Count - 1);
        Log($"포션 옮기기 ({instanceId}) 공용 → 개인", result);
    }

    // ==================== 내부 ====================

    private ContainerRef TargetRef()
    {
        return target == Target.Personal
            ? ContainerRef.Personal(Manager.LocalActor, -1)
            : ContainerRef.PackRef;
    }

    private static int PickTrapKeywordId()
    {
        IReadOnlyList<TrapPotionKeyword> pool = PotionDataManager.Instance.TrapKeywords;
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning("[테스트] 함정 키워드가 없어 정상 포션으로 받습니다.");
            return 0;
        }
        return pool[Random.Range(0, pool.Count)].KeywordID;
    }

    private bool Ready()
    {
        if (Manager == null) { Debug.LogWarning("[테스트] InventoryNetworkSyncManager 가 씬에 없습니다."); return false; }
        if (!PhotonNetwork.InRoom) { Debug.LogWarning("[테스트] 아직 방에 들어가지 않았습니다."); return false; }
        if (PotionDataManager.Instance == null)
        {
            Debug.LogWarning("[테스트] PotionDataManager 가 씬에 없습니다. 마스터가 재료/포션 ID 를 확인할 수 없어 요청이 거부됩니다.");
            return false;
        }
        return true;
    }

    // 마스터 검증과 같은 조건: 데이터 매니저에 등록된 ID 여야 함
    private static bool Known(MaterialData data)
    {
        if (PotionDataManager.Instance.TryGetMaterialData(data.materialID, out MaterialData _)) return true;
        Debug.LogWarning($"[테스트] 재료 '{data.materialName}'(ID {data.materialID}) 가 PotionDataManager 에 등록되어 있지 않습니다.");
        return false;
    }

    private static bool Known(PotionData data)
    {
        if (PotionDataManager.Instance.TryGetPotionData(data.potionID, out PotionData _)) return true;
        Debug.LogWarning($"[테스트] 포션 '{data.potionName}'(ID {data.potionID}) 가 PotionDataManager 에 등록되어 있지 않습니다.");
        return false;
    }

    private static bool Has(Object data, string what)
    {
        if (data != null) return true;
        Debug.LogWarning($"[테스트] {what} 가 비어 있습니다. 버튼 옆 화살표를 펼쳐 에셋을 넣어 주세요.");
        return false;
    }

    private static bool HasAny<T>(List<T> list, string what)
    {
        if (list != null && list.Count > 0) return true;
        Debug.LogWarning($"[테스트] 인스펙터의 '{what}' 이 비어 있습니다.");
        return false;
    }

    private static void Log(string what, InventoryResult result)
    {
        if (result.Success)
        {
            Debug.Log($"[테스트] {what} → 성공 {Describe(result.Outcome)}");
        }
        else
        {
            Debug.LogWarning($"[테스트] {what} → 실패 {Describe(result)}\n" +
                             "받기가 거부되면 TeamInventoryState.AllowGrant 가 켜져 있는지 확인하세요.");
        }
    }

    // InventoryResult 처럼 ToString 이 없는 구조체의 공개 필드/속성을 "이름=값" 으로 풀어 씁니다. (테스트 전용)
    private static string Describe(object value, int depth = 0)
    {
        if (value == null) return "null";
        System.Type type = value.GetType();
        if (depth >= 2 || type.IsPrimitive || type.IsEnum || value is string || value is Object) return value.ToString();

        var sb = new StringBuilder("{ ");
        foreach (FieldInfo f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            sb.Append(f.Name).Append('=').Append(Describe(f.GetValue(value), depth + 1)).Append(' ');
        foreach (PropertyInfo p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (p.GetIndexParameters().Length == 0)
                sb.Append(p.Name).Append('=').Append(Describe(p.GetValue(value), depth + 1)).Append(' ');
        return sb.Append('}').ToString();
    }
}