using R3;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 대기실 채팅창. 메시지 보관과 송수신은 NetworkFlowManager(RoomChatService)가 하고, 여기서는 표시와 입력만 담당
public class RoomChatUI : MonoBehaviour
{
    private const int MAX_LINES = 50;   // RoomChatService.MAX_HISTORY와 맞춤

    [SerializeField] private TMP_InputField input;
    [SerializeField] private Button sendBtn;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Transform content;
    [SerializeField] private ChatLineUI linePrefab;

    private readonly Queue<ChatLineUI> lines = new Queue<ChatLineUI>();
    private NetworkFlowManager manager;
    private IDisposable subscription;

    private void Awake()
    {
        sendBtn.onClick.AddListener(Submit);
        input.onSubmit.AddListener(_ => Submit());   // 엔터로 보내기
    }

    // 대기실 패널이 켜질 때마다 지금까지의 기록을 다시 그리고 새 메시지를 구독
    private void OnEnable()
    {
        manager = NetworkFlowManager.Instance;
        if (manager == null)
        {
            Debug.LogError("[RoomChatUI] NetworkFlowManager 없음");
            return;
        }

        ClearLines();
        foreach (ChatMessage msg in manager.ChatHistory)
            AddLine(msg);

        subscription = manager.ChatReceived.Subscribe(msg =>
        {
            AddLine(msg);
            ScrollToBottom();
        });

        input.text = string.Empty;
        ScrollToBottom();
    }

    private void OnDisable()
    {
        subscription?.Dispose();
        subscription = null;
    }

    private void Submit()
    {
        if (manager == null || string.IsNullOrWhiteSpace(input.text))
            return;

        manager.RequestSendChat(input.text);
        input.text = string.Empty;
        input.ActivateInputField();   // 보낸 뒤에도 바로 이어서 입력할 수 있게 포커스 유지
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
}
