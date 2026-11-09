using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "몬스터 웨이브 테이블", menuName = "Spawn/MonsterWaveTable")]
public class WaveTable : ScriptableObject
{
    [SerializeField] private List<WaveData> waves;

    public IReadOnlyList<WaveData> Waves => waves;


    public WaveData GetWave(int dayCount)
    {
        if (waves == null || waves.Count == 0)
            return null;

        // 넘치면 마지막 웨이브 반복
        int index = Mathf.Clamp(dayCount - 1, 0, waves.Count - 1);

        return waves[index];
    }
}
