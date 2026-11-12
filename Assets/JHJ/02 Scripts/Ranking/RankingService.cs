using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using Photon.Pun;
using System;
using System.Linq;
using System.Threading;
using UnityEngine;

public enum RankingSubmitResult
{
    None,           // 아직 등록 시도 전 (판 진행 중)
    NotEligible,    // 클리어가 아님
    Offline,        // 오프라인 싱글 → 등록 안 함
    Pending,        // 등록 중
    Submitted,      // 등록 완료
    NotHost,        // MasterClient가 아닐때
    Failed          // 등록 실패
}

/// <summary>
/// 랭킹 등록 창구. 클리어한 판만 등록, 오프라인 싱글은 등록하지 않음.
/// </summary>
/// <remarks>
/// GameSessionManager가 판정 순간 방장에서 1번만 호출한다. UI 등 다른 곳에서 호출하지 말 것 (중복 등록)
/// </remarks>
public static class RankingService
{
    private const int MAX_ATTEMPTS = 3;
    private const int FIRST_RETRY_DELAY_MS = 1000;
    
    private static FirebaseRankingStore store = new FirebaseRankingStore();

    public static async UniTask<RankingSubmitResult> SubmitAsync(GameRecord record, CancellationToken ct)
    {
        RankingEntry entry = BuildEntry(record);

        if (entry == null)                      // UserId 없음 등 → BuildEntry에서 null로 알려 줄 때
            return RankingSubmitResult.Failed;

        string key = store.CreateKey();
        int delay = FIRST_RETRY_DELAY_MS;

        for (int attempt = 1; attempt <= MAX_ATTEMPTS; attempt++)
        {
            try
            {
                await store.SaveAsync(key, entry, ct);
                return RankingSubmitResult.Submitted;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RankingService] 저장 실패 ({attempt}/{MAX_ATTEMPTS}): {e.Message}");

                try
                {
                    if (await store.ExistsAsync(key, ct))
                        return RankingSubmitResult.Submitted;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception) { }
            }

            if (attempt == MAX_ATTEMPTS)
                return RankingSubmitResult.Failed;

            await UniTask.Delay(delay, cancellationToken: ct);
            delay *= 2;
        }

        return RankingSubmitResult.Failed;
    }

    private static RankingEntry BuildEntry(GameRecord record)
    {
        string uid = NetworkFlowManager.Instance?.UserId;
        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogWarning("[RankingService] UserId 없음 → 랭킹 저장 안 함");
            return null;
        }

        RankingEntry entry = new RankingEntry
        {
            Uid = uid,
            Members = PhotonNetwork.PlayerList.Select(p => p.NickName).ToList(),
            MemberCount = PhotonNetwork.PlayerList.Length,
            PlayTime = record.PlayTime,
            DayCount = record.DayCount,
        };

        return entry;
    }

    /// 등록할 수 있으면 true. 없으면 false + 이유(Offline / NotEligible)
    public static bool CanSubmit(GameRecord record, out RankingSubmitResult reason)
    {
        if (PhotonNetwork.OfflineMode)          // G-55: 오프라인 먼저
        {
            reason = RankingSubmitResult.Offline;
            return false;
        }

        if (record.Result != GameResult.Clear)
        {
            reason = RankingSubmitResult.NotEligible;
            return false;
        }

        reason = RankingSubmitResult.None;      // 쓰지 않는 값
        return true;
    }

    public static UniTask<List<RankingEntry>> LoadTopAsync(int count, CancellationToken ct)
    {
        return store.LoadTopAsync(count, ct);
    }
}
