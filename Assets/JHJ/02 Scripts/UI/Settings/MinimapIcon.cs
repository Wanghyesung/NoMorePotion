using System.Collections.Generic;
using UnityEngine;
using SF = UnityEngine.SerializeField;

public class MinimapIcon : MonoBehaviour
{
    private static readonly List<MinimapIcon> all = new List<MinimapIcon>();
    public static IReadOnlyList<MinimapIcon> All => all;

    [SF] private Sprite sprite;
    [SF] private Color color = Color.white;
    [SF] private float size = 16f;
    [Tooltip("원 밖에 있으면 테두리에 붙여 표시(끄면 숨김)")]
    [SF] private bool clampToEdge;


    public Sprite Sprite => sprite;
    public Color Color => color;
    public float Size => size;
    public bool ClampToEdge => clampToEdge;

    private void OnEnable()
    {
        all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
    }
}
