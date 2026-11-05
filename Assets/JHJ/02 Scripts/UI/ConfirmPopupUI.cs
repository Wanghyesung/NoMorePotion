using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConfirmPopupUI : MonoBehaviour
{
    [SerializeField] private TMP_Text message;
    [SerializeField] private Button yesBtn;
    [SerializeField] private Button noBtn;

    private UniTaskCompletionSource<bool> pending;

    private void Awake()
    {
        yesBtn.onClick.AddListener(() => Finish(true));
        noBtn.onClick.AddListener(() => Finish(false));
    }

    public UniTask<bool> ShowAsync(string text)
    {
        // 이미 떠있으면 새 요청 무시
        if(pending != null)
            return UniTask.FromResult(false);
        
        pending = new UniTaskCompletionSource<bool>();

        message.text = text;
        gameObject.SetActive(true);

        return pending.Task;

    }

    private void Finish(bool result)
    {
        if (pending == null)
            return;

        // 먼저 비우고 결과 전달, 결과 받은 쪽이 ShowAsync를 다시 불러도 열리도록
        // SetActive(false)보다 먼저 비워야 OnDisable에서 false로 덮어쓰지 않음
        var p = pending;
        pending = null;

        gameObject.SetActive(false);

        p.TrySetResult(result);
    }

    // 버튼 없이 꺼진 경우(부모 패널 비활성화, 씬 전환으로 파괴 등): 대기 중인 쪽에 취소(false) 전달
    // 처리하지 않으면 pending이 남아 이후 ShowAsync가 계속 무시됨
    // OnDestroy 전에도 항상 호출되므로 파괴 시 처리도 여기서 함께 됨
    private void OnDisable()
    {
        var p = pending;
        pending = null;

        p?.TrySetResult(false);
    }
}
