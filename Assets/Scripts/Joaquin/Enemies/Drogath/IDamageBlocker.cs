using UnityEngine;

public interface IDamageBlocker
{
    bool ShouldBlockDamage(Vector3 damageSourcePosition);
}
