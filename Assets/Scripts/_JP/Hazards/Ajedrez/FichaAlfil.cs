//// Archivo: FichaAlfil.cs
//using System.Collections.Generic;
//using UnityEngine;

//public class FichaAlfil : FichaBase
//{
//    public override List<Vector2Int> ObtenerCasillasPosibles()
//    {
//        List<Vector2Int> lista = new List<Vector2Int>();
//        Vector2Int origen = coordActual;
//        Vector2Int[] dirs = new Vector2Int[] { new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1) };
//        foreach (var d in dirs)
//        {
//            List<Casilla> r = tablero.RayCasillasEnDireccion(origen, d);
//            foreach (var c in r) lista.Add(c.coord);
//        }
//        return lista;
//    }

//    protected override List<Vector2Int> CalcularRutaHasta(Vector2Int destino)
//    {
//        List<Vector2Int> ruta = new List<Vector2Int>();
//        Vector2Int diff = destino - coordActual;
//        if (Mathf.Abs(diff.x) != Mathf.Abs(diff.y)) return ruta;
//        Vector2Int dir = new Vector2Int(Mathf.Clamp(diff.x, -1, 1), Mathf.Clamp(diff.y, -1, 1));
//        Vector2Int cur = coordActual;
//        while (cur != destino)
//        {
//            cur += dir;
//            ruta.Add(cur);
//            if (tablero.GetCasilla(cur).tieneOcupante) break;
//        }
//        return ruta;
//    }
//}
