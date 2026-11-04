using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateRoomPopupUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_Dropdown playerCountDropdown;
    [SerializeField] private Toggle privateToggle;
    [SerializeField] private Button confirmBtn;
    [SerializeField] private Button cancelBtn;


    private NetworkFlowManager manager;
    private int SelectedMaxPlayers => RoomRules.MIN_PLAYERS + playerCountDropdown.value; // 드롭다운 인덱스번호(0부터) + minPlayer 1

    private void Awake()
    {
        manager = NetworkFlowManager.Instance;

        if (manager == null)
        {
            Debug.LogError("[CreateRoomPopupUI] NetworkFlowManager 없음");
            return;
        }

        BuildPlayerCountOptions();

        confirmBtn.onClick.AddListener(OnClickConfirm);
        cancelBtn.onClick.AddListener(Close);
    }


    public void Open()
    {
        // 먼저 켜야 Awake(선택지 생성)가 실행된 뒤에 값을 초기화할 수 있음
        gameObject.SetActive(true);

        nameInput.text = "";
        playerCountDropdown.SetValueWithoutNotify(RoomRules.MAX_PLAYERS - RoomRules.MIN_PLAYERS);
        privateToggle.isOn = false;
    }

    private void BuildPlayerCountOptions()
    {
        List<string> options = new List<string>();

        for (int i = RoomRules.MIN_PLAYERS; i <= RoomRules.MAX_PLAYERS; i++)
            options.Add($"{i}명");

        playerCountDropdown.ClearOptions();
        playerCountDropdown.AddOptions(options);
    }

    private void OnClickConfirm()
    {
        if (manager == null)
        {
            Debug.LogError("[CreateRoomPopupUI] NetworkFlowManager 없음");
            return;
        }

        // 이름 공백 처리·기본 이름은 매니저(CreateRoomInternal)가 담당
        manager.RequestCreateRoom(nameInput.text, SelectedMaxPlayers, !privateToggle.isOn);
        Close();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}

