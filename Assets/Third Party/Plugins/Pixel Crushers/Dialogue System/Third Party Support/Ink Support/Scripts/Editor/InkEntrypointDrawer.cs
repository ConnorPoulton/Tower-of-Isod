using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PixelCrushers.DialogueSystem.InkSupport
{
    [CustomPropertyDrawer(typeof(InkEntrypointAttribute))]
    public class InkEntrypointDrawer : PropertyDrawer
    {
        private List<InkEntrypoint> entrypoints = null;
        private string[] entrypointStrings = null;

        private void PreparePopup(bool forceRefresh)
        {
            if (entrypoints == null || entrypointStrings == null || forceRefresh)
            {
                entrypoints = InkEditorUtility.GetAllEntrypoints();
                entrypointStrings = InkEditorUtility.EntrypointsToStrings(entrypoints);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            //return base.GetPropertyHeight(property, label);
            return 2 * EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            //base.OnGUI(position, property, label);
            EditorGUI.BeginProperty(position, GUIContent.none, property);
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            var rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.stringValue = EditorGUI.TextField(rect, property.stringValue);
            var s = property.stringValue;
            string story, knot_stitch;
            var i = s.IndexOf('/');
            if (i == -1)
            {
                story = s;
                knot_stitch = string.Empty;
            }
            else
            {
                story = s.Substring(0, i);
                knot_stitch = s.Substring(i + 1);
            }
            PreparePopup(entrypoints == null || entrypoints.Count == 0);
            var index = InkEditorUtility.GetEntrypointIndex(story, knot_stitch, entrypoints);
            rect.y += EditorGUIUtility.singleLineHeight;
            EditorGUI.BeginChangeCheck();
            index = EditorGUI.Popup(rect, string.Empty, index, entrypointStrings);
            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = EntrypointToString(index, entrypoints, s);
            }
            EditorGUI.EndProperty();
        }

        private string EntrypointToString(int index, List<InkEntrypoint> entrypoints, string defaultValue)
        {
            if (!(0 <= index && index < entrypoints.Count)) return defaultValue;
            var entrypoint = entrypoints[index];
            var result = entrypoint.story;
            if (!string.IsNullOrEmpty(entrypoint.knot))
            {
                if (string.IsNullOrEmpty(entrypoint.stitch))
                {
                    result += "/" + entrypoint.knot;
                }
                else
                {
                    result += "/" + entrypoints[index].knot + "." + entrypoints[index].stitch;
                }
            }
            return result;
        }
    }
}
