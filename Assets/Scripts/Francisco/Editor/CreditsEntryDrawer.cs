using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(CreditsPanel.CreditsEntry))]
public class CreditsEntryDrawer : PropertyDrawer
{
    private const int LineHeight = 18;
    private const int Spacing = 2;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty entryTypeProp = property.FindPropertyRelative("entryType");
        CreditsPanel.EntryType entryType = (CreditsPanel.EntryType)entryTypeProp.enumValueIndex;

        Rect singleLine = new Rect(position.x, position.y, position.width, LineHeight);

        EditorGUI.PropertyField(singleLine, entryTypeProp);
        singleLine.y += LineHeight + Spacing;

        EditorGUI.PropertyField(singleLine, property.FindPropertyRelative("alignment"));
        singleLine.y += LineHeight + Spacing;

        switch (entryType)
        {
            case CreditsPanel.EntryType.Title:
            case CreditsPanel.EntryType.Subtitle:
            case CreditsPanel.EntryType.Role:
            case CreditsPanel.EntryType.Name:
                EditorGUI.PropertyField(singleLine, property.FindPropertyRelative("textContent"));
                singleLine.y += LineHeight + Spacing;
                break;

            case CreditsPanel.EntryType.RoleWithName:
                EditorGUI.PropertyField(singleLine, property.FindPropertyRelative("textContent"), new GUIContent("Role Text"));
                singleLine.y += LineHeight + Spacing;
                EditorGUI.PropertyField(singleLine, property.FindPropertyRelative("secondaryText"), new GUIContent("Name Text"));
                singleLine.y += LineHeight + Spacing;
                break;

            case CreditsPanel.EntryType.ImageWithText:
                EditorGUI.PropertyField(singleLine, property.FindPropertyRelative("image"));
                singleLine.y += LineHeight + Spacing;
                EditorGUI.PropertyField(singleLine, property.FindPropertyRelative("textContent"));
                singleLine.y += LineHeight + Spacing;
                break;

            case CreditsPanel.EntryType.Spacer:
                break;
        }

        EditorGUI.PropertyField(singleLine, property.FindPropertyRelative("customSpacing"));
        singleLine.y += LineHeight + Spacing;

        if (entryType != CreditsPanel.EntryType.Spacer)
        {
            SerializedProperty useCustomColorProp = property.FindPropertyRelative("useCustomColor");
            EditorGUI.PropertyField(singleLine, useCustomColorProp);
            singleLine.y += LineHeight + Spacing;

            if (useCustomColorProp.boolValue)
            {
                EditorGUI.PropertyField(singleLine, property.FindPropertyRelative("customColor"));
                singleLine.y += LineHeight + Spacing;
            }

            EditorGUI.PropertyField(singleLine, property.FindPropertyRelative("fontSize"));
            singleLine.y += LineHeight + Spacing;
        }

        EditorGUI.PropertyField(singleLine, property.FindPropertyRelative("continueOnSameLine"), new GUIContent("Continue on Same Line"));
        singleLine.y += LineHeight + Spacing;

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty entryTypeProp = property.FindPropertyRelative("entryType");
        CreditsPanel.EntryType entryType = (CreditsPanel.EntryType)entryTypeProp.enumValueIndex;

        SerializedProperty useCustomColorProp = property.FindPropertyRelative("useCustomColor");
        bool useCustomColor = useCustomColorProp.boolValue;

        int lines = 4; 

        switch (entryType)
        {
            case CreditsPanel.EntryType.Title:
            case CreditsPanel.EntryType.Subtitle:
            case CreditsPanel.EntryType.Role:
            case CreditsPanel.EntryType.Name:
                lines += 1;
                break;
            case CreditsPanel.EntryType.RoleWithName:
                lines += 2;
                break;
            case CreditsPanel.EntryType.ImageWithText:
                lines += 2;
                break;
            case CreditsPanel.EntryType.Spacer:
                break;
        }

        if (entryType != CreditsPanel.EntryType.Spacer)
        {
            lines += 1; 
            if (useCustomColor)
            {
                lines += 1;
            }
            lines += 1; 
        }

        lines += 1;

        return lines * (LineHeight + Spacing);
    }
}