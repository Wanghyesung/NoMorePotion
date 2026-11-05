using NUnit.Framework;
using UnityEngine;

/*///////////////////////////////////////////
               ParabolicMovement
기능 : 전달받은 포물선 데이터를 복사하여 목표 위치까지 독립적으로 이동
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class ParabolicMovement : MonoBehaviour
{
    private ThrowData throwData;
    private float elapsedTime;
    public bool IsFlying { get; private set; }
    public Vector3 PrePos { get; private set; }

    [SerializeField] private float speedMultiplier = 1f;

    // data: 발사 순간 확정된 월드 궤적. 조준선의 좌표 리스트는 참조하지 않음
    public void Launch(ThrowData data)
    {
        throwData = data;
        elapsedTime = 0f;
        transform.position = data.Origin;
        PrePos = data.Origin;
        IsFlying = true;
    }

    private void Update()
    {
        if (IsFlying == false)
            return;

        elapsedTime = Mathf.Min(elapsedTime + Time.deltaTime * speedMultiplier, throwData.FlightTime);
        PrePos = transform.position;
        transform.position = throwData.GetPosition(elapsedTime);
        if (elapsedTime >= throwData.FlightTime)
        {
            transform.position = throwData.Target;
            IsFlying = false;
        }
    }
}
