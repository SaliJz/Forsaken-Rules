using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RompiblePorEscombros : MonoBehaviour
{
    [Header("Activador")]
    [Tooltip("Déjalo apagado. Al activarlo en Play Mode, el objeto se rompe una sola vez.")]
    public bool romper = false;

    [Header("Prefabs de escombros")]
    [Tooltip("Aquí pones tus prefabs de piedras, fragmentos, pedazos, etc.")]
    public List<GameObject> prefabsEscombros = new List<GameObject>();

    [Min(1)]
    [Tooltip("Cantidad de escombros que aparecerán. También reduce el tamaño de cada escombro.")]
    public int cantidadEscombros = 10;

    [Header("Escala de escombros")]
    [Tooltip("Si el objeto tiene escala 1 y cantidad 10, el escombro será aprox 0.1.")]
    public float multiplicadorEscala = 1f;

    [Range(0f, 0.8f)]
    [Tooltip("Variación leve para que no todos los escombros sean idénticos.")]
    public float variacionEscala = 0.15f;

    [Tooltip("Evita que los escombros salgan demasiado pequeños.")]
    public float escalaMinima = 0.02f;

    [Header("Distribución")]
    [Range(0.05f, 1f)]
    [Tooltip("Qué tanto se dispersan inicialmente dentro del volumen del objeto.")]
    public float rangoSpawnDentroDelObjeto = 0.65f;

    [Header("Física")]
    [Tooltip("Fuerza principal hacia afuera.")]
    public float fuerzaHaciaAfuera = 2f;

    [Tooltip("Fuerza principal hacia arriba.")]
    public float fuerzaHaciaArriba = 5f;

    [Tooltip("Fuerza de rotación de cada escombro.")]
    public float fuerzaRotacion = 4f;

    [Tooltip("Masa asignada a cada escombro.")]
    public float masaEscombro = 1f;

    [Tooltip("Impulse suele quedar mejor para una ruptura instantánea.")]
    public ForceMode modoFuerza = ForceMode.Impulse;

    [Header("Material / Textura")]
    [Tooltip("Si está activo, intenta copiar el material del objeto roto.")]
    public bool usarMaterialDelObjetoOriginal = true;

    [Tooltip("Si asignas un material aquí, este tendrá prioridad sobre el material original.")]
    public Material materialManualEscombros;

    [Tooltip("Mientras más pequeño sea el escombro, más se tilea la textura.")]
    public float multiplicadorTiling = 1f;

    [Header("Limpieza")]
    [Tooltip("Si es mayor a 0, destruye todo el grupo de escombros después de este tiempo.")]
    public float destruirEscombrosDespuesDe = 0f;

    private bool yaSeRompio = false;

    private void Update()
    {
        if (romper && !yaSeRompio)
        {
            Romper();
        }
    }

    [ContextMenu("Romper ahora")]
    public void Romper()
    {
        if (yaSeRompio) return;

        if (prefabsEscombros == null || prefabsEscombros.Count == 0)
        {
            Debug.LogWarning("No hay prefabs de escombros asignados.", this);
            romper = false;
            return;
        }

        yaSeRompio = true;

        Bounds boundsObjeto = ObtenerBoundsObjeto();
        Vector3 centro = boundsObjeto.center;

        float escalaBase = CalcularEscalaBase();
        Material materialParaEscombros = CrearMaterialParaEscombros(escalaBase);

        GameObject grupo = new GameObject("Escombros_" + gameObject.name);
        grupo.transform.position = transform.position;
        grupo.transform.rotation = transform.rotation;
        grupo.transform.localScale = Vector3.one;

        for (int i = 0; i < cantidadEscombros; i++)
        {
            GameObject prefab = ObtenerPrefabAleatorio();

            if (prefab == null)
                continue;

            Vector3 posicionSpawn = ObtenerPosicionSpawn(boundsObjeto);
            Quaternion rotacionSpawn = Random.rotation;

            GameObject escombro = Instantiate(prefab, posicionSpawn, rotacionSpawn, grupo.transform);

            float variacion = Random.Range(1f - variacionEscala, 1f + variacionEscala);
            float escalaFinal = escalaBase * variacion;

            escombro.transform.localScale = prefab.transform.localScale * escalaFinal;

            AplicarMaterial(escombro, materialParaEscombros);
            PrepararFisica(escombro);

            Rigidbody rb = escombro.GetComponent<Rigidbody>();

            Vector3 direccionAfuera = posicionSpawn - centro;

            if (direccionAfuera.sqrMagnitude < 0.001f)
                direccionAfuera = Random.onUnitSphere;

            direccionAfuera.Normalize();

            Vector3 fuerzaFinal =
                direccionAfuera * fuerzaHaciaAfuera +
                Vector3.up * (fuerzaHaciaArriba);

            rb.AddForce(fuerzaFinal, modoFuerza);
            rb.AddTorque(Random.insideUnitSphere * fuerzaRotacion, modoFuerza);
        }

        if (destruirEscombrosDespuesDe > 0f)
            Destroy(grupo, destruirEscombrosDespuesDe);

        Destroy(gameObject);
    }

    private GameObject ObtenerPrefabAleatorio()
    {
        if (prefabsEscombros.Count == 0)
            return null;

        for (int i = 0; i < 10; i++)
        {
            GameObject prefab = prefabsEscombros[Random.Range(0, prefabsEscombros.Count)];

            if (prefab != null)
                return prefab;
        }

        return null;
    }

    private float CalcularEscalaBase()
    {
        Vector3 escalaObjeto = transform.lossyScale;

        float escalaPromedio =
            (Mathf.Abs(escalaObjeto.x) +
             Mathf.Abs(escalaObjeto.y) +
             Mathf.Abs(escalaObjeto.z)) / 3f;

        float escala = (escalaPromedio / cantidadEscombros) * multiplicadorEscala;

        return Mathf.Max(escala, escalaMinima);
    }

    private Bounds ObtenerBoundsObjeto()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        if (renderers == null || renderers.Length == 0)
        {
            Vector3 escala = transform.lossyScale;
            Vector3 tamanoFallback = new Vector3(
                Mathf.Abs(escala.x),
                Mathf.Abs(escala.y),
                Mathf.Abs(escala.z)
            );

            return new Bounds(transform.position, tamanoFallback);
        }

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private Vector3 ObtenerPosicionSpawn(Bounds bounds)
    {
        Vector3 extents = bounds.extents * rangoSpawnDentroDelObjeto;

        Vector3 offset = new Vector3(
            Random.Range(-extents.x, extents.x),
            Random.Range(-extents.y, extents.y),
            Random.Range(-extents.z, extents.z)
        );

        return bounds.center + offset;
    }

    private Material CrearMaterialParaEscombros(float escalaBase)
    {
        Material materialFuente = null;

        if (materialManualEscombros != null)
        {
            materialFuente = materialManualEscombros;
        }
        else if (usarMaterialDelObjetoOriginal)
        {
            Renderer rendererOriginal = GetComponentInChildren<Renderer>();

            if (rendererOriginal != null)
                materialFuente = rendererOriginal.sharedMaterial;
        }

        if (materialFuente == null)
            return null;

        Material materialInstanciado = new Material(materialFuente);

        float tiling = (1f / Mathf.Max(escalaBase, 0.001f)) * multiplicadorTiling;
        tiling = Mathf.Max(tiling, 0.01f);

        Vector2 escalaTextura = new Vector2(tiling, tiling);

        AplicarTilingSeguro(materialInstanciado, escalaTextura);

        return materialInstanciado;
    }

    private void AplicarTilingSeguro(Material material, Vector2 tiling)
    {
        if (material == null) return;

        if (material.HasProperty("_BaseMap"))
            material.SetTextureScale("_BaseMap", tiling);

        if (material.HasProperty("_MainTex"))
            material.SetTextureScale("_MainTex", tiling);

        material.mainTextureScale = tiling;
    }

    private void AplicarMaterial(GameObject escombro, Material material)
    {
        if (escombro == null || material == null)
            return;

        Renderer[] renderers = escombro.GetComponentsInChildren<Renderer>();

        foreach (Renderer r in renderers)
        {
            r.material = material;
        }
    }

    private void PrepararFisica(GameObject escombro)
    {
        Rigidbody rb = escombro.GetComponent<Rigidbody>();

        if (rb == null)
            rb = escombro.AddComponent<Rigidbody>();

        rb.mass = masaEscombro;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Collider collider = escombro.GetComponentInChildren<Collider>();

        if (collider == null)
        {
            escombro.AddComponent<BoxCollider>();
        }

        MeshCollider[] meshColliders = escombro.GetComponentsInChildren<MeshCollider>();

        foreach (MeshCollider meshCollider in meshColliders)
        {
            meshCollider.convex = true;
        }
    }

    private void OnValidate()
    {
        cantidadEscombros = Mathf.Max(1, cantidadEscombros);
        multiplicadorEscala = Mathf.Max(0.001f, multiplicadorEscala);
        escalaMinima = Mathf.Max(0.001f, escalaMinima);
        masaEscombro = Mathf.Max(0.001f, masaEscombro);
        multiplicadorTiling = Mathf.Max(0.001f, multiplicadorTiling);
    }
}
