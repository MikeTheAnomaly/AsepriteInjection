# Aseprite Injection for Unity

This repository adds editor-side extensions around Unity's Aseprite importer so imported `.aseprite` files can be turned into more usable animated sprites and tilemap prefabs with less manual cleanup.

The package is focused on two workflows:

1. Animated Aseprite imports that should produce a better Animator Controller setup.
2. Tile-based Aseprite imports that should produce cleaner tilemap layers, room chunks, sorting, pivots, and disconnected-island handling.

Everything runs during import inside the Unity Editor. There is no runtime system in this repository.

## What This Repository Is

The codebase is a small Unity editor extension split into three parts:

- `Core/`: shared serializable settings stored on each `AsepriteImporter`.
- `Editor/`: custom inspector UI, asset import hook, and post-processors.
- `Tests/`: editor tests for the tilemap island separation behavior.

The package extends Unity's `UnityEditor.U2D.Aseprite.AsepriteImporter` by:

- adding custom import settings directly in the importer inspector,
- storing those settings in `importer.userData` as JSON,
- intercepting the Aseprite import pipeline,
- running custom post-processors after Unity generates animation and tilemap assets.

## Requirements

This code assumes a Unity project that already uses Unity's Aseprite importer packages.

The editor assembly references:

- `Unity.2D.Aseprite.Editor`
- `Unity.2D.Sprite.Editor`

That means this repository is intended to live inside a Unity project where the Aseprite importer is available.

## High-Level Import Flow

When an `.aseprite` asset is imported:

1. The custom inspector exposes extra import options under `Injection Settings`.
2. Those options are serialized into the importer `userData` field.
3. During preprocess, pivot alignment can be forced to bottom-center for tilemap-oriented imports.
4. After Unity finishes the Aseprite import, the repository runs a sequence of post-processors:
   - animation processing,
   - tilemap pivot refinement,
   - tilemap layer separation,
   - disconnected island splitting,
   - anchor offset correction for generated islands,
   - sorting layer override.

The main import hook lives in `Editor/AsepriteImportPostprocessor.cs`.

## Importer Features

### 1. Custom Inspector Settings

The importer inspector is extended with an `Injection Settings` section.

Available options:

- `Enable Tilemap Pivot Adjustment`
  - Forces imported sprite pivots to bottom center for tilemap-friendly placement.
- `Enable Tilemap Island Separation`
  - Splits disconnected tile clusters into separate tilemaps after layer separation.
- `Override Tilemap Sorting Layer`
  - Forces all generated `TilemapRenderer` components onto a chosen sorting layer.
- `Tilemap Sorting Layer`
  - The sorting layer used when the override is enabled.
- `Tilemap Layers Per Chunk`
  - Groups generated z-layers into room chunk parent objects.
  - `1` means no chunk grouping.
- `Override Material`
  - Optional shared material assigned to generated tilemap renderers.
- `Room Names`
  - Comma-separated names used when chunk parents and grouped layers are created.

These settings are defined by `Core/AsepriteInjectionSettings.cs` and persisted by `Editor/AsepriteImportSettingsStorage.cs`.

### 2. Stored Per Importer

Settings are not global project settings. They are stored per imported Aseprite asset in the importer JSON data.

That gives each `.aseprite` file its own configuration without needing a separate ScriptableObject or project-wide registry.

## Animation Features

The animation post-processor improves imported Animator Controller assets.

### Direction Blend Tree Generation

If Unity generates an `AnimatorController`, the processor:

- adds a float parameter named `direction`,
- scans imported `AnimationClip` assets,
- groups clips by base name,
- creates states automatically.

If multiple clips share the same base name and end with the naming pattern below, they are grouped into a `Simple1D` blend tree:

```text
Walk_0deg
Walk_90deg
Walk_180deg
Walk_270deg
```

Those clips become a blend tree state named `Walk` that blends on the `direction` parameter.

If only one clip exists for a base name, a normal state is created instead.

### Naming Convention

The blend grouping logic looks for clip names matching:

```text
<BaseName>_<Degrees>deg
```

Examples:

- `Idle_0deg`
- `Idle_180deg`
- `Run_45deg`

Clips that do not match this pattern still become states, but they are treated as standalone animations.

### Practical Result

This is useful for directional character animation imported from Aseprite, especially when a sprite set contains multiple angle-specific clips for one motion family.

## Tilemap Features

The tilemap side is where most of the repository behavior lives.

### 1. Tilemap Pivot Adjustment

When enabled, the importer adjusts all sprite rects used by tile assets so their pivot becomes:

```text
x = 0.5
y = 0.0
```

That is a bottom-center pivot.

This helps imported tiles align in a more predictable way for grid-based scenes and is applied through Unity's sprite editor data provider.

### 2. Tilemap Layer Separation by Z Depth

If the imported Aseprite asset contains tilemaps with z-depth, the post-processor creates a separate Unity `Tilemap` GameObject for each occupied z-layer.

For every non-empty z slice:

- a new GameObject is created,
- a new `Tilemap` and `TilemapRenderer` are added,
- tile data from that layer is copied into the new tilemap,
- colors and transform matrices are preserved,
- renderer sorting order is set to the z index.

This turns one multi-layer import into multiple Unity tilemaps that are easier to sort, manage, and manipulate.

### 3. Chunk Grouping / Room Grouping

`Tilemap Layers Per Chunk` controls how many generated z-layers are grouped under a parent object.

Behavior:

- `1`: each separated layer is parented directly without chunking.
- `N > 1`: every N generated layers are grouped under a room chunk parent GameObject.

When chunk parents are created:

- the default parent name is `room chunk <index>`,
- if `Room Names` are provided, the parent becomes `room <roomName> <index>`,
- grouped layers can also be renamed to include the room name.

This is useful when z-layers conceptually map to rooms, floors, or sections of a level.

### 4. Optional Material Override

If `Override Material` is assigned in the importer settings, every generated `TilemapRenderer` created during layer separation receives that shared material.

This is useful for custom shaders, palette workflows, lighting materials, or special rendering passes.

### 5. Sorting Layer Override

If enabled, every imported or generated `TilemapRenderer` is moved to the chosen Unity sorting layer.

This runs after tilemaps are generated, so the override applies to the final output objects rather than only the original imported object.

### 6. Disconnected Island Separation

When `Enable Tilemap Island Separation` is enabled, each generated tilemap layer is analyzed for disconnected tile clusters.

Connectivity is evaluated in 4 directions:

- left
- right
- up
- down

If a layer contains more than one disconnected island:

- a new GameObject is created for each island,
- a new `Tilemap` and `TilemapRenderer` are created for each one,
- tile, transform, color, and tile flag data are copied over,
- renderer settings are copied from the source tilemap,
- the original separated tilemap is disabled.

This is especially useful when one logical z-layer contains several physically separate tile clusters that need independent transforms, fading, visibility, or downstream processing.

### 7. Anchor Offset Correction for Islands

After island splitting, the repository adjusts tile anchor and local position for island tilemaps so the anchor effectively lands at the lowest point of the island.

This compensates for the way island-local bounds affect tilemap space and keeps bottom alignment more intuitive.

### 8. Optional Integration by Type Name

The layer separation code looks for project components by type name and attaches or clones them when available.

Supported optional component names:

- `TilemapBake`
- `FadeController`

Behavior:

- new separated layers try to add `TilemapBake`,
- new room chunk parents try to add `FadeController`,
- island tilemaps clone `TilemapBake` from the source tilemap if present.

`TilemapBake` also gets best-effort default configuration through reflection:

- `bakeType = Walls` when that enum value exists,
- `maxWallTilesHeight = 4` when that field exists.

This keeps the package loosely coupled to project-specific components without adding hard assembly references.

## Current Settings Model

The import settings object currently contains:

- `enableTilemapPivotAdjustment`
- `enableTilemapIslandSeparation`
- `enableTilemapSortingLayerOverride`
- `tilemapSortingLayerName`
- `tilemapLayersPerChunk`
- `overrideMaterialGuid`
- `roomNames`

Because the settings are JSON-backed, adding new importer options is straightforward: extend the settings class, expose the field in the custom inspector, and consume it in the post-process pipeline.

## Repository Structure

```text
Core/
  AsepriteInjectionSettings.cs

Editor/
  AsepriteImporterEditor.cs
  AsepriteImportPostprocessor.cs
  AsepriteImportSettingsStorage.cs
  AsepriteInjectionFacade.cs
  PostProcess/
    AnimationPostProcessor.cs
    TilemapPivotPostProcessor.cs
    TilemapSeparationPostProcessor.cs
    TilemapIslandSeparationPostProcessor.cs
    TilemapIslandAnchorOffsetPostProcessor.cs
    TilemapSortingLayerPostProcessor.cs

Tests/
  Offset-Test.aseprite
  TilemapIslandSeparationPostProcessorTests.cs
```

## How To Use It

### Basic Workflow

1. Add this repository into a Unity project that already supports Aseprite imports.
2. Select an imported `.aseprite` asset in Unity.
3. Open the importer inspector.
4. Configure the `Injection Settings` section.
5. Reimport the asset.

### For Animated Sprites

Use clip names like:

```text
Walk_0deg
Walk_90deg
Walk_180deg
Walk_270deg
```

Then reimport. The generated Animator Controller will receive:

- a `direction` parameter,
- a state for `Walk`,
- a blend tree combining those directional clips.

### For Tilemap Imports

Enable the tilemap-related options as needed:

- pivot adjustment for bottom-aligned tiles,
- island separation for disconnected geometry,
- sorting layer override for rendering control,
- layer chunking for room grouping,
- material override for custom render pipelines.

## Test Coverage

The repository currently includes editor tests around island separation.

Covered behavior:

- a known Aseprite test asset produces exactly three generated islands,
- generated islands have the expected tile counts,
- generated islands receive the expected tile anchor offsets,
- generated islands receive the expected local Y position corrections.

The tests use `Tests/Offset-Test.aseprite` as the import fixture.

## Why This Is Useful

Unity's default Aseprite import pipeline is a good starting point, but tilemap-heavy projects often need additional structure after import. This repository automates the repetitive post-import work that would otherwise be done by hand.

In practice, it helps when you want:

- directional animation setup with less manual Animator editing,
- imported tile layers split into separate Unity tilemaps,
- disconnected geometry separated into independently manageable tilemaps,
- consistent tile pivots,
- controlled chunking and room organization,
- automatic renderer material and sorting layer setup,
- optional integration with project-specific tilemap tooling.

## Notes and Constraints

- This package is editor-only.
- It depends on Unity's Aseprite importer APIs.
- Optional integrations such as `TilemapBake` and `FadeController` only activate if those component types exist in the host project.
- Settings are stored in importer JSON, so deleting or replacing importer user data resets them.
- The current tests focus on tilemap island behavior; other processors are not yet covered by automated tests in this repository.

## Good Next Features To Add

If this repository is being used as a base for further work, the most natural extensions are:

- more animation controller configuration options,
- explicit control over blend tree thresholds,
- filtering which tilemaps are separated or chunked,
- configurable island connectivity rules,
- more tests for animation, chunking, sorting layer overrides, and material overrides,
- a packaged Unity UPM distribution layout if this is intended to be reused across projects.