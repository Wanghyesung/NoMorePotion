using System.Collections.Generic;
using UnityEngine;
using static JJH_Constants;
using SF = UnityEngine.SerializeField;
public class MonsterTrigger : MonoBehaviour
{
    [SF] private MonsterMove mover;
    private List<GameObject> players;

    private void Awake()
    {
        players = new List<GameObject>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(PLAYER_TAG))
        {
            if(mover.nowTarget == null)
                mover.nowTarget = other.gameObject;
            Debug.Log(other.gameObject);
            players.Add(other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (players.Contains(other.gameObject))
        {
            Debug.Log(other.gameObject);
            players.Remove(other.gameObject);

            if(other.gameObject == mover.nowTarget && players.Count > 0)
                mover.nowTarget = players[Random.Range(0, players.Count)];
        }
    }
}
