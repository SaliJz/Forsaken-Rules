#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class StepAnimationCurves : EditorWindow
{
    [MenuItem("Tools/Step Selected Animation Curves")]
    static void StepCurves()
    {
        foreach (var obj in Selection.objects)
        {
            if (obj is AnimationClip clip)
            {
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);

                    for (int i = 0; i < curve.keys.Length; i++)
                    {
                        AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                        AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                    }

                    AnimationUtility.SetEditorCurve(clip, binding, curve);
                }

                EditorUtility.SetDirty(clip);
            }
        }

        AssetDatabase.SaveAssets();
    }
}
#endif