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
    #endregion

    #region CameraPoints
    [SerializeField] private Transform baseTarget;
    [SerializeField] private Transform baseCameraPos;
    #endregion

    private Quaternion rotationOffset = Quaternion.identity;

    protected override void Awake()
    {
        base.Awake();

        targetCamera.Follow = baseTarget;
        cameraFollow.TrackerSettings.BindingMode = BindingMode.LockToTarget;
        cameraFollow.FollowOffset = GetOffset(baseCameraPos);
        rotationOffset = Quaternion.Inverse(baseTarget.rotation) * baseCameraPos.rotation;
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
