using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using SF = UnityEngine.SerializeField;
using MoreMountains.Tools;
using TMPro;
using Photon.Pun;
using DamageNumbersPro;
using UnityEngine.InputSystem;

public class SettingsPopupUI : MonoBehaviour
{

    [Header("설정 공용")]
    [SF] private Button closeBtn;
    [SF] private ConfirmPopupUI confirmPopup;
    [SF] private Button saveBtn;        // 설정 저장 버튼

    [Header("탭")]                                
    [SF] private Toggle soundTab;
    [SF] private Toggle minimapTab;
    [SF] private Toggle gameTab;

    [Header("사운드")]
    [SF] private Slider masterSlider;
    [SF] private Slider bgmSlider;
    [SF] private Slider sfxSlider;
    [SF] private TMP_Text masterText;
    [SF] private TMP_Text bgmText;
    [SF] private TMP_Text sfxText;

    [Header("게임")]
    [SF] private Button gameSaveBtn;
    [SF] private Button leaveGameBtn;
    [SF] private DamageNumber saveNotice;
    [SF] private RectTransform noticeCanvas;

    [Header("미니맵")]
    [SF] private Toggle minimapRotateToggle;
    [SF] private Toggle minimapFixedToggle;

    [Header("기타(시스템)")]
    [SF] private Button toTitleBtn;
    [SF] private Button quitBtn;
    [SF] private Button tutorialBtn;


    private float savedMaster;
    private float savedBgm;
    private float savedSfx;
    private bool savedMinimapRotate;
    private const float VOLUME_TOLERANCE = 0.001f;
    private bool isInGame;
    private DamageNumber activeNotice;  // 지금 떠 있는 알림 (설정창 닫을때 바로 지우기 위해)


    public bool IsOpen => gameObject.activeSelf;


    private void Awake()
    {
        closeBtn.onClick.AddListener(Cancel);
        saveBtn.onClick.AddListener(Save);
        toTitleBtn.onClick.AddListener(() => ToTitleFlow().Forget());
        quitBtn.onClick.AddListener(() => QuitFlow().Forget());
        tutorialBtn.onClick.AddListener(() => TutorialFlow().Forget());
        leaveGameBtn.onClick.AddListener(() => ToTitleFlow().Forget());
        gameSaveBtn.onClick.AddListener(() => OnGameSave());

        masterSlider.onValueChanged.AddListener(OnMasterChanged);
        bgmSlider.onValueChanged.AddListener(OnBgmChanged);
        sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        minimapRotateToggle.onValueChanged.AddListener(OnMinimapRotateChanged);
    }

    #region 열기/닫기
    public void Open()
    {
        if (gameObject.activeSelf)
            return;

        // 게임 탭
        NetworkFlowManager manager = NetworkFlowManager.Instance;

        FlowState state = manager != null ? manager.CurrentState : FlowState.Title;
        isInGame = state == FlowState.InGame;
        bool canSave = isInGame && PhotonNetwork.OfflineMode && SaveSession.HasSlot();

        gameTab.gameObject.SetActive(isInGame);
        gameSaveBtn.gameObject.SetActive(canSave);
        leaveGameBtn.gameObject.SetActive(isInGame);
        toTitleBtn.gameObject.SetActive(state == FlowState.Lobby);
        tutorialBtn.gameObject.SetActive(state == FlowState.Title);

        // 사운드탭
        CacheCurrentSettings();

        InitSlider(masterSlider, masterText, MMSoundManager.MMSoundManagerTracks.Master);
        InitSlider(bgmSlider, bgmText, MMSoundManager.MMSoundManagerTracks.Music);
        InitSlider(sfxSlider, sfxText, MMSoundManager.MMSoundManagerTracks.Sfx);


        // 미니맵 탭
        bool rotate = MinimapSettings.RotateWithPlayer;
        Toggle onOption = rotate ? minimapRotateToggle : minimapFixedToggle;
        Toggle offOption = rotate ? minimapFixedToggle : minimapRotateToggle;
        onOption.SetIsOnWithoutNotify(true);
        offOption.SetIsOnWithoutNotify(false);

        // 사운드탭부터
        if (soundTab != null)
            soundTab.isOn = true;

        RefreshSaveButton();

        if (isInGame)
            LockGameplay(true);

        gameObject.SetActive(true);
    }

    public void Close()
    {
        if (!gameObject.activeSelf)
            return;

        ClearNotice();

        if (isInGame)
            LockGameplay(false);

        gameObject.SetActive(false);
    }

    // 저장 안 한 변경을 버리고 닫기
    public void Cancel()
    {
        MMSoundManager sm = MMSoundManager.Instance;
        sm.SetVolumeMaster(savedMaster);
        sm.SetVolumeMusic(savedBgm);
        sm.SetVolumeSfx(savedSfx);
        MinimapSettings.RotateWithPlayer = savedMinimapRotate;
        Close();
    }

    private void LockGameplay(bool locked)
    {
        InputManager input = InputManager.Instance;
        if (input == null)
            return;

        input.SetCursorLocked(locked);
        input.SetInputEnabled(!locked);
    }

    #endregion

    #region 설정 저장

    private void Save()
    {
        MMSoundManager.Instance.SaveSettings();
        MinimapSettings.Save();
        CacheCurrentSettings();
        RefreshSaveButton();
    }

    private void CacheCurrentSettings()
    {
        MMSoundManager sm = MMSoundManager.Instance;
        savedMaster = sm.GetTrackVolume(MMSoundManager.MMSoundManagerTracks.Master, false);
        savedBgm = sm.GetTrackVolume(MMSoundManager.MMSoundManagerTracks.Music, false);
        savedSfx = sm.GetTrackVolume(MMSoundManager.MMSoundManagerTracks.Sfx, false);
        savedMinimapRotate = MinimapSettings.RotateWithPlayer;
    }

    // 저장된 값이랑 달라졌을때만 저장버튼 활성화
    private void RefreshSaveButton()
    {
        saveBtn.interactable = IsChanged(masterSlider.value, savedMaster)
                            || IsChanged(bgmSlider.value, savedBgm)
                            || IsChanged(sfxSlider.value, savedSfx)
                            || minimapRotateToggle.isOn != savedMinimapRotate;
    }

    private bool IsChanged(float current, float saved)
    {
        return Mathf.Abs(current - saved) > VOLUME_TOLERANCE;
    }
    #endregion

    #region 사운드

    private void OnMasterChanged(float v)
    {
        MMSoundManager.Instance.SetVolumeMaster(v);
        SetPercentText(masterText, v);
        RefreshSaveButton();
    }

    private void OnBgmChanged(float v)
    {
        MMSoundManager.Instance.SetVolumeMusic(v);
        SetPercentText(bgmText, v);
        RefreshSaveButton();
    }

    private void OnSfxChanged(float v)
    {
        MMSoundManager.Instance.SetVolumeSfx(v);
        SetPercentText(sfxText, v);
        RefreshSaveButton();
    }

    private void SetPercentText(TMP_Text text, float v)
    {
        text.text = $"{Mathf.RoundToInt(v * 100)}%";
    }

    private void InitSlider(Slider slider, TMP_Text text, MMSoundManager.MMSoundManagerTracks track)
    {
        float v = MMSoundManager.Instance.GetTrackVolume(track, false);
        slider.SetValueWithoutNotify(v);
        SetPercentText(text, v);
    }

    #endregion

    #region 기타
    private async UniTaskVoid ToTitleFlow()
    {
        NetworkFlowManager manager = NetworkFlowManager.Instance;
        if (manager == null)
        {
            Debug.LogError("[SettingsPopupUI] NetworkFlowManager 없음");
            return;
        }

        string message = !isInGame ? "타이틀로 돌아가시겠습니까?"
                : PhotonNetwork.OfflineMode && SaveSession.HasSlot() ? "마지막 저장 이후의 진행은 사라집니다.\n게임을 나가시겠습니까?"
                : PhotonNetwork.OfflineMode ? "게임을 종료하고 타이틀로 돌아가시겠습니까?"  // 추후 튜토리얼에서 사용될 예정
                : "게임을 나가 로비로 돌아가시겠습니까?";

        if (!await Confirm(message))
            return;

        Cancel();

        if (isInGame)
        {
            InputManager input = InputManager.Instance;
            if (input != null)
            {
                input.FocusCursor();
            }
            manager.RequestLeaveGame();
        }
        else
        {
            manager.RequestReturnToTitle();
        }
    }

    private async UniTaskVoid TutorialFlow()
    {
        NetworkFlowManager manager = NetworkFlowManager.Instance;

        if (manager == null)
        {
            Debug.LogError("[SettingsPopupUI] NetworkFlowManager 없음");
            return;
        }

        if (!await Confirm("튜토리얼을 진행하시겠습니까?"))
            return;

        Cancel();
        manager.RequestStartTutorial();
    }

    private async UniTaskVoid QuitFlow()
    {
        if (await Confirm("게임을 종료하시겠습니까?"))
            QuitGame.Quit();
    }

    // 확인 팝업이 없으면 바로 진행
    private UniTask<bool> Confirm(string message)
    {
        return confirmPopup != null ? confirmPopup.ShowAsync(message) : UniTask.FromResult(true);
    }
    #endregion

    #region 미니맵
    private void OnMinimapRotateChanged(bool isOn)
    {
        MinimapSettings.RotateWithPlayer = isOn;
        RefreshSaveButton();
    }
    #endregion

    #region 게임저장
    private void OnGameSave()
    {
        if (SaveManager.Instance == null)
            return;

        SaveResult result = SaveManager.Instance.SaveNow();
        string message = "저장상태 확인불가";

        switch (result)
        {
            case SaveResult.Saved:
                message = "저장되었습니다!";
                break;

            case SaveResult.BlockedAtNight:
                if (SaveManager.Instance.LastSavedDay > 0)
                    message = $"밤에는 저장할 수 없습니다\n(마지막 저장: {SaveManager.Instance.LastSavedDay}일차 낮)";
                else
                    message = "밤에는 저장할 수 없습니다.";
                break;

            case SaveResult.Failed:
                message = "저장에 실패했습니다.";
                break;

            default:
                break;
        }

        ShowNotice(message);
    }

    // DamageNumberPro로 노티스 보여줌
    private void ShowNotice(string message)
    {
        if (saveNotice == null || noticeCanvas == null || Mouse.current == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            noticeCanvas,
            Mouse.current.position.ReadValue(),
            null,
            out Vector2 localPoint);

        ClearNotice();
        activeNotice = saveNotice.SpawnGUI(noticeCanvas, localPoint + new Vector2(0, 50), message);
    }

    private void ClearNotice()
    {
        if (activeNotice != null && activeNotice.gameObject.activeSelf)
            activeNotice.DestroyDNP();

        activeNotice = null;
    }
    #endregion
}
