using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

namespace GhostInTheHall.AsepriteInjection
{
    public class AsepriteImportPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessAsset()
        {
            if (assetImporter is not AsepriteImporter asepriteImporter)
            {
                return;
            }

            var settings = AsepriteImportSettingsStorage.GetSettings(asepriteImporter);
            if (settings.enableTilemapPivotAdjustment)
            {
                asepriteImporter.pivotAlignment = SpriteAlignment.BottomCenter;
            }

            asepriteImporter.OnPostAsepriteImport -= OnPostAsepriteImport;
            asepriteImporter.OnPostAsepriteImport += OnPostAsepriteImport;
        }

        private static void OnPostAsepriteImport(AsepriteImporter.ImportEventArgs args)
        {
            AnimationPostProcessor.Run(args);
            TilemapPivotPostProcessor.Run(args);
            var generatedTilemaps = TilemapSeparationPostProcessor.Run(args);
            TilemapIslandSeparationPostProcessor.Run(args, generatedTilemaps);
            TilemapIslandAnchorOffsetPostProcessor.Run(args);
            TilemapSortingLayerPostProcessor.Run(args);
            PhysicsPostProcessor.Run(args, generatedTilemaps);
        }
    }
}
