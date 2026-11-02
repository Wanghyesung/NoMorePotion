using UnityEngine;
public enum DamageType
{
    None,
    Normal,
    Fire,
    Ice,
    Electric
}

public interface IAttacker
{
    public DamageType DamageType { get; set; }
}
