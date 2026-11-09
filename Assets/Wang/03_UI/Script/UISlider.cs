using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;



/*///////////////////////////////////////////
                 UISlider

기능 : Slider 조절하여 이미지를 표현하는 역할
      현재 값 변경은 Health 구독으로 전달
 *///////////////////////////////////////////

public class UISlider : MonoBehaviour
{
    private int maxValue = 1;
    [SerializeField] private float lerpDuration = 0.5f;

    private CancellationTokenSource ctsLerp;
    [SerializeField] private Slider slider = null;

    private readonly ReactiveProperty<int> Range = new(0);
    public ReadOnlyReactiveProperty<int> Health => Range;

    [SerializeField] private TextMeshProUGUI hpText;
    private void Awake()
    {
        if (slider == null)
            slider = GetComponent<Slider>();
    }

    public void SetRange(int maxValue, int curValue)
    {
        this.maxValue = maxValue;
        Range.Value = curValue;

        if (slider != null && this.maxValue > 0)
            slider.value = (float)Range.Value / this.maxValue;

        // 같은 인스턴스의 값을 갱신해야 기존 구독이 유지된다.
        hpText.text = $"{Range.Value} / {maxValue}";
    }

    // 외부에서 새로운 현재값을 받아 슬라이더를 업데이트하는 함수
    public void UpdateSlider(int newValue)
    {

        if (ctsLerp != null)
        {
            ctsLerp.Cancel();
            ctsLerp.Dispose();
        }

        // 데이터 갱신
        Range.Value = newValue;
        float fTargetFill = maxValue > 0 ? (float)newValue / maxValue : 0f;
        hpText.text = $"{Range.Value} / {maxValue}";

        ctsLerp = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        LerpSliderAsync(slider.value, fTargetFill, ctsLerp.Token).Forget();
    }

    private async UniTaskVoid LerpSliderAsync(float startFill, float endFill, CancellationToken ct)
    {
        float elapsed = 0f;

        while (elapsed < lerpDuration)
        {
            elapsed += Time.deltaTime;

            // 0에서 1 사이의 진행 비율 계산 (Clamped)
            float fProgress = Mathf.Clamp01(elapsed / lerpDuration);

            // 시작 fillAmount에서 목표 fillAmount까지 보간
            slider.value = Mathf.Lerp(startFill, endFill, fProgress);

            await UniTask.Yield(ct);
        }

        slider.value = endFill;

        ctsLerp.Dispose();
        ctsLerp = null;
    }

    private void OnDestroy()
    {
        if (ctsLerp != null)
        {
            ctsLerp.Cancel();
            ctsLerp.Dispose();
            ctsLerp = null;
        }

        Range.Dispose();
    }

}
