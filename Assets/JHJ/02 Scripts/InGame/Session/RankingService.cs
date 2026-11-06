using Photon.Pun;
using UnityEngine;

public enum RankingSubmitResult
{
    None,           // 아직 등록 시도 전 (판 진행 중)
    NotEligible,    // 클리어가 아님
    Offline,        // 오프라인 싱글 → 등록 안 함
    Submitted,      // 등록 요청 보냄
    NotImplemented  // 온라인이지만 등록 미구현
}

/// <summary>
/// 랭킹 등록 창구. 클리어한 판만 등록, 오프라인 싱글은 등록하지 않음.
/// </summary>
/// <remarks>
/// GameSessionManager가 판정 순간 방장에서 1번만 호출한다. UI 등 다른 곳에서 호출하지 말 것 (중복 등록)
/// TODO(온라인): 서버(Firebase 등) 등록 구현. 비동기가 되면 UniTask 반환 + Pending 상태 추가
/// </remarks>
public static class RankingService
{
    public static RankingSubmitResult TrySubmit(GameRecord record)
    {
        if (PhotonNetwork.OfflineMode)
        {
            Debug.Log("[RankingService] 오프라인 모드, 랭킹 등록 안 함");
            return RankingSubmitResult.Offline;
        }

        if (record.Result != GameResult.Clear)
            return RankingSubmitResult.NotEligible;

        Debug.Log($"[RankingService] 랭킹 등록 미구현 : {record.DayCount}일차, {record.PlayTime:F1}초");
        return RankingSubmitResult.NotImplemented;
    }
}
