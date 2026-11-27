
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SF = UnityEngine.SerializeField;
using R3;

public class CastleTeleportInteract : MonoBehaviour
{
    [SF] private Collider rangeTrigger;

    private HashSet<Collider> playerColliders = new HashSet<Collider>();
    private bool subscribed;

    private void Start()
    {
        Refresh();
        if(AreaBossProgress.Instance == null)
        {
            Debug.LogWarning("[CastleTeleportInteract] AreaBossProgress 인스턴스 없음");
            return;
        }

        AreaBossProgress.Instance.Changed.Subscribe(_ => Refresh()).AddTo(this);
    }

    private void Refresh()
    {
        bool opened = AreaBossProgress.IsAllCleared();

        rangeTrigger.enabled = opened;

        if (!opened)
        {
            playerColliders.Clear();
            Unsubscribe();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsLocalPlayerCollider(other))
            return;

        playerColliders.Add(other);
        Subscribe();
    }

    private void OnTriggerExit(Collider other)
    {
        if (playerColliders.Remove(other))
        {
            if (playerColliders.Count == 0)
                Unsubscribe();
        }
    }

    private void OnDisable()
    {
        playerColliders.Clear();
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (subscribed || InputManager.Instance == null)
            return;

        InputManager.Instance.InteractAction.performed += OnInteract;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        if (InputManager.Instance != null)
            InputManager.Instance.InteractAction.performed -= OnInteract;

        subscribed = false;
    }

    private static bool IsLocalPlayerCollider(Collider other)
    {
        Player player = other.GetComponentInParent<Player>();
        return player != null && player.IsLocalPlayer;
    }


    private void OnInteract(InputAction.CallbackContext context)
    {
        InputManager input = InputManager.Instance;

        if (input == null || !input.HasGameplayInput)
            return;

        CastleTeleportManager manager = CastleTeleportManager.Instance;
        if (manager == null)
            return;

        if (!manager.TryRequestVote(out string reason))
            NetworkFlowManager.Instance?.AddSystemChat(reason);
    }
}
