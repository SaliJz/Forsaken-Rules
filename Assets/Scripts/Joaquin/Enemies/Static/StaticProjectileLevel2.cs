using UnityEngine;

public class StaticProjectileLevel2 : StaticProjectileBase
{
    [Header("Mine Settings")]
    [SerializeField] private GameObject minePrefab;
    [SerializeField] private GameObject PlayerImpactVFX;
    private float storedMineDamage;

    public void InitializeLevel2(float pSpeed, float pDamage, string pWord, float pMineDmg)
    {
        base.Initialize(pSpeed, pDamage, pWord);
        this.storedMineDamage = pMineDmg;
    }

    protected override void OnPlayerHit(GameObject player)
    {
        hasImpacted = true;
        player.GetComponent<IDamageable>()?.TakeDamage(damage);

        if (audioSource != null && impactSound != null) audioSource.PlayOneShot(impactSound);
        if (minePrefab != null)
        {
            GameObject mineInstance = Instantiate(minePrefab, transform.position, Quaternion.identity);

            if (mineInstance.TryGetComponent<StaticTrapMine>(out var trapScript))
            {
                //Instancia de explosión distinta al colisionar con el player
                if (PlayerImpactVFX != null)
                {
                    Instantiate(PlayerImpactVFX, transform.position, PlayerImpactVFX.transform.rotation);
                }

                string currentWord = wordTrail != null ? wordTrail.GetWord() : "STATIC";
                trapScript.InitializeTrap(currentWord, storedMineDamage, false);
            }
            else
            {
                Debug.LogWarning($"[{name}] minePrefab no tiene StaticTrapMine. Destruyendo instancia huerfana.", this);
                Destroy(mineInstance);
            }
        }

        Destroy(gameObject);
    }

    protected override void OnEnvironmentHit(GameObject obstacle)
    {
        hasImpacted = true;

        if (audioSource != null && impactSound != null) audioSource.PlayOneShot(impactSound);
        if (minePrefab != null)
        {
            GameObject mineInstance = Instantiate(minePrefab, transform.position, Quaternion.identity);

            if (mineInstance.TryGetComponent<StaticTrapMine>(out var trapScript))
            {
                //Instancia el efecto de impacto al crear minas
                if (proyectileImpactVFX != null)
                {
                    Instantiate(proyectileImpactVFX, transform.position, proyectileImpactVFX.transform.rotation);
                }

                string currentWord = wordTrail != null ? wordTrail.GetWord() : "STATIC";
                trapScript.InitializeTrap(currentWord, storedMineDamage);
            }
            else
            {
                Debug.LogWarning($"[{name}] minePrefab no tiene StaticTrapMine. Destruyendo instancia huerfana.", this);
                Destroy(mineInstance);
            }
        }

        Destroy(gameObject);
    }
}