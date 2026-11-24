using System;
using UnityEngine;
using UnityEngine.UI;

public enum KeyGuide
{
    W, A, S, D, E, F, Q, R,
    Space, Shift, Tab, Alt, Alpha1, Alpha4, Enter, Esc, MouseLeft, MouseRight,
    WASD,
    MouseLeftHold,
    ThrowOptions,
    ReverseWASD,
    None = -1
}

[DisallowMultipleComponent]
public sealed class KeyCanvas : MonoBehaviour
{
    [Serializable]
    private sealed class KeyView
    {
        [SerializeField] private RectTransform rect;
        [SerializeField] private Animator animator;
        [SerializeField] private TMPro.TMP_Text stepLabel;

        private Image image;
        private Transform anchor;
        private Camera camera;
        private Vector3 offset;
        private KeyGuide currentKey = KeyGuide.None;

        public Vector2 DefaultSize { get; private set; }

        public void Initialize()
        {
            if (rect == null)
                return;

            image = rect.GetComponent<Image>();
            DefaultSize = rect.sizeDelta;
            Hide();
        }

        public void Show(KeyGuide key, Transform target, Camera sourceCamera, Vector3 positionOffset, Vector2 size, int step = 0)
        {
            anchor = target;
            camera = sourceCamera;
            offset = positionOffset;
            rect.sizeDelta = size;
            if (stepLabel != null)
            {
                stepLabel.gameObject.SetActive(step > 0);
                if (step > 0)
                    stepLabel.SetText("{0}.", step);
            }
            bool play = !rect.gameObject.activeSelf || currentKey != key;
            rect.gameObject.SetActive(true);
            UpdateData();
            if (!play)
                return;

            currentKey = key;
            string name = key == KeyGuide.Alpha1 ? "1" : key == KeyGuide.Alpha4 ? "4" : key.ToString();
            animator.Play(Animator.StringToHash("GuideKey_" + name), 0, 0f);
        }

        // 위치와 가시성만 갱신하며 애니메이션은 재생하지 않는다.
        public void UpdateData()
        {
            if (rect == null || !rect.gameObject.activeSelf)
                return;

            image.enabled = anchor != null && anchor.gameObject.activeInHierarchy;
            if (!image.enabled)
                return;

            Vector3 screenPoint = anchor.position + offset;
            if (camera != null)
            {
                screenPoint = camera.WorldToScreenPoint(screenPoint);
                image.enabled = screenPoint.z > 0f &&
                    camera.pixelRect.Contains(new Vector2(screenPoint.x, screenPoint.y));
                if (!image.enabled)
                    return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)rect.parent, screenPoint, null, out Vector2 localPoint);
            rect.localPosition = new Vector3(localPoint.x, localPoint.y, 0f);
        }

        public void Hide()
        {
            anchor = null;
            if (rect != null)
                rect.gameObject.SetActive(false);
        }
    }

    public static KeyCanvas Instance { get; private set; }

    [SerializeField] private KeyView interactionView = new();
    [SerializeField] private KeyView panelView = new();
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.75f, 0f);
    [SerializeField] private Vector2 playerKeySize = new Vector2(80f, 80f);
    [SerializeField] private Vector2 mouseHoldKeySize = new Vector2(160f, 160f);
    [SerializeField] private Vector2 wasdKeySize = new Vector2(256f, 256f);
    [SerializeField] private Vector2 throwKeySize = new Vector2(480f, 160f);
    [SerializeField] private Vector2 reverseKeySize = new Vector2(384f, 256f);
    [SerializeField] private RectTransform playerKeyAnchor;

    private Transform interactionTarget;
    private KeyGuide interactionKeyType;
    private bool hasPlayerGuide;
    private bool interactionVisible = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        interactionView.Initialize();
        panelView.Initialize();
    }

    private void LateUpdate()
    {
        interactionView.UpdateData();
        panelView.UpdateData();
    }

    private void OnDisable()
    {
        hasPlayerGuide = false;
        ClearInteractionPosition();
        HidePanelKey();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => Instance = null;

    public void PlayAnimationKey(KeyGuide key, Transform target)
    {
        interactionTarget = target;
        interactionKeyType = key;
        if (!hasPlayerGuide && interactionVisible)
            interactionView.Show(key, target, worldCamera, worldOffset, interactionView.DefaultSize);
    }

    // 일반 상호작용 표시만 제어한다. Tab 등 가이드 키는 그대로 표시한다.
    public void SetInteractionVisible(bool visible)
    {
        interactionVisible = visible;
        if (hasPlayerGuide)
            return;

        if (visible && interactionTarget != null)
            interactionView.Show(interactionKeyType, interactionTarget, worldCamera, worldOffset, interactionView.DefaultSize);
        else
            interactionView.Hide();
    }

    public void ShowPlayerKey(KeyGuide key, int step = 0)
    {
        if (key == KeyGuide.None)
        {
            HidePlayerKey();
            return;
        }

        hasPlayerGuide = true;
        Vector2 size;
        if (key == KeyGuide.WASD)
            size = wasdKeySize;
        else if (key == KeyGuide.MouseLeftHold)
            size = mouseHoldKeySize;
        else if (key == KeyGuide.ReverseWASD)
            size = reverseKeySize;
        else if (key == KeyGuide.ThrowOptions)
            size = throwKeySize;
        else
            size = playerKeySize;
        interactionView.Show(key, playerKeyAnchor, null, Vector3.zero, size, step);
    }

    public void HidePlayerKey()
    {
        if (!hasPlayerGuide)
            return;

        hasPlayerGuide = false;
        if (interactionVisible && interactionTarget != null)
            interactionView.Show(interactionKeyType, interactionTarget, worldCamera, worldOffset, interactionView.DefaultSize);
        else
            interactionView.Hide();
    }

    public void ShowPanelKey(KeyGuide key, RectTransform anchor)
    {
        if (key == KeyGuide.None)
        {
            HidePanelKey();
            return;
        }

        Canvas canvas = anchor.GetComponentInParent<Canvas>().rootCanvas;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        panelView.Show(key, anchor, camera, Vector3.zero, panelView.DefaultSize);
    }

    public void HidePanelKey() => panelView.Hide();

    public void ClearInteractionPosition()
    {
        interactionTarget = null;
        if (!hasPlayerGuide)
            interactionView.Hide();
    }
}
