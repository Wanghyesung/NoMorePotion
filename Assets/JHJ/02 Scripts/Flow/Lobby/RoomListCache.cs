using System.Collections.Generic;
using Photon.Realtime;

/// <summary>
/// 로비 방 목록 캐시
/// OnRoomListUpdate에서 변경된 방만 전달, 전체 목록을 방 이름 기준으로 유지
/// </summary>
public class RoomListCache 
{
    private Dictionary<string, RoomInfo> rooms = new Dictionary<string, RoomInfo>();


    /// <summary>
    /// 변경된 방 목록을 캐시에 반영
    /// </summary>
    public void Apply(List<RoomInfo> changedList)
    {
        foreach (var room in changedList)
        {
            if(room.RemovedFromList)
                rooms.Remove(room.Name);
            else
                rooms[room.Name] = room; 
        }
    }

    /// <summary>
    /// 캐시 비움
    /// 로비 입장이나 연결 끊김 시 호출
    /// </summary>
    public void Clear()
    {
        rooms.Clear();
    }

    /// <summary>
    /// 현재 캐시된 방 목록을 새 List로 반환
    /// ReactiveProperty는 같은 객체를 다시 넣으면 알림을 보내지 않음 새 인스턴스 만들어야 (빈도가 낮음..!)
    /// </summary>
    public List<RoomInfo> ToList()
    {
        return new List<RoomInfo>(rooms.Values);
    }
}
