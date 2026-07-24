//using System.Collections.Generic;
//using UnityEngine;

//public class Casilla : MonoBehaviour
//{
//    [Tooltip("Asignar coordenadas manualmente en inspector si se usa mapa irregular.")]
//    public Vector2Int coord = new Vector2Int(int.MinValue, int.MinValue); // valor por defecto indica 'no asignada'
//    [HideInInspector] public Vector3 worldPos;
//    [HideInInspector] public float tam;
//    public bool tieneOcupante = false;
//    public GameObject ocupanteGO;

//    // Reserva temporal para evitar que otras fichas vayan a la misma casilla
//    [HideInInspector] public GameObject reservadoPor;

//    private List<GameObject> marcas = new List<GameObject>();
//    private BoardGenerator board;

//    public void Inicializar(Vector2Int coord, Vector3 worldPos, float tam, BoardGenerator board)
//    {
//        this.coord = coord;
//        this.worldPos = worldPos;
//        this.tam = tam;
//        this.board = board;
//    }

//    public void SetBoard(BoardGenerator board)
//    {
//        this.board = board;
//    }

//    void Awake()
//    {
//        worldPos = transform.position;
//        if (tam <= 0) tam = 1f;
//    }

//    public void SetOcupante(GameObject go)
//    {
//        ocupanteGO = go;
//        tieneOcupante = go != null;
//        // si ocupante se asigna, limpiar reserva si la ocupó
//        if (go != null && reservadoPor == go) reservadoPor = null;
//    }

//    // intenta reservar la casilla para 'go' (false si está ocupada por otro o reservada por otro)
//    public bool TryReserve(GameObject go)
//    {
//        if (tieneOcupante && ocupanteGO != go) return false;
//        if (reservadoPor != null && reservadoPor != go) return false;
//        reservadoPor = go;
//        return true;
//    }

//    public void ReleaseReservation(GameObject go)
//    {
//        if (reservadoPor == go) reservadoPor = null;
//    }

//    public void AgregarMarca(GameObject marcaPrefab, float duracion)
//    {
//        if (marcaPrefab == null) return;
//        GameObject m = Instantiate(marcaPrefab, worldPos + Vector3.up * 0.01f, Quaternion.identity, transform);
//        marcas.Add(m);
//        Destroy(m, duracion);
//    }

//    public void LimpiarMarcas()
//    {
//        foreach (var m in marcas) if (m != null) Destroy(m);
//        marcas.Clear();
//    }
//}
