using Photon.Pun;
using R3;
using UnityEngine;


/// <summary>
/// 한 판의 승패 판정. 앱 종료·인게임 나가기는 NetworkFlowManager 담당.
/// </summary>
/// <remarks>
/// [보고] GameEndManager.Instance.ReportClear() / ReportDefeat()
///   방장에서만 반영, 먼저 온 보고만 유효 (이후 보고는 무시)
///
/// [구독] using R3; 필요, Start에서 구독
///   GameEndManager.Instance.Result
///       .Where(r => r != GameResult.None)
///       .Subscribe(r => { ... }).AddTo(this);
///   .Skip(1) 대신 Where 사용 → 판이 끝난 뒤에 구독해도 결과를 받음
///
/// [확인만] IsEnded : 구독 없이 지금 끝났는지 확인
public class GameEndManager : MonoBehaviour
{
    public static GameEndManager Instance;

    private ReactiveProperty<GameResult> result = new ReactiveProperty<GameResult>(GameResult.None);


    /// <summary> 판 결과. 끝나기 전에는 None, 구독 즉시 현재 값 받음 </summary>
    public ReadOnlyReactiveProperty<GameResult> Result => result;
    /// <summary> 판이 끝났는지 (구독 없이 확인용) </summary>
    public bool IsEnded => result.CurrentValue != GameResult.None;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GameEndManager] 인스턴스 중복, 삭제");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        result?.Dispose();
    }
    
    public void ReportClear() => Finish(GameResult.Clear) ;
    
    public void ReportDefeat() => Finish(GameResult.Defeat);
    

    private void Finish(GameResult result)
    {
        // TODO : 방장 외 클라이언트에 결과 전달 필요
        if (!PhotonNetwork.IsMasterClient)
            return;

        if(IsEnded)
        {
            Debug.Log($"[GameEndManager] 이미 종료됨, 무시 : {result}");
            return;
        }

        this.result.Value = result;

        Debug.Log($"[GameEndManager] 게임 종료 : {result}");
    }


    [ContextMenu("디버그 클리어")]
    private void DebugClear() => ReportClear();

    [ContextMenu("디버그 패배")]
    private void DebugDefeat() => ReportDefeat();
}
