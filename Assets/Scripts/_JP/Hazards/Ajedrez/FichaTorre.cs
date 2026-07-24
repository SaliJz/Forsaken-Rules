using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FichaTorre : FichaBase
{
    public override List<Vector2Int> ObtenerCasillasPosibles()
    {
        List<Vector2Int> lista = new List<Vector2Int>();
        Vector2Int origen = coordActual;

        Vector2Int[] dirs = new Vector2Int[]
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        Vector2Int coordJugador = new Vector2Int(int.MinValue, int.MinValue);
        if (jugadorTransform == null)
        {
            var pj = GameObject.FindGameObjectWithTag(tagJugador);
            if (pj != null) jugadorTransform = pj.transform;
        }
        if (jugadorTransform != null)
            coordJugador = WorldPosACoord(jugadorTransform.position);

        foreach (var d in dirs)
        {
            List<Vector2Int> r = tablero.RayCasillasEnDireccion(origen, d);
            foreach (var c in r)
            {
                lista.Add(c);
                if (c == coordJugador)
                    break;
            }
        }

        return lista;
    }

    protected override List<Vector2Int> CalcularRutaHasta(Vector2Int destino)
    {
        List<Vector2Int> ruta = new List<Vector2Int>();

        Vector2Int dir = new Vector2Int(
            Mathf.Clamp(destino.x - coordActual.x, -1, 1),
            Mathf.Clamp(destino.y - coordActual.y, -1, 1)
        );

        if (dir.x != 0 && dir.y != 0)
            return ruta;

        Vector2Int cur = coordActual;

        while (cur != destino)
        {
            cur += dir;
            ruta.Add(cur);

            if (!tablero.ExisteCasilla(cur))
                break;

            if (tablero.EstaOcupada(cur) && cur != destino)
                break;
        }

        return ruta;
    }

    public IEnumerator RutinaAtaqueDirecta(Vector2Int destino)
    {
        estaMoviendo = true;

        yield return new WaitForSeconds(tiempoEsperaInicial);

        List<Vector2Int> ruta = CalcularRutaHasta(destino);
        if (ruta == null || ruta.Count == 0)
        {
            estaMoviendo = false;
            yield break;
        }

        float eta = tiempoAntesMover + (ruta.Count / Mathf.Max(1f, tilesPorSegundo));

        bool reserved = tablero.RequestRouteReservation(ruta, gameObject, eta, true);

        if (!reserved)
        {
            var ultima = ruta[ruta.Count - 1];
            if (tablero.RequestRouteReservation(new List<Vector2Int> { ultima }, gameObject, eta, true))
            {
                ruta = new List<Vector2Int> { ultima };
                reserved = true;
            }
        }

        if (!reserved)
        {
            estaMoviendo = false;
            yield break;
        }

        foreach (var c in ruta)
            tablero.AgregarMarcaEnCoord(c, marcadorTrailPrefab, tiempoAntesMover + (ruta.Count / Mathf.Max(1f, tilesPorSegundo)) + 0.2f);

        yield return new WaitForSeconds(tiempoAntesMover);

        tablero.NotifyRouteStarted(gameObject);

        Vector2Int finalCoord = ruta[ruta.Count - 1];
        Vector2Int origenCoord = coordActual;

        tablero.SetOcupante(origenCoord, null);

        float velocidad = tilesPorSegundo * tamCasilla;
        if (velocidad <= 0f) velocidad = tamCasilla;

        for (int i = 0; i < ruta.Count; i++)
        {
            Vector2Int coordPaso = ruta[i];
            Vector3 target = tablero.GetWorldPos(coordPaso) + Vector3.up * 0.1f;

            while (Vector3.Distance(transform.position, target) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, velocidad * Time.deltaTime);
                yield return null;
            }

            transform.position = target;
            coordActual = coordPaso;
            tablero.SetOcupante(coordActual, gameObject);

            yield return null;
        }

        tablero.ReleaseRutaReservation(ruta, gameObject);

        Vector2Int coordJugadorActual = new Vector2Int(int.MinValue, int.MinValue);
        if (jugadorTransform == null)
        {
            var pj = GameObject.FindGameObjectWithTag(tagJugador);
            if (pj != null) jugadorTransform = pj.transform;
        }
        if (jugadorTransform != null)
            coordJugadorActual = WorldPosACoord(jugadorTransform.position);

        if (coordActual == coordJugadorActual)
            AttackPlayer(transform.position);

        tablero.NotifyRouteFinished(gameObject);
        estaMoviendo = false;
    }
}