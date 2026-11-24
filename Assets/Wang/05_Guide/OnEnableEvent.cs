using UnityEngine;
using UnityEngine.Events;

// 오브젝트가 켜질 때 Inspector에 연결한 이벤트를 실행한다.
public sealed class OnEnableEvent : MonoBehaviour
{
    [SerializeField] private UnityEvent onEnable = new();

    private void OnEnable() => onEnable.Invoke();
}
