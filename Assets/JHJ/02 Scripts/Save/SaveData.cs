using System;

/// <summary>
/// 세이브 슬롯 한개에 들어가는 묶음 /  로직없이 값만
/// 저장할 항목이 생기면 필드 추가
/// </summary>
[Serializable]
public class SaveData
{
    public const int CURRENT_VERSION = 1;

    public int Version = CURRENT_VERSION;   // 예전 파일 걸러내는 용도로 버전 필드 추가
    public DateTime SavedAt;    // 슬롯 UI 표시용
    public TimeState Clock;     // 게임 내 일차, 낮/밤, 경과 시간
    public int WorldSeed;       // 월드 배치 시드, 0 = 없음
    public string WorldObjects;  // 월드오브젝트NetState 스냅샷 -> Base64로 바꾼 문자열
}
