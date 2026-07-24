//// Archivo: FichaPeon.cs
//using System.Collections.Generic;
//using UnityEngine;

//public class FichaPeon : FichaBase
//{
//    [Tooltip("Direccion hacia adelante: 1 = hacia y positivo, -1 = hacia y negativo")]
//    public int direccion = 1;

//    public override List<Vector2Int> ObtenerCasillasPosibles()
//    {
//        List<Vector2Int> lista = new List<Vector2Int>();
//        Vector2Int adelante = coordActual + new Vector2Int(0, direccion);
//        Vector2Int diagIzq = coordActual + new Vector2Int(-1, direccion);
//        Vector2Int diagDer = coordActual + new Vector2Int(1, direccion);

//        if (tablero.ExisteCasilla(diagIzq)) lista.Add(diagIzq);
//        if (tablero.ExisteCasilla(diagDer)) lista.Add(diagDer);
//        if (tablero.ExisteCasilla(adelante) && !tablero.GetCasilla(adelante).tieneOcupante) lista.Add(adelante);

//        return lista;
//    }

//    protected override List<Vector2Int> CalcularRutaHasta(Vector2Int destino)
//    {
//        List<Vector2Int> ruta = new List<Vector2Int>();
//        ruta.Add(destino);
//        return ruta;
//    }
//}
