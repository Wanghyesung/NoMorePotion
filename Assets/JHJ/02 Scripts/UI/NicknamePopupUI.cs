using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NicknamePopupUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_Text placeholderText;
    [SerializeField] private Button saveBtn;
    [SerializeField] private Button cancelBtn;


    private void Awake()
    {
        nameInput.characterLimit = NicknameStore.MAX_LENGTH;
        placeholderText.text = $"{NicknameStore.MAX_LENGTH}자 이내";
        saveBtn.onClick.AddListener(OnClickSave);
        cancelBtn.onClick.AddListener(Close);
    }

    public void Open(string nickname, bool canCancel)
    {
        gameObject.SetActive(true);

        nameInput.text = nickname;
        cancelBtn.gameObject.SetActive(canCancel);
    }

    private void OnClickSave()
    {

        if (NetworkFlowManager.Instance == null)
        {
            Debug.LogError("[NicknamePopupUI] NetworkFlowManager 없음");
            return;
        }

        if (NetworkFlowManager.Instance.RequestChangeNickname(nameInput.text))
            Close();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
