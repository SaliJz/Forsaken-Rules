using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TorreIA_Tablero : MonoBehaviour
{
    [Header("Tablero")]
    public List<Transform> casillasTablero = new List<Transform>();
    public Transform jugador;
    public List<Transform> casillasBloqueadas = new List<Transform>();

    [Header("Movimiento")]
    public float velocidadRecta = 12f;
    public float velocidadGiro = 5f;
    public float distanciaLlegada = 0.02f;
    public float tiempoPausaEnCasilla = 0.15f;
    public float tiempoPausaEnGiro = 0.25f;
    public bool centrarAlInicio = true;

    [Header("Ruta")]
    public float toleranciaAgrupacion = 0.05f;
    public bool recalcularDespuesDeCadaTramo = true;

    private readonly Dictionary<Vector2Int, Transform> mapaCasillas = new Dictionary<Vector2Int, Transform>();
    private readonly HashSet<Vector2Int> casillasBloqueadasMapa = new HashSet<Vector2Int>();
    private readonly List<float> posicionesX = new List<float>();
    private readonly List<float> posicionesZ = new List<float>();

    private bool tableroListo = false;

    private void Start()
    {
        ConstruirTablero();

        if (centrarAlInicio)
        {
            Transform casillaMasCercana = ObtenerCasillaMasCercana(transform.position);
            if (casillaMasCercana != null)
            {
                Vector3 pos = casillaMasCercana.position;
                pos.y = transform.position.y;
                transform.position = pos;
            }
        }

        StartCoroutine(RutinaTorre());
    }

    private void Update()
    {
        if (!tableroListo && casillasTablero != null && casillasTablero.Count > 0)
        {
            ConstruirTablero();
        }
    }

    private IEnumerator RutinaTorre()
    {
        while (true)
        {
            if (jugador == null || casillasTablero == null || casillasTablero.Count == 0)
            {
                yield return null;
                continue;
            }

            if (!tableroListo)
            {
                ConstruirTablero();
                yield return null;
                continue;
            }

            Vector2Int celdaInicio = ObtenerCeldaMasCercana(transform.position);
            Vector2Int celdaMeta = ObtenerCeldaMasCercana(jugador.position);

            if (!mapaCasillas.ContainsKey(celdaInicio) || !mapaCasillas.ContainsKey(celdaMeta))
            {
                yield return null;
                continue;
            }

            List<Vector2Int> ruta = ObtenerRutaMasCorta(celdaInicio, celdaMeta);

            if (ruta == null || ruta.Count < 2)
            {
                yield return null;
                continue;
            }

            List<TramoRuta> tramos = AgruparRutaPorDireccion(ruta);

            for (int i = 0; i < tramos.Count; i++)
            {
                TramoRuta tramo = tramos[i];

                if (tramo.celdas.Count < 2)
                    continue;

                Vector2Int celdaObjetivo = tramo.celdas[tramo.celdas.Count - 1];

                if (!mapaCasillas.ContainsKey(celdaObjetivo))
                    break;

                float velocidadActual = tramo.celdas.Count >= 3 ? velocidadRecta : velocidadGiro;

                Vector3 posicionObjetivo = mapaCasillas[celdaObjetivo].position;
                posicionObjetivo.y = transform.position.y;

                while ((transform.position - posicionObjetivo).sqrMagnitude > distanciaLlegada * distanciaLlegada)
                {
                    transform.position = Vector3.MoveTowards(
                        transform.position,
                        posicionObjetivo,
                        velocidadActual * Time.deltaTime
                    );

                    Vector3 direccion = posicionObjetivo - transform.position;
                    direccion.y = 0f;

                    if (direccion.sqrMagnitude > 0.0001f)
                    {
                        transform.forward = direccion.normalized;
                    }

                    yield return null;
                }

                transform.position = posicionObjetivo;

                if (i < tramos.Count - 1)
                {
                    yield return new WaitForSeconds(tiempoPausaEnGiro);
                }
                else
                {
                    yield return new WaitForSeconds(tiempoPausaEnCasilla);
                }

                if (recalcularDespuesDeCadaTramo)
                {
                    break;
                }
            }

            yield return null;
        }
    }

    private void ConstruirTablero()
    {
        mapaCasillas.Clear();
        casillasBloqueadasMapa.Clear();
        posicionesX.Clear();
        posicionesZ.Clear();

        if (casillasTablero == null || casillasTablero.Count == 0)
        {
            tableroListo = false;
            return;
        }

        for (int i = 0; i < casillasTablero.Count; i++)
        {
            Transform casilla = casillasTablero[i];
            if (casilla == null) continue;

            AgregarValorUnicoAproximado(posicionesX, casilla.position.x);
            AgregarValorUnicoAproximado(posicionesZ, casilla.position.z);
        }

        posicionesX.Sort();
        posicionesZ.Sort();

        for (int i = 0; i < casillasTablero.Count; i++)
        {
            Transform casilla = casillasTablero[i];
            if (casilla == null) continue;

            int indiceX = ObtenerIndiceMasCercano(posicionesX, casilla.position.x);
            int indiceZ = ObtenerIndiceMasCercano(posicionesZ, casilla.position.z);

            Vector2Int celda = new Vector2Int(indiceX, indiceZ);

            if (!mapaCasillas.ContainsKey(celda))
            {
                mapaCasillas.Add(celda, casilla);
            }
        }

        if (casillasBloqueadas != null)
        {
            for (int i = 0; i < casillasBloqueadas.Count; i++)
            {
                Transform bloqueada = casillasBloqueadas[i];
                if (bloqueada == null) continue;

                Vector2Int celdaBloqueada = ObtenerCeldaMasCercana(bloqueada.position);
                if (mapaCasillas.ContainsKey(celdaBloqueada))
                {
                    casillasBloqueadasMapa.Add(celdaBloqueada);
                }
            }
        }

        tableroListo = true;
    }

    private List<Vector2Int> ObtenerRutaMasCorta(Vector2Int inicio, Vector2Int meta)
    {
        if (!mapaCasillas.ContainsKey(inicio) || !mapaCasillas.ContainsKey(meta))
            return null;

        if (inicio == meta)
            return new List<Vector2Int> { inicio };

        if (inicio.x == meta.x && HayLineaLibre(inicio, meta))
            return ConstruirRutaRecta(inicio, meta);

        if (inicio.y == meta.y && HayLineaLibre(inicio, meta))
            return ConstruirRutaRecta(inicio, meta);

        Vector2Int esquina1 = new Vector2Int(meta.x, inicio.y);
        Vector2Int esquina2 = new Vector2Int(inicio.x, meta.y);

        bool ruta1Valida =
            mapaCasillas.ContainsKey(esquina1) &&
            !casillasBloqueadasMapa.Contains(esquina1) &&
            HayLineaLibre(inicio, esquina1) &&
            HayLineaLibre(esquina1, meta);

        bool ruta2Valida =
            mapaCasillas.ContainsKey(esquina2) &&
            !casillasBloqueadasMapa.Contains(esquina2) &&
            HayLineaLibre(inicio, esquina2) &&
            HayLineaLibre(esquina2, meta);

        if (ruta1Valida)
            return UnirRutas(ConstruirRutaRecta(inicio, esquina1), ConstruirRutaRecta(esquina1, meta));

        if (ruta2Valida)
            return UnirRutas(ConstruirRutaRecta(inicio, esquina2), ConstruirRutaRecta(esquina2, meta));

        return BuscarRutaAStar(inicio, meta);
    }

    private bool HayLineaLibre(Vector2Int inicio, Vector2Int meta)
    {
        if (inicio.x != meta.x && inicio.y != meta.y)
            return false;

        if (inicio == meta)
            return true;

        if (inicio.x == meta.x)
        {
            int paso = meta.y > inicio.y ? 1 : -1;
            for (int y = inicio.y + paso; y != meta.y + paso; y += paso)
            {
                Vector2Int celda = new Vector2Int(inicio.x, y);

                if (!mapaCasillas.ContainsKey(celda))
                    return false;

                if (casillasBloqueadasMapa.Contains(celda) && celda != meta)
                    return false;
            }
        }
        else
        {
            int paso = meta.x > inicio.x ? 1 : -1;
            for (int x = inicio.x + paso; x != meta.x + paso; x += paso)
            {
                Vector2Int celda = new Vector2Int(x, inicio.y);

                if (!mapaCasillas.ContainsKey(celda))
                    return false;

                if (casillasBloqueadasMapa.Contains(celda) && celda != meta)
                    return false;
            }
        }

        return true;
    }

    private List<Vector2Int> ConstruirRutaRecta(Vector2Int inicio, Vector2Int meta)
    {
        List<Vector2Int> ruta = new List<Vector2Int>();
        ruta.Add(inicio);

        if (inicio.x == meta.x)
        {
            int paso = meta.y > inicio.y ? 1 : -1;
            for (int y = inicio.y + paso; y != meta.y + paso; y += paso)
            {
                ruta.Add(new Vector2Int(inicio.x, y));
            }
        }
        else if (inicio.y == meta.y)
        {
            int paso = meta.x > inicio.x ? 1 : -1;
            for (int x = inicio.x + paso; x != meta.x + paso; x += paso)
            {
                ruta.Add(new Vector2Int(x, inicio.y));
            }
        }

        return ruta;
    }

    private List<Vector2Int> UnirRutas(List<Vector2Int> ruta1, List<Vector2Int> ruta2)
    {
        if (ruta1 == null || ruta2 == null)
            return null;

        List<Vector2Int> rutaFinal = new List<Vector2Int>(ruta1);
        for (int i = 1; i < ruta2.Count; i++)
        {
            rutaFinal.Add(ruta2[i]);
        }

        return rutaFinal;
    }

    private List<TramoRuta> AgruparRutaPorDireccion(List<Vector2Int> ruta)
    {
        List<TramoRuta> tramos = new List<TramoRuta>();

        if (ruta == null || ruta.Count < 2)
            return tramos;

        TramoRuta tramoActual = new TramoRuta();
        tramoActual.celdas.Add(ruta[0]);

        Vector2Int direccionActual = ruta[1] - ruta[0];
        tramoActual.direccion = direccionActual;
        tramoActual.celdas.Add(ruta[1]);

        for (int i = 2; i < ruta.Count; i++)
        {
            Vector2Int direccionNueva = ruta[i] - ruta[i - 1];

            if (direccionNueva == direccionActual)
            {
                tramoActual.celdas.Add(ruta[i]);
            }
            else
            {
                tramos.Add(tramoActual);

                tramoActual = new TramoRuta();
                tramoActual.direccion = direccionNueva;
                tramoActual.celdas.Add(ruta[i - 1]);
                tramoActual.celdas.Add(ruta[i]);

                direccionActual = direccionNueva;
            }
        }

        tramos.Add(tramoActual);
        return tramos;
    }

    private List<Vector2Int> BuscarRutaAStar(Vector2Int inicio, Vector2Int meta)
    {
        if (!mapaCasillas.ContainsKey(inicio) || !mapaCasillas.ContainsKey(meta))
            return null;

        if (inicio == meta)
            return new List<Vector2Int> { inicio };

        List<NodoBusqueda> abiertos = new List<NodoBusqueda>();
        HashSet<Vector2Int> cerrados = new HashSet<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> vinoDe = new Dictionary<Vector2Int, Vector2Int>();
        Dictionary<Vector2Int, float> costoG = new Dictionary<Vector2Int, float>();

        abiertos.Add(new NodoBusqueda(inicio, 0f));
        costoG[inicio] = 0f;

        while (abiertos.Count > 0)
        {
            int mejorIndice = 0;
            float mejorF = abiertos[0].f;

            for (int i = 1; i < abiertos.Count; i++)
            {
                if (abiertos[i].f < mejorF)
                {
                    mejorF = abiertos[i].f;
                    mejorIndice = i;
                }
            }

            NodoBusqueda actualNodo = abiertos[mejorIndice];
            abiertos.RemoveAt(mejorIndice);

            Vector2Int actual = actualNodo.celda;

            if (actual == meta)
                return ReconstruirRuta(vinoDe, inicio, meta);

            cerrados.Add(actual);

            List<Vector2Int> vecinos = ObtenerVecinosOrtogonales(actual);

            for (int i = 0; i < vecinos.Count; i++)
            {
                Vector2Int vecino = vecinos[i];

                if (!mapaCasillas.ContainsKey(vecino))
                    continue;

                if (casillasBloqueadasMapa.Contains(vecino) && vecino != meta)
                    continue;

                if (cerrados.Contains(vecino))
                    continue;

                float nuevoCostoG = costoG[actual] + 1f;

                if (!costoG.ContainsKey(vecino) || nuevoCostoG < costoG[vecino])
                {
                    costoG[vecino] = nuevoCostoG;
                    vinoDe[vecino] = actual;

                    float f = nuevoCostoG + HeuristicaManhattan(vecino, meta);

                    bool actualizado = false;
                    for (int j = 0; j < abiertos.Count; j++)
                    {
                        if (abiertos[j].celda == vecino)
                        {
                            abiertos[j] = new NodoBusqueda(vecino, f);
                            actualizado = true;
                            break;
                        }
                    }

                    if (!actualizado)
                    {
                        abiertos.Add(new NodoBusqueda(vecino, f));
                    }
                }
            }
        }

        return null;
    }

    private List<Vector2Int> ReconstruirRuta(Dictionary<Vector2Int, Vector2Int> vinoDe, Vector2Int inicio, Vector2Int meta)
    {
        List<Vector2Int> ruta = new List<Vector2Int>();
        Vector2Int actual = meta;
        ruta.Add(actual);

        while (actual != inicio)
        {
            if (!vinoDe.ContainsKey(actual))
                return null;

            actual = vinoDe[actual];
            ruta.Add(actual);
        }

        ruta.Reverse();
        return ruta;
    }

    private List<Vector2Int> ObtenerVecinosOrtogonales(Vector2Int celda)
    {
        return new List<Vector2Int>
        {
            new Vector2Int(celda.x + 1, celda.y),
            new Vector2Int(celda.x - 1, celda.y),
            new Vector2Int(celda.x, celda.y + 1),
            new Vector2Int(celda.x, celda.y - 1)
        };
    }

    private float HeuristicaManhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private Vector2Int ObtenerCeldaMasCercana(Vector3 posicionMundo)
    {
        Vector2Int mejorCelda = default;
        float mejorDistancia = float.MaxValue;
        bool encontro = false;

        foreach (var par in mapaCasillas)
        {
            Transform casilla = par.Value;
            if (casilla == null) continue;

            float distancia = (casilla.position - posicionMundo).sqrMagnitude;
            if (distancia < mejorDistancia)
            {
                mejorDistancia = distancia;
                mejorCelda = par.Key;
                encontro = true;
            }
        }

        return encontro ? mejorCelda : default;
    }

    private Transform ObtenerCasillaMasCercana(Vector3 posicionMundo)
    {
        Transform mejor = null;
        float mejorDistancia = float.MaxValue;

        for (int i = 0; i < casillasTablero.Count; i++)
        {
            Transform casilla = casillasTablero[i];
            if (casilla == null) continue;

            float distancia = (casilla.position - posicionMundo).sqrMagnitude;
            if (distancia < mejorDistancia)
            {
                mejorDistancia = distancia;
                mejor = casilla;
            }
        }

        return mejor;
    }

    private void AgregarValorUnicoAproximado(List<float> lista, float valor)
    {
        for (int i = 0; i < lista.Count; i++)
        {
            if (Mathf.Abs(lista[i] - valor) <= toleranciaAgrupacion)
                return;
        }

        lista.Add(valor);
    }

    private int ObtenerIndiceMasCercano(List<float> lista, float valor)
    {
        int mejorIndice = 0;
        float mejorDistancia = float.MaxValue;

        for (int i = 0; i < lista.Count; i++)
        {
            float distancia = Mathf.Abs(lista[i] - valor);
            if (distancia < mejorDistancia)
            {
                mejorDistancia = distancia;
                mejorIndice = i;
            }
        }

        return mejorIndice;
    }

    private class NodoBusqueda
    {
        public Vector2Int celda;
        public float f;

        public NodoBusqueda(Vector2Int celda, float f)
        {
            this.celda = celda;
            this.f = f;
        }
    }

    private class TramoRuta
    {
        public Vector2Int direccion;
        public List<Vector2Int> celdas = new List<Vector2Int>();
    }
}