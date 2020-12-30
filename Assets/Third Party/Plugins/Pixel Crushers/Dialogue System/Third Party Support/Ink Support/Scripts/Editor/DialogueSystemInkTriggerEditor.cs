using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace PixelCrushers.DialogueSystem.InkSupport
{

    [CustomEditor(typeof(DialogueSystemInkTrigger), true)]
    public class DialogueSystemInkTriggerEditor : DialogueSystemTriggerEditor
    {
        private List<InkEntrypoint> entrypoints;
        private string[] entrypointStrings;

        public override void OnEnable()
        {
            base.OnEnable();
            entrypoints = InkEditorUtility.GetAllEntrypoints();
            entrypointStrings = InkEditorUtility.EntrypointsToStrings(entrypoints);
        }

        protected void SetEntrypoint(SerializedProperty conversationProperty, SerializedProperty startAtKnotProperty, int index)
        {
            if (!(0 <= index && index < entrypoints.Count)) return;
            var entrypoint = entrypoints[index];
            conversationProperty.stringValue = entrypoint.story;
            if (string.IsNullOrEmpty(entrypoint.knot))
            {
                startAtKnotProperty.stringValue = string.Empty;
            }
            else
            {
                if (string.IsNullOrEmpty(entrypoint.stitch))
                {
                    startAtKnotProperty.stringValue = entrypoint.knot;
                }
                else
                {
                    startAtKnotProperty.stringValue = (entrypoints[index].knot + "." + entrypoints[index].stitch);
                }
            }
        }

        protected override void DrawConversationAction()
        {
            base.DrawConversationAction();
            if (foldouts.conversationFoldout)
            {
                EditorGUILayout.LabelField("Ink-Specific", EditorStyles.boldLabel);
                var conversationProperty = serializedObject.FindProperty("conversation");
                var startConversationAtKnotProperty = serializedObject.FindProperty("startConversationAtKnot");
                EditorGUILayout.PropertyField(startConversationAtKnotProperty, new GUIContent("Start At Knot/Stitch"), true);
                var index = InkEditorUtility.GetEntrypointIndex(conversationProperty.stringValue, startConversationAtKnotProperty.stringValue, entrypoints);
                EditorGUI.BeginChangeCheck();
                index = EditorGUILayout.Popup("Entrypoint Picker", index, entrypointStrings);
                if (EditorGUI.EndChangeCheck())
                {
                    SetEntrypoint(conversationProperty, startConversationAtKnotProperty, index);
                }
            }
        }

        protected override void DrawBarkAction()
        {
            base.DrawBarkAction();
            if (foldouts.barkFoldout)
            {
                EditorGUILayout.LabelField("Ink-Specific", EditorStyles.boldLabel);
                var barkConversationProperty = serializedObject.FindProperty("barkConversation");
                var barkKnotProperty = serializedObject.FindProperty("barkKnot");
                EditorGUILayout.PropertyField(barkKnotProperty, true);
                var index = InkEditorUtility.GetEntrypointIndex(barkConversationProperty.stringValue, barkKnotProperty.stringValue, entrypoints);
                EditorGUI.BeginChangeCheck();
                index = EditorGUILayout.Popup("Entrypoint Picker", index, entrypointStrings);
                if (EditorGUI.EndChangeCheck())
                {
                    SetEntrypoint(barkConversationProperty, barkKnotProperty, index);
                }
            }
        }


    }
}