using UnityEngine;

public enum CastleStage
{
    Ready,
    Voting,
    InCastle,
    Fighting
}

public enum VoteCloseReason
{
    Passed,
    Rejected,
    TimeOut,
    PlayerChanged
}
