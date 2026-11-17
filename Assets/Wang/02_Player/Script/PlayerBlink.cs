using MoreMountains.Feedbacks;
using UnityEngine;

// 캐릭터 색상이 바뀌어도 피격 깜빡임이 변경된 기본색으로 돌아오게 한다.
public sealed class PlayerBlink : MMBlink
{
    public void SetBaseColor(Color color)
    {
        // 비활성 상태에서 먼저 색상을 지정하는 경우에도 블록을 준비한다.
        if (_propertyBlock == null)
            InitializeBlinkProperties();

        // 다음 깜빡임의 기준색과 지금 화면에 표시할 색상을 함께 갱신한다.
        _initialColor = color;
        _currentColor = color;
        ApplyCurrentColor(TargetRenderer, MaterialIndex);
    }

    protected override void ApplyCurrentColor(Renderer targetRenderer, int materialIndex)
    {
        if (UseMaterialPropertyBlocks == false)
        {
            base.ApplyCurrentColor(targetRenderer, materialIndex);
            return;
        }

        // 몸의 머티리얼 슬롯에만 적용한다. 같은 Renderer의 얼굴·입은 유지한다.
        targetRenderer.GetPropertyBlock(_propertyBlock, materialIndex);
        _propertyBlock.SetColor(_propertyID, _currentColor);
        targetRenderer.SetPropertyBlock(_propertyBlock, materialIndex);
    }
}
