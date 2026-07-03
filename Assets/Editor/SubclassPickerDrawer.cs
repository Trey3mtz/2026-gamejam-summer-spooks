#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SpookyGame.Editor 
{
    [CustomPropertyDrawer(typeof(SubclassPickerAttribute))]
    public class SubclassPickerDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Rect foldoutRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);
            Rect buttonRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y, position.width - EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);

            // 1. Draw the Foldout instead of a static PrefixLabel
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            string currentTypeName = property.managedReferenceValue?.GetType().Name ?? "None";
            if (EditorGUI.DropdownButton(buttonRect, new GUIContent(currentTypeName), FocusType.Keyboard))
            {
                ShowDropdown(property);
            }

            // 2. Safely draw the child fields if the foldout is expanded
            if (property.isExpanded && property.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                
                SerializedProperty iterator = property.Copy();
                SerializedProperty endProperty = iterator.GetEndProperty();
                
                bool enterChildren = true;
                float currentY = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                while (iterator.NextVisible(enterChildren))
                {
                    if (SerializedProperty.EqualContents(iterator, endProperty))
                        break;

                    enterChildren = false; // Only iterate immediate children

                    float childHeight = EditorGUI.GetPropertyHeight(iterator, true);
                    Rect childRect = new Rect(position.x, currentY, position.width, childHeight);
                    
                    EditorGUI.PropertyField(childRect, iterator, true);
                    
                    currentY += childHeight + EditorGUIUtility.standardVerticalSpacing;
                }
                
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            
            if (property.isExpanded && property.managedReferenceValue != null)
            {
                SerializedProperty iterator = property.Copy();
                SerializedProperty endProperty = iterator.GetEndProperty();
                
                bool enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    if (SerializedProperty.EqualContents(iterator, endProperty))
                        break;
                        
                    enterChildren = false;
                    height += EditorGUI.GetPropertyHeight(iterator, true) + EditorGUIUtility.standardVerticalSpacing;
                }
            }
            return height;
        }

        private void ShowDropdown(SerializedProperty property)
        {
            GenericMenu menu = new GenericMenu();
            
            menu.AddItem(new GUIContent("None"), property.managedReferenceValue == null, () => 
            {
                property.managedReferenceValue = null;
                property.serializedObject.ApplyModifiedProperties();
            });

            Type targetType = fieldInfo.FieldType;
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
            {
                targetType = targetType.GetGenericArguments()[0];
            }
            else if (targetType.IsArray)
            {
                targetType = targetType.GetElementType();
            }

            var types = TypeCache.GetTypesDerivedFrom(targetType)
                .Where(t => !t.IsAbstract && !t.IsInterface && !t.IsGenericType);

            foreach (Type type in types)
            {
                menu.AddItem(new GUIContent(type.Name), property.managedReferenceValue?.GetType() == type, () => 
                {
                    property.managedReferenceValue = Activator.CreateInstance(type);
                    property.serializedObject.ApplyModifiedProperties();
                });
            }

            menu.ShowAsContext();
        }
    }
}
#endif