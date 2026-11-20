using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using SF = UnityEngine.SerializeField;
using MoreMountains.Tools;
using TMPro;
using Photon.Pun;

public class SettingsPopupUI : MonoBehaviour
{
    [SF] private Button toTitleBtn;
    [SF] private Button quitBtn;
    [SF] private Button closeBtn;
    [SF] private ConfirmPopupUI confirmPopup;
    [SF] private Button saveBtn;

    [Header("탭")]                                
    [SF] private Toggle soundTab;

    [Header("사운드")]
    [SF] private Slider masterSlider;
    [SF] private Slider bgmSlider;
    [SF] private Slider sfxSlider;
    [SF] private TMP_Text masterText;
    [SF] private TMP_Text bgmText;
    [SF] private TMP_Text sfxText;

    [Header("게임")]
    [SF] private TMP_Text toTitleText;  // 버튼라벨

    private float savedMaster;
    private float savedBgm;
    private float savedSfx;
    private const float VOLUME_TOLERANCE = 0.001f;
    private bool isInGame;


    public bool IsOpen => gameObject.activeSelf;


    private void Awake()
    {
        closeBtn.onClick.AddListener(Cancel);
        saveBtn.onClick.AddListener(Save);
        toTitleBtn.onClick.AddListener(() => ToTitleFlow().Forget());
        quitBtn.onClick.AddListener(() => QuitFlow().Forget());

        masterSlider.onValueChanged.AddListener(OnMasterChanged);
        bgmSlider.onValueChanged.AddListener(OnBgmChanged);
        sfxSlider.onValueChanged.AddListener(OnSfxChanged);

    }

    #region 열기/닫기
    public void Open()
    {
        if (gameObject.activeSelf)
            return;

        NetworkFlowManager manager = NetworkFlowManager.Instance;

        FlowState state = manager != null ? manager.CurrentState : FlowState.Title;
        isInGame = state == FlowState.InGame;

        // 로비 : 타이틀로 /  인게임 : 게임 나가기
        toTitleBtn.gameObject.SetActive(state == FlowState.Lobby || isInGame);

        if (toTitleText != null)
            toTitleText.text = isInGame ? "게임 나가기" : "타이틀로 돌아가기";

        CacheCurrentVolumes();

        InitSlider(masterSlider, masterText, MMSoundManager.MMSoundManagerTracks.Master);
        InitSlider(bgmSlider, bgmText, MMSoundManager.MMSoundManagerTracks.Music);
        InitSlider(sfxSlider, sfxText, MMSoundManager.MMSoundManagerTracks.Sfx);

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

    #region 저장

    private void Save()
    {
        MMSoundManager.Instance.SaveSettings();
        CacheCurrentVolumes();
        RefreshSaveButton();
    }

    private void CacheCurrentVolumes()
    {
        MMSoundManager sm = MMSoundManager.Instance;
        savedMaster = sm.GetTrackVolume(MMSoundManager.MMSoundManagerTracks.Master, false);
        savedBgm = sm.GetTrackVolume(MMSoundManager.MMSoundManagerTracks.Music, false);
        savedSfx = sm.GetTrackVolume(MMSoundManager.MMSoundManagerTracks.Sfx, false);
    }

    // 저장된 값이랑 달라졌을때만 저장버튼 활성화
    private void RefreshSaveButton()
    {
        saveBtn.interactable = IsChanged(masterSlider.value, savedMaster)
                            || IsChanged(bgmSlider.value, savedBgm)
                            || IsChanged(sfxSlider.value, savedSfx);
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

    #region 게임
    private async UniTaskVoid ToTitleFlow()
    {
        NetworkFlowManager manager = NetworkFlowManager.Instance;
        if (manager == null)
        {
            Debug.LogError("[SettingsPopupUI] NetworkFlowManager 없음");
            return;
        }

        string message = !isInGame ? "타이틀로 돌아가시겠습니까?"
                : PhotonNetwork.OfflineMode ? "게임을 종료하고 타이틀로 돌아가시겠습니까?"
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
}
