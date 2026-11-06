using System;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.SO
{
    /// <summary>
    /// 월드 오브젝트가 드롭하는 재료 한 종류와 그 개수 범위입니다. 예) 꽃잎 1~3개
    /// 실제로 몇 개가 나오는지는 <see cref="Roll"/> 로 정합니다.
    /// </summary>
    [Serializable]
    public class MaterialDrop
    {
        [SerializeField, LabelText("재료"), Required]
        private MaterialData _material;
        public MaterialData Material => _material;

        [SerializeField, LabelText("최소 개수"), MinValue(1)]
        private int _min = 1;
        public int Min => _min;

        [SerializeField, LabelText("최대 개수"), MinValue(1)]
        private int _max = 1;
        public int Max => _max;

        /// <summary>
        /// 최소~최대 사이(양 끝 포함)에서 개수를 하나 정합니다. 최대가 최소보다 작게 잘못 입력되어 있으면 최소 개수를 사용합니다.
        /// 멀티플레이에서는 이 값을 한 곳(권한을 가진 쪽)에서만 정하고 결과만 공유해야 합니다.
        /// </summary>
        public int Roll()
        {
            int min = Mathf.Max(1, _min);
            int max = Mathf.Max(min, _max);
            return UnityEngine.Random.Range(min, max + 1);
        }
    }

    /// <summary>
    /// 드롭을 굴린 결과 한 건입니다. 재료 ID 와 개수만 담습니다. (네트워크로 보내거나 바닥 아이템을 만들 때 사용)
    /// </summary>
    public struct MaterialDropResult
    {
        public int MaterialId;
        public int Count;
    }
}
