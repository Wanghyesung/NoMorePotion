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

        gameObject.SetActive(false);

        // 먼저 비우고 결과 전달, 결과 받은 쪽이 ShowAsync를 다시 불러도 열리도록
        var p = pending;
        pending = null;

        p.TrySetResult(result);
    }

    private void OnDestroy()
    {
        pending?.TrySetResult(false);
        pending = null;
    }
}
