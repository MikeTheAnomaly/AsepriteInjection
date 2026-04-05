using UnityEditor.U2D.Aseprite;
using UnityEngine;

namespace GhostInTheHall.AsepriteInjection
{
    // Compatibility facade while call sites migrate to AsepriteImportSettingsStorage.
    public static class AsepriteInjection
    {
        public static AsepriteInjectionSettings GetSettings(AsepriteImporter importer)
        {
            return AsepriteImportSettingsStorage.GetSettings(importer);
        }

        public static void SaveSettings(AsepriteImporter importer, AsepriteInjectionSettings settings)
        {
            AsepriteImportSettingsStorage.SaveSettings(importer, settings);
        }

        public static Material ResolveOverrideMaterial(AsepriteInjectionSettings settings)
        {
            return AsepriteImportSettingsStorage.ResolveOverrideMaterial(settings);
        }

        public static void SetOverrideMaterial(AsepriteInjectionSettings settings, Material material)
        {
            AsepriteImportSettingsStorage.SetOverrideMaterial(settings, material);
        }
    }
}
