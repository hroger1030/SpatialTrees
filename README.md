# Spatial Trees

This library lets you quickly build and search a quadtree (2D) or octree (3D) spatial index. Both are optimized for fast
inserts and updates of objects, making them well suited to real-time game and physics applications. The octree mirrors the
quadtree's design one dimension up — same API shape, same splitting behavior, just with a Z axis added.

## Table of contents

- [Spatial Trees](#spatial-trees)
  - [Table of contents](#table-of-contents)
  - [Requirements](#requirements)
  - [Project layout](#project-layout)
  - [Building and testing](#building-and-testing)
  - [Thread safety](#thread-safety)
  - [Creating a quadtree](#creating-a-quadtree)
  - [Items](#items)
  - [Creating an octree](#creating-an-octree)
  - [Volume items](#volume-items)
  - [License](#license)

The project references another library of mine, `Geometry`, which provides basic geometric primitives (points, rectangles,
circles, etc.) and is available on this same GitHub account. Clone the `Geometry` repository as a sibling of this one — e.g.
`../Geometry` relative to this repo's root — and the solution will build without any further changes.

## Requirements

- .NET 10 SDK
- The `Geometry` library, cloned next to this repository (see above)

## Project layout

```
SpatialTrees/
├── SpatialTrees.sln
├── SpatialTrees/              # library project
│   ├── SpatialTrees.csproj
│   ├── Quadtree/
│   │   ├── IMapObject.cs
│   │   ├── Quadtree.cs
│   │   ├── QuadtreeNode.cs
│   │   └── eQuadrant.cs
│   └── Octree/
│       ├── IMapObject3d.cs
│       ├── Octree.cs
│       ├── OctreeNode.cs
│       └── eOctant.cs
└── SpatialTreeTests/          # NUnit test project
    ├── SpatialTreeTests.csproj
    ├── Quadtree/
    │   ├── SpatialTreesTests.cs
    │   ├── TestItem.cs
    │   └── ...                # AddItem/MoveItem/RemoveItem/Clear/Resize/etc. test fixtures
    └── Octree/
        ├── OctreeCollisionTests.cs
        ├── TestVolumeItem.cs
        └── ...                # same fixture layout as Quadtree, one dimension up
```

## Building and testing

```
dotnet build SpatialTrees.sln
dotnet test SpatialTrees.sln
```

Unit tests are written with NUnit and live in the `SpatialTreeTests` project.

## Thread safety

`Quadtree` and `Octree` are **not** thread safe. They are intended for single-threaded use; if you access one from more than
one thread, you must serialize the calls yourself. Thread-safe variants of both structures are planned for a future release.

## Creating a quadtree

To initialize a new quadtree, use the following code:

```csharp
var boundingBox = new Rectangle(0, 0, 1000, 1000);
var maxDepth = 5;
var maxObjects = 100;
var tree = new Quadtree(boundingBox, maxDepth, maxObjects);
```

- `boundingBox` is the outer boundary of the search space.
- `maxDepth` is the number of levels of "resolution". The more levels you add, the more finely the space is subdivided, and the
  more memory is consumed.
- `maxObjects` is a per-node limit on how many objects a node holds before it splits. Set it as high as you like if you have
  memory and CPU to spare.

Searches use binary space partitioning, which is very fast. Objects are indexed internally, so moving them within the tree is
also quick.

The following methods are available on a `Quadtree`:

```
Resize()
    Doubles the outer bounding box by adding a new top-level node.

AddItem(IMapObject item)
    Adds an item to the tree, or re-places it if it is already present. Throws if the
    item's bounding-box centre is outside the world or it has no object type.

MoveItem(IMapObject item)
    Re-places an item after its position or size changed; adds it if it was never tracked.
    Same throwing contract as AddItem.

RemoveItem(IMapObject item)
    Removes the specified item. Returns true if it was found and removed, false otherwise.

Clear()
    Removes all items from the tree.

GetCollidingItems(Rectangle collisionBox, int objectTypes, ref HashSet<IMapObject> itemsFound)
    Returns a list of unique items colliding with the given rectangle.

GetCollidingItems(Circle collisionCircle, int objectTypes, ref HashSet<IMapObject> itemsFound)
    Returns a list of unique items colliding with the given circle.
```

## Items

The quadtree works with any object that implements the `IMapObject` interface:

```csharp
public interface IMapObject
{
    int ObjectTypes { get; set; }
    Point2 Location { get; set; }
    Rectangle BoundingBox { get; }
}
```

As long as your objects implement these members, you can add anything you want to the structure with little effort. The
`ObjectTypes` property lets you intermix different kinds of objects and selectively filter them in searches using bit flags.

## Creating an octree

The `Octree` is the three-dimensional counterpart to the `Quadtree` — same constructor shape, same methods, same splitting
and filtering behavior, just with a `Cube`/`Sphere`/`Point3` in place of `Rectangle`/`Circle`/`Point2`:

```csharp
var boundingBox = new Cube(0, 0, 0, 1000, 1000, 1000);
var maxDepth = 5;
var maxObjects = 100;
var tree = new Octree(boundingBox, maxDepth, maxObjects);
```

It exposes the same set of methods as `Quadtree` — `Resize()`, `AddItem(IMapObject3d item)`, `MoveItem(IMapObject3d item)`,
`RemoveItem(IMapObject3d item)`, `Clear()`, and two `GetCollidingItems` overloads (one for a `Cube` search volume, one for a
`Sphere`). A node splits into 8 octants instead of 4 quadrants once it holds more than `maxObjects` items.

## Volume items

The octree works with any object that implements the `IMapObject3d` interface:

```csharp
public interface IMapObject3d
{
    int ObjectTypes { get; set; }
    Point3 Location { get; set; }
    Cube BoundingBox { get; }
}
```

## License

This library is covered by the MIT license — do pretty much anything you want with it, except claim it as your own work. Go
build something cool with it, and sell it for a lot of money.
