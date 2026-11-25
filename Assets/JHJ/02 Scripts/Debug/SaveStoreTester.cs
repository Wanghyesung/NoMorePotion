using UnityEngine;

/// <summary> SaveStore 단독 테스트. 컴포넌트 우클릭(⋮) 메뉴로 실행 </summary>
public class SaveStoreTester : MonoBehaviour
{
    [SerializeField] private int slot = 0;

    [ContextMenu("저장 테스트")]
    private void TestSave()
    {
        var data = new SaveData
        {
            Clock = new TimeState { DayCount = 3, Phase = TimePhase.Day, Elapsed = 4.5f, TotalElapsed = 64.5f }
        };

        bool ok = SaveStore.Save(slot, data);
        Debug.Log($"[SaveStoreTester] 저장 {(ok ? "성공" : "실패")} → {Application.persistentDataPath}");
    }

    [ContextMenu("불러오기 테스트")]
    private void TestLoad()
    {
        if (SaveStore.TryLoad(slot, out SaveData data))
            Debug.Log($"[SaveStoreTester] 불러옴 : {data.Clock.DayCount}일차 {data.Clock.Phase}, 총 {data.Clock.TotalElapsed}초 (저장 {data.SavedAt})");
        else
            Debug.Log($"[SaveStoreTester] {slot}번 슬롯 없음");
    }

    [ContextMenu("삭제 테스트")]
    private void TestDelete()
    {
        SaveStore.Delete(slot);
        Debug.Log($"[SaveStoreTester] 삭제 후 존재 여부 : {SaveStore.Exists(slot)}");
    }
}