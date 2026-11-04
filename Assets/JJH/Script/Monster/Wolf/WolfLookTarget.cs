using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using static JJH_Enums;

using SF = UnityEngine.SerializeField;

/// <summary>
/// 리그 멀티 에임이 고정된 시점 object 이동 클래스
/// </summary>
[RequireComponent(typeof(MonsterMove))]
public class WolfLookTarget : MonoBehaviour
{
    #region 직렬화 필드
    [SF] private MonsterMove mover;
    [SF] private Rig headRig;
    [SF] private Transform lookTarget;

    [Header("조절값")]
    [SF] private float targetHeight = 1.2f;
    [SF] private float weightSpeed = 4f;
    [SF] private float followSpeed = 10f; // 시선 속도

    [InfoBox("모션을 우선할 경우 true, 아닐 경우 false")]
    [SF] private bool releaseDuringAnimation = true; //모션 우선 설정
    #endregion

    private void Awake()
    {
        if (mover == null)
            mover = GetComponent<MonsterMove>();
    }

    private void Update()
    {
        bool look = ShouldLook();

        if (look)
        {
            Vector3 goal = mover.nowTarget.transform.position + Vector3.up * targetHeight;

            // 이전 위치가 고정되어있지 않았을 경우 보정
            lookTarget.position = headRig.weight <= 0.01f
                ? goal
                : Vector3.Lerp(lookTarget.position, goal, Time.deltaTime * followSpeed);
        }

        headRig.weight = Mathf.MoveTowards(headRig.weight, look ? 1f : 0f, Time.deltaTime * weightSpeed);
    }

    /// <summary>
    /// 몬스터가 현재 고개를 회전 할 수 있는 상태인지 확인하는 함수
    /// </summary>
    /// <returns>몬스터의 고개 회전 여부</returns>
    private bool ShouldLook()
    {
        if (mover.nowTarget == null || mover.state == null) return false;
        if (releaseDuringAnimation && mover.IsAnimation) return false;

        return mover.state.GetStateType() switch
        {
            MonsterState.Chase or 
            MonsterState.Attack or 
            MonsterState.Encircle or 
            MonsterState.Pound => true,
            _ => false,
        };
    }
}
