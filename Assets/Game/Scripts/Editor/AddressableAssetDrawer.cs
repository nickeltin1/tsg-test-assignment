using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Scripts.Editor
{
    /// <summary>
    /// Using just simple attribute for now with prop name to inline dropdowns
    /// Of course can be moved to more complex drawer with reflections and <see cref="ScriptAttributeUtility"/> 
    /// </summary>
    [CustomPropertyDrawer(typeof(InlinePropertyAttribute), true)]
    public class AddressableAssetDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            var childs = GetVisibleChilds(property);
            using (new EditorGUI.PropertyScope(rect, label, property))
            {
                var y = rect.y;
                foreach (var child in childs)
                {
                    var height = EditorGUI.GetPropertyHeight(child, true);
                    var line = new Rect(rect.x, y, rect.width, height);
                    var childLabel =
                        EditorGUIUtility.TrTextContent(property.displayName + "/" + child.displayName, child.tooltip);
                    EditorGUI.PropertyField(line, child, childLabel, true);
                    y += height;// + EditorGUIUtility.standardVerticalSpacing;
                }
            }
        }


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = EditorGUI.GetPropertyHeight(property, label, true);
            if (property.isExpanded)
            {
                height -= EditorGUIUtility.singleLineHeight;
            }
            return height;
        }
        
        public static IEnumerable<SerializedProperty> GetVisibleChilds(SerializedProperty property)
        {
            var currentProperty = property.Copy();
            var nextSiblingProperty = property.Copy();
            nextSiblingProperty.NextVisible(false);

            if (currentProperty.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(currentProperty, nextSiblingProperty))
                        break;

                    yield return currentProperty;
                }
                while (currentProperty.NextVisible(false));
            }
        }
    }
}