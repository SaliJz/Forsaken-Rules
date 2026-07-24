using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.AI;

public class Sala5SequenceManager : MonoBehaviour
{
    #region Unity Events & Components

    [Header("Eventos de Flujo")]
    public UnityEvent OnSequenceStart = new UnityEvent();
    public UnityEvent OnDoorUnlocked = new UnityEvent();

    [Header("Componentes de Juego")]
    [SerializeField] private ShieldSkill shieldSkill;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private DialogManager dialogManager;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip infernalSound;

    [Header("CONFIGURACIÓN DE COMBATE")]
    [SerializeField] private GameObject enemySlimePrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private int maxActiveSlimes = 3;
    [SerializeField] private float initialCombatDuration = 5f;
    [SerializeField] private bool slimesGrantLifesteal = false;

    private List<GameObject> activeSlimes = new List<GameObject>();
    private bool sequenceStarted = false;
    private bool combatActive = false;
    private bool enemiesPaused = false; 

    [Header("Umbrales de Vida")]
    [SerializeField][Range(0.1f, 1f)] private float targetHealthPercentAdult = 0.666f;
    [SerializeField][Range(0.1f, 1f)] private float targetHealthPercentElder = 0.333f;

    [Header("Dialogos")]
    [SerializeField] private DialogLine[] dialogoInicioAlTocarTrigger;
    [SerializeField] private DialogLine[] dialogoFuerzaHabilidad;
    [SerializeField] private DialogLine[] dialogoInterrupcionCombate;
    [SerializeField] private DialogLine[] dialogoReaccionCambioAdulto;
    [SerializeField] private DialogLine[] dialogoFinAbrePuerta;

    #endregion

    #region Lifecycle & Entry Point
     
    private void Awake()
    {
        if (dialogManager == null)
        {
            Debug.LogError("DialogManager no está asignado.");
        }
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!sequenceStarted && other.CompareTag("Player"))
        {
            sequenceStarted = true;
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            StartSala5Sequence();
        }
    }

    public void StartSala5Sequence()
    {
        StartCoroutine(SequenceFlow());
    }

    #endregion

    #region Sequence Flow

    private IEnumerator SequenceFlow()
    {
        OnSequenceStart?.Invoke();

        if (shieldSkill != null)
        {
            shieldSkill.SetInputBlocked(true);
            Debug.Log("[Sala5] Input de habilidad BLOQUEADO para tutorial");
        }

        yield return new WaitForSeconds(1f);

        Debug.Log("FASE 1: Diálogo inicial");
        yield return StartCoroutine(PlayDialog(dialogoInicioAlTocarTrigger));

        yield return new WaitForSeconds(0.5f);

        Debug.Log("FASE 3: Diálogo fuerza habilidad");
        yield return StartCoroutine(PlayDialog(dialogoFuerzaHabilidad));

        Debug.Log("FASE 4: Iniciando combate");
        ActivateCombatAndSpawn();
        combatActive = true;

        yield return new WaitForSeconds(initialCombatDuration);

        Debug.Log("FASE 5: Pausando combate - Diálogo interrupción");
        PauseEnemies(true);

        yield return StartCoroutine(PlayDialog(dialogoInterrupcionCombate));
        PauseEnemies(false);

        Debug.Log("FASE 6: Drenando vida a adulto");
        if (shieldSkill != null && playerHealth != null)
        {
            float targetHealth = playerHealth.MaxHealth * targetHealthPercentAdult;
            yield return StartCoroutine(DrainHealthGradually(targetHealth, 2f));
        }

        Debug.Log("FASE 7: Pausando combate - Diálogo reacción adulto");
        PauseEnemies(true);
        yield return StartCoroutine(PlayDialog(dialogoReaccionCambioAdulto));
        PauseEnemies(false);

        Debug.Log("FASE 8: Drenando vida a anciano");
        if (shieldSkill != null && playerHealth != null)
        {
            float targetHealth = playerHealth.MaxHealth * targetHealthPercentElder;
            yield return StartCoroutine(DrainHealthGradually(targetHealth, 3f));
        }

        Debug.Log("FASE 9: Liberando forzado y desactivando escudo");
        if (shieldSkill != null)
        {
            shieldSkill.SetForcedActive(false);
            yield return new WaitForSeconds(0.2f);
            shieldSkill.DeactivateSkillPublic();
        }

        StopSlimeSpawnAndCombat();
        combatActive = false;

        Debug.Log("FASE 10: Diálogo final");
        if (shieldSkill != null)
        {
            shieldSkill.SetInputBlocked(false);
            Debug.Log("[Sala5] Input de habilidad DESBLOQUEADO");
        }

        UnlockDoorAndTransition();

        yield return StartCoroutine(PlayDialog(dialogoFinAbrePuerta));
    }

    private IEnumerator PlayDialog(DialogLine[] lines)
    {
        if (dialogManager == null || lines == null || lines.Length == 0) yield break;

        dialogManager.StartDialog(lines);
        while (dialogManager.IsActive)
        {
            yield return null;
        }

        yield return new WaitForSeconds(0.3f);
    }

    #endregion

    #region Combat Logic

    private void ActivateCombatAndSpawn()
    {
        SpawnInitialSlimes();
    }

    private void StopSlimeSpawnAndCombat()
    {
        StopAllCoroutines();

        foreach (var slime in activeSlimes)
        {
            if (slime != null) Destroy(slime);
        }
        activeSlimes.Clear();
    }

    private void PauseEnemies(bool pause)
    {
        enemiesPaused = pause; 

        Debug.Log($"[Sala5] Pausando enemigos: {pause} - Enemigos activos: {activeSlimes.Count}");

        foreach (GameObject slime in activeSlimes)
        {
            if (slime != null)
            {
                ApplyPauseToEnemy(slime, pause);
            }
        }
    }

    private void ApplyPauseToEnemy(GameObject enemy, bool pause)
    {
        if (enemy == null) return;

        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.isStopped = pause;
            if (pause)
            {
                agent.velocity = Vector3.zero; 
            }
        }

        Animator animator = enemy.GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = !pause;
        }

        MonoBehaviour[] behaviors = enemy.GetComponents<MonoBehaviour>();
        foreach (var behavior in behaviors)
        {
            if (behavior == null) continue;

            System.Type behaviorType = behavior.GetType();
            if (behaviorType == typeof(NavMeshAgent) ||
                behaviorType == typeof(Animator) ||
                behaviorType.Name == "EnemyHealth") 
            {
                continue;
            }

            behavior.enabled = !pause;
        }

        Debug.Log($"[Sala5] Enemigo {enemy.name} pausado: {pause}");
    }

    private void SpawnInitialSlimes()
    {
        for (int i = 0; i < maxActiveSlimes && i < spawnPoints.Length; i++)
        {
            SpawnNewSlime(spawnPoints[i].position);
        }
    }

    private void SpawnNewSlime(Vector3 position)
    {
        if (enemySlimePrefab == null)
        {
            Debug.LogError("No hay prefab de Slime asignado.");
            return;
        }

        GameObject newSlime = Instantiate(enemySlimePrefab, position, Quaternion.identity);
        activeSlimes.Add(newSlime);

        if (enemiesPaused)
        {
            ApplyPauseToEnemy(newSlime, true);
            Debug.Log($"[Sala5] Nuevo slime spawneado y PAUSADO inmediatamente");
        }

        EnemyHealth slimeHealth = newSlime.GetComponent<EnemyHealth>();
        if (slimeHealth != null)
        {
            slimeHealth.OnDeath += OnSlimeDeath;

            var field = typeof(EnemyHealth).GetField("canGrantLifestealOnDeath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(slimeHealth, slimesGrantLifesteal);
                Debug.Log($"[Sala5] Slime spawneado - Lifesteal: {slimesGrantLifesteal}");
            }
        }
        else
        {
            Debug.LogWarning("Slime no tiene componente EnemyHealth con delegate OnDeath.");
        }
    }

    private void OnSlimeDeath(GameObject deadSlime)
    {
        EnemyHealth slimeHealth = deadSlime.GetComponent<EnemyHealth>();
        if (slimeHealth != null)
        {
            slimeHealth.OnDeath -= OnSlimeDeath;
        }

        activeSlimes.Remove(deadSlime);

        if (combatActive && activeSlimes.Count < maxActiveSlimes && spawnPoints.Length > 0)
        {
            int spawnIndex = Random.Range(0, spawnPoints.Length);
            StartCoroutine(SpawnSlimeAfterDelay(spawnPoints[spawnIndex].position, 1f));
        }
    }

    private IEnumerator SpawnSlimeAfterDelay(Vector3 position, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (combatActive)
        {
            SpawnNewSlime(position);
        }
    }

    #endregion

    #region Health Drain

    private IEnumerator DrainHealthGradually(float targetHealth, float drainRate)
    {
        if (playerHealth == null) yield break;

        Debug.Log($"[Sala5] Iniciando drenaje. Vida actual: {playerHealth.CurrentHealth}, Objetivo: {targetHealth}");

        if (shieldSkill != null)
        {
            shieldSkill.ToggleSkillDirectly();
            shieldSkill.SetForcedActive(true);
        }

        while (playerHealth.CurrentHealth > targetHealth)
        {
            float damageThisTick = Mathf.Min(drainRate, playerHealth.CurrentHealth - targetHealth);
            playerHealth.TakeDamage(damageThisTick, true);

            Debug.Log($"[Sala5] Drenando {damageThisTick} de vida. Vida restante: {playerHealth.CurrentHealth}/{targetHealth}");

            yield return new WaitForSeconds(0.5f);

            if (playerHealth.CurrentHealth <= targetHealth)
            {
                break;
            }
        }

        Debug.Log($"[Sala5] Drenaje completado. Vida actual: {playerHealth.CurrentHealth}");
    }

    #endregion

    #region Door and Transition

    private void UnlockDoorAndTransition()
    {
        Debug.Log("[Sala5] UnlockDoorAndTransition ejecutándose");

        if (audioSource != null && infernalSound != null)
        {
            audioSource.PlayOneShot(infernalSound);
            Debug.Log("[Sala5] Sonido infernal reproducido");
        }
        else
        {
            Debug.LogWarning("[Sala5] AudioSource o InfernalSound no asignado");
        }

        if (OnDoorUnlocked != null)
        {
            Debug.Log($"[Sala5] Invocando OnDoorUnlocked - Listeners: {OnDoorUnlocked.GetPersistentEventCount()}");
            OnDoorUnlocked.Invoke();
        }
        else
        {
            Debug.LogWarning("[Sala5] OnDoorUnlocked es null");
        }
    }

    #endregion
}