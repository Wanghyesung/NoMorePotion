using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Photon.Pun;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// [개발용] 테스트용 포션과 재료를 인벤토리에 넣어 주는 컴포넌트입니다. 씬에 아무 오브젝트에나 붙여서 씁니다.
    /// - 시작할 때 자동 지급을 켜 두면 방에 들어간 뒤 각자 한 번씩 지급합니다. (공용 인벤은 마스터만 지급해서 중복되지 않음)
    /// - 플레이 중에는 아래 버튼으로 언제든 더 넣을 수 있습니다.
    /// 요청은 마스터가 검증해서 모두에게 적용되는 일반 흐름을 그대로 탑니다. TeamInventoryState.AllowGrant 가 꺼져 있으면 거부되므로 출시 빌드에서는 쓰지 마세요.
    /// </summary>
    public class InventoryDebugGrant : MonoBehaviour
    {
        [Serializable]
        private struct PotionGrant
        {
            [LabelText("포션 ID")] public int PotionId;
            [LabelText("개수"), MinValue(1)] public int Count;
        }

        [Serializable]
        private struct MaterialGrant
        {
            [LabelText("재료 ID")] public int MaterialId;
            [LabelText("개수"), MinValue(1)] public int Count;
        }

        [SerializeField, LabelText("시작할 때 자동 지급"), BoxGroup("자동 지급")]
        private bool _grantOnStart = true;

        [SerializeField, LabelText("공용 인벤에 넣을 포션"), BoxGroup("지급 목록")]
        private List<PotionGrant> _packPotions = new List<PotionGrant> { new PotionGrant { PotionId = 10001, Count = 5 } };

        [SerializeField, LabelText("개인 인벤에 넣을 포션 (내 것)"), BoxGroup("지급 목록")]
        private List<PotionGrant> _personalPotions = new List<PotionGrant> { new PotionGrant { PotionId = 10001, Count = 3 } };

        [SerializeField, LabelText("공용 인벤에 넣을 재료"), BoxGroup("지급 목록")]
        private List<MaterialGrant> _packMaterials = new List<MaterialGrant> { new MaterialGrant { MaterialId = 20001, Count = 10 } };

        private void Start()
        {
            if (_grantOnStart)
            {
                AutoGrantAsync().Forget();
            }
        }

        // 오브젝트가 사라져 취소되면 UniTaskVoid.Forget 이 조용히 넘깁니다.
        private async UniTaskVoid AutoGrantAsync()
        {
            // 방에 들어가 있고 인벤토리 매니저가 준비될 때까지 기다립니다. (오프라인 방 모드도 InRoom 입니다)
            await UniTask.WaitUntil(() => PhotonNetwork.InRoom && InventoryNetworkSyncManager.Instance != null,
                cancellationToken: this.GetCancellationTokenOnDestroy());

            // 늦게 들어온 사람의 사본이 마스터의 전체 목록을 받을 시간을 잠깐 줍니다.
            await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: this.GetCancellationTokenOnDestroy());

            GrantPersonalPotionsAsync().Forget();

            // 공용 인벤은 모두가 지급하면 중복되므로 마스터만 지급합니다.
            if (PhotonNetwork.IsMasterClient)
            {
                GrantPackAsync().Forget();
            }
        }

        [Button("공용 인벤에 지급 (포션 + 재료)", ButtonSizes.Large), BoxGroup("지급 버튼")]
        public void GrantPack()
        {
            GrantPackAsync().Forget();
        }

        [Button("내 개인 인벤에 포션 지급", ButtonSizes.Large), BoxGroup("지급 버튼")]
        public void GrantPersonalPotions()
        {
            GrantPersonalPotionsAsync().Forget();
        }

        private async UniTaskVoid GrantPackAsync()
        {
            InventoryNetworkSyncManager manager = InventoryNetworkSyncManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[InventoryDebugGrant] : InventoryNetworkSyncManager 가 없어 지급할 수 없습니다.");
                return;
            }

            for (int i = 0; i < _packPotions.Count; i++)
            {
                for (int n = 0; n < _packPotions[i].Count; n++)
                {
                    await GrantPotionAsync(manager, ContainerRef.PackRef, _packPotions[i].PotionId);
                }
            }

            for (int i = 0; i < _packMaterials.Count; i++)
            {
                InventoryResult result = await manager.GrantMaterialAsync(ContainerRef.PackRef, _packMaterials[i].MaterialId, _packMaterials[i].Count);
                LogResult($"재료 {_packMaterials[i].MaterialId} x{_packMaterials[i].Count} -> 공용 인벤", result);
            }
        }

        private async UniTaskVoid GrantPersonalPotionsAsync()
        {
            InventoryNetworkSyncManager manager = InventoryNetworkSyncManager.Instance;
            if (manager == null || manager.LocalActor <= 0)
            {
                Debug.LogWarning("[InventoryDebugGrant] : 방에 들어가 있지 않아 개인 인벤에 지급할 수 없습니다.");
                return;
            }

            ContainerRef mine = ContainerRef.Personal(manager.LocalActor);

            for (int i = 0; i < _personalPotions.Count; i++)
            {
                for (int n = 0; n < _personalPotions[i].Count; n++)
                {
                    await GrantPotionAsync(manager, mine, _personalPotions[i].PotionId);
                }
            }
        }

        // 포션은 한 개씩 지급합니다. (포션 인스턴스마다 번호가 필요하고, 개인 인벤은 칸 하나에 한 개라서)
        private async UniTask GrantPotionAsync(InventoryNetworkSyncManager manager, ContainerRef to, int potionId)
        {
            InventoryResult result = await manager.GrantPotionAsync(to, potionId);

            if (!result.Success)
            {
                LogResult($"포션 {potionId} -> {to.Kind}", result);
            }
        }

        private static void LogResult(string what, InventoryResult result)
        {
            if (!result.Success)
            {
                Debug.LogWarning($"[InventoryDebugGrant] : {what} 지급 실패 ({result.Reason})");
            }
#if UNITY_EDITOR
            else
            {
                Debug.Log($"[InventoryDebugGrant] : {what} 지급 완료");
            }
#endif
        }
    }
}
