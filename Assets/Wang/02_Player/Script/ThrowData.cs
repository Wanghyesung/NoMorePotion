using UnityEngine;

/*///////////////////////////////////////////
                  ThrowData
기능 : 목표 위치와 포물선 높이로 초기 속도·비행 시간을 역산
       계산한 궤적에서 시간별 위치를 제공
 *///////////////////////////////////////////

public readonly struct ThrowData
{
    #region TrajectoryData
    public Vector3 Origin { get; }
    public Vector3 Velocity { get; }
    public Vector3 Gravity { get; }
    public float FlightTime { get; }
    #endregion

    // origin/target: 월드 위치, gravity: 중력 가속도, arcHeight: 두 위치 중 높은 곳 위의 정점 높이
    public ThrowData(Vector3 origin, Vector3 target, Vector3 gravity, float arcHeight)
    {
        
        float acceleration = gravity.magnitude;
        Origin = origin;
        Gravity = gravity;

        float targetHeight = Vector3.Dot(target - origin, -gravity / acceleration);
        float peakHeight = Mathf.Max(0f, targetHeight) + arcHeight;

        FlightTime = Mathf.Sqrt(2f * peakHeight / acceleration)
            + Mathf.Sqrt(2f * (peakHeight - targetHeight) / acceleration);

        // 목표 = 출발 + 초기 속도 * 시간 + 1/2 * 중력 * 시간²을 초기 속도에 대해 역산
        Velocity = (target - origin - gravity * (0.5f * FlightTime * FlightTime)) / FlightTime;
    }

    // time: 발사 후 경과 시간(초). p0 + v0t + 1/2gt^2
    public Vector3 GetPosition(float time)
    {
        return Origin + Velocity * time + Gravity * (0.5f * time * time);
    }
}
