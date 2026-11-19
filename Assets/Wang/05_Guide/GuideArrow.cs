using UnityEngine;

[DisallowMultipleComponent]
public sealed class GuideArrow : MonoBehaviour
{
    private Transform target;

    public void SetTargetRotate(Transform targetTransform)
    {
        target = targetTransform;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector3 angles = transform.eulerAngles;
        angles.y = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        transform.eulerAngles = angles;
    }

    private void OnDisable() => target = null;
}
