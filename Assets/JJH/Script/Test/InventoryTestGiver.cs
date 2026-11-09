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
/// 테스트 씬 전용. Photon 오프라인 모드로 방을 만들고, 매니저의 정식 요청(*Async)으로 재료·포션을 넣습니다.
/// 동기화/데이터 코드는 건드리지 않으며, 나중에 실제 네트워크에서도 같은 경로로 동작합니다.
/// </summary>
public class InventoryTestGiver : MonoBehaviourPunCallbacks
{
    [SerializeField, LabelText("시작 시 오프라인 모드로 방 만들기")]
    private bool startOffline = true;

    [SerializeField, LabelText("테스트 재료 목록")]
    private List<MaterialData> testMaterials = new();

    [SerializeField, LabelText("테스트 포션 목록")]
    private List<PotionData> testPotions = new();

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

    private bool Ready()
    {
        if (Manager == null) { Debug.LogWarning("[테스트] InventoryNetworkSyncManager 가 씬에 없습니다."); return false; }
        if (!PhotonNetwork.InRoom) { Debug.LogWarning("[테스트] 아직 방에 들어가지 않았습니다."); return false; }
        return true;
    }

    // ==================== 재료 ====================

/*    [Button("재료 받기"), BoxGroup("재료")]
    private void GiveMaterial(MaterialData data, int count = 10) => GiveMaterialAsync(data, count).Forget();

    [Button("테스트 재료 전부 받기"), BoxGroup("재료")]
    private void GiveAllMaterials(int count = 10) => GiveAllMaterialsAsync(count).Forget();

    [Button("포션 레시피 재료 받기 (조합 테스트용)"), BoxGroup("재료")]
    private void GiveRecipeMaterials(PotionData potion, int sets = 1) => GiveRecipeAsync(potion, sets).Forget();

    [Button("재료 빼기"), BoxGroup("재료")]
    private void RemoveMaterial(MaterialData data, int count = 1) => RemoveMaterialAsync(data, count).Forget();*/
/*
    private async UniTaskVoid GiveMaterialAsync(MaterialData data, int count)
    {
        if (!Has(data, "재료 데이터") || !Ready()) return;
        InventoryResult result = await Manager.AddMaterialAsync(data.materialID, count);
        Debug.Log($"[테스트] 재료 넣기 {data.materialName} x{count} → {Describe(result)} | 보유 {Manager.State.Materials.GetCount(data.materialID)}");
    }

    private async UniTaskVoid GiveAllMaterialsAsync(int count)
    {
        if (!HasAny(testMaterials, "테스트 재료 목록") || !Ready()) return;
        foreach (MaterialData data in testMaterials)
        {
            if (data == null) continue;
            InventoryResult result = await Manager.AddMaterialAsync(data.materialID, count);
            Debug.Log($"[테스트] 재료 넣기 {data.materialName} x{count} → {Describe(result)} | 보유 {Manager.State.Materials.GetCount(data.materialID)}");
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
            if (data == null) continue;
            InventoryResult result = await Manager.AddMaterialAsync(data.materialID, sets);
            Debug.Log($"[테스트] 레시피 재료 {data.materialName} x{sets} → {Describe(result)}");
        }
    }

    private async UniTaskVoid RemoveMaterialAsync(MaterialData data, int count)
    {
        if (!Has(data, "재료 데이터") || !Ready()) return;
        InventoryResult result = await Manager.RemoveMaterialAsync(data.materialID, count);
        Debug.Log($"[테스트] 재료 빼기 {data.materialName} x{count} → {Describe(result)}");
    }

    // ==================== 포션 ====================

    [Button("포션 받기"), BoxGroup("포션")]
    private void GivePotion(PotionData data, int count = 1) => GivePotionAsync(data, count).Forget();

    [Button("테스트 포션 무작위로 받기"), BoxGroup("포션")]
    private void GiveRandomPotions(int count = 10) => GiveRandomAsync(count).Forget();

    [Button("첫 번째 포션 꺼내기"), BoxGroup("포션")]
    private void TakeFirstPotion() => TakeFirstAsync().Forget();

    private async UniTaskVoid GivePotionAsync(PotionData data, int count)
    {
        if (!Has(data, "포션 데이터") || !Ready()) return;
        for (int i = 0; i < count; i++)
        {
            // 인스턴스 ID 는 0 으로 보내면 매니저(마스터)가 발급합니다
            InventoryResult result = await Manager.AddPotionAsync(new PotionInstance(data, null, 0));
            Debug.Log($"[테스트] 포션 넣기 {data.potionName} → {Describe(result)} | 보유 {Manager.State.Potions.Count}/{Manager.State.Potions.Capacity}");
        }
    }

    private async UniTaskVoid GiveRandomAsync(int count)
    {
        if (!HasAny(testPotions, "테스트 포션 목록") || !Ready()) return;
        for (int i = 0; i < count; i++)
        {
            PotionData data = testPotions[Random.Range(0, testPotions.Count)];
            if (data == null) continue;
            InventoryResult result = await Manager.AddPotionAsync(new PotionInstance(data, null, 0));
            Debug.Log($"[테스트] 포션 넣기 {data.potionName} → {Describe(result)} | 보유 {Manager.State.Potions.Count}/{Manager.State.Potions.Capacity}");
        }
    }

    private async UniTaskVoid TakeFirstAsync()
    {
        if (!Ready()) return;
        IReadOnlyList<PotionInstance> items = Manager.State.Potions.Items;
        if (items.Count == 0) { Debug.Log("[테스트] 꺼낼 포션이 없습니다."); return; }

        PotionInstance first = items[0];
        InventoryResult result = await Manager.TakePotionAsync(first.InstanceId);
        Debug.Log($"[테스트] 포션 꺼내기 {first.Data.potionName}({first.InstanceId}) → {Describe(result)}");
    }
*/
    // ==================== 로그용 ====================

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