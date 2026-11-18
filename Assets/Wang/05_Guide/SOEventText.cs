using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EventText", menuName = "Guide/Event Text")]
public sealed class SOEventText : ScriptableObject
{
    [Serializable]
    public sealed class EventText
    {
        [TextArea(2, 5)] public string text;
        [Tooltip("글자 하나를 표시하는 간격(초). 0이면 문장을 즉시 표시합니다.")]
        [Min(0f)] public float speed = 0.03f;
    }

    [SerializeField] private List<EventText> texts = new();

    public IReadOnlyList<EventText> Texts => texts;
}
