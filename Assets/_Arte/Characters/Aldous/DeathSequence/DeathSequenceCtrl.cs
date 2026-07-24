using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class DeathSequenceCtrl : FullScreenEffectsBase
{
    [Header("Director Settings")]
    [SerializeField] public PlayableDirector deathDirector;
    [SerializeField] private GameObject playerGFX;

    [Header("Occlusion Boxcast")]
    [SerializeField] private Camera mainCam;
    [SerializeField] private LayerMask occlusionMask;
    [SerializeField] private Vector3 boxHalfExtents = new Vector3(5f, 3f, 0.5f);
    [SerializeField] private Vector3 nearbyHalfExtents = new Vector3(3f, 2f, 3f);
    [SerializeField] private List<GameObject> hiddenObjects = new();


    private Action onSequenceFinished;

    private static readonly string intensidadProp = "_Intensidad";

    void Awake()
    {
        mainCam = Camera.main;
    }

    public void StartSequence(Action onFinished = null)
    {
        StartCoroutine(DeathSequence(onFinished));
    }

    private void onDeathSequenceFinished(PlayableDirector director)
    {
        deathDirector.stopped -= onDeathSequenceFinished;
        Time.timeScale = 1;
        onSequenceFinished?.Invoke();
        onSequenceFinished = null;
    }

    private IEnumerator DeathSequence(Action onFinished = null)
    {
        yield return StartCoroutine(BlackFade());
        
        CollectHiddenObjects();
        // HideNearbyObjects();

        foreach(GameObject g in hiddenObjects)
        {
            g.SetActive(false);
        }

        onSequenceFinished = onFinished;
        deathDirector.stopped += onDeathSequenceFinished;

        PlayerShaderCtrl shader = playerGFX.GetComponent<PlayerShaderCtrl>();
        shader.ResetAllEffects();

        PlayerAnimCtrl anim = playerGFX.GetComponent<PlayerAnimCtrl>();
        anim.PlayDeath();
        anim.enabled = false;

        deathDirector.Play();
    }

    private IEnumerator BlackFade(float duration = 0.5f)
    {
        float elapsed = 0f;
        Time.timeScale = 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed/duration;
            SetFloat(intensidadProp, Mathf.Lerp(0, 1, t));

            yield return null;
        }

        Time.timeScale = 0f;
        SetFloat(intensidadProp, 1);
    }

    private void CollectHiddenObjects()
    {
        ParticleSystem[] allParticles = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
        DamageNumber[] allDamageNumber = FindObjectsByType<DamageNumber>(FindObjectsSortMode.None);

        Vector3 camPos = mainCam.transform.position;
        Vector3 playerPos = playerGFX.transform.position;

        Vector3 direction = (playerPos - camPos).normalized;
        float distance = Vector3.Distance(camPos, playerPos);
        Vector3 occlusionCenter = camPos + direction * (distance * 0.5f);

        //Raycast Objetos con colision entre el jugador y la camara
        RaycastHit[] occludingHits = Physics.BoxCastAll(
            camPos,
            boxHalfExtents,
            direction,
            mainCam.transform.rotation,
            distance,
            occlusionMask,
            QueryTriggerInteraction.Collide
        );
        //Overlap box Objetos con colision cerca al jugador
        Collider[] nearbyHits = Physics.OverlapBox(
            playerGFX.transform.position,
            nearbyHalfExtents,
            playerGFX.transform.rotation,
            occlusionMask,
            QueryTriggerInteraction.Collide
        );

        //Agregar objetos entre la camara y el jugador a la lista
        foreach(var hit in occludingHits)
        {
            GameObject obj = hit.collider.gameObject;
            hiddenObjects.Add(obj);
        }
        //Agregar objetos cerca al jugador a la lista
        foreach(Collider hit in nearbyHits)
        {
            GameObject obj = hit.gameObject;
            if (hiddenObjects.Contains(obj)) continue;
            hiddenObjects.Add(obj);
        }
        //Añadir particulas entre el jugador y la camara y cercanas al jugador a la lista
        foreach(var ps in allParticles)
        {
            GameObject obj = ps.gameObject;

            if (hiddenObjects.Contains(obj)) continue;

            bool insideOcclusionBox = IsPointInsideOrientedBox(
                ps.transform.position,
                occlusionCenter,
                mainCam.transform.rotation,
                boxHalfExtents
            );

            bool insideNearbyBox = IsPointInsideOrientedBox(
                ps.transform.position,
                playerGFX.transform.position,
                playerGFX.transform.rotation,
                nearbyHalfExtents
            );

            if (insideNearbyBox || insideOcclusionBox) hiddenObjects.Add(obj);
        }
        ///Desactivar numeros flotantes de daño
        foreach(var dn in allDamageNumber)
        {
            GameObject obj = dn.gameObject;
            if (hiddenObjects.Contains(obj)) continue;
            hiddenObjects.Add(obj);
        }
    }

    // private void HideNearbyObjects()
    // {
    //     Collider[] hits = Physics.OverlapBox(
    //         playerGFX.transform.position,
    //         nearbyHalfExtents,
    //         playerGFX.transform.rotation,
    //         occlusionMask
    //     );

    //     foreach(Collider hit in hits)
    //     {
    //         GameObject obj = hit.gameObject;
    //         // if (obj == playerGFX) continue;
    //         if (hiddenObjects.Contains(obj)) continue;

    //         // obj.SetActive(false);
    //         hiddenObjects.Add(obj);
    //     }
    // }

    private bool IsPointInsideOrientedBox(Vector3 point, Vector3 boxCenter, Quaternion boxOrientation, Vector3 halfExtents)
    {
        Vector3 localPoint = Quaternion.Inverse(boxOrientation) * (point - boxCenter);

        return Mathf.Abs(localPoint.x) <= halfExtents.x
            && Mathf.Abs(localPoint.y) <= halfExtents.y
            && Mathf.Abs(localPoint.z) <= halfExtents.z;
    }

    #if UNITY_EDITOR
    void Update()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "AndreiNew")
        {
            testInputs();
        }
    }

    private void testInputs()
    {
        if (Input.GetKeyDown(KeyCode.F2)) 
        {
            playerGFX.SetActive(true);
            StartSequence();
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(playerGFX.transform.position, nearbyHalfExtents);
    }

    #endif
}
