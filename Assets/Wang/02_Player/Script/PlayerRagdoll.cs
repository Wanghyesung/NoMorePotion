using MoreMountains.Tools;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerRagdoll : MonoBehaviour
{
    [SerializeField] private MMRagdoller ragdoller;
    [SerializeField, Min(0f)] private float deathImpulse = 3f;
    private Rigidbody rootBody;
    private Collider[] rootColliders;
    private Rigidbody[] boneBodies;
    private Collider[] boneColliders;

    private void Awake()
    {
        rootBody = GetComponent<Rigidbody>();
        rootColliders = GetComponents<Collider>();
        boneBodies = ragdoller.GetComponentsInChildren<Rigidbody>();
        boneColliders = ragdoller.GetComponentsInChildren<Collider>();
    }

    public void Play(Vector3 attackPosition)
    {
        Vector3 direction = transform.position - attackPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = -transform.forward;

        rootBody.linearVelocity = Vector3.zero;
        rootBody.angularVelocity = Vector3.zero;
        rootBody.isKinematic = true;
        foreach (Collider rootCollider in rootColliders)
            rootCollider.enabled = false;

        ragdoller.Ragdolling = true;
        foreach (Rigidbody body in boneBodies)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        Rigidbody hitBody = ragdoller.MainRigidbody;
        Vector3 hitPoint = hitBody.worldCenterOfMass;
        float closestDistance = float.PositiveInfinity;

        //제일 가까운 위치에 힘 주기
        foreach (Collider boneCollider in boneColliders)
        {
            Vector3 point = boneCollider.ClosestPoint(attackPosition);
            float distance = (point - attackPosition).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                hitBody = boneCollider.attachedRigidbody;
                hitPoint = point;
            }
        }
        hitBody.AddForceAtPosition(direction.normalized * deathImpulse, hitPoint, ForceMode.Impulse);
    }
}
