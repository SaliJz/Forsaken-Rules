//// Archivo: FichaCaballo.cs
//using System.Collections.Generic;
//using UnityEngine;

//public class FichaCaballo : FichaBase
//{
//    public override List<Vector2Int> ObtenerCasillasPosibles()
//    {
//        List<Vector2Int> lista = new List<Vector2Int>();
//        Vector2Int[] offsets = new Vector2Int[]
//        {
//            new Vector2Int(1,2), new Vector2Int(2,1),
//            new Vector2Int(-1,2), new Vector2Int(-2,1),
//            new Vector2Int(1,-2), new Vector2Int(2,-1),
//            new Vector2Int(-1,-2), new Vector2Int(-2,-1)
//        };
//        foreach (var o in offsets)
//        {
//            Vector2Int c = coordActual + o;
//            if (tablero.ExisteCasilla(c)) lista.Add(c);
//        }
//        return lista;
//    }

//    protected override List<Vector2Int> CalcularRutaHasta(Vector2Int destino)
//    {
//        List<Vector2Int> ruta = new List<Vector2Int>();
//        ruta.Add(destino);
//        return ruta;
//    }
//}
