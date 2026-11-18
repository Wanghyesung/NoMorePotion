using UnityEngine;
using System.Collections.Generic;
using SF = UnityEngine.SerializeField;
using Enzeun.Runtime;

public class NotificationUI : MonoBehaviour
{
    public static NotificationUI Instance { get; private set; }

    [SF] private NotificationLineUI linePrefab;
    [SF] private Transform container;
    [SF] private int maxLines = 5;
    [SF] private Color gainColor = new Color(0.55f, 0.85f, 0.45f);   // 획득: 초록
    [SF] private Color lossColor = new Color(1f, 0.45f, 0.40f);      // 소모: 빨강

    private readonly Queue<NotificationLineUI> pool = new Queue<NotificationLineUI>();
    private readonly List<NotificationLineUI> active = new List<NotificationLineUI>();

    private InventoryNetworkSyncManager inv;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (container == null)
            container = transform;
    }

    private void Start()
    {
        inv = InventoryNetworkSyncManager.Instance;
        if (inv != null)
            inv.OnPackItemChanged += OnPackItemChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (inv != null)
            inv.OnPackItemChanged -= OnPackItemChanged;
    }


    private void OnPackItemChanged(int kind, int id, int count)
    {
        string name = GetItemName(kind, id);
        if (name == null)
            return;

        string sign = count > 0 ? "+" : "";
        Color color = count > 0 ? gainColor : lossColor;
        string hex = ColorUtility.ToHtmlStringRGB(color);   // # 없는 "8CD973" 형태

        // 이름 부분만 색 적용 (TMP 리치 텍스트)
        Push($"{sign}{count} <color=#{hex}>{name}</color>");
    }


    public static void Show(string msg)
    {
        if (Instance == null)
            return;

        Instance.Push(msg);
    }


    private void Push(string msg)
    {
        if (string.IsNullOrEmpty(msg))
            return;

        if (active.Count >= maxLines)
            active[0].ForceEnd();

        NotificationLineUI line = pool.Count > 0 ? pool.Dequeue() : CreateLine();
        line.gameObject.SetActive(true);
        line.transform.SetAsLastSibling(); // 맨 아래로 -> 레이아웃이 기존 줄을 위로 밈 

        active.Add(line);
        line.Play(msg);
    }

    private NotificationLineUI CreateLine()
    {
        NotificationLineUI line = Instantiate(linePrefab, container);
        line.OnFinished += Return;               // 끝나면 반납되게 연결
        return line;
    }

    private void Return(NotificationLineUI line)
    {
        if (!active.Remove(line))
            return;

        line.gameObject.SetActive(false);
        pool.Enqueue(line);
    }

    private string GetItemName(int kind, int id)
    {
        PotionDataManager dataManager = PotionDataManager.Instance;
        if (dataManager == null)
            return null;

        if (kind == TeamInventoryState.ItemKindMaterial && dataManager.TryGetMaterialData(id, out var materialData))
            return materialData.materialName;

        if (kind == TeamInventoryState.ItemKindPotion && dataManager.TryGetPotionData(id, out var potionData))
            return potionData.potionName;

        return null;
    }
}
