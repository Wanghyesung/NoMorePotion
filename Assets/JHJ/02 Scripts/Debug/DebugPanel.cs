using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// [개발용] F8로 여닫는 디버그 패널 (OnGUI). 모든 빌드에서 동작.
/// 텔포는 누구나 자기 플레이어만 이동, 시간/웨이브/수정구슬은 판정 권한(방장)만.
/// </summary>
/// <remarks>
/// 텔포 목표는 targets에 Transform으로 연결. 버튼 이름은 Transform 이름을 그대로 씀
/// (예: "평원 보스", "수정구슬 앞")
/// 패널이 열린 동안은 커서를 풀고 플레이어 조작을 막음 (버튼 클릭이 공격으로 들어가지 않게)
/// </remarks>
public class DebugPanel : MonoBehaviour
{
    [FormerlySerializedAs("teleportKey")]
    [SerializeField] private Key toggleKey = Key.F8;
    [SerializeField] private Transform[] targets;           // 텔포 목표 (지역보스 앞, 수정구슬 앞 등)
    [SerializeField, Range(1f, 4f)] private float uiScale = 2f;   // 패널 전체 크기 (글씨 포함), 1080p 기준

    private const float BASE_HEIGHT = 1080f;                // 이 해상도 기준으로 크기 맞춤
    private const float WINDOW_WIDTH = 720f;                 // 두 칸(왼쪽 텔포 / 오른쪽 나머지) + 여백
    private const float COLUMN_WIDTH = 330f;
    private const int TELEPORT_COLUMNS = 2;                 // 텔포 버튼 한 줄에 몇 개

    private bool isOpen;
    private Rect windowRect = new Rect(20f, 20f, WINDOW_WIDTH, 10f);
    private float currentScale = 1f;

    // 열 때 찾아 둠 (씬마다 다를 수 있어서)
    private NightWaveSpawner waveSpawner;
    private CrystalCore crystal;

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current[toggleKey].wasPressedThisFrame)
            return;

        SetOpen(!isOpen);
    }

    private void OnDisable()
    {
        if (isOpen)
            SetOpen(false);
    }

    private void SetOpen(bool open)
    {
        isOpen = open;
        Debug.Log($"[DebugPanel] {(open ? "열림" : "닫힘")}");

        if (open)
        {
            waveSpawner = FindAnyObjectByType<NightWaveSpawner>();
            crystal = FindAnyObjectByType<CrystalCore>();
        }

        // 설정창과 같은 방식: 열면 커서 보이기 + 플레이어 조작 막기
        InputManager input = InputManager.Instance;
        if (input != null)
        {
            input.SetCursorLocked(open);
            input.SetInputEnabled(!open);
        }
    }

    #region 그리기
    private void OnGUI()
    {
        if (!isOpen)
            return;

        // 해상도가 달라도 비슷한 크기로 보이게 확대 (uiScale 로 전체 크기 조절)
        currentScale = uiScale * Screen.height / BASE_HEIGHT;
        GUI.matrix = Matrix4x4.Scale(new Vector3(currentScale, currentScale, 1f));

        windowRect.width = WINDOW_WIDTH;
        windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, $"디버그 패널 ({toggleKey})");
    }

    private void DrawWindow(int id)
    {
        // 스크롤 없이 내용 높이만큼 창이 늘어남 (GUILayout.Window 가 내용에 맞춰 높이를 정함)
        // 왼쪽: 텔포 / 오른쪽: 시간, 웨이브, 수정구슬 → 세로 길이를 줄여 스크롤을 덜 하게
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(COLUMN_WIDTH));
        DrawTeleport();
        GUILayout.EndVertical();

        GUILayout.Space(10f);

        GUILayout.BeginVertical(GUILayout.Width(COLUMN_WIDTH));
        DrawTime();
        DrawWave();
        DrawCrystal();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        if (GUILayout.Button("닫기"))
            SetOpen(false);

        GUI.DragWindow();
    }

    private void DrawTeleport()
    {
        GUILayout.Label("■ 텔레포트 (나만)");

        if (targets == null || targets.Length == 0)
        {
            GUILayout.Label("  목표 위치 없음 (targets 연결 필요)");
            return;
        }

        // 한 줄에 TELEPORT_COLUMNS 개씩
        int inRow = 0;
        GUILayout.BeginHorizontal();

        foreach (Transform target in targets)
        {
            if (target == null)
                continue;

            if (inRow == TELEPORT_COLUMNS)
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                inRow = 0;
            }

            if (GUILayout.Button(target.name))
                TeleportLocal(target);

            inRow++;
        }

        GUILayout.EndHorizontal();
    }

    private void DrawTime()
    {
        GUILayout.Label("■ 시간 (방장)");

        TimeManager time = TimeManager.Instance;
        if (time == null)
        {
            GUILayout.Label("  TimeManager 없음");
            return;
        }

        string phase = time.Phase.CurrentValue == TimePhase.Day ? "낮" : "밤";
        GUILayout.Label($"  {time.DayCount.CurrentValue}일차 {phase} (남은 {time.RemainSec.CurrentValue}초)");

        GUI.enabled = GameAuthority.HasAuthority;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("다음 낮으로"))
            time.DebugSkipTo(TimePhase.Day);
        if (GUILayout.Button("다음 밤으로"))
            time.DebugSkipTo(TimePhase.Night);
        GUILayout.EndHorizontal();
        GUI.enabled = true;
    }

    private void DrawWave()
    {
        GUILayout.Space(8f);
        GUILayout.Label("■ 웨이브 (방장)");

        if (waveSpawner == null)
        {
            GUILayout.Label("  NightWaveSpawner 없음");
            return;
        }

        GUI.enabled = GameAuthority.HasAuthority;
        bool on = waveSpawner.WaveEnabled;
        if (GUILayout.Button(on ? "웨이브 켜짐 → 끄기" : "웨이브 꺼짐 → 켜기"))
            waveSpawner.SetWaveEnabled(!on);
        GUI.enabled = true;
    }

    private void DrawCrystal()
    {
        GUILayout.Space(8f);
        GUILayout.Label("■ 수정구슬 (방장)");

        if (crystal == null)
        {
            GUILayout.Label("  CrystalCore 없음");
            return;
        }

        GUILayout.Label($"  HP {crystal.HP} / {crystal.MaxHP}");

        GUI.enabled = GameAuthority.HasAuthority && !crystal.IsBroken;
        if (GUILayout.Button("수정구슬 회복 (최대치)"))
            crystal.Heal(crystal.MaxHP);    // 최대치 제한, HUD 갱신, 동기화는 Heal 안에서 처리
        GUI.enabled = true;
    }
    #endregion

    #region 텔레포트
    private void TeleportLocal(Transform target)
    {
        global::Player local = FindLocalPlayer();
        if (local == null)
        {
            Debug.LogWarning("[DebugPanel] 내 플레이어를 찾지 못했습니다.");
            return;
        }

        local.transform.SetPositionAndRotation(target.position, target.rotation);

        Rigidbody body = local.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = target.position;
            body.rotation = target.rotation;
            body.linearVelocity = Vector3.zero;
        }

        Debug.Log($"[DebugPanel] {target.name} 으로 이동");
    }

    private static global::Player FindLocalPlayer()
    {
        foreach (global::Player player in FindObjectsByType<global::Player>(FindObjectsSortMode.None))
        {
            if (player.IsLocalPlayer)
                return player;
        }
        return null;
    }
    #endregion
}
