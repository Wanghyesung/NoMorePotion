using UnityEngine;

public interface IMonsterState
{
    public void OnStart();
    public void OnStay();
    public void OnEnd();
}
