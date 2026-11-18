using R3;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static JJH_Enums;

/// <summary>
/// 지역보스 처치 상태 표시 + 패널 접기/펼치기"
/// </summary>
public class AreaBossUI : MonoBehaviour
{
    [Serializable]
    private class Slot
    {
        public SpawnAreaType area;
        public GameObject clearedMark;
    }

    [Header("상태 표시")]
    [SerializeField] private Slot[] slots;
    [SerializeField] private TMP_Text countText;   // "1/4" (접어도 보이는 곳에 두기)

    [Header("접기/펼치기")]
    [SerializeField] private Button foldButton;       // 제목줄 전체 또는 화살표 아이콘
    [SerializeField] private GameObject detailRoot;   // 접으면 숨길 영역 (슬롯 묶음)
    [SerializeField] private RectTransform arrowIcon;
    [SerializeField] private bool startExpanded = true;

    private bool isExpanded;

    private void Start()
    {
        foldButton.onClick.AddListener(ToggleFold);
        SetExpanded(startExpanded);

        AreaBossProgress areaBossProgress = AreaBossProgress.Instance;

        if(areaBossProgress == null)
        {
            Debug.LogError("[AreaBossUI] areaBossProgress 없음");
            return;
        }

        areaBossProgress.Changed.Subscribe(_ => Render()).AddTo(this);
        Render();
    }

    private void ToggleFold()
    {
        SetExpanded(!isExpanded);
    }

    private void SetExpanded(bool expanded)
    {
        isExpanded = expanded;
        detailRoot.SetActive(expanded);

        if (arrowIcon != null)
            arrowIcon.localEulerAngles = new Vector3(0f, 0f, expanded ? 0f : 180f);
    }

    private void Render()
    {
        AreaBossProgress progress = AreaBossProgress.Instance;

        if (progress == null)
            return;

        int count = 0;
        foreach(Slot s in slots)
        {
            bool isCleared = progress.IsCleared(s.area);
            s.clearedMark.SetActive(isCleared);

            if (isCleared)
                count++;
        }

        if (countText != null)
            countText.text = $"{count} / {slots.Length}";
    }
}
