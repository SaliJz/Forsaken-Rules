using UnityEditor;
using UnityEngine;

public class MeshSaver : MonoBehaviour
{
    [MenuItem("Tools/Save selected mesh")]
    public static void SaveMeshOfSelected()
    {
        GameObject selection = Selection.activeGameObject;

        if (selection == null)
        {
            Debug.LogError("Selecciona un objeto en la jerarquia primero.");
            return;
        }

        MeshFilter mf = selection.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogError("El objeto no tiene una malla valida para guardar.");
            return;
        }

        string path = "Assets/" + selection.name + "_Mesh.asset";

        Mesh meshToSave = Instantiate(mf.sharedMesh);

        AssetDatabase.CreateAsset(meshToSave, path);
        AssetDatabase.SaveAssets();

        mf.sharedMesh = meshToSave;

        Debug.Log("Malla guardada con exito en: " + path);
    }
}
