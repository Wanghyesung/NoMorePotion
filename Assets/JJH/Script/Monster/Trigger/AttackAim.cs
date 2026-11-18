using UnityEngine;

using SF = UnityEngine.SerializeField;

public class AttackAim : MonoBehaviour
{
    [SF] private MonsterMove mover;
    //몸통 중앙 높이의 빈 오브젝트
    [SF] private Transform pivot;
    [SF] private float maxUpAngle = 45f;
    [SF] private float maxDownAngle = 30f;
    [SF] private float heightDeadzone = 0.1f;

    public float MaxUpAngle => maxUpAngle;
    public float MaxDownAngle => maxDownAngle;

    private void Awake()
    {
        if (mover == null)
            mover = GetComponent<MonsterMove>();
    }

    /// <summary>
    /// 현재 타겟 높이에 맞춰 피벗 각도를 고정하는 함수
    /// </summary>
    public void Aim()
    {
        if (pivot == null) return;

        GameObject target = mover != null ? mover.nowTarget : null;
        if (target == null)
        {
            ResetAim();
            return;
        }

        // 발 높이끼리 비교
        float dy = target.transform.position.y - transform.position.y;
        if (Mathf.Abs(dy) <= heightDeadzone)
        {
            ResetAim();
            return;
        }

        Vector3 flat = target.transform.position - pivot.position;
        flat.y = 0f;

        // + 위, - 아래
        float pitch = Mathf.Atan2(dy, flat.magnitude) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(pitch, -maxDownAngle, maxUpAngle);

        pivot.localRotation = Quaternion.Euler(-pitch, 0f, 0f);
    }

    /// <summary>
    /// 에임을 위해 돌아간 회전각 초기화
    /// </summary>
    public void ResetAim()
    {
        if (pivot != null)
            pivot.localRotation = Quaternion.identity;
    }
}
