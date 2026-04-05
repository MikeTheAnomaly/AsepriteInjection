using System;

namespace GhostInTheHall.AsepriteInjection
{
    [Serializable]
    public class AsepriteInjectionSettings
    {
        public bool enableTilemapPivotAdjustment = true;
        public bool enableTilemapIslandSeparation = false;
        public bool enableTilemapSortingLayerOverride = false;
        public string tilemapSortingLayerName = "Default";
        public int tilemapLayersPerChunk = 1;
        public string overrideMaterialGuid;
        public string[] roomNames = Array.Empty<string>();
    }
}
