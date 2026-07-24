// HitboxSpawner.cs
using UnityEngine;

/// <summary>
/// Crea y gestiona el hitbox delante del enemigo.
/// Se puede asignar un prefab (con AttackHitbox) o se crea un cube primitivo.
/// </summary>
public class HitboxSpawner : MonoBehaviour
{
    public GameObject hitboxPrefab;       // opcional
    public Vector3 hitboxSize = new Vector3(1.2f, 1.2f, 1.2f);
    public float offset = 1.0f;
    public Color previewColor = new Color(1f, 0.2f, 0.2f, 0.8f);

    GameObject current;

    /// <summary>Crear hitbox (inactiva en daño) delante del transform.</summary>
    public GameObject Create(bool activeDamage, int damage, float life)
    {
        Cleanup();

        Vector3 pos = transform.position + transform.forward * offset;
        Quaternion rot = Quaternion.LookRotation(transform.forward);

        if (hitboxPrefab != null)
        {
            current = Instantiate(hitboxPrefab, pos, rot);
            current.transform.localScale = hitboxSize;
        }
        else
        {
            current = GameObject.CreatePrimitive(PrimitiveType.Cube);
            current.transform.position = pos;
            current.transform.rotation = rot;
            current.transform.localScale = hitboxSize;
            var rb = current.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);
        }

        current.transform.SetParent(transform, true);

        var hit = current.GetComponent<AttackHitbox>();
        if (hit == null) hit = current.AddComponent<AttackHitbox>();
        hit.damage = damage;
        hit.life = life;
        hit.SetActiveDamage(activeDamage);

        var rend = current.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(Shader.Find("Standard"));
            try { rend.material.SetFloat("_Mode", 3); } catch { }
            rend.material.color = previewColor;
        }

        var col = current.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        return current;
    }

    /// <summary>Activar o desactivar que haga daño.</summary>
    public void ActivateDamage(bool on)
    {
        if (current == null) return;
        var hit = current.GetComponent<AttackHitbox>();
        if (hit != null) hit.SetActiveDamage(on);
    }

    public void Cleanup()
    {
        if (current != null) Destroy(current);
        current = null;
    }
}
