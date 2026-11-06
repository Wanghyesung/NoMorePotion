
using Photon.Realtime;

/// <summary>
/// Photon 방 커스텀 속성의 키 모음
/// 같은 키를 여러 곳에서 쓰기 때문에 문자열을 직접 사용하지 않고 이 상수를 사용함
/// (생성, 목록 UI, 대기실 UI, 설정 변경 등)
/// </summary>
/// <remarks>
/// - 키는 네트워크로 전송되므로 짧게 유지한다.
/// - 새 키를 추가할 때 기존 키와 겹치지 않는지 확인한다.
/// - 로비 목록에 보여야 하는 키는 방 생성 시 CustomRoomPropertiesForLobby에도 등록해야 한다.
/// - 플레이어별 값(레디, 색 등)은 여기가 아니라 PlayerProps에 둔다.
/// </remarks>
public static class RoomProps 
{
    public const string DISPLAY_NAME = "dn";


    public static string GetDisplayName(RoomInfo room)
    {
        if (room.CustomProperties.TryGetValue(DISPLAY_NAME, out object value) && value is string name && !string.IsNullOrEmpty(name))
        {
            return name;
        }

        return room.Name;
    }
}
