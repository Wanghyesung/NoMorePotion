using TMPro;
using UnityEngine;

public class EffectTextGroup : MonoBehaviour
{
    public TextMeshProUGUI EffectName;
    public TextMeshProUGUI EffectPower;

    public void Init(string name, string power)
    {
        EffectName.SetText(name);
        EffectPower.SetText(power);
        if(!gameObject.activeSelf) gameObject.SetActive(true);
    }

    public void Release()
    {
        EffectName.SetText(string.Empty);
        EffectPower.SetText(string.Empty);
        gameObject.SetActive(false);
    }
}
