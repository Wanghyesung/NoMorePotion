using Enzeun.Runtime;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// 씬의 게임 카메라에서 내 플레이어를 중심으로 3D 거리 가림 효과를 적용한다.
[DisallowMultipleComponent]
public sealed class WorldVignetteController : MonoBehaviour
{
    private static readonly int playerPositionId = Shader.PropertyToID("_PlayerPositionWS");
    private static readonly int visibleDistanceId = Shader.PropertyToID("_VisibleDistance");
    private static readonly int fadeRatioId = Shader.PropertyToID("_FadeRatio");
    private static readonly int effectStrengthId = Shader.PropertyToID("_EffectStrength");

    [SerializeField] private ScriptableRendererData rendererData;
    [SerializeField] private Material sourceMaterial;
    [SerializeField] private MentalStrengthManager mentalStrengthManager;

    // 가시 거리 = 기준 거리 × 정신력 비율. 25일 때 정신력 30%는 7.5까지 보인다.
    [SerializeField, Min(0f)] private float baseDistance = 25f;
    // 정신력이 0이어도 이 끝 반경을 유지한다. 안쪽 경계는 fadeRatio에 따라 어둡게 보간한다.
    [SerializeField, Min(0.01f)] private float minimumDistance = 3f;

    // 가시 거리 중 어두워지는 경계 구간의 비율. 0.5면 반경의 절반부터 어두워진다.
    [SerializeField, Range(0.01f, 1f)] private float fadeRatio = 0.5f;
    // 효과의 켜짐/꺼짐 시간과 반경 보간 시간. 0이면 즉시 반영한다.
    [SerializeField, Min(0f)] private float transitionTime = 0.5f;

    private Transform target;
    private FullScreenPassRendererFeature renderFeature;
    private Material runtimeMaterial;
    private float currentStrength;
    private float currentDistance;
    private float distanceVelocity;

    private void OnEnable()
    {
        if (rendererData == null || sourceMaterial == null || mentalStrengthManager == null)
        {
            Debug.LogError("WorldVignetteController: Renderer Data, Source Material, Mental Strength Manager를 연결하세요.", this);
            enabled = false;
            return;
        }

        // 활성화할 때만 렌더 패스를 찾고, LateUpdate에서는 검색하지 않는다.
        foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
        {
            if (feature is FullScreenPassRendererFeature fullScreenPass &&
                fullScreenPass.passMaterial == sourceMaterial)
            {
                renderFeature = fullScreenPass;
                break;
            }
        }

        if (renderFeature == null)
        {
            Debug.LogError("WorldVignetteController: 해당 머티리얼의 Full Screen Pass가 없습니다.", this);
            enabled = false;
            return;
        }

        // 원본 머티리얼 에셋에 플레이어 위치와 실행 중 거리가 저장되지 않게 복제한다.
        runtimeMaterial = new Material(sourceMaterial);
        currentStrength = 0f;
        currentDistance = 0f;
        distanceVelocity = 0f;
        renderFeature.passMaterial = runtimeMaterial;
        renderFeature.SetActive(false);
    }

    public void SetTarget(Transform playerTransform)
    {
        target = playerTransform;
    }

    private void LateUpdate()
    {
        // 플레이어가 없으면 즉시 해제한다. 정신력 회복은 아래에서 서서히 해제한다.
        if (target == null || mentalStrengthManager == null)
            return;

        // 이동 이후의 월드 좌표를 HLSL 상수 버퍼의 _PlayerPositionWS에 전달한다.
        Vector3 position = target.position;
        runtimeMaterial.SetVector(playerPositionId, new Vector4(position.x, position.y, position.z, 1f));

        // 상태 이벤트는 단계가 바뀔 때만 발생하므로 실제 수치를 읽는다.
        // 30% 이하부터 효과를 켜되, 범위는 정신력의 실제 퍼센트만큼 남긴다.
        // 회복 중에는 30% 반경을 유지하며 밝아져, 효과가 꺼지기 전에 반경이 급팽창하지 않는다.
        float mentalRatio = Mathf.Clamp(mentalStrengthManager.mentalStrength / 100f, 0f, 0.3f);
        float minDistance = Mathf.Max(0.01f, minimumDistance);
        float visibleDistance = Mathf.Max(minDistance, baseDistance * mentalRatio);
        float targetStrength = mentalStrengthManager.mentalStrength <= 30 ? 1f : 0f;

        // 처음 켤 때는 올바른 반경에서 시작하고, 이후 정신력 변화만 부드럽게 따라간다.
        if (currentStrength == 0f || transitionTime <= 0f)
        {
            currentDistance = visibleDistance;
            distanceVelocity = 0f;
        }
        else
        {
            currentDistance = Mathf.SmoothDamp(currentDistance, visibleDistance,
                ref distanceVelocity, transitionTime);
        }

        // Inspector에서 최소 거리를 올려도 보간 때문에 그보다 좁아지지 않도록 보장한다.
        currentDistance = Mathf.Max(minDistance, currentDistance);
        currentStrength = transitionTime <= 0f ? targetStrength :
            Mathf.MoveTowards(currentStrength, targetStrength, Time.deltaTime / transitionTime);

        runtimeMaterial.SetFloat(visibleDistanceId, currentDistance);
        runtimeMaterial.SetFloat(fadeRatioId, Mathf.Clamp(fadeRatio, 0.01f, 1f));
        runtimeMaterial.SetFloat(effectStrengthId, Mathf.SmoothStep(0f, 1f, currentStrength));
        // 회복 시에도 전환이 끝날 때까지 패스를 유지해야 갑자기 밝아지지 않는다.
        renderFeature.SetActive(currentStrength > 0f);
    }

    private void OnDisable()
    {
        // 씬 전환이나 컴포넌트 비활성화 시 렌더러를 원본 머티리얼로 되돌린다.
        if (renderFeature != null && renderFeature.passMaterial == runtimeMaterial)
        {
            renderFeature.SetActive(false);
            renderFeature.passMaterial = sourceMaterial;
        }

        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);

        runtimeMaterial = null;
        renderFeature = null;
    }
}
