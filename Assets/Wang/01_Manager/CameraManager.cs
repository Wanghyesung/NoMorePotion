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
    private Transform baseTarget;
    #endregion

    private Vector3 initialFollowOffset;
    private float initialPitch;

    protected override void Awake()
    {
        base.Awake();
        // 씬에서 맞춘 구도는 플레이어 프리팹의 스케일과 관계없이 유지한다.
        initialFollowOffset = cameraFollow.FollowOffset;
        // 시작 기울기만 보존하고 좌우 방향은 마우스로 회전하는 Player 루트를 따른다.
        initialPitch = Mathf.DeltaAngle(0f, targetCamera.transform.eulerAngles.x);
        cameraFollow.TrackerSettings.BindingMode = BindingMode.LockToTarget;
    }

    public void SetPlayer(Player player)
    {
        if (player == null)
        {
            Debug.LogWarning("카메라를 연결할 Player가 필요합니다.", this);
            return;
        }

        // View는 이동키에 따라 따로 회전하므로 카메라 추적 기준으로 사용하지 않는다.
        baseTarget = player.LookPivot;
        targetCamera.Follow = baseTarget;
        cameraFollow.FollowOffset = initialFollowOffset;

        player.SetLookPitch(initialPitch);

        // 방 입장 대기 중 대상 없이 갱신된 Follow의 회전 이력을 새 플레이어 기준으로 초기화한다.
        targetCamera.PreviousStateIsValid = false;
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage == CinemachineCore.Stage.Aim && baseTarget != null)
            state.RawOrientation = baseTarget.rotation;
    }

}
