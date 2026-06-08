// Editor/SerializeReferenceDrawer.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using PolicyClient;

[CustomPropertyDrawer(typeof(BaseBehavior), true)]
public class BaseBehaviorDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        
        // Draw each child field
        SerializedProperty child = property.Copy();
        SerializedProperty end = property.GetEndProperty();
        bool enterChildren = true;

        while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
        {
            position.height = EditorGUI.GetPropertyHeight(child, true);
            EditorGUI.PropertyField(position, child, true);
            position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
            enterChildren = false;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float total = 0f;
        SerializedProperty child = property.Copy();
        SerializedProperty end = property.GetEndProperty();
        bool enterChildren = true;

        while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
        {
            total += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
            enterChildren = false;
        }

        return total;
    }
}
#endif