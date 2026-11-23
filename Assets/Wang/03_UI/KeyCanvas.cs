using UnityEngine;

public enum KeyGuide
{
    W, A, S, D, E, F, Q, R,
    Space, Shift, Tab, Alt, Alpha1, Alpha4, Enter, Esc, MouseLeft, MouseRight
}

[DisallowMultipleComponent]
public sealed class KeyCanvas : MonoBehaviour
{
    public static KeyCanvas Instance { get; private set; }

    [SerializeField] private RectTransform interactionKey;
    [SerializeField] private Animator interactionAnimator;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.75f, 0f);

    private RectTransform keyParent;
    private KeyGuide currentKey;

    public Vector3 InteractionPosition { get; private set; }
    public bool HasInteractionPosition { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        keyParent = (RectTransform)interactionKey.parent;
        interactionKey.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        ClearInteractionPosition();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance()
    {
        Instance = null;
    }

    public void PlayAnimationKey(KeyGuide key, Vector3 position)
    {
        InteractionPosition = position;
        HasInteractionPosition = true;

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(position + worldOffset);
        if (screenPoint.z <= 0f || !worldCamera.pixelRect.Contains(new Vector2(screenPoint.x, screenPoint.y)))
        {
            interactionKey.gameObject.SetActive(false);
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(keyParent, screenPoint, null, out Vector2 localPoint);
        interactionKey.localPosition = new Vector3(localPoint.x, localPoint.y, 0f);

        if (interactionKey.gameObject.activeSelf && currentKey == key)
            return;

        currentKey = key;
        interactionKey.gameObject.SetActive(true);
        string keyName = key == KeyGuide.Alpha1 ? "1" : key == KeyGuide.Alpha4 ? "4" : key.ToString();
        interactionAnimator.Play(Animator.StringToHash("GuideKey_" + keyName), 0, 0f);
    }

    public void ClearInteractionPosition()
    {
        InteractionPosition = default;
        HasInteractionPosition = false;
        if (interactionKey != null)
            interactionKey.gameObject.SetActive(false);
    }
}
