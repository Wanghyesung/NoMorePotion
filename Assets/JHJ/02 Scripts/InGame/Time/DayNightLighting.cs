using UnityEngine;
using SF = UnityEngine.SerializeField;

/// <summary>
/// 낮/밤 진행에 맞춰 해의 위치, 조명, 환경광을 바꾼다.
/// - 해는 낮 동안 0→180도(동→서), 밤 동안 180→360도(땅 밑)로 한 바퀴 돈다.
/// - 각 단계의 마지막 구간(transitionRatio)에서 다음 단계 조명으로 섞인다.
/// </summary>
/// <remarks>
/// 환경광은 Lighting 창의 Environment Lighting Source가 Color일 때만 반영된다.
/// Procedural Skybox의 하늘색은 Lighting 창 Sun Source에 이 라이트가 지정돼 있어야 따라 바뀐다.
/// 해가 움직이므로 Directional Light는 베이크(라이트맵) 불가, 실시간 그림자 사용.
/// 그림자 비용은 Shadow Distance / Cascade / Resolution으로 조절
/// </remarks>
public class DayNightLighting : MonoBehaviour
{
    [SF] private Light sun;

    [Header("낮")]
    [SF] private float dayIntensity = 1f;
    [SF] private Color dayLightColor = Color.white;
    [SF] private Color dayAmbientColor = new Color(0.21f, 0.23f, 0.26f);

    [Header("밤")]
    [SF] private float nightIntensity = 0.2f;
    [SF] private Color nightLightColor = new Color(0.5f, 0.6f, 1f);
    [SF] private Color nightAmbientColor = new Color(0.1f, 0.1f, 0.2f);

    [Header("전환")]
    [SF, Range(0f, 1f)] private float transitionRatio = 0.2f;   // 단계 끝 몇 %부터 바뀔지

    [Header("해 위치")]
    [SF] private float sunYaw = -30f;   // 해가 뜨고 지는 방향 (Y 회전)



    private void Update()
    {
        TimeManager tm = TimeManager.Instance;
        if (tm == null || sun == null) return;

        TimePhase phase = tm.Phase.CurrentValue;
        float progress = tm.PhaseProgress;

        // 0 = 완전 낮, 1 = 완전 밤
        float darkness = CalculateDarkness(phase, progress);

        sun.intensity = Mathf.Lerp(dayIntensity, nightIntensity, darkness);
        sun.color = Color.Lerp(dayLightColor, nightLightColor, darkness);
        RenderSettings.ambientLight = Color.Lerp(dayAmbientColor, nightAmbientColor, darkness);


        // TODO(최적화): 밤엔 해가 지평선 아래라 그림자가 안 보이는데도 계산됨.
        //   해가 지평선 아래일 때 sun.shadows = None, 위로 올라오면 원래 값으로 복구.
        //   매 프레임 대입하지 말고 상태가 바뀔 때만. Profiler로 그림자 비용 확인 후 적용.
        sun.transform.rotation = Quaternion.Euler(CalculateSunAngle(phase, progress), sunYaw, 0f);
    }

    private float CalculateDarkness(TimePhase phase, float progress)
    {
        // 전환 구간 안에서 얼마나 왔는지 (구간 전이면 0, 끝이면 1)
        float t = Mathf.InverseLerp(1f - transitionRatio, 1f, progress);

        // 낮: 밝음 → 어두움 / 밤: 어두움 → 밝음
        return phase == TimePhase.Day ? t : 1f - t;
    }

    private float CalculateSunAngle(TimePhase phase, float progress)
    {
        // 낮: 0~180도 (지평선 위), 밤: 180~360도 (지평선 아래)
        return phase == TimePhase.Day ? progress * 180f : 180f + progress * 180f;
    }
}