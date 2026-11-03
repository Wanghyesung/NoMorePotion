using UnityEngine;

using SF = UnityEngine.SerializeField;
[RequireComponent(typeof(MonsterAnimationController))]

[RequireComponent(typeof(MonsterMove))]
public class MonsterCombat : MonoBehaviour
{
    [SF] private MonsterAnimationController anim;
    [SF] private MonsterMove mover;

    private void Awake()
    {
        anim = GetComponent<MonsterAnimationController>();
        mover = GetComponent<MonsterMove>();
    }

    private void Start()
    {
    }
}
