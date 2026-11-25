using System;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// 세이브 슬롯 파일 읽기/쓰기/삭제
/// 슬롯 번호는 0~SLOT_COUNT -1
/// </summary>
public static class SaveStore
{
    public const int SLOT_COUNT = 3;

    // 슬롯 유효 여부
    public static bool IsValidSlot(int slot)
    {
        return slot >= 0 && slot < SLOT_COUNT;
    }

    // 슬롯 파일 위치
    private static string GetPath(int slot)
    {
        return Path.Combine(Application.persistentDataPath, $"save_slot{slot}.json");
    }

    // 슬롯 파일 존재 여부, 슬롯 UI에서 빈 슬롯 표시
    public static bool Exists(int slot)
    {
        return IsValidSlot(slot) && File.Exists(GetPath(slot));
    }

    /// <summary>
    /// 저장(덮어쓰기) / 임시 파일에 다 쓴뒤 교체 -> 쓰다가 튕겨도 기존파일 안전하도록
    /// 수동 저장 및 체크포인트에서 사용
    /// </summary>
    public static bool Save(int slot, SaveData data)
    {
        if (!IsValidSlot(slot) || data == null)
            return false;

        string path = GetPath(slot);
        string temp = path + ".tmp";

        try
        {
            data.Version = SaveData.CURRENT_VERSION;
            data.SavedAt = DateTime.Now;        // JsonUtility는 DateTime을 저장하지 못함 따라서 Newtonsoft 사용
            File.WriteAllText(temp, JsonConvert.SerializeObject(data, Formatting.Indented));        // temp 경로의 파일에 Json형식의 텍스트로 직렬화 저장

            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveStore] {slot}번 슬롯 저장 실패 : {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 불러오기 / 파일이 없거나 깨졌거나 버전다르면 false
    /// 이어하기에서 사용
    /// </summary>
    public static bool TryLoad(int slot, out SaveData data)
    {
        data = null;

        if (!Exists(slot))
            return false;

        try
        {
            // Json파일을 SaveData로 변환 후 data 변수에 저장
            data = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(GetPath(slot)));
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveStore] {slot}번 슬롯 불러오기 실패 : {e.Message}");
            data = null;
            return false;
        }

        if (data == null || data.Version != SaveData.CURRENT_VERSION)
        {
            Debug.LogWarning($"[SaveStore] {slot}번 슬롯 버전 불일치 또는 비어 있음, 무시");
            data = null;
            return false;
        }

        return true;
    }

    // 슬롯 파일 삭제 (클리어 또는 수동 삭제)
    public static void Delete(int slot)
    {
        if (!Exists(slot))
            return;

        try
        {
            File.Delete(GetPath(slot));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveStore] {slot}번 슬롯 삭제 실패 : {e.Message}");
        }
    }
}
