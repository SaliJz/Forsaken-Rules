using UnityEngine;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class FullScreenEffectsBase : MonoBehaviour
{
    #region Referencia

    [Header("Render Feature")]
    [Tooltip("Nombre exacto del FullScreenPassRenderer")]
    public string featureNombre = "FullScreenFeedback";

    [Tooltip("Renderer exacto donde vive la instancia del feature que quieres controlar, si está vacio usara el primero con el que encuentre una coincidencia con el nombre")]
    public ScriptableRendererData rendererDataObjetivo;

    [Tooltip("Material Original que se instanciará en le feature del PC Renderer")]
    public Material MaterialOriginal;


    private Material instanciaMaterial;
    private FullScreenPassRendererFeature feature;

    #endregion

    #region Unity Lifecycle
    protected virtual void Start()
    {
        if (!InitializeMaterialFromFeature())
        {
            enabled = false;
        }
    }

    protected virtual void OnDestroy()
    {
        if (feature != null && MaterialOriginal != null)
        {
            feature.passMaterial = MaterialOriginal;
        }

        if(instanciaMaterial != null)
        {
            Destroy(instanciaMaterial);
        }
    }

    #endregion

    #region Inicializacion de Material / Feature

    private bool InitializeMaterialFromFeature()
    {
        if (MaterialOriginal == null)
        {
            Debug.LogError($"[{GetType().Name}] No hay Material Asignado");
            return false;
        }

        if (rendererDataObjetivo != null)
        {
            feature = SearchInRenderData(rendererDataObjetivo, featureNombre);

            if(feature == null)
            {
                Debug.LogError($"[{GetType().Name}] No se encontró el FullScreenPassFeature: '{featureNombre}' en el Render Data Activo", this);
                return false;
            }
        }
        else
        {
            feature = SearchFeatureByName(featureNombre);

            if(feature == null)
            {
                Debug.LogError($"[{GetType().Name}] No se encontró el FullScreenPassFeature: '{featureNombre}' en el Render Data Activo", this);
                return false;
            }
        }

        instanciaMaterial = new Material (MaterialOriginal);
        instanciaMaterial.name = $"{MaterialOriginal.name} (Instancia)";

        feature.passMaterial = instanciaMaterial;

        return true;
    }

    private FullScreenPassRendererFeature SearchFeatureByName(string name)
    {
        var urpAsset = UniversalRenderPipeline.asset;
        if(urpAsset == null)
        {
            Debug.LogError($"[{GetType().Name}] El pipeline activo no tiene URP o no hay UniversalRenderPipelineAsset asignado");
            return null;
        }

        #if UNITY_EDITOR
        var rendererDataList = getRendererDataEditor(urpAsset);
        if(rendererDataList != null)
        {
            foreach(var data in rendererDataList)
            {
                var feature = SearchInRenderData(data, name);
                if (feature != null) return feature;
            }
        }
        #endif

        var rendererDataListField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if(rendererDataListField != null)
        {
            if (rendererDataListField.GetValue(urpAsset) is ScriptableRendererData[] dataArray)
            {
                foreach (var data in dataArray)
                {
                    var feature = SearchInRenderData(data, name);
                    if (feature != null) return feature;
                }
            }
        }

        return null;
    }

    private static FullScreenPassRendererFeature SearchInRenderData(ScriptableRendererData data, string name)
    {
        if(data == null ) return null;

        foreach(var ft in data.rendererFeatures)
        {
            if(ft is FullScreenPassRendererFeature fsfeature && ft.name == name)
            {
                return fsfeature;
            }
        }

        return null;
    }

    #if UNITY_EDITOR
    private static ScriptableRendererData[] getRendererDataEditor(UniversalRenderPipelineAsset urpAsset)
    {
        var so = new SerializedObject(urpAsset);
        var prop = so.FindProperty("m_RendererDataList");
        if (prop == null || !prop.isArray) return null;

        var result = new ScriptableRendererData[prop.arraySize];
        for (int i = 0; i < prop.arraySize; i++)
        {
            result [i] = prop.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
        }

        return result;
    }
    #endif
    #endregion

    #region  Helpers
    protected void SetFloat(string property, float value)
    {
        if (instanciaMaterial == null) return;
        instanciaMaterial.SetFloat(property, value);
    }

    protected void SetColor(string property, Color value)
    {
        if (instanciaMaterial == null) return;
        instanciaMaterial.SetColor(property, value);
    }
    #endregion
}
