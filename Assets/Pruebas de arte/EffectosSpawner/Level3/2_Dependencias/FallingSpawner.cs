using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FallingSpawner : MonoBehaviour
{
    [Header("Spawn")]
    public List<GameObject> objectsToSpawn = new List<GameObject>();

    [Header("Movement")]
    public float speed = 5f;

    [Header("2D Animation")]
    public GameObject animacion2D;

    [Range(0.1f, 2f)]
    public float distanciaHaciaCamara = 1f;

    [Header("Destroy Times")]
    [Range(1f, 2f)]
    public float destroySpawnerAfterAnimation = 2f;

    private Transform mainCamera;
    private GameObject spawnedObject;
    private bool stopped = false;
    private bool touchedGround = false;

    private float destroySpawnedObjectTime = 0.5f;

    private void Start()
    {
        GameObject cameraObject = GameObject.Find("Main Camera");

        if (cameraObject != null)
        {
            mainCamera = cameraObject.transform;
        }

        if (animacion2D != null)
        {
            animacion2D.SetActive(false);
        }

        SpawnRandomObject();
    }

    private void Update()
    {
        if (stopped)
        {
            return;
        }

        transform.position += Vector3.down * speed * Time.deltaTime;
    }

    private void SpawnRandomObject()
    {
        if (objectsToSpawn == null || objectsToSpawn.Count == 0)
        {
            Debug.LogWarning("La lista objectsToSpawn está vacía.");
            return;
        }

        int randomIndex = Random.Range(0, objectsToSpawn.Count);
        GameObject prefabToSpawn = objectsToSpawn[randomIndex];

        if (prefabToSpawn == null)
        {
            Debug.LogWarning("El prefab elegido está vacío.");
            return;
        }

        spawnedObject = Instantiate(
            prefabToSpawn,
            transform.position,
            transform.rotation,
            transform
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        if (touchedGround)
        {
            return;
        }

        if (other.CompareTag("Ground"))
        {
            touchedGround = true;
            stopped = true;

            StopSpawnedObjectRotation();
            PlaceAnimationTowardsCamera();
            ActivateAnimation2D();

            StartCoroutine(DestroySpawnedObjectCounter());
            AnimacionFinish();
        }
    }

    private void StopSpawnedObjectRotation()
    {
        if (spawnedObject == null)
        {
            return;
        }

        RotateLoop rotateLoop = spawnedObject.GetComponent<RotateLoop>();

        if (rotateLoop != null)
        {
            rotateLoop.LOOP = false;
        }
    }

    private void PlaceAnimationTowardsCamera()
    {
        if (animacion2D == null)
        {
            return;
        }

        if (mainCamera == null)
        {
            GameObject cameraObject = GameObject.Find("Main Camera");

            if (cameraObject != null)
            {
                mainCamera = cameraObject.transform;
            }
        }

        if (mainCamera == null)
        {
            Debug.LogWarning("No se encontró un objeto llamado Main Camera.");
            return;
        }

        Vector3 directionToCamera = mainCamera.position - transform.position;
        directionToCamera.Normalize();

        animacion2D.transform.position = transform.position + directionToCamera * distanciaHaciaCamara;
    }

    private void ActivateAnimation2D()
    {
        if (animacion2D != null)
        {
            animacion2D.SetActive(true);
        }
    }

    private IEnumerator DestroySpawnedObjectCounter()
    {
        yield return new WaitForSeconds(destroySpawnedObjectTime);

        if (spawnedObject != null)
        {
            Destroy(spawnedObject);
        }
    }

    public void AnimacionFinish()
    {
        StartCoroutine(DestroySpawnerCounter());
    }

    private IEnumerator DestroySpawnerCounter()
    {
        yield return new WaitForSeconds(destroySpawnerAfterAnimation);

        Destroy(gameObject);
    }
}
