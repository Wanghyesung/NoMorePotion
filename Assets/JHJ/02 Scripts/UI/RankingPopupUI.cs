using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

/// <summary>
/// 로비 랭킹 창. 열 때마다 서버에서 상위 10개 조회
/// 예외: 아웃게임 UI 중 유일하게 매니저를 거치지 않고 RankingService를 직접 await
/// (조회는 흐름 상태를 바꾸지 않는 읽기 전용이라)
/// </summary>
public class RankingPopupUI : MonoBehaviour
{
    private const int TOP_COUNT = 10;
    private const int PODIUM_COUNT = 3;

    [SerializeField] private GameObject root;
    [SerializeField] private GameObject content;
    [SerializeField] private RankingPodiumSlotUI[] podium;     // [0]=1위 [1]=2위 [2]=3위 (화면 배치와 다름)
    [SerializeField] private RankingRowUI[] rows;           // 위에서부터 7개 (4~10위)
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private TMP_Text messageText;

    [SerializeField] private Button closeBtn;

    private CancellationTokenSource loadCts;


    private void Awake()
    {
        closeBtn.onClick.AddListener(Close);
        root.SetActive(false);
    }

    private void OnDestroy()
    {
        CancelLoad();
    }

    public void Open()
    {
        root.SetActive(true);
        LoadAsync().Forget();
    }

    public void Close()
    {
        CancelLoad();
        root.SetActive(false);
    }

    private async UniTaskVoid LoadAsync()
    {
        CancelLoad();
        loadCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

        ShowMessage("불러오는 중...");

        List<RankingEntry> entries;
        try
        {
            entries = await RankingService.LoadTopAsync(TOP_COUNT, loadCts.Token);
        }
        catch (OperationCanceledException)
        { 
            return;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RankingPopupUI] 랭킹 조회 실패: {e.Message}");
            ShowMessage("랭킹을 불러오지 못했습니다");
            return;
        }

        Render(entries);
    }

    private void Render(List<RankingEntry> entries)
    {
        messageText.gameObject.SetActive(false);
        content.SetActive(true);

        for(int i =0; i < PODIUM_COUNT; i++)
        {
            if (i < entries.Count)
                podium[i].Show(entries[i]);
            else
                podium[i].Hide();
        }

        for(int i = 0; i < rows.Length; i++)
        {
            int index = PODIUM_COUNT + i;
            if (index < entries.Count)
                rows[i].Show(index + 1, entries[index]);
            else
                rows[i].Hide();
        }

        Canvas.ForceUpdateCanvases();                       // 방금 켠 목록의 레이아웃을 먼저 계산해야 맨 위로 맞춰짐
        scrollRect.verticalNormalizedPosition = 1;
    }

    private void ShowMessage(string text)
    {
        content.SetActive(false);
        messageText.text = text;
        messageText.gameObject.SetActive(true);
    }

    private void CancelLoad()
    {
        loadCts?.Cancel();
        loadCts?.Dispose();
        loadCts = null;
    }
}
