using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

// 테스트 전용: 키 입력으로 FlowState 전환 / 팝업 테스트
// 1~5: Title, Connecting, Lobby, Room, InGame / P: 테스트 팝업
public class DebugStateSwitcher : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Update()
    {
        NetworkFlowManager manager = NetworkFlowManager.Instance;
        Keyboard keyboard = Keyboard.current;
        if (manager == null || keyboard == null) return;

        if (keyboard.digit1Key.wasPressedThisFrame) manager.Debug_SetState(FlowState.Title);
        if (keyboard.digit2Key.wasPressedThisFrame) manager.Debug_SetState(FlowState.Connecting);
        if (keyboard.digit3Key.wasPressedThisFrame) manager.Debug_SetState(FlowState.Lobby);
        if (keyboard.digit4Key.wasPressedThisFrame) manager.Debug_SetState(FlowState.Room);
        if (keyboard.digit5Key.wasPressedThisFrame) manager.Debug_SetState(FlowState.InGame);
        if (keyboard.pKey.wasPressedThisFrame) manager.Debug_RaiseError("Test Popup");

        if (keyboard.aKey.wasPressedThisFrame) manager.RequestCreateRoom("", 5, true);
        if (keyboard.bKey.wasPressedThisFrame) manager.RequestCreateRoom("테스트방", 3, false);
        if (keyboard.cKey.wasPressedThisFrame) JoinFirstRoom(manager);
        if (keyboard.dKey.wasPressedThisFrame) manager.RequestLeaveRoom();

        if (keyboard.eKey.wasPressedThisFrame) manager.RequestJoinByCode("zzzzzz");
        if (keyboard.fKey.wasPressedThisFrame) manager.RequestQuickMatch();

    }

    private void JoinFirstRoom(NetworkFlowManager manager)
    {
        var rooms = manager.RoomList.CurrentValue;

        if (rooms.Count == 0)
        {
            Debug.Log("[DebugStateSwitcher] 입장할 방이 없음");
            return;
        }

        manager.RequestJoinRoom(rooms[0].Name);
    }
#endif
}