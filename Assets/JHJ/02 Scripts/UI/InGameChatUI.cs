using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using R3;
using SF = UnityEngine.SerializeField;

public class InGameChatUI : MonoBehaviour
{
    private const int MAX_LINES = 50;

    [SF] private TMP_InputField input;
    [SF] private GameObject inputRoot;  //평소엔 숨겨지도록
    [SF] private ScrollRect scrollRect;
    [SF] private Transform content;
    [SF] private ChatLineUI linePrefab;
    [SF] private CanvasGroup canvasGroup;
    [SF] private float fadeDelay = 5f;    //마지막 메세지 후 페이드아웃 될 시간
    [SF] private float idleAlpha = 0.3f;
    [SF] private float hideDelay = 30f;     // 완전히 숨겨지는 시간


    private readonly Queue<ChatLineUI> lines = new Queue<ChatLineUI>();
    private NetworkFlowManager manager;
    private IDisposable subscription;
    private bool isTyping;
    private float lastActiveTime;
    private int closedFrame = -1;

    #region 생명주기
    private void Awake()
    {
        input.onSubmit.AddListener(_ => Submit());
        inputRoot.SetActive(false);
    }

    private void OnEnable()
    {
        manager = NetworkFlowManager.Instance;
        if (manager == null)
        {
            Debug.LogError($"[InGameChatUI] NetworkFlowManager 없음");
            return;
        }

        ClearLines();
        foreach (ChatMessage msg in manager.ChatHistory)
        {
            AddLine(msg);
        }

        subscription = manager.ChatReceived.Subscribe(msg =>
        {
            AddLine(msg);
            ScrollToBottom();
            WakeUp();
        });

        ScrollToBottom();
        WakeUp();
    }

    private void OnDisable()
    {
        subscription?.Dispose();
        subscription = null;

        if (isTyping)
            Close();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (!isTyping && Time.frameCount != closedFrame &&
            (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            Open();
        else if (isTyping && keyboard.escapeKey.wasPressedThisFrame)
            Close();

        float idleTime = Time.time - lastActiveTime;
        float target;

        if (isTyping || idleTime < fadeDelay)
            target = 1f;

        else if (idleTime < hideDelay)
            target = idleAlpha;

        else
            target = 0f;

        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, Time.deltaTime);
        canvasGroup.blocksRaycasts = canvasGroup.alpha > 0f;
    }

    #endregion

    #region 메서드
    private void Submit()
    {
        if (!isTyping)
            return;

        if (!string.IsNullOrWhiteSpace(input.text))
            manager.RequestSendChat(input.text);
        Close();    // 빈채로 엔터 누르면 그냥 닫히도록
    }

    private void Open()
    {
        isTyping = true;
        inputRoot.SetActive(true);
        input.text = string.Empty;
        input.ActivateInputField();
        InputManager.Instance?.SetInputEnabled(false);
        InputManager.Instance?.SetCursorLocked(false);
        WakeUp();
    }

    private void Close()
    {
        isTyping = false;
        closedFrame = Time.frameCount;
        input.DeactivateInputField();
        inputRoot.SetActive(false);
        InputManager.Instance?.SetInputEnabled(true);
        InputManager.Instance?.SetCursorLocked(true);
        WakeUp();
    }

    private void WakeUp()
    {
        lastActiveTime = Time.time;
    }

    private void AddLine(ChatMessage msg)
    {
        ChatLineUI line = Instantiate(linePrefab, content);
        line.Bind(msg);
        lines.Enqueue(line);

        if (lines.Count > MAX_LINES)
            Destroy(lines.Dequeue().gameObject);
    }

    private void ClearLines()
    {
        while (lines.Count > 0)
            Destroy(lines.Dequeue().gameObject);

        // 씬에 디자인용으로 놓아 둔 예시 줄도 함께 정리
        foreach (Transform child in content)
            Destroy(child.gameObject);
    }

    private void ScrollToBottom()
    {
        Canvas.ForceUpdateCanvases();                 // 방금 추가한 줄의 높이를 먼저 계산해야 맨 아래로 맞춰짐
        scrollRect.verticalNormalizedPosition = 0;
    }

    #endregion
}
