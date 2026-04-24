using System;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

namespace GhostInTheHall.AsepriteInjection
{
    public static class AsepriteImporterInspectorExtension
    {
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            Editor.finishedDefaultHeaderGUI += OnPostHeaderGUI;
        }

        private static void OnPostHeaderGUI(Editor editor)
        {
            if (editor.target is AsepriteImporter importer)
            {
                DrawInjectionSettings(importer);
            }
        }

        private static void DrawInjectionSettings(AsepriteImporter importer)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Injection Settings", EditorStyles.boldLabel);

            var settings = AsepriteImportSettingsStorage.GetSettings(importer);
            var overrideMaterial = AsepriteImportSettingsStorage.ResolveOverrideMaterial(settings);

            EditorGUI.BeginChangeCheck();

            settings.enableTilemapPivotAdjustment = EditorGUILayout.Toggle(
                new GUIContent(
                    "Enable Tilemap Pivot Adjustment",
                    "When enabled, automatically sets sprite pivots to bottom center for tilemap usage"),
                settings.enableTilemapPivotAdjustment);

            settings.physicsImportTarget = (PhysicsImportTarget)EditorGUILayout.EnumPopup(
                new GUIContent(
                    "Physics Target",
                    "Select which physics representation should be generated for imported tilemaps."),
                settings.physicsImportTarget);

            settings.enableTilemapIslandSeparation = EditorGUILayout.Toggle(
                new GUIContent(
                    "Enable Tilemap Island Separation",
                    "After tilemap layer separation, split disconnected tile islands into separate tilemaps and disable the original layer tilemap."),
                settings.enableTilemapIslandSeparation);

            settings.enableTilemapSortingLayerOverride = EditorGUILayout.Toggle(
                new GUIContent(
                    "Override Tilemap Sorting Layer",
                    "When enabled, all imported TilemapRenderer components are forced to the selected sorting layer."),
                settings.enableTilemapSortingLayerOverride);

            if (settings.enableTilemapSortingLayerOverride)
            {
                var sortingLayers = SortingLayer.layers;
                if (sortingLayers.Length > 0)
                {
                    var layerNames = sortingLayers.Select(layer => layer.name).ToArray();
                    var selectedIndex = Array.IndexOf(layerNames, settings.tilemapSortingLayerName);
                    if (selectedIndex < 0)
                    {
                        selectedIndex = 0;
                    }

                    selectedIndex = EditorGUILayout.Popup(
                        new GUIContent("Tilemap Sorting Layer", "Sorting layer to apply to all imported tilemaps."),
                        selectedIndex,
                        layerNames);

                    settings.tilemapSortingLayerName = layerNames[selectedIndex];
                }
                else
                {
                    settings.tilemapSortingLayerName = EditorGUILayout.TextField(
                        new GUIContent("Tilemap Sorting Layer", "Sorting layer name to apply to all imported tilemaps."),
                        settings.tilemapSortingLayerName);
                }
            }

            settings.tilemapLayersPerChunk = EditorGUILayout.IntField(
                new GUIContent(
                    "Tilemap Layers Per Chunk",
                    "Group every N generated tilemap layers under a parent GameObject. Use 1 for no grouping."),
                settings.tilemapLayersPerChunk);

            overrideMaterial = (Material)EditorGUILayout.ObjectField(
                new GUIContent("Override Material", "Optional material to assign to generated TilemapRenderer components."),
                overrideMaterial,
                typeof(Material),
                allowSceneObjects: false);
            AsepriteImportSettingsStorage.SetOverrideMaterial(settings, overrideMaterial);

            var roomNamesText = string.Join(", ", settings.roomNames ?? Array.Empty<string>());
            roomNamesText = EditorGUILayout.TextField(
                new GUIContent("Room Names", "Comma-separated list of room names for organizing tilemaps, bottom-up order."),
                roomNamesText);
            settings.roomNames = roomNamesText
                .Replace(" ", string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            AsepriteImportSettingsStorage.SaveSettings(importer, settings);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(importer));
        }
    }
}
