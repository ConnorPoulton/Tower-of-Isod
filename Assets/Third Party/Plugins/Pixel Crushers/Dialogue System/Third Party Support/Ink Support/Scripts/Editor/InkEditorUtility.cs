using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace PixelCrushers.DialogueSystem.InkSupport
{

    public static class InkEditorUtility
    {
        public static List<InkEntrypoint> GetAllEntrypoints()
        {
            var filesProcessed = new List<string>();
            var entrypoints = new List<InkEntrypoint>();
            var dialogueSystemInkIntegration = GameObject.FindObjectOfType<DialogueSystemInkIntegration>();
            if (dialogueSystemInkIntegration != null && dialogueSystemInkIntegration.inkJSONAssets.Count > 0)
            {
                dialogueSystemInkIntegration.inkJSONAssets.ForEach(asset => AddInkJsonAssetToEntrypoints(asset, entrypoints, filesProcessed));
            }
            return entrypoints;
        }

        public static string[] EntrypointsToStrings(List<InkEntrypoint> entrypoints)
        {
            var entrypointStrings = new string[entrypoints.Count];
            for (int i = 0; i < entrypoints.Count; i++)
            {
                entrypointStrings[i] = entrypoints[i].ToPopupString();
            }
            return entrypointStrings;
        }

        private static void AddInkJsonAssetToEntrypoints(TextAsset asset, List<InkEntrypoint> entrypoints, List<string> filesProcessed)
        {
            try
            {
                if (asset == null) return;
                entrypoints.Add(new InkEntrypoint(asset.name, string.Empty, string.Empty));
                var assetPath = AssetDatabase.GetAssetPath(asset).Substring("Assets".Length);
                var inkFullPath = Application.dataPath + assetPath.Replace(".json", ".ink");
                var rootPath = Path.GetDirectoryName(inkFullPath);
                var rootFilename = Path.GetFileName(inkFullPath);
                ProcessFile(asset, entrypoints, rootPath, rootFilename, filesProcessed);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void ProcessFile(TextAsset asset, List<InkEntrypoint> entrypoints, string rootPath, string inkFilePath, List<string> filesProcessed)
        {
            var inkFullPath = rootPath + "/" + inkFilePath;
            //Debug.Log("ProcessFile " + inkFullPath);
            if (filesProcessed.Contains(inkFullPath)) return;
            filesProcessed.Add(inkFullPath);
            var lines = System.IO.File.ReadAllLines(inkFullPath);
            var knot = string.Empty;
            foreach (var line in lines)
            {
                if (line.StartsWith("=="))
                {
                    // Knot:
                    var s = line;
                    while (s.Length > 0 && (s[0] == '=' || s[0] == ' '))
                    {
                        s = s.Substring(1);
                    }
                    while (s.Length > 0 && (s[s.Length - 1] == '=' || s[s.Length - 1] == ' '))
                    {
                        s = s.Substring(0, s.Length - 1);
                    }
                    if (s.Length > 0 && !s.StartsWith("function", System.StringComparison.OrdinalIgnoreCase))
                    {
                        knot = s;
                        entrypoints.Add(new InkEntrypoint(asset.name, knot, string.Empty));
                    }
                }
                else if (line.StartsWith("= "))
                {
                    // Stitch:
                    var stitch = line.Substring(2).Trim();
                    entrypoints.Add(new InkEntrypoint(asset.name, knot, stitch));
                }
                else if (line.StartsWith("INCLUDE "))
                {
                    // Include:
                    var includedFilename = line.Substring("INCLUDE ".Length).Trim();
                    ProcessFile(asset, entrypoints, rootPath, includedFilename, filesProcessed);
                }
            }
        }

        public static int GetEntrypointIndex(string conversation, string path, List<InkEntrypoint> entrypoints)
        {
            var knot = string.Empty;
            var stitch = string.Empty;
            if (!string.IsNullOrEmpty(path))
            {
                var parts = path.Split('.');
                knot = parts[0];
                if (parts.Length > 1) stitch = parts[1];
            }
            for (int i = 0; i < entrypoints.Count; i++)
            {
                var entrypoint = entrypoints[i];
                if (string.Equals(entrypoint.story, conversation) &&
                    string.Equals(entrypoint.knot, knot) &&
                    string.Equals(entrypoint.stitch, stitch))
                    return i;
            }
            return -1;
        }

    }
}