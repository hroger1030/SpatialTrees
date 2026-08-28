# Spatial Trees

This library lets you quickly build and search a quadtree (2D) or octree (3D) spatial index. Both are tuned for real-time
game and physics use: fast incremental inserts and moves, a one-shot bulk `Build` for the load-everything-up-front case, and
collision queries that allocate nothing on the heap. The octree mirrors the quadtree's design one dimension up — same API
shape, same splitting behavior, just with a Z axis added.

## Table of contents

- [Spatial Trees](#spatial-trees)
  - [Table of contents](#table-of-contents)
  - [Requirements](#requirements)
  - [Project layout](#project-layout)
  - [Building and testing](#building-and-testing)
  - [Thread safety](#thread-safety)
  - [Creating a quadtree](#creating-a-quadtree)
  - [Choosing MaxDepth](#choosing-maxdepth)
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
│   │   ├── IMapObject2d.cs
│   │   ├── Quadtree.cs
│   │   ├── QuadtreeNode.cs
│   │   └── eQuadrant.cs
│   └── Octree/
│       ├── IMapObject3d.cs
│       ├── Octree.cs
│       ├── OctreeNode.cs
│       └── eOctant.cs
├── SpatialTreeTests/          # NUnit test project
│   ├── SpatialTreeTests.csproj
│   ├── Quadtree/
│   │   ├── SpatialTreesTests.cs
│   │   ├── TestItem.cs
│   │   └── ...                # AddItem/MoveItem/RemoveItem/Build/Clear/Resize/etc. fixtures
│   └── Octree/
│       ├── OctreeCollisionTests.cs
│       ├── TestVolumeItem.cs
│       └── ...                # same fixture layout as Quadtree, one dimension up
└── BenchMarks/                # BenchmarkDotNet perf suite (see BenchMarks/README.md)
    └── BenchMarks.csproj
```

## Building and testing

```
dotnet build SpatialTrees.sln
dotnet test SpatialTrees.sln
```

Unit tests are written with NUnit and live in the `SpatialTreeTests` project.

The `BenchMarks` project is a BenchmarkDotNet suite for measuring insert, build, and query performance. Run it in Release:
`dotnet run -c Release --project BenchMarks -- --filter *Quadtree*` (see `BenchMarks/README.md`).

## Thread safety

`Quadtree` and `Octree` are **not** thread safe. They are intended for single-threaded use; if you access one from more than
one thread, you must serialize the calls yourself. Thread-safe variants of both structures are planned for a future release.

## Creating a quadtree

To initialize a new quadtree, use the following code:

```csharp
var boundingBox = new Rectangle(0, 0, 1000, 1000);
var maxDepth = 8;
var maxObjects = 16;
var tree = new Quadtree(boundingBox, maxDepth, maxObjects);
```

- `boundingBox` is the outer boundary of the search space.
- `maxDepth` is the number of levels of "resolution". The more levels you add, the more finely the space is subdivided, and the
  more memory is consumed. See [Choosing MaxDepth](#choosing-maxdepth) for how to pick it.
- `maxObjects` is a per-node limit on how many objects a node holds before it splits. A query has to scan a node's items
  linearly before it can prune past that node, so keep this small (8–32); raise `maxDepth` instead if you need more capacity.

The parameterless / bounding-box-only constructors default to `maxDepth = 8`, `maxObjects = 16`.

A fourth optional argument, `expectedItemCount`, pre-sizes the internal item-to-node index so a large build doesn't
repeatedly grow it:

```csharp
var tree = new Quadtree(boundingBox, maxDepth, maxObjects, expectedItemCount: 50_000);
```

If you have every item up front, build the tree in one pass instead of adding them one at a time:

```csharp
var tree = Quadtree.Build(boundingBox, maxDepth, maxObjects, items); // items: IReadOnlyCollection<IMapObject2d>
```

`Build` partitions the items one quadrant boundary at a time and assembles the nodes bottom-up, so it does none of the
repeated leaf split-and-redistribute work the incremental path does and sizes the internal index and each leaf's item list
exactly. It is materially faster and lighter for the build-once case; use `AddItem` for changes afterwards. Items must be
distinct references and each must satisfy the same rules `AddItem` enforces. There is also a `Quadtree.Build(boundingBox,
items)` overload that uses the default depth and object limits.

Searches use binary space partitioning, which is very fast, and collision queries allocate nothing on the heap — the caller
supplies the result list and it is reused. Objects are indexed internally, so moving them within the tree is also quick.

The following methods are available on a `Quadtree`:

```
static Build(Rectangle boundingBox, int maxDepth, int maxObjects, IReadOnlyCollection<IMapObject2d> items)
static Build(Rectangle boundingBox, IReadOnlyCollection<IMapObject2d> items)
    Builds a tree from all of its items in one bottom-up pass. The short overload uses
    the default depth and object limits. Throws ArgumentException for an item outside
    the world or with no object type bits, same as AddItem.

Resize()
    Doubles the outer bounding box by adding a new top-level node, and increments MaxDepth.

AddItem(IMapObject2d item)
    Adds an item to the tree, or re-places it if it is already present. Throws
    ArgumentException if the item's bounding-box centre is outside the world or it has
    no object type bits set.

MoveItem(IMapObject2d item)
    Re-places an item after its position or size changed; adds it if it was never tracked.
    Same throwing contract as AddItem; a rejected move leaves the item where it was.

RemoveItem(IMapObject2d item)
    Removes the specified item. Returns true if it was found and removed, false otherwise.

Clear()
    Removes all items from the tree. The world rectangle and MaxDepth are left as they are.

GetCollidingItems(Rectangle collisionBox, int objectTypes, ref List<IMapObject2d> itemsFound)
GetCollidingItems(Circle collisionCircle, int objectTypes, ref List<IMapObject2d> itemsFound)
    Clears itemsFound, then fills it with every unique item whose bounding box overlaps the
    query region and whose ObjectTypes shares a bit with objectTypes. Allocates a list only
    if the caller passed null. Returns true if anything was found.
```

`ObjectIndex`, `TopNode`, `WorldRectangle`, `MaxDepth`, and `MaxNodeObjects` are exposed as read-only properties for
inspection.

## Choosing MaxDepth

Every level of the tree bisects each axis, so a leaf at depth *d* (the root is depth 1) spans `worldSize / 2^(d - 1)`
per axis. To pick `maxDepth`, decide the **smallest cell** you want the tree to be able to resolve — usually the size of
your smallest game object, or the grid resolution collision queries need — and work back from the ratio of the map to that
cell:

```
maxDepth = ceil( log2( worldSize / smallestCell ) ) + 1
```

`worldSize` is the longer axis of a non-square map (cells stay square; the short axis just holds more of them). The same
formula applies to the octree.

| worldSize / smallestCell | maxDepth |
| -----------------------: | :------: |
| 1                        | 1        |
| 2                        | 2        |
| 3 – 4                    | 3        |
| 5 – 8                    | 4        |
| 9 – 16                   | 5        |
| 17 – 32                  | 6        |
| 33 – 64                  | 7        |
| 65 – 128  *(default)*    | 8        |
| 129 – 256                | 9        |
| 257 – 512                | 10       |
| 513 – 1,024              | 11       |
| 1,025 – 2,048            | 12       |
| 2,049 – 4,096            | 13       |
| 4,097 – 8,192            | 14       |
| 8,193 – 16,384           | 15       |
| 16,385 – 32,768          | 16       |
| 32,769 – 65,536          | 17       |

Examples:

| Map (per axis) | Smallest cell | maxDepth |
| -------------: | ------------: | :------: |
| 10,000         | 100           | 8        |
| 10,000         | 10            | 11       |
| 10,000         | 1             | 15       |
| 4,096          | 1             | 13       |
| 65,536         | 256           | 9        |

Notes:

- This is the depth cap for a **fully packed** region. A leaf also stops splitting once it holds no more than `maxObjects`
  items, so sparse areas never reach `maxDepth` — it is only the worst-case floor on cell size.
- Extra depth is cheap: interior nodes defer their item-list allocation and an empty quadrant costs nothing, so rounding
  `maxDepth` up a level or two for headroom is fine.
- `Resize()` adds a level on top and increments `maxDepth`, so the smallest resolvable cell stays the same size while the
  world doubles.

## Items

The quadtree works with any object that implements the `IMapObject2d` interface:

```csharp
public interface IMapObject2d
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
var maxDepth = 8;
var maxObjects = 16;
var tree = new Octree(boundingBox, maxDepth, maxObjects);
```

It exposes the same set of methods as `Quadtree` — `static Octree.Build(...)`, `Resize()`, `AddItem(IMapObject3d item)`,
`MoveItem(IMapObject3d item)`, `RemoveItem(IMapObject3d item)`, `Clear()`, and two `GetCollidingItems` overloads (one for a
`Cube` search volume, one for a `Sphere`). A node splits into 8 octants instead of 4 quadrants when it already holds
`maxObjects` items and another one arrives. [Choosing MaxDepth](#choosing-maxdepth) works the same way — the cell-size
formula is identical, only the per-node child count differs.

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
