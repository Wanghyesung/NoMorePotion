using UnityEngine;

/*///////////////////////////////////////////
                  ThrowData
기능 : 목표점과 포물선 높이로 초기 속도·도착 시간을 계산
 *///////////////////////////////////////////

public readonly struct ThrowData
{
    #region TrajectoryData
    public Vector3 Origin { get; }
    public Vector3 Target { get; }
    public float ArcHeight { get; }
    public Vector3 Velocity { get; }
    public Vector3 Gravity { get; }
    public float FlightTime { get; }
    #endregion

    public ThrowData(Vector3 origin, Vector3 target, Vector3 gravity, float arcHeight)
    {
        Origin = origin;
        Target = target;
        Gravity = gravity;
        ArcHeight = arcHeight;

        float acceleration = gravity.magnitude;
        // 출발점을 높이 0으로 보고, 출발점과 목표점 중 높은 쪽보다 arcHeight만큼 더 올라간다.
        float targetHeight = Vector3.Dot(target - origin, -gravity.normalized);
        float peakHeight = Mathf.Max(0f, targetHeight) + arcHeight;
        // 정점까지 올라가는 시간과 정점에서 목표 높이까지 내려가는 시간을 더한다.
        FlightTime = Mathf.Sqrt(2f * peakHeight / acceleration)
            + Mathf.Sqrt(2f * (peakHeight - targetHeight) / acceleration);
        // 정해진 시간에 목표점에 도착하도록 운동식에서 초기 속도를 역산한다.
        Velocity = (target - origin - gravity * (0.5f * FlightTime * FlightTime)) / FlightTime;
    }

    // time: 발사 후 경과 시간(초). p0 + v0t + 1/2gt^2
    public Vector3 GetPosition(float time)
    {
        // 프레임 간격이나 통신 지연으로 도착 시간이 지나도 목표점을 넘어가지 않는다.
        if (time >= FlightTime)
            return Target;
        return Origin + Velocity * time + Gravity * (0.5f * time * time);
    }

    // 손에서 놓는 순간 출발점만 갱신하고, 같은 목표점에 도착하도록 속도와 시간을 다시 계산한다.
    public ThrowData WithOrigin(Vector3 origin) => new ThrowData(origin, Target, Gravity, ArcHeight);
}
