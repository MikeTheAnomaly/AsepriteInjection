using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

namespace GhostInTheHall.AsepriteInjection
{
    internal static class AsepriteImportSettingsStorage
    {
        public static AsepriteInjectionSettings GetSettings(AsepriteImporter importer)
        {
            if (importer == null || string.IsNullOrEmpty(importer.userData))
            {
                return new AsepriteInjectionSettings();
            }

            try
            {
                return JsonUtility.FromJson<AsepriteInjectionSettings>(importer.userData) ?? new AsepriteInjectionSettings();
            }
            catch
            {
                return new AsepriteInjectionSettings();
            }
        }

        public static void SaveSettings(AsepriteImporter importer, AsepriteInjectionSettings settings)
        {
            if (importer == null)
            {
                return;
            }

            importer.userData = JsonUtility.ToJson(settings ?? new AsepriteInjectionSettings());
            EditorUtility.SetDirty(importer);
        }

        public static Material ResolveOverrideMaterial(AsepriteInjectionSettings settings)
        {
            if (settings == null || string.IsNullOrEmpty(settings.overrideMaterialGuid))
            {
                return null;
            }

            var assetPath = AssetDatabase.GUIDToAssetPath(settings.overrideMaterialGuid);
            return string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        }

        public static void SetOverrideMaterial(AsepriteInjectionSettings settings, Material material)
        {
            if (settings == null)
            {
                return;
            }

            if (material == null)
            {
                settings.overrideMaterialGuid = null;
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(material);
            settings.overrideMaterialGuid = string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.AssetPathToGUID(assetPath);
        }
    }
}
