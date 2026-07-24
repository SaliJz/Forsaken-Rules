using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    #region Enums & Internal Data

    public enum VisualizationMode { None, Gizmos, TranslucentMesh }

    [System.Serializable]
    public class TileData
    {
        public Vector2Int coord;
        public bool active;
        public bool blocked;
        public GameObject occupant;

        [System.NonSerialized] public TileData north;
        [System.NonSerialized] public TileData south;
        [System.NonSerialized] public TileData east;
        [System.NonSerialized] public TileData west;
        [System.NonSerialized] public GameObject meshObject;
    }

    #endregion

    #region Serialized Fields

    [Header("Grid Shape")]
    public int gridWidth = 5;
    public int gridHeight = 5;
    public float tileWidth = 1f;
    public float tileHeight = 1f;
    public float tilePadding = 0f;

    [Header("Grid Data")]
    [SerializeField] private bool[] gridCells;

    [Header("Visualization")]
    public VisualizationMode visualizationMode = VisualizationMode.Gizmos;
    public Color gizmoColorActive = new Color(0f, 1f, 1f, 0.4f);
    public Color gizmoColorBlocked = new Color(1f, 0f, 0f, 0.4f);
    public Color gizmoColorReserved = new Color(1f, 0.5f, 0f, 0.4f);
    public Color meshColorPrimary = new Color(0f, 0.8f, 1f, 0.25f);
    public Color meshColorSecondary = new Color(0f, 0.4f, 0.6f, 0.25f);

    #endregion

    #region Runtime State

    private Dictionary<Vector2Int, TileData> board = new Dictionary<Vector2Int, TileData>();
    private Dictionary<Vector2Int, (GameObject owner, float expireTime)> reservations = new Dictionary<Vector2Int, (GameObject, float)>();
    private HashSet<GameObject> movingPieces = new HashSet<GameObject>();
    private List<(GameObject obj, float destroyTime)> activeMarkers = new List<(GameObject, float)>();
    private List<GameObject> meshObjects = new List<GameObject>();

    public bool IsReady { get; private set; } = false;

    public float StepX => tileWidth + tilePadding;
    public float StepZ => tileHeight + tilePadding;

    private const float MESH_SCALE_FACTOR = 0.038f;

    #endregion

    #region Initialization

    private void Awake()
    {
        InitializeBoard();
    }

    private void Update()
    {
        CleanExpiredReservations();
        CleanExpiredMarkers();
    }

    public void InitializeBoard()
    {
        board.Clear();
        ClearMeshObjects();

        if (gridCells == null || gridCells.Length != gridWidth * gridHeight)
            ResizeGrid();

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                if (!GetCell(x, z)) continue;

                var tile = new TileData
                {
                    coord = new Vector2Int(x, z),
                    active = true,
                    blocked = false,
                    occupant = null
                };

                board[tile.coord] = tile;
            }
        }

        ConnectNeighbors();

        if (visualizationMode == VisualizationMode.TranslucentMesh)
            BuildMeshObjects();

        IsReady = true;
    }

    private void ConnectNeighbors()
    {
        foreach (var pair in board)
        {
            var tile = pair.Value;
            board.TryGetValue(tile.coord + Vector2Int.up, out tile.north);
            board.TryGetValue(tile.coord + Vector2Int.down, out tile.south);
            board.TryGetValue(tile.coord + Vector2Int.right, out tile.east);
            board.TryGetValue(tile.coord + Vector2Int.left, out tile.west);
        }
    }

    #endregion

    #region Grid Data Helpers

    public void ResizeGrid()
    {
        bool[] newCells = new bool[gridWidth * gridHeight];

        if (gridCells != null)
            for (int i = 0; i < Mathf.Min(gridCells.Length, newCells.Length); i++)
                newCells[i] = gridCells[i];

        gridCells = newCells;
    }

    public bool GetCell(int x, int z)
    {
        if (gridCells == null || x < 0 || x >= gridWidth || z < 0 || z >= gridHeight)
            return false;
        return gridCells[z * gridWidth + x];
    }

    public void SetCell(int x, int z, bool value)
    {
        if (gridCells == null || x < 0 || x >= gridWidth || z < 0 || z >= gridHeight)
            return;
        gridCells[z * gridWidth + x] = value;
    }

    public Vector3 GetWorldPosition(int x, int z)
    {
        float offsetX = (gridWidth - 1) * StepX * 0.5f;
        float offsetZ = (gridHeight - 1) * StepZ * 0.5f;
        return transform.position + new Vector3(x * StepX - offsetX, 0f, z * StepZ - offsetZ);
    }

    #endregion

    #region Mesh Visualization

    private void BuildMeshObjects()
    {
        ClearMeshObjects();

        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");

        foreach (var pair in board)
        {
            Vector2Int coord = pair.Key;
            Vector3 worldPos = GetWorldPosition(coord.x, coord.y);

            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = $"Tile_{coord.x}_{coord.y}";
            obj.transform.SetParent(transform);
            obj.transform.position = worldPos;

            float finalScaleX = tileWidth * MESH_SCALE_FACTOR;
            float finalScaleZ = tileHeight * MESH_SCALE_FACTOR;

            obj.transform.localScale = new Vector3(finalScaleX, 0.05f, finalScaleZ);

            Destroy(obj.GetComponent<Collider>());

            var renderer = obj.GetComponent<MeshRenderer>();
            Material mat = new Material(unlitShader);

            bool isEven = (coord.x + coord.y) % 2 == 0;
            mat.color = isEven ? meshColorPrimary : meshColorSecondary;

            mat.SetFloat("_Surface", 1);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;

            renderer.material = mat;
            pair.Value.meshObject = obj;
            meshObjects.Add(obj);
        }
    }

    private void ClearMeshObjects()
    {
        foreach (var obj in meshObjects)
            if (obj != null) Destroy(obj);
        meshObjects.Clear();
    }

    #endregion

    #region Board Interface

    public List<Vector2Int> RaycastTilesInDirection(Vector2Int origin, Vector2Int direction)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        Vector2Int current = origin + direction;

        while (board.ContainsKey(current))
        {
            TileData tile = board[current];
            if (tile.blocked) break;
            result.Add(current);
            if (tile.occupant != null) break;
            current += direction;
        }

        return result;
    }

    public bool TileExists(Vector2Int coord)
    {
        return board.ContainsKey(coord) && !board[coord].blocked;
    }

    public bool IsTileOccupied(Vector2Int coord)
    {
        return board.TryGetValue(coord, out var t) && t.occupant != null;
    }

    public Vector3 GetWorldPosFromCoord(Vector2Int coord)
    {
        if (board.ContainsKey(coord))
            return GetWorldPosition(coord.x, coord.y);

        Debug.LogWarning($"[BoardManager] {name}: coord {coord} not found.");
        return Vector3.zero;
    }

    public void SetOccupant(Vector2Int coord, GameObject occupant)
    {
        if (!board.TryGetValue(coord, out var tile))
        {
            Debug.LogWarning($"[BoardManager] {name}: coord {coord} not found.");
            return;
        }

        if (occupant != null && tile.occupant != null && tile.occupant != occupant)
            HandleCollision(tile.occupant, occupant);

        tile.occupant = occupant;
    }

    public Vector2Int WorldPosToCoord(Vector3 position)
    {
        Vector2Int best = default;
        float bestDist = float.MaxValue;

        foreach (var pair in board)
        {
            float dist = (GetWorldPosition(pair.Key.x, pair.Key.y) - position).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = pair.Key;
            }
        }

        return best;
    }

    public TileData GetTile(Vector2Int coord)
    {
        board.TryGetValue(coord, out var t);
        return t;
    }

    #endregion

    #region Route Reservations

    public bool RequestRouteReservation(List<Vector2Int> route, GameObject owner, float duration)
    {
        if (route == null || route.Count == 0) return false;

        float expireTime = Time.time + duration;

        foreach (var coord in route)
            if (reservations.TryGetValue(coord, out var r) && r.owner != owner)
                return false;

        foreach (var coord in route)
            reservations[coord] = (owner, expireTime);

        return true;
    }

    public void ReleaseRouteReservation(List<Vector2Int> route, GameObject owner)
    {
        if (route == null) return;
        foreach (var coord in route)
            if (reservations.TryGetValue(coord, out var r) && r.owner == owner)
                reservations.Remove(coord);
    }

    private void CleanExpiredReservations()
    {
        var expired = reservations
            .Where(r => Time.time > r.Value.expireTime)
            .Select(r => r.Key)
            .ToList();
        foreach (var coord in expired)
            reservations.Remove(coord);
    }

    #endregion

    #region Movement Notifications

    public void NotifyRouteStarted(GameObject piece) => movingPieces.Add(piece);
    public void NotifyRouteFinished(GameObject piece) => movingPieces.Remove(piece);

    #endregion

    #region Trail Markers

    public void AddMarkerAtCoord(Vector2Int coord, GameObject prefab, float duration)
    {
        if (prefab == null || !board.ContainsKey(coord)) return;
        Vector3 pos = GetWorldPosFromCoord(coord) + Vector3.up * 0.05f;
        GameObject marker = Instantiate(prefab, pos, Quaternion.identity, transform);
        activeMarkers.Add((marker, Time.time + duration));
    }

    private void CleanExpiredMarkers()
    {
        for (int i = activeMarkers.Count - 1; i >= 0; i--)
        {
            if (Time.time >= activeMarkers[i].destroyTime)
            {
                if (activeMarkers[i].obj != null) Destroy(activeMarkers[i].obj);
                activeMarkers.RemoveAt(i);
            }
        }
    }

    #endregion

    #region Collision Logic

    private void HandleCollision(GameObject pieceA, GameObject pieceB)
    {
        pieceA.GetComponent<BoardPiece>()?.ApplyStun();
        pieceB.GetComponent<BoardPiece>()?.ApplyStun();
    }

    #endregion

    #region Piece Connection

    public bool ConnectPiece(BoardPiece piece, Vector2Int startCoord)
    {
        if (!board.ContainsKey(startCoord))
        {
            Debug.LogWarning($"[BoardManager] {name}: coord {startCoord} not found.");
            return false;
        }

        piece.boardManager = this;
        piece.cellSize = tileWidth;
        piece.currentCoord = startCoord;
        piece.transform.position = GetWorldPosFromCoord(startCoord) + Vector3.up * 0.1f;

        SetOccupant(startCoord, piece.gameObject);
        return true;
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (visualizationMode != VisualizationMode.Gizmos || gridCells == null) return;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                if (!GetCell(x, z)) continue;

                Vector2Int coord = new Vector2Int(x, z);
                Vector3 worldPos = GetWorldPosition(x, z);

                bool isReserved = reservations.ContainsKey(coord);
                bool isBlocked = board.TryGetValue(coord, out var t) && t.blocked;

                Gizmos.color = isBlocked ? gizmoColorBlocked : isReserved ? gizmoColorReserved : gizmoColorActive;
                Gizmos.DrawCube(worldPos, new Vector3(tileWidth, 0.05f, tileHeight));

                Gizmos.color = new Color(gizmoColorActive.r, gizmoColorActive.g, gizmoColorActive.b, 1f);
                Gizmos.DrawWireCube(worldPos, new Vector3(tileWidth, 0.05f, tileHeight));
            }
        }
    }

    #endregion
}