using UnityEngine;
using UnityEngine.AI;
using SF = UnityEngine.SerializeField;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterMove : MonoBehaviour
{
    [SF] private NavMeshAgent agent;
    [SF] private GameObject nowTarget;

    private void Awake()
    {
        if(agent == null)
            agent = GetComponent<NavMeshAgent>();
    }

}
