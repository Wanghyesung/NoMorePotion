using MoreMountains.Feedbacks;
using UnityEngine;
//using static UnityEditor.PlayerSettings;
using SF = UnityEngine.SerializeField;
public class GiantAnimationBridge : MonsterAnimationBridge
{
    [SF] private MMF_Player mmfPlayer;

    private void FootSound() => mmfPlayer.PlayFeedbacks(transform.position);
}
