using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadingPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private GameObject progress;
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private float fillSpeed = 2f;


    private float target;
    private bool completing;
    

    private void Update()
    {
        slider.value = Mathf.MoveTowards(slider.value, target, fillSpeed * Time.unscaledDeltaTime);

        if (completing && slider.value >= 1)
            Hide();
    }

    public void ShowWaiting()
    {
        progress.SetActive(false);
        loadingPanel.SetActive(true);
    }

    public void SetProgress(int loaded, int total)
    {
        if (completing) return;

        progress.SetActive(true);
        target = total > 0 ? (float)loaded / total : 0;

        statusText.text = $"저주받은 세계로 들어갈 준비 완료 {loaded} / {total}";
    }

    public void Hide()
    {
        loadingPanel.SetActive(false);
    }
    public void CompleteAndHide()
    {
        progress.SetActive(true);
        target = 1f;
        completing = true;
    }

}
