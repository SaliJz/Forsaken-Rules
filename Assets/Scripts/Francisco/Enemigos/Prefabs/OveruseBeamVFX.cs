using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class OveruseBeamVFX : MonoBehaviour
{
    #region Inspector Fields

    [Header("Cone Shape")]
    [SerializeField] private float coneAngle = 60f;
    [SerializeField] private float coneRadius = 7.5f;
    [SerializeField] private int segments = 32;

    [Header("Appearance")]
    [SerializeField] private Color beamColor = new Color(0f, 0.83f, 1f, 0.35f);

    #endregion

    #region Private State

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private static Material sharedBeamMaterial;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        BuildMesh();
        ApplyMaterial();
    }

    #endregion

    #region Public API

    public void Configure(float angle, float radius, Color color)
    {
        coneAngle = angle;
        coneRadius = radius;
        beamColor = color;

        BuildMesh();
        ApplyMaterial();
    }

    #endregion

    #region Mesh Building

    private void BuildMesh()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();

        Mesh mesh = new Mesh { name = "BeamConeMesh" };

        int vertCount = segments + 2;
        var vertices = new Vector3[vertCount];
        var triangles = new int[segments * 3];
        var colors = new Color[vertCount];

        vertices[0] = Vector3.zero;
        colors[0] = beamColor;

        float halfAngle = coneAngle * 0.5f * Mathf.Deg2Rad;
        float startAngle = -halfAngle;
        float angleStep = (coneAngle * Mathf.Deg2Rad) / segments;

        for (int i = 0; i <= segments; i++)
        {
            float a = startAngle + angleStep * i;
            vertices[i + 1] = new Vector3(Mathf.Sin(a) * coneRadius, 0f, Mathf.Cos(a) * coneRadius);
            colors[i + 1] = new Color(beamColor.r, beamColor.g, beamColor.b, 0f);
        }

        for (int i = 0; i < segments; i++)
        {
            int idx = i * 3;
            triangles[idx] = 0;
            triangles[idx + 1] = i + 1;
            triangles[idx + 2] = i + 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colors;
        mesh.RecalculateNormals();

        meshFilter.sharedMesh = mesh;
    }

    private void ApplyMaterial()
    {
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();

        if (sharedBeamMaterial == null)
        {
            sharedBeamMaterial = new Material(Shader.Find("Particles/Standard Unlit"))
            {
                name = "BeamConeMat"
            };

            if (sharedBeamMaterial.shader.name == "Hidden/InternalErrorShader")
            {
                sharedBeamMaterial.shader = Shader.Find("Unlit/Color");
            }
        }

        Material instanceMat = new Material(sharedBeamMaterial);
        instanceMat.color = beamColor;
        instanceMat.renderQueue = 3000;

        meshRenderer.sharedMaterial = instanceMat;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    #endregion

    #region Editor Utility

#if UNITY_EDITOR
    [ContextMenu("Rebuild Mesh")]
    private void EditorRebuild()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        BuildMesh();
        ApplyMaterial();
    }
#endif

    #endregion
}