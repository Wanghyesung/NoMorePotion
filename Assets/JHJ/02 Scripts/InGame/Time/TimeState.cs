using System;

/// <summary>
/// 타임매니저의 현재 시간 상태. 세이브 시 복원에 필요한 최소값 묶음.
/// </summary>
/// <remarks>
/// class를 사용한 이유: 타임매니저가 매 프레임 값을 갱신하는 가변 상태라
/// 가변 struct의 복사 함정(복사본 수정, 프로퍼티 CS1612)을 피하기 위함.
/// 참조 전달로 외부에서 값이 바뀌는 문제는 외부에 넘길 때 복사본을 제공해 막아야함.
/// TODO : 세이브 시 복사본으로 제공하도록
/// </remarks>
[Serializable]
public class TimeState 
{
    public int DayCount;
    public TimePhase Phase;
    public float Elapsed;
    public float TotalElapsed;

}
