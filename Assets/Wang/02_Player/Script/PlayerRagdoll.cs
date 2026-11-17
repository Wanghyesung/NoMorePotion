using MoreMountains.Tools;
using Photon.Pun;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerRagdoll : MonoBehaviour
{
    [SerializeField] private MMRagdoller ragdoller;
    [SerializeField, Min(0f)] private float deathImpulse = 3f;
    private Rigidbody rootBody;
    private Collider[] rootColliders;
    private bool[] rootColliderEnabled;
    private Rigidbody[] boneBodies;
    private Collider[] boneColliders;
    private PhotonView photonView;

    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
        rootBody = GetComponent<Rigidbody>();
        rootColliders = GetComponents<Collider>();
        rootColliderEnabled = new bool[rootColliders.Length];
        for (int i = 0; i < rootColliders.Length; i++)
            rootColliderEnabled[i] = rootColliders[i].enabled;
        boneBodies = ragdoller.GetComponentsInChildren<Rigidbody>();
        boneColliders = ragdoller.GetComponentsInChildren<Collider>();
    }

    public void Restore()
    {
        foreach (Rigidbody body in boneBodies)
        {
            if (body == rootBody)
                continue;
            if (body.isKinematic == false)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
            body.detectCollisions = false;
        }
        ragdoller.CurrentState = MMRagdoller.RagdollStates.Animated;
        // Remote roots stay kinematic; only the owner runs root physics.
        rootBody.isKinematic = photonView != null && photonView.IsMine == false;
        if (rootBody.isKinematic == false)
        {
            rootBody.linearVelocity = Vector3.zero;
            rootBody.angularVelocity = Vector3.zero;
        }
        for (int i = 0; i < rootColliders.Length; i++)
            rootColliders[i].enabled = rootColliderEnabled[i];
    }

    public void Play(Vector3 attackPosition)
    {
        if (photonView != null && photonView.ViewID != 0)
        {
            if (photonView.IsMine == false)
                return;

            // Send once on death; each client runs the ragdoll physics locally.
            photonView.RPC(nameof(RpcPlayRagdoll), RpcTarget.Others, attackPosition);
        }

        PlayLocal(attackPosition);
    }

    [PunRPC]
    private void RpcPlayRagdoll(Vector3 attackPosition, PhotonMessageInfo info)
    {
        if (info.Sender != photonView.Owner || photonView.IsMine)
            return;

        PlayLocal(attackPosition);
    }

    private void PlayLocal(Vector3 attackPosition)
    {
        Vector3 direction = transform.position - attackPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = -transform.forward;

        // Remote player roots are already kinematic; do not set their velocities.
        if (rootBody.isKinematic == false)
        {
            rootBody.linearVelocity = Vector3.zero;
            rootBody.angularVelocity = Vector3.zero;
            rootBody.isKinematic = true;
        }
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
