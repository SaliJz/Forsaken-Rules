using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct SceneTransition
{
    public KeyCode inputKey;
    public string targetSceneName;
}

[CreateAssetMenu(fileName = "SceneShortcutData", menuName = "Config/Scene Shortcuts")]
public class SceneShortcutData : ScriptableObject
{
    [Header("Configuración Global de Atajos")]
    public List<SceneTransition> sceneTransitions;
}