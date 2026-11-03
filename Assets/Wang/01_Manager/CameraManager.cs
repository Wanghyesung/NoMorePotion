using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

/*///////////////////////////////////////////
                 CameraManager
기능 : 배치한 Transform을 기준으로 Cinemachine 위치와 회전을 설정
       조준·복귀 시 오프셋을 일정 속도로 이동하고 이전 이동을 취소
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class CameraManager : CinemachineExtension
{
    #region CameraSettings
    [SerializeField] private CinemachineCamera targetCamera;
    [SerializeField] private CinemachineFollow cameraFollow;
    [SerializeField, Min(0.01f)] private float moveSpeed = 3f;
    #endregion

    #region CameraPoints
    [SerializeField] private Transform baseTarget;
    [SerializeField] private Transform baseCameraPos;
    [SerializeField] private Transform zoomPos;
    #endregion

    private CancellationTokenSource moveCancellation;
    private Quaternion rotationOffset = Quaternion.identity;

    protected override void Awake()
    {
        base.Awake();
        targetCamera.Follow = baseTarget;
        cameraFollow.TrackerSettings.BindingMode = BindingMode.LockToTarget;
        cameraFollow.FollowOffset = GetOffset(baseCameraPos);
        rotationOffset = Quaternion.Inverse(baseTarget.rotation) * baseCameraPos.rotation;
    }

    // active: true면 ZoomPos, false면 BaseCameraPos로 이동
    public void SetZoom(bool active)
    {
        MoveTo(active ? zoomPos : baseCameraPos);
    }

    // destination: 카메라가 도착할 위치와 회전을 표시한 Transform
    public void MoveTo(Transform destination)
    {
        CancelMove();
        if (!isActiveAndEnabled || destination == null)
            return;

        moveCancellation = new CancellationTokenSource();
        MoveAsync(destination, moveCancellation).Forget();
    }

    private async UniTask MoveAsync(Transform destination, CancellationTokenSource cancellation)
    {
        CancellationToken token = cancellation.Token;
      
        while (destination != null && baseTarget != null)
        {
            // 다음 프레임에서 한 번만 이동하여 호출 시점에 따른 중복 이동을 방지
            await UniTask.NextFrame(PlayerLoopTiming.Update, token);
            if (destination == null || baseTarget == null)
                return;

            Vector3 offset = GetOffset(destination);
            float distance = Vector3.Distance(cameraFollow.FollowOffset, offset);
            float ratio = Mathf.Clamp01(moveSpeed * Time.deltaTime / distance);

            Quaternion destinationRotation = Quaternion.Inverse(baseTarget.rotation) * destination.rotation;
            rotationOffset = Quaternion.Slerp(rotationOffset, destinationRotation, ratio);
            cameraFollow.FollowOffset = Vector3.MoveTowards(cameraFollow.FollowOffset, offset, moveSpeed * Time.deltaTime);

            if ((cameraFollow.FollowOffset - offset).sqrMagnitude <= 0.000001f)
            {
                cameraFollow.FollowOffset = offset;
                rotationOffset = destinationRotation;
                return;
            }
        }
      
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

    private void CancelMove()
    {
        if (moveCancellation == null)
            return;

        moveCancellation.Cancel();
        moveCancellation.Dispose();
        moveCancellation = null;
    }

    private void OnDisable()
    {
        CancelMove();
        if (cameraFollow != null && baseTarget != null && baseCameraPos != null)
        {
            cameraFollow.FollowOffset = GetOffset(baseCameraPos);
            rotationOffset = Quaternion.Inverse(baseTarget.rotation) * baseCameraPos.rotation;
        }
    }
}
