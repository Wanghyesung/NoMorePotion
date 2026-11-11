#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using Enzeun.Runtime;
using Enzeun.SO;
using UnityEditor;
using UnityEngine;


namespace Enzeun.Editor
{
    /// <summary>
    /// 인벤토리 규칙(TeamInventoryState)을 마스터 1 + 클라이언트 2 로 흉내 내어 시험합니다. 네트워크와 씬 없이 에디터에서 돌아갑니다.
    /// 모든 요청과 적용을 실제 메시지 형식(byte 배열)으로 왕복시켜서, 규칙과 직렬화를 함께 확인합니다.
    /// 메뉴 : Enzeun/인벤토리/시뮬레이션 시험 실행
    ///
    /// 확인하는 것 : 바닥 줍기 경쟁과 부분 줍기, 공용 인벤과 개인 인벤 사이 이동, 칸 이동, 조합(재료와 포션), 결과 자리 대체,
    /// 창고 접근 제한, 남의 개인 인벤토리 접근 거부, 사망과 시체 상자, 방 나가기, 버전 순서, 스냅샷, 포션 ID 중복
    /// </summary>
    public static class InventorySimulation
    {
        // 플레이어 번호입니다. 1 이 마스터이고 2, 3 은 클라이언트입니다.
        private const int MasterActor = 1;
        private const int ActorA = 2;
        private const int ActorB = 3;

        private const int MatSmall = 20001;     // 한도 99
        private const int MatOther = 20002;     // 한도 99
        private const int MatLimited = 20003;   // 한도 5 (가득 참 시험용)
        private const int PotionBase = 10001;
        private const int PotionUpgraded = 10002;

        private struct Res
        {
            public bool Ok;
            public InventoryDenyReason Reason;
            public InventoryOutcome Outcome;
        }

        /// <summary>
        /// 시험용 가짜 게임 데이터입니다. 재료 3종, 포션 2종(기본 → 합친 것), 함정 키워드 1종을 가집니다.
        /// </summary>
        private sealed class FakeData : IInventoryData
        {
            public readonly PotionData PotionA;
            public readonly PotionData PotionB;
            public readonly TrapPotionKeyword Keyword;

            public FakeData()
            {
                PotionA = MakePotion(PotionBase);
                PotionB = MakePotion(PotionUpgraded);
                Keyword = ScriptableObject.CreateInstance<TrapPotionKeyword>();
                SetInt(Keyword, "_keywordID", 50001);
            }

            public void Destroy()
            {
                UnityEngine.Object.DestroyImmediate(PotionA);
                UnityEngine.Object.DestroyImmediate(PotionB);
                UnityEngine.Object.DestroyImmediate(Keyword);
            }

            private static PotionData MakePotion(int id)
            {
                var data = ScriptableObject.CreateInstance<PotionData>();
                SetInt(data, "_potionID", id);
                return data;
            }

            private static void SetInt(UnityEngine.Object target, string field, int value)
            {
                var so = new SerializedObject(target);
                so.FindProperty(field).intValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            public bool IsKnownMaterial(int materialId) => materialId == MatSmall || materialId == MatOther || materialId == MatLimited;

            public int GetMaterialLimit(int materialId) => materialId == MatLimited ? 5 : 99;

            public bool TryGetPotion(int potionId, out PotionData data)
            {
                data = potionId == PotionBase ? PotionA : potionId == PotionUpgraded ? PotionB : null;
                return data != null;
            }

            public bool TryGetKeyword(int keywordId, out TrapPotionKeyword keyword)
            {
                keyword = keywordId == 50001 ? Keyword : null;
                return keyword != null;
            }

            public PotionData FindRecipe(int firstMaterialId, int secondMaterialId)
            {
                int low = Math.Min(firstMaterialId, secondMaterialId);
                int high = Math.Max(firstMaterialId, secondMaterialId);
                return low == MatSmall && high == MatOther ? PotionA : null;
            }

            public PotionData FindUpgrade(int firstPotionId, int secondPotionId)
            {
                return firstPotionId == PotionBase && secondPotionId == PotionBase ? PotionB : null;
            }
        }

        private static FakeData _data;
        private static TeamInventoryState _master;
        private static TeamInventoryState _clientA;
        private static TeamInventoryState _clientB;
        private static TeamInventoryState[] _all;
        private static int _nextRequestId;
        private static byte[] _lastApplyBytes;
        private static List<string> _failures;
        private static int _checks;

        [MenuItem("Enzeun/인벤토리/시뮬레이션 시험 실행")]
        public static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        /// <summary>
        /// 시험을 모두 실행하고 결과 요약을 돌려줍니다. 실패한 항목은 요약에 나열합니다.
        /// </summary>
        public static string Run()
        {
            IInventoryData previousPackerData = InventoryPacker.Data;
            _failures = new List<string>();
            _checks = 0;
            _data = new FakeData();

            try
            {
                InventoryPacker.Data = _data;
                PotionInstanceIds.Reset();
                _nextRequestId = 1;

                _master = new TeamInventoryState(_data);
                _clientA = new TeamInventoryState(_data);
                _clientB = new TeamInventoryState(_data);
                _all = new[] { _master, _clientA, _clientB };

                TestGrantAndGround();
                TestPickup();
                TestPackAndPersonal();
                TestCraft();
                TestStorageAndAccess();
                TestTakeOut();
                TestWorldGrant();
                TestDeathAndLeave();
                TestVersionAndSnapshot();
                CheckPotionIdsUnique("마지막 상태");
            }
            catch (Exception exception)
            {
                _failures.Add("예외 발생 : " + exception);
            }
            finally
            {
                InventoryPacker.Data = previousPackerData;
                PotionInstanceIds.Reset();
                _data.Destroy();
            }

            var summary = new StringBuilder();
            summary.AppendLine($"[InventorySimulation] 확인 {_checks}건, 실패 {_failures.Count}건");

            for (int i = 0; i < _failures.Count; i++)
            {
                summary.AppendLine(" - 실패 : " + _failures[i]);
            }

            return summary.ToString();
        }

        // ==================== 시험 ============================

        private static void TestGrantAndGround()
        {
            // 공용 인벤에 재료를 만들어 넣고(개발용), 바닥에 주머니를 만든다.
            Expect("공용 인벤 재료 생성", Send(ActorA, GrantMaterial(ContainerRef.PackRef, MatSmall, 3)).Ok);
            Expect("공용 인벤 재료 생성 2", Send(ActorA, GrantMaterial(ContainerRef.PackRef, MatOther, 3)).Ok);
            Expect("공용 인벤 재료 생성 3", Send(ActorA, GrantMaterial(ContainerRef.PackRef, MatLimited, 3)).Ok);
            Expect("공용 인벤 개수", _master.Pack.Materials.GetCount(MatSmall) == 3 && _master.Pack.Materials.GetCount(MatLimited) == 3);
            ExpectSynced("재료 생성 후");

            var spawns = new List<GroundSpawn>
            {
                new GroundSpawn { MaterialId = MatSmall, Count = 3, Position = new Vector3(1, 0, 1) },
                new GroundSpawn { MaterialId = MatLimited, Count = 8, Position = new Vector3(2, 0, 2) },
                new GroundSpawn { MaterialId = 99999, Count = 1, Position = Vector3.zero },   // 모르는 재료는 건너뜀
            };

            Expect("바닥 주머니 생성", _master.TryCreateGroundSpawn(spawns, out InventoryCommand spawnApply) && Broadcast(spawnApply));
            Expect("바닥 주머니 2개(모르는 재료 제외)", _master.GroundDrops.Count == 2 && _clientA.GroundDrops.Count == 2 && _clientB.GroundDrops.Count == 2);
            Expect("주머니 위치 유지", _clientA.GroundDrops.TryGetValue(1, out GroundDrop drop) && drop.Position == new Vector3(1, 0, 1) && drop.Count == 3);
            ExpectSynced("바닥 주머니 생성 후");
        }

        private static void TestPickup()
        {
            // 두 명이 같은 주머니를 주우면 먼저 도착한 한 명만 성공한다.
            Res first = Send(ActorA, Transfer(ContainerRef.Ground(1), ContainerRef.PackRef));
            Res second = Send(ActorB, Transfer(ContainerRef.Ground(1), ContainerRef.PackRef));
            Expect("먼저 줍기 성공", first.Ok && first.Outcome.Value == 3);
            Expect("나중에 줍기 거부(NotFound)", !second.Ok && second.Reason == InventoryDenyReason.NotFound);
            Expect("공용 인벤 재료 증가", _master.Pack.Materials.GetCount(MatSmall) == 6);
            Expect("주머니 사라짐", !_master.GroundDrops.ContainsKey(1) && !_clientB.GroundDrops.ContainsKey(1));
            ExpectSynced("줍기 후");

            // 한도가 5 인 재료: 이미 3개가 있어 2개만 들어가고 6개는 바닥에 남는다.
            Res partial = Send(ActorA, Transfer(ContainerRef.Ground(2), ContainerRef.PackRef));
            Expect("부분 줍기 성공(2개)", partial.Ok && partial.Outcome.Value == 2);
            Expect("공용 인벤 한도까지", _master.Pack.Materials.GetCount(MatLimited) == 5);
            Expect("주머니에 6개 남음", _master.GroundDrops.TryGetValue(2, out GroundDrop remaining) && remaining.Count == 6);
            Res full = Send(ActorA, Transfer(ContainerRef.Ground(2), ContainerRef.PackRef));
            Expect("가득 차면 거부(Full)", !full.Ok && full.Reason == InventoryDenyReason.Full);
            ExpectSynced("부분 줍기 후");
        }

        private static void TestPackAndPersonal()
        {
            // 공용 인벤 -> 내 개인 인벤 (알아서 칸 고르기)
            Res toPersonal = Send(ActorA, Transfer(ContainerRef.PackRef, ContainerRef.Personal(ActorA), MatSmall, 4));
            Expect("공용 -> 개인 이동", toPersonal.Ok && toPersonal.Outcome.Value == 4);
            PersonalInventory mine = _master.GetPersonal(ActorA);
            Expect("개인 0번 칸에 재료", mine != null && mine.Slots[0].Kind == InventorySlotKind.Material && mine.Slots[0].Count == 4);
            Expect("공용 인벤 감소", _master.Pack.Materials.GetCount(MatSmall) == 2);

            // 남의 개인 인벤토리는 쓸 수 없다.
            Res stolen = Send(ActorB, Transfer(ContainerRef.PackRef, ContainerRef.Personal(ActorA), MatSmall, 1));
            Expect("남의 개인 인벤 거부(NoAccess)", !stolen.Ok && stolen.Reason == InventoryDenyReason.NoAccess);
            Res stolenFrom = Send(ActorB, Transfer(ContainerRef.Personal(ActorA, 0), ContainerRef.PackRef));
            Expect("남의 개인 인벤에서 꺼내기 거부", !stolenFrom.Ok && stolenFrom.Reason == InventoryDenyReason.NoAccess);

            // 개인 인벤에 포션을 만들어 넣고, 칸 이동(빈 칸으로, 서로 바꿈)
            Expect("개인 인벤 포션 생성", Send(ActorA, GrantPotion(ContainerRef.Personal(ActorA), PotionBase)).Ok);
            Expect("포션은 다음 빈 칸(1번)", mine.Slots[1].Kind == InventorySlotKind.Potion);

            Res toEmpty = Send(ActorA, Transfer(ContainerRef.Personal(ActorA, 0), ContainerRef.Personal(ActorA, 5)));
            Expect("빈 칸으로 이동", toEmpty.Ok && mine.Slots[0].IsEmpty && mine.Slots[5].Count == 4);
            Res swap = Send(ActorA, Transfer(ContainerRef.Personal(ActorA, 5), ContainerRef.Personal(ActorA, 1)));
            Expect("서로 바꿈", swap.Ok && mine.Slots[1].Kind == InventorySlotKind.Material && mine.Slots[5].Kind == InventorySlotKind.Potion);
            ExpectSynced("개인 인벤 이동 후");

            // 개인 -> 공용 (재료 일부만)
            Res back = Send(ActorA, Transfer(ContainerRef.Personal(ActorA, 1), ContainerRef.PackRef, 0, 1));
            Expect("개인 -> 공용 일부 이동", back.Ok && back.Outcome.Value == 1 && mine.Slots[1].Count == 3);
            Expect("공용 인벤 증가", _master.Pack.Materials.GetCount(MatSmall) == 3);

            // 한 번의 요청이 아니라 한 칸이 가득 차는 경우: 알아서 고르기는 같은 재료 칸을 먼저 채운다.
            Res merge = Send(ActorA, Transfer(ContainerRef.PackRef, ContainerRef.Personal(ActorA), MatSmall, 2));
            Expect("같은 재료 칸에 합침", merge.Ok && mine.Slots[1].Count == 5);
            ExpectSynced("개인 -> 공용 후");
        }

        private static void TestCraft()
        {
            // 개인 1번 칸에 재료(20001) 5개, 공용 인벤에 20002 가 3개 있다.
            PersonalInventory mine = _master.GetPersonal(ActorA);
            int otherBefore = _master.Pack.Materials.GetCount(MatOther);

            // 조합: 개인 칸의 재료 + 공용 인벤의 재료 -> 포션, 결과는 내 개인 인벤
            Res craft = Send(ActorA, Craft(PersonalSlot(ActorA, 1), PackMaterial(MatOther)));
            Expect("조합 성공", craft.Ok && craft.Outcome.Potion != null && craft.Outcome.Potion.InstanceId > 0);
            Expect("개인 재료 1 차감", mine.Slots[1].Count == 4);
            Expect("공용 재료 1 차감", _master.Pack.Materials.GetCount(MatOther) == otherBefore - 1);
            Expect("결과 포션이 개인 인벤에", craft.Ok && ContainsPotion(mine, craft.Outcome.Potion.InstanceId));
            ExpectSynced("조합 후");

            // 공용 20002 를 모두 써서 비운 뒤, 재료가 모자란 조합은 거부되고 아무것도 바뀌지 않는다. (20001 은 넉넉히 채워 둔다)
            Expect("공용 20001 보충", Send(ActorA, GrantMaterial(ContainerRef.PackRef, MatSmall, 30)).Ok);
            int guard = 0;
            while (_master.Pack.Materials.GetCount(MatOther) > 0 && guard++ < 20)
            {
                Send(ActorA, Craft(PackMaterial(MatSmall), PackMaterial(MatOther)));
            }

            Expect("공용 20002 가 비워짐", _master.Pack.Materials.GetCount(MatOther) == 0);

            string beforeFail = Digest(_master);
            Res empty = Send(ActorA, Craft(PackMaterial(MatSmall), PackMaterial(MatOther)));
            Expect("재료 부족 거부(NotEnough)", !empty.Ok && empty.Reason == InventoryDenyReason.NotEnough);
            Expect("거부되면 사본이 그대로", Digest(_master) == beforeFail);

            // 레시피가 없는 조합
            Res noRecipe = Send(ActorA, Craft(PackMaterial(MatSmall), PackMaterial(MatSmall)));
            Expect("레시피 없음 거부(Invalid)", !noRecipe.Ok && noRecipe.Reason == InventoryDenyReason.Invalid);

            // 재료와 포션을 섞을 수 없다.
            Res mixed = Send(ActorA, Craft(PackMaterial(MatSmall), PersonalSlot(ActorA, 5)));
            Expect("재료와 포션 혼합 거부", !mixed.Ok);

            // 함정 키워드가 붙은 조합
            Expect("공용 20002 보충", Send(ActorA, GrantMaterial(ContainerRef.PackRef, MatOther, 2)).Ok);
            Res trap = Send(ActorA, Craft(PackMaterial(MatSmall), PackMaterial(MatOther), 50001));
            Expect("함정 포션 조합", trap.Ok && trap.Outcome.Potion != null && trap.Outcome.Potion.TrapKeyword != null);
            Res badKeyword = Send(ActorA, Craft(PackMaterial(MatSmall), PackMaterial(MatOther), 60000));
            Expect("모르는 키워드 거부", !badKeyword.Ok && badKeyword.Reason == InventoryDenyReason.Invalid);
            ExpectSynced("조합 여러 번 후");

            // 포션 + 포션 (같은 포션 2개 -> 합친 포션): 공용 인벤에 포션 2개를 만든다.
            Send(ActorA, GrantPotion(ContainerRef.PackRef, PotionBase));
            Send(ActorA, GrantPotion(ContainerRef.PackRef, PotionBase));
            var packPotions = new List<PotionInstance>(_master.Pack.Potions.Items);
            int idA = packPotions[0].InstanceId;
            int idB = packPotions[1].InstanceId;
            Res upgrade = Send(ActorA, Craft(PackPotion(idA), PackPotion(idB)));
            Expect("포션 + 포션 조합", upgrade.Ok && upgrade.Outcome.Potion != null && upgrade.Outcome.Potion.Data.potionID == PotionUpgraded);
            Expect("재료 포션 2개 사라짐", !_master.Pack.Potions.Contains(idA) && !_master.Pack.Potions.Contains(idB));
            Res sameTwice = Send(ActorA, Craft(PackPotion(idA), PackPotion(idA)));
            Expect("없는 포션 거부", !sameTwice.Ok);

            // 개인 인벤이 가득 차면 결과는 공용 인벤으로 간다.
            FillPersonalWithPotions(ActorA);
            Expect("개인 인벤 가득", FindEmptySlotIndex(_master.GetPersonal(ActorA)) < 0);
            Expect("공용 20002 보충 2", Send(ActorA, GrantMaterial(ContainerRef.PackRef, MatOther, 1)).Ok);
            int packPotionsBefore = _master.Pack.Potions.Count;
            Res fallback = Send(ActorA, Craft(PackMaterial(MatSmall), PackMaterial(MatOther)));
            Expect("개인이 가득 차면 공용 인벤으로", fallback.Ok && _master.Pack.Potions.Count == packPotionsBefore + 1
                && _master.Pack.Potions.Contains(fallback.Outcome.Potion.InstanceId));
            ExpectSynced("조합 결과 자리 대체 후");
            CheckPotionIdsUnique("조합 후");
        }

        private static void TestStorageAndAccess()
        {
            // 창고는 베이스캠프 안에 있는 플레이어(여기서는 A 만)만 쓸 수 있다.
            _master.SetStorageAccessGate(actor => actor == ActorA);

            Expect("재료 보충", Send(ActorA, GrantMaterial(ContainerRef.PackRef, MatSmall, 10)).Ok);
            Res denied = Send(ActorB, Transfer(ContainerRef.PackRef, ContainerRef.StorageRef, MatSmall, 5));
            Expect("영역 밖 플레이어는 창고 거부(NoAccess)", !denied.Ok && denied.Reason == InventoryDenyReason.NoAccess);

            Res stored = Send(ActorA, Transfer(ContainerRef.PackRef, ContainerRef.StorageRef, MatSmall, 5));
            Expect("영역 안 플레이어는 창고에 넣기 성공", stored.Ok && _master.Storage.Materials.GetCount(MatSmall) == 5);

            Res fromStorage = Send(ActorB, Transfer(ContainerRef.StorageRef, ContainerRef.PackRef, MatSmall, 1));
            Expect("영역 밖에서 창고에서 꺼내기 거부", !fromStorage.Ok && fromStorage.Reason == InventoryDenyReason.NoAccess);

            // 창고 한도는 공용 인벤의 2 배(재료 99 -> 198)
            Expect("창고 한도 2배", _master.Storage.Materials.GetRoom(MatSmall) == 198 - 5);

            // 창고에서는 조합하지 않는다.
            Ingredient fromStore = new Ingredient { Source = ContainerRef.StorageRef, MaterialId = MatSmall };
            Res craftStorage = Send(ActorA, Craft(fromStore, PackMaterial(MatOther)));
            Expect("창고 재료로 조합 거부", !craftStorage.Ok && craftStorage.Reason == InventoryDenyReason.NoAccess);

            // 창고에 포션을 만들어 넣고, 인스턴스 ID 로 꺼낸다.
            Expect("창고 포션 생성", Send(ActorA, GrantPotion(ContainerRef.StorageRef, PotionBase)).Ok);
            int storedPotionId = _master.Storage.Potions.Items[0].InstanceId;
            Res takePotion = Send(ActorA, Transfer(ContainerRef.StorageRef, ContainerRef.PackRef, 0, 0, storedPotionId));
            Expect("창고 포션을 공용 인벤으로", takePotion.Ok && _master.Pack.Potions.Contains(storedPotionId) && _master.Storage.Potions.Count == 0);
            ExpectSynced("창고 시험 후");

            _master.SetStorageAccessGate(null);
        }

        private static void TestTakeOut()
        {
            _master.SetRandom(new System.Random(1234));

            // 공용 인벤에 포션을 넉넉히 만들고, 양손용으로 2개를 무작위로 꺼낸다.
            for (int i = 0; i < 3; i++)
            {
                Send(ActorA, GrantPotion(ContainerRef.PackRef, PotionBase));
            }

            int packBefore = _master.Pack.Potions.Count;
            Res two = Send(ActorA, new InventoryCommand { Op = InventoryOp.TakeOut, Count = 2, From = new ContainerRef { Slot = -1 } });
            Expect("무작위 꺼내기 2개", two.Ok && two.Outcome.Potions != null && two.Outcome.Potions.Count == 2);
            Expect("공용 인벤에서 2개 줄어듦", _master.Pack.Potions.Count == packBefore - 2 && _clientB.Pack.Potions.Count == packBefore - 2);

            if (two.Ok && two.Outcome.Potions != null && two.Outcome.Potions.Count == 2)
            {
                int first = two.Outcome.Potions[0].InstanceId;
                int second = two.Outcome.Potions[1].InstanceId;
                Expect("꺼낸 포션은 서로 다르고 인벤토리에서 빠짐",
                    first > 0 && second > 0 && first != second && !_master.Pack.Potions.Contains(first) && !_clientA.Pack.Potions.Contains(second));
            }

            ExpectSynced("무작위 꺼내기 후");

            Res zero = Send(ActorA, new InventoryCommand { Op = InventoryOp.TakeOut, Count = 0, From = new ContainerRef { Slot = -1 } });
            Res three = Send(ActorA, new InventoryCommand { Op = InventoryOp.TakeOut, Count = 3, From = new ContainerRef { Slot = -1 } });
            Expect("개수 0 또는 3 은 거부(Invalid)", !zero.Ok && zero.Reason == InventoryDenyReason.Invalid && !three.Ok && three.Reason == InventoryDenyReason.Invalid);

            // 퀵슬롯: 개인 인벤의 정확한 칸에서 꺼낸다.
            Send(ActorA, GrantPotion(ContainerRef.Personal(ActorA), PotionBase));
            PersonalInventory mine = _master.GetPersonal(ActorA);
            int potionSlot = -1;
            int materialSlot = -1;
            for (int i = 0; i < mine.Capacity; i++)
            {
                if (potionSlot < 0 && mine.Slots[i].Kind == InventorySlotKind.Potion)
                {
                    potionSlot = i;
                }

                if (materialSlot < 0 && mine.Slots[i].Kind == InventorySlotKind.Material)
                {
                    materialSlot = i;
                }
            }

            Expect("퀵슬롯 시험 준비(포션 칸 있음)", potionSlot >= 0);

            if (potionSlot >= 0)
            {
                int expectedId = mine.Slots[potionSlot].Potion.InstanceId;
                Res quick = Send(ActorA, new InventoryCommand { Op = InventoryOp.TakeOut, Count = 1, From = new ContainerRef { Slot = potionSlot } });
                Expect("퀵슬롯 꺼내기", quick.Ok && quick.Outcome.Potions != null && quick.Outcome.Potions.Count == 1 && quick.Outcome.Potions[0].InstanceId == expectedId);
                Expect("그 칸이 비워짐", _master.GetPersonal(ActorA).Slots[potionSlot].IsEmpty && _clientB.GetPersonal(ActorA).Slots[potionSlot].IsEmpty);

                Res empty = Send(ActorA, new InventoryCommand { Op = InventoryOp.TakeOut, Count = 1, From = new ContainerRef { Slot = potionSlot } });
                Expect("빈 퀵슬롯은 거부(SlotEmpty)", !empty.Ok && empty.Reason == InventoryDenyReason.SlotEmpty);
            }

            if (materialSlot >= 0)
            {
                Res material = Send(ActorA, new InventoryCommand { Op = InventoryOp.TakeOut, Count = 1, From = new ContainerRef { Slot = materialSlot } });
                Expect("재료 칸은 거부(Invalid)", !material.Ok && material.Reason == InventoryDenyReason.Invalid);
            }

            Res outOfRange = Send(ActorA, new InventoryCommand { Op = InventoryOp.TakeOut, Count = 1, From = new ContainerRef { Slot = 10 } });
            Expect("범위 밖 칸은 거부(Invalid)", !outOfRange.Ok && outOfRange.Reason == InventoryDenyReason.Invalid);
            ExpectSynced("퀵슬롯 꺼내기 후");

            // 공용 인벤의 포션을 모두 꺼낸 뒤에는 개인 인벤에서 꺼낸다.
            int guard = 0;
            while (_master.Pack.Potions.Count > 0 && guard++ < 200)
            {
                Send(ActorA, new InventoryCommand { Op = InventoryOp.TakeOut, Count = 2, From = new ContainerRef { Slot = -1 } });
            }

            Expect("공용 인벤 포션이 모두 빠짐", _master.Pack.Potions.Count == 0);

            Send(ActorA, GrantPotion(ContainerRef.Personal(ActorA), PotionBase));
            Res fallback = Send(ActorA, new InventoryCommand { Op = InventoryOp.TakeOut, Count = 2, From = new ContainerRef { Slot = -1 } });
            Expect("공용 인벤이 비면 개인 인벤에서 꺼냄", fallback.Ok && fallback.Outcome.Potions != null && fallback.Outcome.Potions.Count >= 1);

            // 개인 인벤의 포션까지 모두 꺼내면 더 꺼낼 것이 없다.
            guard = 0;
            Res last = default;
            do
            {
                last = Send(ActorA, new InventoryCommand { Op = InventoryOp.TakeOut, Count = 2, From = new ContainerRef { Slot = -1 } });
            }
            while (last.Ok && guard++ < 50);

            Expect("포션이 없으면 NotFound", !last.Ok && last.Reason == InventoryDenyReason.NotFound);
            ExpectSynced("개인 인벤 꺼내기 후");

            // 뒤 시험이 쓰도록 개인 인벤에 포션을 하나 되돌려 둔다.
            Send(ActorA, GrantPotion(ContainerRef.Personal(ActorA), PotionBase));
            _master.SetRandom(null);
        }

        private static void TestWorldGrant()
        {
            // 상호작용으로 얻은 월드 오브젝트의 재료는 공용 인벤에 바로 들어가고, 한도를 넘는 만큼만 바닥 주머니가 된다.
            int smallBefore = _master.Pack.Materials.GetCount(MatSmall);
            int smallRoom = _master.Pack.Materials.GetRoom(MatSmall);
            int groundBefore = _master.GroundDrops.Count;

            var spawn = new Vector3(3, 0, 4);
            var fit = new List<GroundSpawn> { new GroundSpawn { MaterialId = MatSmall, Count = 1, Position = spawn } };

            if (smallRoom >= 1)
            {
                Expect("월드 재료 지급 변경 생성", _master.TryCreateWorldGrant(fit, out InventoryCommand grant) && Broadcast(grant));
                Expect("공용 인벤에 바로 들어감", _master.Pack.Materials.GetCount(MatSmall) == smallBefore + 1 && _clientB.Pack.Materials.GetCount(MatSmall) == smallBefore + 1);
                Expect("한도 안이면 주머니가 생기지 않음", _master.GroundDrops.Count == groundBefore);
            }

            // 한도를 넘는 만큼은 지정한 위치에 주머니로 남는다. (한도 5 인 재료로 시험)
            int limitedRoom = _master.Pack.Materials.GetRoom(MatLimited);
            int limitedBefore = _master.Pack.Materials.GetCount(MatLimited);
            var over = new List<GroundSpawn> { new GroundSpawn { MaterialId = MatLimited, Count = limitedRoom + 2, Position = spawn } };

            Expect("넘치는 월드 재료 지급 변경 생성", _master.TryCreateWorldGrant(over, out InventoryCommand overGrant) && Broadcast(overGrant));
            Expect("한도까지만 들어감", _master.Pack.Materials.GetRoom(MatLimited) == 0 && _clientA.Pack.Materials.GetCount(MatLimited) == limitedBefore + limitedRoom);

            bool foundOverflow = false;
            foreach (KeyValuePair<int, GroundDrop> entry in _master.GroundDrops)
            {
                if (entry.Value.MaterialId == MatLimited && entry.Value.Count == 2 && entry.Value.Position == spawn)
                {
                    foundOverflow = true;
                }
            }

            Expect("넘친 2개는 그 위치의 주머니", foundOverflow && _clientB.GroundDrops.Count == _master.GroundDrops.Count);

            // 알 수 없는 재료나 0 개는 변경이 없다.
            var bad = new List<GroundSpawn>
            {
                new GroundSpawn { MaterialId = 99999, Count = 1, Position = spawn },
                new GroundSpawn { MaterialId = MatSmall, Count = 0, Position = spawn },
            };
            Expect("알 수 없는 재료와 0 개는 거부", !_master.TryCreateWorldGrant(bad, out InventoryCommand _));
            ExpectSynced("월드 재료 지급 후");
        }

        private static void TestDeathAndLeave()
        {
            PersonalInventory mine = _master.GetPersonal(ActorA);
            int packBefore = _master.Pack.Materials.GetCount(MatSmall);
            int packPotionsBefore = _master.Pack.Potions.Count;
            int itemCount = CountItems(mine);

            Res die = Send(ActorA, new InventoryCommand { Op = InventoryOp.Die, Position = new Vector3(5, 0, 5) });
            Expect("사망 -> 시체 상자", die.Ok && die.Outcome.CorpseId > 0 && _master.Corpses.ContainsKey(die.Outcome.CorpseId));
            Expect("개인 인벤이 비워짐", CountItems(_master.GetPersonal(ActorA)) == 0);
            Expect("시체 상자에 모두 옮겨짐", CountItems(_master.Corpses[die.Outcome.CorpseId].Slots) == itemCount);
            Expect("공용 인벤은 그대로", _master.Pack.Materials.GetCount(MatSmall) == packBefore && _master.Pack.Potions.Count == packPotionsBefore);
            Expect("시체 위치", _clientB.Corpses[die.Outcome.CorpseId].Position == new Vector3(5, 0, 5));
            ExpectSynced("사망 후");

            Res dieAgain = Send(ActorA, new InventoryCommand { Op = InventoryOp.Die, Position = Vector3.zero });
            Expect("남길 것이 없으면 거부(Invalid)", !dieAgain.Ok && dieAgain.Reason == InventoryDenyReason.Invalid);

            // B 가 시체 상자에서 칸을 꺼내 자기 개인 인벤으로 가져간다. 다 꺼내면 상자가 사라진다.
            int corpseId = die.Outcome.CorpseId;
            int guard = 0;
            while (_master.Corpses.ContainsKey(corpseId) && guard++ < 20)
            {
                CorpseBox box = _master.Corpses[corpseId];
                int slot = -1;
                for (int i = 0; i < box.Slots.Capacity; i++)
                {
                    if (!box.Slots.Slots[i].IsEmpty)
                    {
                        slot = i;
                        break;
                    }
                }

                Res take = Send(ActorB, Transfer(ContainerRef.Corpse(corpseId, slot), ContainerRef.Personal(ActorB)));
                Expect("시체 상자에서 꺼내기", take.Ok);
                if (!take.Ok)
                {
                    break;
                }
            }

            Expect("다 꺼내면 시체 상자 사라짐", !_master.Corpses.ContainsKey(corpseId) && !_clientA.Corpses.ContainsKey(corpseId));
            Expect("B 가 모두 받음", CountItems(_master.GetPersonal(ActorB)) >= itemCount - 1);
            ExpectSynced("시체 상자 줍기 후");

            // B 가 방을 나가면 B 의 개인 인벤토리는 시체 상자가 되고 사라진다.
            Expect("방 나가기 변경 생성", _master.TryCreateLeave(ActorB, new Vector3(9, 0, 9), out InventoryCommand leave) && Broadcast(leave));
            Expect("나간 플레이어의 개인 인벤 제거", _master.GetPersonal(ActorB) == null && _clientA.GetPersonal(ActorB) == null);
            Expect("나간 플레이어의 시체 상자", _master.Corpses.Count == 1 && _clientA.Corpses.Count == 1);
            ExpectSynced("방 나가기 후");
        }

        private static void TestVersionAndSnapshot()
        {
            // 같은 적용을 다시 받으면 무시한다.
            Expect("재료 생성", Send(ActorA, GrantMaterial(ContainerRef.PackRef, MatOther, 1)).Ok);
            Expect("마지막 적용 풀기", InventoryMessages.TryUnpackApply(_lastApplyBytes, out InventoryCommand again));
            InventoryApplyStatus ignored = _clientA.Apply(again, out InventoryOutcome _);
            Expect("이미 적용한 메시지는 무시(Ignored)", ignored == InventoryApplyStatus.Ignored);

            // 번호가 건너뛰면 VersionGap
            InventoryCommand gap = again;
            gap.Version = _clientA.Version + 2;
            Expect("번호가 건너뛰면 VersionGap", _clientA.Apply(gap, out InventoryOutcome _) == InventoryApplyStatus.VersionGap);
            Expect("VersionGap 은 사본을 바꾸지 않음", Digest(_clientA) == Digest(_master));

            // 스냅샷: 늦게 들어온 사람이 전체 목록을 받아 같은 상태가 된다.
            var late = new TeamInventoryState(_data);
            Expect("스냅샷 읽기", late.TryLoadSnapshot(_master.CreateSnapshot()));
            Expect("스냅샷 후 같은 상태", Digest(late) == Digest(_master));

            // 깨진 스냅샷은 기존 상태를 바꾸지 않는다.
            string lateBefore = Digest(late);
            byte[] broken = _master.CreateSnapshot();
            Array.Resize(ref broken, broken.Length - 3);
            Expect("깨진 스냅샷은 거부", !late.TryLoadSnapshot(broken));
            Expect("거부된 스냅샷은 사본을 바꾸지 않음", Digest(late) == lateBefore);

            // 요청 직렬화: 깨진 요청은 풀리지 않는다.
            byte[] request = InventoryMessages.PackRequest(Transfer(ContainerRef.PackRef, ContainerRef.Personal(ActorA), MatSmall, 1));
            Array.Resize(ref request, request.Length - 1);
            Expect("깨진 요청은 거부", !InventoryMessages.TryUnpackRequest(request, out InventoryCommand _));
        }

        // ==================== 요청 흐름 흉내 ============================

        private static Res Send(int actor, InventoryCommand command)
        {
            return Send(actor, command, out _);
        }

        /// <summary>
        /// 요청 한 건을 실제 흐름대로 처리합니다. 요청을 byte 로 묶고 풀어서 마스터가 검증하고, 적용도 byte 로 묶고 풀어서 모두가 적용합니다.
        /// </summary>
        private static Res Send(int actor, InventoryCommand command, out string note)
        {
            note = "";
            command.RequestId = _nextRequestId++;

            byte[] requestBytes = InventoryMessages.PackRequest(command);
            if (!InventoryMessages.TryUnpackRequest(requestBytes, out InventoryCommand request))
            {
                Fail($"요청 직렬화 왕복 실패 ({command.Op})");
                return new Res { Ok = false, Reason = InventoryDenyReason.Invalid };
            }

            if (!_master.TryValidate(request, actor, out InventoryCommand apply, out InventoryDenyReason reason))
            {
                return new Res { Ok = false, Reason = reason };
            }

            InventoryOutcome requesterOutcome = default;
            bool applied = BroadcastWithOutcome(apply, actor, out requesterOutcome);
            return new Res { Ok = applied, Reason = applied ? InventoryDenyReason.None : InventoryDenyReason.OutOfSync, Outcome = requesterOutcome };
        }

        /// <summary>
        /// 마스터가 만든 적용을 byte 로 묶고 풀어서 모든 사본에 적용합니다.
        /// </summary>
        private static bool Broadcast(InventoryCommand apply)
        {
            return BroadcastWithOutcome(apply, -1, out InventoryOutcome _);
        }

        private static bool BroadcastWithOutcome(InventoryCommand apply, int requesterActor, out InventoryOutcome requesterOutcome)
        {
            requesterOutcome = default;

            byte[] bytes = InventoryMessages.PackApply(apply);
            _lastApplyBytes = bytes;

            if (!InventoryMessages.TryUnpackApply(bytes, out InventoryCommand received))
            {
                Fail($"적용 직렬화 왕복 실패 ({apply.Op})");
                return false;
            }

            bool allApplied = true;

            for (int i = 0; i < _all.Length; i++)
            {
                InventoryApplyStatus status = _all[i].Apply(received, out InventoryOutcome outcome);

                if (status != InventoryApplyStatus.Applied)
                {
                    allApplied = false;
                    Fail($"적용 실패 : 사본 {i}, 상태 {status}, 종류 {apply.Op}");
                }

                // 요청한 사람의 결과를 돌려줍니다. (마스터 사본을 기준으로 봅니다)
                if (i == 0)
                {
                    requesterOutcome = outcome;
                }
            }

            return allApplied;
        }

        // ==================== 도우미 ============================

        private static InventoryCommand GrantMaterial(ContainerRef to, int materialId, int count)
        {
            return new InventoryCommand { Op = InventoryOp.Grant, To = to, MaterialId = materialId, Count = count };
        }

        private static InventoryCommand GrantPotion(ContainerRef to, int potionId)
        {
            return new InventoryCommand { Op = InventoryOp.Grant, To = to, PotionId = potionId };
        }

        private static InventoryCommand Transfer(ContainerRef from, ContainerRef to, int materialId = 0, int count = 0, int instanceId = 0)
        {
            return new InventoryCommand { Op = InventoryOp.Transfer, From = from, To = to, MaterialId = materialId, Count = count, InstanceId = instanceId };
        }

        private static InventoryCommand Craft(Ingredient a, Ingredient b, int keywordId = 0)
        {
            return new InventoryCommand { Op = InventoryOp.Craft, IngredientA = a, IngredientB = b, KeywordId = keywordId };
        }

        private static Ingredient PackMaterial(int materialId)
        {
            return new Ingredient { Source = ContainerRef.PackRef, MaterialId = materialId };
        }

        private static Ingredient PackPotion(int instanceId)
        {
            return new Ingredient { Source = ContainerRef.PackRef, InstanceId = instanceId };
        }

        private static Ingredient PersonalSlot(int actor, int slot)
        {
            return new Ingredient { Source = ContainerRef.Personal(actor, slot) };
        }

        private static bool ContainsPotion(PersonalInventory inventory, int instanceId)
        {
            for (int i = 0; i < inventory.Capacity; i++)
            {
                if (inventory.Slots[i].Kind == InventorySlotKind.Potion && inventory.Slots[i].Potion.InstanceId == instanceId)
                {
                    return true;
                }
            }

            return false;
        }

        private static int FindEmptySlotIndex(PersonalInventory inventory)
        {
            for (int i = 0; i < inventory.Capacity; i++)
            {
                if (inventory.Slots[i].IsEmpty)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int CountItems(PersonalInventory inventory)
        {
            int count = 0;

            if (inventory == null)
            {
                return 0;
            }

            for (int i = 0; i < inventory.Capacity; i++)
            {
                if (!inventory.Slots[i].IsEmpty)
                {
                    count++;
                }
            }

            return count;
        }

        private static void FillPersonalWithPotions(int actor)
        {
            int guard = 0;

            while (FindEmptySlotIndex(_master.GetPersonal(actor)) >= 0 && guard++ < 20)
            {
                Send(actor, GrantPotion(ContainerRef.Personal(actor), PotionBase));
            }
        }

        /// <summary>
        /// 모든 사본(마스터, 클라이언트 둘)의 상태를 글자로 만들어 비교합니다. 사본이 하나라도 다르면 실패합니다.
        /// </summary>
        private static void ExpectSynced(string label)
        {
            string reference = Digest(_master);

            for (int i = 1; i < _all.Length; i++)
            {
                _checks++;

                if (Digest(_all[i]) != reference)
                {
                    Fail($"사본 불일치 ({label}) : 사본 {i}\n   마스터 : {reference}\n   사본 {i} : {Digest(_all[i])}");
                }
            }
        }

        private static void CheckPotionIdsUnique(string label)
        {
            var seen = new HashSet<int>();
            bool unique = true;

            void Visit(PotionInstance potion)
            {
                if (potion != null && !seen.Add(potion.InstanceId))
                {
                    unique = false;
                }
            }

            foreach (PotionInstance potion in _master.Pack.Potions.Items) Visit(potion);
            foreach (PotionInstance potion in _master.Storage.Potions.Items) Visit(potion);

            foreach (KeyValuePair<int, PersonalInventory> entry in _master.Personals)
            {
                for (int i = 0; i < entry.Value.Capacity; i++)
                {
                    if (entry.Value.Slots[i].Kind == InventorySlotKind.Potion) Visit(entry.Value.Slots[i].Potion);
                }
            }

            foreach (KeyValuePair<int, CorpseBox> entry in _master.Corpses)
            {
                for (int i = 0; i < entry.Value.Slots.Capacity; i++)
                {
                    if (entry.Value.Slots.Slots[i].Kind == InventorySlotKind.Potion) Visit(entry.Value.Slots.Slots[i].Potion);
                }
            }

            Expect($"포션 인스턴스 ID 중복 없음 ({label})", unique);
        }

        private static string Digest(TeamInventoryState state)
        {
            var sb = new StringBuilder();
            sb.Append("v").Append(state.Version).Append(" | pack ").Append(StoreDigest(state.Pack));
            sb.Append(" | storage ").Append(StoreDigest(state.Storage));

            var actors = new List<int>(state.Personals.Keys);
            actors.Sort();
            sb.Append(" | personal");
            foreach (int actor in actors)
            {
                sb.Append(" [").Append(actor).Append(":").Append(SlotsDigest(state.Personals[actor])).Append("]");
            }

            var corpseIds = new List<int>(state.Corpses.Keys);
            corpseIds.Sort();
            sb.Append(" | corpse");
            foreach (int id in corpseIds)
            {
                CorpseBox box = state.Corpses[id];
                sb.Append(" [").Append(id).Append("@").Append(box.Position).Append(":").Append(SlotsDigest(box.Slots)).Append("]");
            }

            var dropIds = new List<int>(state.GroundDrops.Keys);
            dropIds.Sort();
            sb.Append(" | ground");
            foreach (int id in dropIds)
            {
                GroundDrop drop = state.GroundDrops[id];
                sb.Append(" [").Append(id).Append(":").Append(drop.MaterialId).Append("x").Append(drop.Count).Append("@").Append(drop.Position).Append("]");
            }

            return sb.ToString();
        }

        private static string StoreDigest(ItemStore store)
        {
            var ids = new List<int>(store.Materials.Counts.Keys);
            ids.Sort();

            var sb = new StringBuilder("m{");
            foreach (int id in ids)
            {
                sb.Append(id).Append("=").Append(store.Materials.Counts[id]).Append(",");
            }

            sb.Append("} p{");
            foreach (PotionInstance potion in store.Potions.Items)
            {
                sb.Append(potion.InstanceId).Append(":").Append(potion.Data.potionID).Append(":").Append(potion.TrapKeyword != null ? potion.TrapKeyword.KeywordID : 0).Append(",");
            }

            return sb.Append("}").ToString();
        }

        private static string SlotsDigest(PersonalInventory inventory)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < inventory.Capacity; i++)
            {
                InventorySlot slot = inventory.Slots[i];

                if (slot.IsEmpty)
                {
                    sb.Append("_");
                }
                else if (slot.Kind == InventorySlotKind.Material)
                {
                    sb.Append("M").Append(slot.MaterialId).Append("x").Append(slot.Count);
                }
                else
                {
                    sb.Append("P").Append(slot.Potion.InstanceId).Append(":").Append(slot.Potion.Data.potionID).Append(":")
                      .Append(slot.Potion.TrapKeyword != null ? slot.Potion.TrapKeyword.KeywordID : 0);
                }

                sb.Append(" ");
            }

            return sb.ToString();
        }

        private static void Expect(string label, bool condition)
        {
            _checks++;

            if (!condition)
            {
                Fail(label);
            }
        }

        private static void Fail(string message)
        {
            _failures.Add(message);
        }
    }
}
#endif
