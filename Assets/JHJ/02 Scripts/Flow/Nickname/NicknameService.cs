using Photon.Pun;
using R3;
using System;

/// <summary>
/// 닉네임 값 보관, 검증, 저장, Photon 닉네임 반영.
/// </summary>
/// <remarks>
/// - NetworkFlowManager 전용. 상태(FlowState) 검사는 매니저가 하고 이 클래스는 상태를 모른다.
/// - UI는 매니저가 공개한 Nickname / HasSavedNickname을 사용한다 (이 클래스를 직접 쓰지 않음)
/// - 저장소(PlayerPrefs)는 NicknameStore, 글자 수 상한은 NicknameStore.MAX_LENGTH
/// - PhotonNetwork.NickName은 항상 이 클래스가 맞춘다 (생성 시, 변경 시)
/// </remarks>
public class NicknameService : IDisposable
{
    private readonly NicknameStore store = new NicknameStore();
    private readonly Action<string> raiseError;
    private readonly ReactiveProperty<string> nickname;


    public ReadOnlyReactiveProperty<string> Nickname => nickname;
    /// <summary> 저장된 닉네임이 있는지. false면 기본 닉네임(PlayerXXXX) 사용 중 </summary>
    public bool HasSaved { get; private set; }


    /// <param name="raiseError">검증 실패 메시지를 띄울 함수 (매니저의 에러 알림)</param>
    public NicknameService(Action<string> raiseError)
    {
        this.raiseError = raiseError;

        // 저장된 닉네임이 있으면 사용, 없으면 기본 닉네임
        string saved = store.Load();
        HasSaved = !string.IsNullOrEmpty(saved);

        nickname = new ReactiveProperty<string>(HasSaved ? saved : store.CreateDefault());
        PhotonNetwork.NickName = nickname.Value;
    }

    /// <summary>
    /// 닉네임 변경. 검증 실패 시 에러를 알리고 false. 성공 시 저장 + Photon 반영 후 true.
    /// </summary>
    public bool TryChange(string nick)
    {
        nick = (nick ?? "").Trim();

        if (string.IsNullOrEmpty(nick))
        {
            raiseError("닉네임을 입력하세요.");
            return false;
        }

        // [임시] 닉네임 길이 규칙 확인
        if (nick.Length > NicknameStore.MAX_LENGTH)
        {
            raiseError($"닉네임 글자 수를 {NicknameStore.MAX_LENGTH}자 이내로 입력하세요.");
            return false;
        }

        store.Save(nick);
        HasSaved = true;
        nickname.Value = nick;
        PhotonNetwork.NickName = nick;
        return true;
    }

    public void Dispose()
    {
        nickname.Dispose();
    }
}
