# VoxelGame — Project Context

## Keeping this file up to date

**Agents must update this file** when they discover non-obvious facts about the engine, APIs, coordinate systems, or asset pipeline. Add findings under the relevant section (or create a new one). Remove entries that are no longer true. The goal is that a future agent starting cold can read this file and avoid re-discovering the same pitfalls.

## Engine

This project uses a **custom-built Godot 4.6** with the
[Voxel Tools module](https://voxel-tools.readthedocs.io/en/latest/) compiled in as a C++ module.
There is no `addons/` folder — the module generates C# classes at build time.

`NuGet.config` must stay. It points to the local nupkg feed at
`C:\Users\rurik\Godot\Engines\Custom\godot-4.6.2-voxel-steam\godot\bin\GodotSharp\Tools\nupkgs`
and is required for `dotnet restore` on a fresh checkout.

## VoxelVoxSceneImporter — loading .vox files

The Voxel Tools module registers `VoxelVoxSceneImporter`, which auto-imports `.vox` files as `PackedScene`. Load and instantiate at runtime:

```csharp
var scene    = ResourceLoader.Load<PackedScene>("res://src/Assets/part.vox");
var instance = scene.Instantiate<Node3D>();
```

The imported scene contains a root `Node3D` with a `MeshInstance3D` child. The mesh origin is at **(0, 0, 0)** — no padding, no centering.

### Importer axis mapping (non-obvious — confirmed by binary AABB inspection)

The importer does **not** use the standard MV→Godot axis mapping. Instead:

| MV axis | Importer Godot axis |
|---------|-------------------|
| X (right) | **Z** |
| Y (depth) | **X** |
| Z (up)    | Y     |

To restore the expected orientation (MV.X→GodotX, MV.Y→GodotZ, MV.Z→GodotY), apply this transform to each instantiated mesh before placing it under its pivot:

```csharp
instance.Scale           = new Vector3(VoxelScale, VoxelScale, -VoxelScale);
instance.RotationDegrees = new Vector3(0f, -90f, 0f);
// Centering: mesh spans (0,0,0) to (SizeY, SizeZ, SizeX) in Godot space after import.
// After applying the transform above the effective extents are (SizeX, SizeZ, SizeY),
// so centre at:
instance.Position = new Vector3(
    -(sx / 2f) * VoxelScale,   // MV SizeX
    -(sz / 2f) * VoxelScale,   // MV SizeZ (up)
    -(sy / 2f) * VoxelScale);  // MV SizeY (depth)
```

The negative Z scale flips winding on some faces; if materials appear inside-out, set `CullMode = No Culling` on the material.

## MagicaVoxel coordinate system

MagicaVoxel is **Z-up**. Standard remapping to Godot (Y-up):

| MV axis   | Godot axis |
|-----------|-----------|
| X (right) | X         |
| Y (depth) | Z         |
| Z (up)    | Y         |

Scene-graph translations (from MV nTRN chunks) convert to Godot pivot position as:
```csharp
new Vector3(mvT.X * VoxelScale, mvT.Z * VoxelScale, mvT.Y * VoxelScale)
```

## raider.vox — split files

The original `raider.vox` was split into per-part files in `src/Assets/`.
The individual files contain no scene-graph data, so the original MV world-space
translations must be applied manually:

| File | MV size (X×Y×Z) | MV scene translation (x, y, z) |
|------|----------------|-------------------------------|
| `raider_head.vox`   | 40×36×32 | (0, −5, 99)  |
| `raider_neck.vox`   | 16×16×8  | (0, −2, 79)  |
| `raider_torso.vox`  | 56×32×32 | (0, −3, 61)  |
| `raider_hand_r.vox` | 12×12×16 | (30, −4, 41) |
| `raider_hand_l.vox` | 12×12×16 | (−30, −4, 41)|
| `raider_legs.vox`   | 40×32×28 | (0, −3, 31)  |
| `raider_foot_r.vox` | 16×32×8  | (13, −7, 4)  |
| `raider_foot_l.vox` | 16×32×8  | (−12, −7, 4) |

These are assembled at runtime by `src/Entities/RaiderBody.cs`.
