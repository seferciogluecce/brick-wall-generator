# Brick Wall Generator

Brick Wall Generator is a small Unity Editor utility that creates a one-shot brick wall from a scene object's rendered bounds.

The tool creates ordinary Unity cube GameObjects in the scene. Generated walls are independent after creation and are not linked back to the source object.

## Installation

Copy `Editor/BrickWallGeneratorWindow.cs` into an `Editor` folder in a Unity project.

Open the tool from:

`Tools > Brick Wall Generator`

## Requirements

- Unity Editor project
- Editor-time use only
- A scene Bounds Object with:
  - direct `MeshFilter`
  - valid mesh
  - direct enabled `MeshRenderer`
  - positive visible dimensions
  - positive transform scale on the object and its ancestors

Child renderers are not used for bounds. `SkinnedMeshRenderer`, `SpriteRenderer`, collider-only objects, and transform-only objects are outside the supported bounds-source scope.

## Basic Usage

1. Select or assign a Bounds Object in the scene.
2. Press `Use Selected` if you want to assign the current scene selection.
3. Configure rows, columns, gaps, staggered rows, material, colliders, and source hiding.
4. Press `Generate Brick Wall`.

The Bounds Object defines the generated wall's position, orientation, width, height, and depth. The source mesh shape is not reproduced; its rectangular rendered bounds define the wall volume.

## Controls

- `Columns`: horizontal base division, range 1-64.
- `Rows`: vertical division, range 1-64.
- `Horizontal Gap`: Unity-unit space between columns.
- `Vertical Gap`: Unity-unit space between rows.
- `Shift Alternate Rows`: offsets every other row in positive local X.
- `Shift Amount`: 0%-100% of the horizontal brick pitch.
- `Fill Shifted Ends`: adds partial edge bricks so shifted rows stay inside the same wall bounds.
- `Wall Name`: generated parent name. Empty names fall back to `Brick Wall`.
- `Material (Optional)`: optional material override for all generated bricks.
- `Add Colliders`: when enabled, each brick keeps its generated `BoxCollider`.
- `Hide Bounds Object`: when enabled, the Bounds Object is deactivated after successful generation.

High row and column values create many individual GameObjects.

## Generated Output

Each successful generation creates a new parent GameObject next to the Bounds Object in the hierarchy. Bricks are children of that parent.

The parent has only a `Transform`. Bricks have:

- `Transform`
- `MeshFilter`
- `MeshRenderer`
- optional `BoxCollider`

The tool does not add runtime components or maintain a live link. Existing walls are never replaced, updated, regenerated, or deleted automatically.

## Materials

Material resolution order:

1. `Material (Optional)` override
2. first shared material on the Bounds Object's direct `MeshRenderer`
3. Unity default material behavior

Multi-material Bounds Objects use only the first shared material as fallback.

## Undo

Generation is registered as one Unity Undo operation. Undo removes the generated wall and restores the Bounds Object active state when `Hide Bounds Object` changed it.

## Prefab Mode And Scenes

The tool supports normal scenes, multi-scene editing, scene prefab instances, and Prefab Mode. Project-window prefab assets are not valid Bounds Objects.

Generated output is created in the same editable scene or Prefab Stage context as the Bounds Object.

## Limitations

- Runtime generation is not supported.
- Generated walls are not live-updated from the Bounds Object.
- Negative or zero scale on the Bounds Object or its ancestors is unsupported.
- No prefab input, custom brick mesh, mesh combining, material variation, layer/tag/static copying, or preset system is included.

## Tested Unity Version

Validated in Unity `6000.3.15f1`.
