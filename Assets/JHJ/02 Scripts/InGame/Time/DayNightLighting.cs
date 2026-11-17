using UnityEngine;
using SF = UnityEngine.SerializeField;

/// <summary>
/// 낮/밤 진행에 맞춰 해의 색·세기, 환경광, 하늘 밝기를 바꾼다.
/// - 해는 고정 각도(sunRotation). 시간 흐름은 색 변화로만 표현한다.
/// - 각 단계의 마지막 구간(transitionRatio)에서 다음 단계로 섞인다.
/// - 어두운 구간(shadowOffDarkness 이상)에서는 해 그림자를 끈다.
/// </summary>
/// <remarks>
/// 환경광은 Lighting 창의 Environment Lighting Source가 Color일 때만 반영된다.
/// Skybox는 원본(skyboxSource)을 복사해서 사용한다 (원본 에셋 값이 바뀌지 않도록).
/// 해를 고정한 이유: 해 뜰 때·질 때 그림자가 길어져 그림자 캐스터가 크게 늘어남.
/// </remarks>
public class DayNightLighting : MonoBehaviour
{
    [Header("해")]
    [SF] private Light sun;
    [SF] private Vector3 sunRotation = new Vector3(50f, -30f, 0f); // 고정 각도
    [SF] private Gradient sunColor; // 왼쪽(0) = 낮, 가운데 = 노을(주황), 오른쪽(1) = 밤
    [SF] private AnimationCurve sunIntensity = AnimationCurve.Linear(0f, 1f, 1f, 0.2f);

    [Header("환경광")]
    [SF] private Gradient ambientColor;         // Lighting Source = Color일때만 반영

    [Header("하늘")]
    [SF] private Material skyboxSource;     // 원본 Skybox 머터리얼(직접 수정x)
    [SF] private AnimationCurve skyExposure = AnimationCurve.Linear(0f, 1.3f, 1f, 0.15f);
    [SF] private Gradient skyTint;

    [Header("그림자")]
    [SF, Range(0f, 1f)] private float shadowOffDarkness = 0.8f; // 이 이상 어두우면 그림자 끔

    [Header("전환")]
    [SF, Range(0f, 1f)] private float transitionRatio = 0.2f;   // 단계 끝 몇 %부터 바뀔지



    private Material skyboxInstance;
    private LightShadows originalShadows;
    private bool shadowsOn = true;
    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");


    private void Start()
    {
        if (sun == null) 
            return;

        sun.transform.rotation = Quaternion.Euler(sunRotation);
        originalShadows = sun.shadows;

        // 머터리얼 원본 직접 바꾸면 에디터에서 Play멈춰도 바뀐값 에셋에 남게됨
        if(skyboxSource != null)
        {
            skyboxInstance = new Material(skyboxSource);
            RenderSettings.skybox = skyboxInstance;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
    }

    private void OnDestroy()
    {
        if (skyboxInstance != null)
            Destroy(skyboxInstance);
    }

    private void Update()
    {
        TimeManager timeManager = TimeManager.Instance;

        if (timeManager == null || sun == null)
            return;

        float darkness = CalculateDarkness(timeManager.Phase.CurrentValue, timeManager.PhaseProgress);

        sun.color = sunColor.Evaluate(darkness);
        sun.intensity = sunIntensity.Evaluate(darkness);
        RenderSettings.ambientLight = ambientColor.Evaluate(darkness);

        if(skyboxInstance != null)
        {
            skyboxInstance.SetFloat(ExposureId, skyExposure.Evaluate(darkness));
            skyboxInstance.SetColor(SkyTintId, skyTint.Evaluate(darkness));
        }

        UpdateShadows(darkness);
    }

    private float CalculateDarkness(TimePhase phase, float progress)
    {
        // 전환 구간 안에서 얼마나 왔는지 (구간 전이면 0, 끝이면 1)
        float t = Mathf.InverseLerp(1f - transitionRatio, 1f, progress);

        // 낮: 밝음 → 어두움 / 밤: 어두움 → 밝음
        return phase == TimePhase.Day ? t : 1f - t;
    }

    private void UpdateShadows(float darkness)
    {
        bool shouldOn = darkness < shadowOffDarkness;

        if (shouldOn == shadowsOn)
            return;

        shadowsOn = shouldOn;
        sun.shadows = shouldOn ? originalShadows : LightShadows.None;
    }



}