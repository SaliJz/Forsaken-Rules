using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BoardGenerator : MonoBehaviour
{
    [Header("Opciones")]
    [Tooltip("Si true genera automaticamente una cuadricula. Si false, recolecta los Transform existentes.")]
    public bool generarAutomatico = false;

    [Header("Prefabs casillas (solo si generarAutomatico true)")]
    public GameObject prefabCasillaBlanca;
    public GameObject prefabCasillaNegra;

    [Header("Parametros tablero (solo si generarAutomatico true)")]
    public int ancho = 8;
    public int alto = 8;
    public float tamCasilla = 1f;
    public Transform padreCasillas;

    [Tooltip("Lista de Transform de las casillas/plano.")]
    public List<Transform> casillasTransform = new List<Transform>();

    private readonly Dictionary<Vector2Int, Transform> casillas = new Dictionary<Vector2Int, Transform>();
    private readonly Dictionary<Transform, Vector2Int> coordPorTransform = new Dictionary<Transform, Vector2Int>();
    private readonly Dictionary<Vector2Int, GameObject> ocupantes = new Dictionary<Vector2Int, GameObject>();

    private class ReservationInfo
    {
        public GameObject owner;
        public float eta;
    }

    private readonly Dictionary<Vector2Int, ReservationInfo> routeReservations = new Dictionary<Vector2Int, ReservationInfo>();
    private readonly HashSet<GameObject> startedOwners = new HashSet<GameObject>();

    public int reintentosRecolectar = 30;
    public float delayEntreReintentos = 0.05f;

    void Start()
    {
        EnsureReady();
        if (casillas.Count == 0)
            StartCoroutine(RetryCollectRoutine());
    }

    public int CasillasCount => casillas != null ? casillas.Count : 0;

    public void EnsureReady()
    {
        if (CasillasCount > 0) return;

        if (generarAutomatico)
        {
            if (prefabCasillaBlanca == null || prefabCasillaNegra == null)
            {
                Debug.LogWarning("BoardGenerator: generarAutomatico=true pero faltan prefabs. Intentando recolectar casillas existentes.");
                RecolectarCasillasExistentes();
                return;
            }

            GenerarTableroAutomatico();
            SyncListFromDict();
        }
        else
        {
            if (casillasTransform != null && casillasTransform.Count > 0)
                SyncDictFromList();
            else
                RecolectarCasillasExistentes();
        }
    }

    #region generación / recolección
    public void GenerarTableroAutomatico()
    {
        if (padreCasillas != null)
        {
            for (int i = padreCasillas.childCount - 1; i >= 0; i--)
            {
                GameObject child = padreCasillas.GetChild(i).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(child);
                else Destroy(child);
#else
                Destroy(child);
#endif
            }
        }

        casillas.Clear();
        coordPorTransform.Clear();
        casillasTransform.Clear();
        ocupantes.Clear();
        routeReservations.Clear();
        startedOwners.Clear();

        for (int y = 0; y < alto; y++)
        {
            for (int x = 0; x < ancho; x++)
            {
                bool esBlanca = (x + y) % 2 == 0;
                GameObject prefab = esBlanca ? prefabCasillaBlanca : prefabCasillaNegra;
                Vector3 pos = transform.position + new Vector3(x * tamCasilla, 0f, y * tamCasilla);

                GameObject go = Instantiate(prefab, pos, Quaternion.identity, padreCasillas);
                go.name = $"Casilla_{x}_{y}";

                RegistrarCasilla(go.transform, new Vector2Int(x, y));
            }
        }
    }

    public void RecolectarCasillasExistentes()
    {
        if (casillasTransform != null && casillasTransform.Count > 0)
        {
            SyncDictFromList();
            return;
        }

        casillas.Clear();
        coordPorTransform.Clear();
        casillasTransform.Clear();
        ocupantes.Clear();

        if (padreCasillas != null)
        {
            for (int i = 0; i < padreCasillas.childCount; i++)
            {
                Transform tr = padreCasillas.GetChild(i);
                if (tr == null) continue;

                Vector2Int coord = AutoAsignarCoordPorPosicion(tr.position);
                while (casillas.ContainsKey(coord)) coord.x += 1;

                RegistrarCasilla(tr, coord);
            }
        }
        else
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform tr = transform.GetChild(i);
                if (tr == null) continue;

                Vector2Int coord = AutoAsignarCoordPorPosicion(tr.position);
                while (casillas.ContainsKey(coord)) coord.x += 1;

                RegistrarCasilla(tr, coord);
            }
        }

        if (casillas.Count == 0)
            Debug.LogWarning("BoardGenerator: no se encontraron casillas en la escena.");
    }

    private void RegistrarCasilla(Transform tr, Vector2Int coord)
    {
        if (tr == null) return;

        casillas[coord] = tr;
        coordPorTransform[tr] = coord;

        if (!casillasTransform.Contains(tr))
            casillasTransform.Add(tr);
    }

    private IEnumerator RetryCollectRoutine()
    {
        int tries = 0;
        while (tries < reintentosRecolectar && casillas.Count == 0)
        {
            RecolectarCasillasExistentes();
            if (casillas.Count > 0) break;

            tries++;
            yield return new WaitForSeconds(delayEntreReintentos);
        }

        if (casillas.Count == 0)
            Debug.LogWarning("BoardGenerator: RetryCollectRoutine terminó sin encontrar casillas.");
    }

    private void SyncDictFromList()
    {
        casillas.Clear();
        coordPorTransform.Clear();

        if (casillasTransform == null) casillasTransform = new List<Transform>();

        foreach (var tr in casillasTransform)
        {
            if (tr == null) continue;
            if (!tr.gameObject.scene.IsValid()) continue;

            Vector2Int coord = AutoAsignarCoordPorPosicion(tr.position);
            while (casillas.ContainsKey(coord)) coord.x += 1;

            RegistrarCasilla(tr, coord);
        }
    }

    private void SyncListFromDict()
    {
        if (casillasTransform == null) casillasTransform = new List<Transform>();
        casillasTransform.Clear();

        foreach (var kv in casillas)
        {
            if (kv.Value != null)
                casillasTransform.Add(kv.Value);
        }
    }

    private Vector2Int AutoAsignarCoordPorPosicion(Vector3 worldPos)
    {
        Vector3 local = worldPos - transform.position;
        int x = Mathf.RoundToInt(local.x / tamCasilla);
        int y = Mathf.RoundToInt(local.z / tamCasilla);
        return new Vector2Int(x, y);
    }
    #endregion

    #region API de casillas
    public bool ExisteCasilla(Vector2Int coord)
    {
        if (casillas.ContainsKey(coord)) return true;

        if (casillasTransform != null)
        {
            foreach (var tr in casillasTransform)
            {
                if (tr == null) continue;
                if (GetCoordFromTransform(tr, out Vector2Int c) && c == coord)
                {
                    casillas[coord] = tr;
                    return true;
                }
            }
        }

        return false;
    }

    public Transform GetCasillaTransform(Vector2Int coord)
    {
        if (casillas.TryGetValue(coord, out Transform tr)) return tr;

        if (casillasTransform != null)
        {
            foreach (var item in casillasTransform)
            {
                if (item == null) continue;
                if (GetCoordFromTransform(item, out Vector2Int c) && c == coord)
                {
                    casillas[coord] = item;
                    return item;
                }
            }
        }

        RecolectarCasillasExistentes();
        casillas.TryGetValue(coord, out tr);
        return tr;
    }

    public bool GetCoordFromTransform(Transform tr, out Vector2Int coord)
    {
        if (tr == null)
        {
            coord = default;
            return false;
        }

        if (coordPorTransform.TryGetValue(tr, out coord))
            return true;

        foreach (var kv in casillas)
        {
            if (kv.Value == tr)
            {
                coord = kv.Key;
                coordPorTransform[tr] = coord;
                return true;
            }
        }

        coord = default;
        return false;
    }

    public Vector2Int GetNearestCoord(Vector3 worldPos)
    {
        float minDist = float.MaxValue;
        Vector2Int best = Vector2Int.zero;

        foreach (var kv in casillas)
        {
            if (kv.Value == null) continue;
            Vector3 p = kv.Value.position;
            float d = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(worldPos.x, 0f, worldPos.z));
            if (d < minDist)
            {
                minDist = d;
                best = kv.Key;
            }
        }

        return best;
    }

    public Vector3 GetWorldPos(Vector2Int coord)
    {
        Transform tr = GetCasillaTransform(coord);
        return tr != null ? tr.position : Vector3.zero;
    }

    public bool EstaOcupada(Vector2Int coord)
    {
        return ocupantes.TryGetValue(coord, out GameObject go) && go != null;
    }

    public GameObject GetOcupante(Vector2Int coord)
    {
        ocupantes.TryGetValue(coord, out GameObject go);
        return go;
    }

    public void SetOcupante(Vector2Int coord, GameObject go)
    {
        if (go == null)
        {
            if (ocupantes.ContainsKey(coord))
                ocupantes.Remove(coord);
            return;
        }

        ocupantes[coord] = go;
    }

    public IEnumerable<Transform> GetAllCasillas() => casillasTransform;
    #endregion

    #region Reserva / coordinación
    public bool RequestRouteReservation(List<Vector2Int> ruta, GameObject requester, float eta, bool permitirUltimaOcupadaPorOtro = false)
    {
        if (ruta == null || ruta.Count == 0) return false;

        for (int i = 0; i < ruta.Count; i++)
        {
            Vector2Int coord = ruta[i];
            bool esUltima = (i == ruta.Count - 1);

            Transform tr = GetCasillaTransform(coord);
            if (tr == null) return false;

            if (EstaOcupada(coord))
            {
                GameObject ocupante = GetOcupante(coord);
                if (ocupante != requester && !(esUltima && permitirUltimaOcupadaPorOtro))
                    return false;
            }

            if (routeReservations.TryGetValue(coord, out ReservationInfo existing) && existing.owner != requester)
            {
                if (startedOwners.Contains(existing.owner))
                    return false;

                if (eta + 0.001f < existing.eta)
                {
                    ReleaseAllReservationsOfOwner(existing.owner);
                }
                else
                {
                    return false;
                }
            }
        }

        foreach (var coord in ruta)
        {
            if (GetCasillaTransform(coord) == null) continue;
            routeReservations[coord] = new ReservationInfo() { owner = requester, eta = eta };
        }

        return true;
    }

    public void ReleaseRutaReservation(List<Vector2Int> ruta, GameObject requester)
    {
        if (ruta == null) return;

        foreach (var coord in ruta)
            ReleaseRutaReservationForCoord(coord, requester);
    }

    public void ReleaseRutaReservationForCoord(Vector2Int coord, GameObject requester)
    {
        if (routeReservations.TryGetValue(coord, out ReservationInfo info) && info.owner == requester)
            routeReservations.Remove(coord);
    }

    public void ReleaseAllReservationsOfOwner(GameObject owner)
    {
        if (owner == null) return;

        var keys = routeReservations.Where(kv => kv.Value.owner == owner).Select(kv => kv.Key).ToList();
        foreach (var k in keys)
            routeReservations.Remove(k);

        startedOwners.Remove(owner);
    }

    public void NotifyRouteStarted(GameObject owner)
    {
        if (owner == null) return;
        startedOwners.Add(owner);
    }

    public void NotifyRouteFinished(GameObject owner)
    {
        if (owner == null) return;
        startedOwners.Remove(owner);
        ReleaseAllReservationsOfOwner(owner);
    }
    #endregion

    #region Marcas
    public void AgregarMarcaEnCoord(Vector2Int coord, GameObject marcaPrefab, float duracion)
    {
        if (marcaPrefab == null) return;

        Transform tr = GetCasillaTransform(coord);
        if (tr == null) return;

        GameObject m = Instantiate(marcaPrefab, tr.position + Vector3.up * 0.01f, Quaternion.identity, tr);
        Destroy(m, duracion);
    }
    #endregion

    #region Raycasts / utilidades
    public List<Vector2Int> RayCasillasEnDireccion(Vector2Int inicio, Vector2Int dir)
    {
        List<Vector2Int> lista = new List<Vector2Int>();
        Vector2Int cur = inicio + dir;

        while (ExisteCasilla(cur))
        {
            lista.Add(cur);

            if (EstaOcupada(cur))
                break;

            cur += dir;
        }

        return lista;
    }
    #endregion

    #region Editor tools
    [ContextMenu("Refresh Casillas (Recolectar)")]
    public void ContextRefreshCasillas()
    {
        RecolectarCasillasExistentes();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (casillasTransform == null) casillasTransform = new List<Transform>();
            casillasTransform.RemoveAll(item => item == null);
            SyncDictFromList();
        }
    }
#endif
    #endregion
}








//using System.Collections;
 //using System.Collections.Generic;
 //using System.Linq;
 //using UnityEngine;

//#if UNITY_EDITOR
//using UnityEditor;
//#endif

//public class BoardGenerator : MonoBehaviour
//{
//    [Header("Opciones")]
//    [Tooltip("Si true genera automaticamente una cuadricula. Si false, recolecta los Transform existentes.")]
//    public bool generarAutomatico = false;

//    [Header("Prefabs casillas (solo si generarAutomatico true)")]
//    public GameObject prefabCasillaBlanca;
//    public GameObject prefabCasillaNegra;

//    [Header("Parametros tablero (solo si generarAutomatico true)")]
//    public int ancho = 8;
//    public int alto = 8;
//    public float tamCasilla = 1f;
//    public Transform padreCasillas;

//    [Tooltip("Lista de Transform de las casillas/plano.")]
//    public List<Transform> casillasTransform = new List<Transform>();

//    private readonly Dictionary<Vector2Int, Transform> casillas = new Dictionary<Vector2Int, Transform>();
//    private readonly Dictionary<Vector2Int, GameObject> ocupantes = new Dictionary<Vector2Int, GameObject>();

//    private class ReservationInfo
//    {
//        public GameObject owner;
//        public float eta;
//    }

//    private readonly Dictionary<Vector2Int, ReservationInfo> routeReservations = new Dictionary<Vector2Int, ReservationInfo>();
//    private readonly HashSet<GameObject> startedOwners = new HashSet<GameObject>();

//    public int reintentosRecolectar = 30;
//    public float delayEntreReintentos = 0.05f;

//    void Start()
//    {
//        EnsureReady();
//        if (casillas.Count == 0)
//            StartCoroutine(RetryCollectRoutine());
//    }

//    public int CasillasCount => casillas != null ? casillas.Count : 0;

//    public void EnsureReady()
//    {
//        if (CasillasCount > 0) return;

//        if (generarAutomatico)
//        {
//            if (prefabCasillaBlanca == null || prefabCasillaNegra == null)
//            {
//                Debug.LogWarning("BoardGenerator: generarAutomatico=true pero faltan prefabs. Intentando recolectar casillas existentes.");
//                RecolectarCasillasExistentes();
//                return;
//            }

//            GenerarTableroAutomatico();
//            SyncListFromDict();
//        }
//        else
//        {
//            if (casillasTransform != null && casillasTransform.Count > 0)
//                SyncDictFromList();
//            else
//                RecolectarCasillasExistentes();
//        }
//    }

//    #region generación / recolección
//    public void GenerarTableroAutomatico()
//    {
//        if (padreCasillas != null)
//        {
//            for (int i = padreCasillas.childCount - 1; i >= 0; i--)
//            {
//                GameObject child = padreCasillas.GetChild(i).gameObject;
//#if UNITY_EDITOR
//                if (!Application.isPlaying) DestroyImmediate(child);
//                else Destroy(child);
//#else
//                Destroy(child);
//#endif
//            }
//        }

//        casillas.Clear();
//        casillasTransform.Clear();
//        ocupantes.Clear();
//        routeReservations.Clear();
//        startedOwners.Clear();

//        for (int y = 0; y < alto; y++)
//        {
//            for (int x = 0; x < ancho; x++)
//            {
//                bool esBlanca = (x + y) % 2 == 0;
//                GameObject prefab = esBlanca ? prefabCasillaBlanca : prefabCasillaNegra;
//                Vector3 pos = transform.position + new Vector3(x * tamCasilla, 0f, y * tamCasilla);

//                GameObject go = Instantiate(prefab, pos, Quaternion.identity, padreCasillas);
//                go.name = $"Casilla_{x}_{y}";

//                Transform tr = go.transform;
//                Vector2Int coord = new Vector2Int(x, y);

//                casillas[coord] = tr;
//                casillasTransform.Add(tr);
//            }
//        }
//    }

//    public void RecolectarCasillasExistentes()
//    {
//        if (casillasTransform != null && casillasTransform.Count > 0)
//        {
//            SyncDictFromList();
//            return;
//        }

//        casillas.Clear();
//        casillasTransform.Clear();
//        ocupantes.Clear();

//        Transform[] encontradas;

//        if (padreCasillas != null)
//        {
//            encontradas = padreCasillas.GetComponentsInChildren<Transform>(true)
//                .Where(t => t != null && t != padreCasillas)
//                .ToArray();
//        }
//        else
//        {
//            encontradas = FindObjectsOfType<Transform>(true)
//                .Where(t => t != null && t.gameObject.scene.IsValid() && t != transform)
//                .ToArray();
//        }

//        foreach (var tr in encontradas)
//        {
//            if (tr == null) continue;

//            Vector2Int coord = AutoAsignarCoordPorPosicion(tr.position);

//            while (casillas.ContainsKey(coord))
//                coord.x += 1;

//            casillas[coord] = tr;
//            casillasTransform.Add(tr);
//        }

//        if (casillas.Count == 0)
//            Debug.LogWarning("BoardGenerator: no se encontraron casillas en la escena.");
//    }

//    private IEnumerator RetryCollectRoutine()
//    {
//        int tries = 0;
//        while (tries < reintentosRecolectar && casillas.Count == 0)
//        {
//            RecolectarCasillasExistentes();
//            if (casillas.Count > 0) break;

//            tries++;
//            yield return new WaitForSeconds(delayEntreReintentos);
//        }

//        if (casillas.Count == 0)
//            Debug.LogWarning("BoardGenerator: RetryCollectRoutine terminó sin encontrar casillas.");
//    }

//    private void SyncDictFromList()
//    {
//        casillas.Clear();
//        if (casillasTransform == null) casillasTransform = new List<Transform>();

//        foreach (var tr in casillasTransform)
//        {
//            if (tr == null) continue;
//            if (!tr.gameObject.scene.IsValid()) continue;

//            Vector2Int coord = AutoAsignarCoordPorPosicion(tr.position);

//            while (casillas.ContainsKey(coord))
//                coord.x += 1;

//            casillas[coord] = tr;
//        }
//    }

//    private void SyncListFromDict()
//    {
//        if (casillasTransform == null) casillasTransform = new List<Transform>();
//        casillasTransform.Clear();

//        foreach (var kv in casillas)
//        {
//            if (kv.Value != null)
//                casillasTransform.Add(kv.Value);
//        }
//    }

//    private Vector2Int AutoAsignarCoordPorPosicion(Vector3 worldPos)
//    {
//        Vector3 local = worldPos - transform.position;
//        int x = Mathf.RoundToInt(local.x / tamCasilla);
//        int y = Mathf.RoundToInt(local.z / tamCasilla);
//        return new Vector2Int(x, y);
//    }
//    #endregion

//    #region API de casillas
//    public bool ExisteCasilla(Vector2Int coord)
//    {
//        if (casillas.ContainsKey(coord)) return true;

//        if (casillasTransform != null)
//        {
//            foreach (var tr in casillasTransform)
//            {
//                if (tr == null) continue;
//                if (AutoAsignarCoordPorPosicion(tr.position) == coord)
//                {
//                    casillas[coord] = tr;
//                    return true;
//                }
//            }
//        }

//        return false;
//    }

//    public Transform GetCasillaTransform(Vector2Int coord)
//    {
//        if (casillas.TryGetValue(coord, out Transform tr))
//            return tr;

//        if (casillasTransform != null)
//        {
//            foreach (var item in casillasTransform)
//            {
//                if (item == null) continue;
//                if (AutoAsignarCoordPorPosicion(item.position) == coord)
//                {
//                    casillas[coord] = item;
//                    return item;
//                }
//            }
//        }

//        RecolectarCasillasExistentes();
//        casillas.TryGetValue(coord, out tr);
//        return tr;
//    }

//    public Vector3 GetWorldPos(Vector2Int coord)
//    {
//        Transform tr = GetCasillaTransform(coord);
//        return tr != null ? tr.position : Vector3.zero;
//    }

//    public bool EstaOcupada(Vector2Int coord)
//    {
//        return ocupantes.TryGetValue(coord, out GameObject go) && go != null;
//    }

//    public GameObject GetOcupante(Vector2Int coord)
//    {
//        ocupantes.TryGetValue(coord, out GameObject go);
//        return go;
//    }

//    public void SetOcupante(Vector2Int coord, GameObject go)
//    {
//        if (go == null)
//        {
//            if (ocupantes.ContainsKey(coord))
//                ocupantes.Remove(coord);
//            return;
//        }

//        ocupantes[coord] = go;
//    }

//    public IEnumerable<Transform> GetAllCasillas() => casillasTransform;
//    #endregion

//    #region Reserva / coordinación
//    public bool RequestRouteReservation(List<Vector2Int> ruta, GameObject requester, float eta, bool permitirUltimaOcupadaPorOtro = false)
//    {
//        if (ruta == null || ruta.Count == 0) return false;

//        for (int i = 0; i < ruta.Count; i++)
//        {
//            Vector2Int coord = ruta[i];
//            bool esUltima = (i == ruta.Count - 1);

//            Transform tr = GetCasillaTransform(coord);
//            if (tr == null) return false;

//            if (EstaOcupada(coord))
//            {
//                GameObject ocupante = GetOcupante(coord);
//                if (ocupante != requester && !(esUltima && permitirUltimaOcupadaPorOtro))
//                    return false;
//            }

//            if (routeReservations.TryGetValue(coord, out ReservationInfo existing) && existing.owner != requester)
//            {
//                if (startedOwners.Contains(existing.owner))
//                    return false;

//                if (eta + 0.001f < existing.eta)
//                {
//                    ReleaseAllReservationsOfOwner(existing.owner);
//                }
//                else
//                {
//                    return false;
//                }
//            }
//        }

//        foreach (var coord in ruta)
//        {
//            Transform tr = GetCasillaTransform(coord);
//            if (tr == null) continue;

//            routeReservations[coord] = new ReservationInfo() { owner = requester, eta = eta };
//        }

//        return true;
//    }

//    public void ReleaseRutaReservation(List<Vector2Int> ruta, GameObject requester)
//    {
//        if (ruta == null) return;

//        foreach (var coord in ruta)
//            ReleaseRutaReservationForCoord(coord, requester);
//    }

//    public void ReleaseRutaReservationForCoord(Vector2Int coord, GameObject requester)
//    {
//        if (routeReservations.TryGetValue(coord, out ReservationInfo info) && info.owner == requester)
//            routeReservations.Remove(coord);
//    }

//    public void ReleaseAllReservationsOfOwner(GameObject owner)
//    {
//        if (owner == null) return;

//        var keys = routeReservations.Where(kv => kv.Value.owner == owner).Select(kv => kv.Key).ToList();
//        foreach (var k in keys)
//            routeReservations.Remove(k);

//        startedOwners.Remove(owner);
//    }

//    public void NotifyRouteStarted(GameObject owner)
//    {
//        if (owner == null) return;
//        startedOwners.Add(owner);
//    }

//    public void NotifyRouteFinished(GameObject owner)
//    {
//        if (owner == null) return;
//        startedOwners.Remove(owner);
//        ReleaseAllReservationsOfOwner(owner);
//    }
//    #endregion

//    #region Marcas
//    public void AgregarMarcaEnCoord(Vector2Int coord, GameObject marcaPrefab, float duracion)
//    {
//        if (marcaPrefab == null) return;

//        Transform tr = GetCasillaTransform(coord);
//        if (tr == null) return;

//        GameObject m = Instantiate(marcaPrefab, tr.position + Vector3.up * 0.01f, Quaternion.identity, tr);
//        Destroy(m, duracion);
//    }
//    #endregion

//    #region Raycasts / utilidades
//    public List<Vector2Int> RayCasillasEnDireccion(Vector2Int inicio, Vector2Int dir)
//    {
//        List<Vector2Int> lista = new List<Vector2Int>();
//        Vector2Int cur = inicio + dir;

//        while (ExisteCasilla(cur))
//        {
//            lista.Add(cur);

//            if (EstaOcupada(cur))
//                break;

//            cur += dir; 
//        }

//        return lista;
//    }
//    #endregion

//    #region Editor tools
//    [ContextMenu("Refresh Casillas (Recolectar)")]
//    public void ContextRefreshCasillas()
//    {
//        RecolectarCasillasExistentes();
//    }

//#if UNITY_EDITOR
//    void OnValidate()
//    {
//        if (!EditorApplication.isPlayingOrWillChangePlaymode)
//        {
//            if (casillasTransform == null) casillasTransform = new List<Transform>();
//            casillasTransform.RemoveAll(item => item == null);
//            SyncDictFromList();
//        }
//    }
//#endif
//    #endregion
//}