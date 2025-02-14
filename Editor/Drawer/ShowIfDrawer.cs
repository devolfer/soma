using UnityEditor;
using UnityEngine;

namespace Devolfer.Soma
{
    [CustomPropertyDrawer(typeof(ShowIfAttribute))]
    public class ShowIfDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            ShowIfAttribute showIf = (ShowIfAttribute)attribute;

            bool shouldShow = GetConditionValue(property, showIf.ConditionFieldName);

            if (shouldShow || property.serializedObject.isEditingMultipleObjects)
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            ShowIfAttribute showIf = (ShowIfAttribute)attribute;

            bool shouldShow = GetConditionValue(property, showIf.ConditionFieldName);

            return shouldShow || property.serializedObject.isEditingMultipleObjects ?
                EditorGUI.GetPropertyHeight(property, label, true) :
                0f;
        }

        private static bool GetConditionValue(SerializedProperty property, string conditionFieldName)
        {
            SerializedProperty conditionProperty =
                property.serializedObject.FindProperty(
                    property.propertyPath.Replace(property.name, conditionFieldName));

            if (conditionProperty is { propertyType: SerializedPropertyType.Boolean })
            {
                return conditionProperty.hasMultipleDifferentValues || conditionProperty.boolValue;
            }

            Debug.LogWarning($"ShowIf: Could not find boolean field '{conditionFieldName}'");

            return false;
        }
    }
}