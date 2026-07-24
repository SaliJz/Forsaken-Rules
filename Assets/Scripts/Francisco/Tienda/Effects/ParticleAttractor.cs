using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class ParticleAttractor : MonoBehaviour
{
    private ParticleSystem vfxParticleSystem;
    private ParticleSystem.Particle[] particles;

    private Transform playerTransform;

    private Vector3[] shakeAnchor;
    private bool[] shakeAnchorSet;
    private float[] shakeSeed;
    private float[] shakeSpeedMult;
    private float[] shakeIntensityMult;

    [Header("Atracción hacia el Jugador")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float speedIncreaseRate = 15f;
    [SerializeField] private float delayBeforeAttract = 1.0f;

    [Header("Configuración del Temblor")]
    [SerializeField] private float shakeStartTime = 0.25f;
    [SerializeField] private float shakeIntensity = 0.4f;
    [SerializeField] private float shakeSpeed = 10f;
    [SerializeField] private float shakeVariation = 0.5f;

    [Header("Seguridad de Vida")]
    [SerializeField] private float arrivalDistance = 0.3f;
    [SerializeField] private float lifetimeRefreshBuffer = 0.5f;

    private void Start()
    {
        vfxParticleSystem = GetComponent<ParticleSystem>();

        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }

    private void LateUpdate()
    {
        if (playerTransform == null || vfxParticleSystem == null) return;

        int maxParticles = vfxParticleSystem.main.maxParticles;
        if (particles == null || particles.Length < maxParticles)
        {
            particles = new ParticleSystem.Particle[maxParticles];
            shakeAnchor = new Vector3[maxParticles];
            shakeAnchorSet = new bool[maxParticles];
            shakeSeed = new float[maxParticles];
            shakeSpeedMult = new float[maxParticles];
            shakeIntensityMult = new float[maxParticles];
        }

        int aliveCount = vfxParticleSystem.GetParticles(particles);

        if (aliveCount == 0 && !vfxParticleSystem.IsAlive(true))
        {
            Destroy(gameObject);
            return;
        }

        for (int i = 0; i < aliveCount; i++)
        {
            float particleAge = particles[i].startLifetime - particles[i].remainingLifetime;

            if (particleAge < shakeStartTime)
            {
                shakeAnchorSet[i] = false;
                continue;
            }

            if (particleAge >= shakeStartTime && particleAge < delayBeforeAttract)
            {
                particles[i].velocity = Vector3.zero;

                if (!shakeAnchorSet[i])
                {
                    shakeAnchor[i] = particles[i].position;
                    shakeAnchorSet[i] = true;

                    shakeSeed[i] = Random.Range(0f, 1000f);
                    shakeSpeedMult[i] = 1f + Random.Range(-shakeVariation, shakeVariation);
                    shakeIntensityMult[i] = 1f + Random.Range(-shakeVariation, shakeVariation);
                }

                float t = Time.unscaledTime * shakeSpeed * shakeSpeedMult[i] + shakeSeed[i];

                Vector3 shakeOffset = new Vector3(
                    Mathf.Sin(t),
                    Mathf.Cos(t * 1.3f),
                    Mathf.Sin(t * 1.7f)
                ) * shakeIntensity * shakeIntensityMult[i];

                particles[i].position = shakeAnchor[i] + shakeOffset;
            }
            else if (particleAge >= delayBeforeAttract)
            {
                shakeAnchorSet[i] = false;

                float distToPlayer = Vector3.Distance(particles[i].position, playerTransform.position);

                if (distToPlayer <= arrivalDistance)
                {
                    particles[i].remainingLifetime = 0f;
                    continue;
                }

                if (particles[i].remainingLifetime < lifetimeRefreshBuffer)
                {
                    particles[i].remainingLifetime = lifetimeRefreshBuffer;
                }

                Vector3 directionToPlayer = (playerTransform.position - particles[i].position).normalized;
                float timeAttracting = particleAge - delayBeforeAttract;
                float currentSpeed = (timeAttracting * timeAttracting) * speedIncreaseRate + 12f;

                particles[i].velocity = Vector3.Lerp(particles[i].velocity, directionToPlayer * currentSpeed, Time.deltaTime * 15f);
            }
        }

        vfxParticleSystem.SetParticles(particles, aliveCount);
    }
}