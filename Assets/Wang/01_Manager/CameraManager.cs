using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

/*///////////////////////////////////////////
                 CameraManager
기능 : 배치한 Transform을 기준으로 Cinemachine 위치와 회전을 설정
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class CameraManager : CinemachineExtension
{
    #region CameraSettings
    [SerializeField] private CinemachineCamera targetCamera;
    [SerializeField] private CinemachineFollow cameraFollow;
    [SerializeField, Min(0f)] private float pitchSensitivity = 0.15f;
    #endregion

    #region CameraPoints
    private Transform baseTarget;
    #endregion

    private Quaternion rotationOffset = Quaternion.identity;
    private Vector3 rotationAngles;

    protected override void Awake()
    {
        base.Awake();
        cameraFollow.TrackerSettings.BindingMode = BindingMode.LockToTarget;
    }

    public void SetPlayer(Player player)
    {
        if (player == null || player.CameraPosition == null)
        {
            Debug.LogWarning("카메라를 연결할 Player와 Camera Position이 필요합니다.", this);
            return;
        }

        baseTarget = player.transform;
        targetCamera.Follow = baseTarget;
        cameraFollow.FollowOffset = GetOffset(player.CameraPosition);
        rotationOffset = Quaternion.Inverse(baseTarget.rotation) * player.CameraPosition.rotation;
        rotationAngles = rotationOffset.eulerAngles;
        rotationAngles.x = Mathf.Clamp(Mathf.DeltaAngle(0f, rotationAngles.x), -15f, 30f);
        rotationOffset = Quaternion.Euler(rotationAngles);
    }

    private void Update()
    {
        InputManager input = InputManager.Instance;
        if (baseTarget == null || input == null || input.HasGameplayInput == false)
            return;

        // 마우스를 올리면 위를 본다. Player 루트는 기울이지 않고 카메라 피치만 제한한다.
        rotationAngles.x = Mathf.Clamp(
            rotationAngles.x - input.InputInfo.Delta.y * pitchSensitivity, -15f, 30f);
        rotationOffset = Quaternion.Euler(rotationAngles);
    }

    private Vector3 GetOffset(Transform point)
    {
        // LockToTarget의 회전 기준으로 변환하며 Transform 스케일은 적용하지 않음
        return Quaternion.Inverse(baseTarget.rotation) * (point.position - baseTarget.position);
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage == CinemachineCore.Stage.Aim && baseTarget != null)
            state.RawOrientation = baseTarget.rotation * rotationOffset;
    }

}
