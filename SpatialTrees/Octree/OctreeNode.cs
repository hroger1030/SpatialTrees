/*
The MIT License (MIT)

Copyright (c) 2017 Roger Hill

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files
(the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge,
publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using Geometry;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;

namespace SpatialTrees
{
    [DebuggerDisplay("Node depth: {Depth}, Center: {BoundingBox.Center}, {GetChildObjectCount()} items")]
    public class OctreeNode
    {
        public const int LEAVES = 8;

        public Octree Octree { get; protected set; }
        public OctreeNode Parent { get; protected set; }
        public OctreeNode[] Leaves { get; protected set; }
        public Cube BoundingBox { get; protected set; }
        public List<IMapObject3d> NodeItems { get; protected set; }
        public int Depth { get; protected set; }

        // number of items held anywhere in this node's subtree (this node's NodeItems
        // plus every descendant's). Maintained incrementally by StoreItem / RemoveStoredItem
        // so CollapseUpward does not have to re-walk the subtree on every remove.
        public int SubtreeCount { get; protected set; }

        // bounding-box centre, cached at construction so routing does not recompute it
        // on every level of every insert.
        public Point3 Center { get; protected set; }

        public bool IsSplit
        {
            get { return Leaves != null; }
        }

        public OctreeNode this[int i]
        {
            get
            {
                if (i > -1 && i < LEAVES)
                {
                    return Leaves[i];
                }
                else
                {
                    throw new IndexOutOfRangeException("OctreeNode " + i.ToString() + " does not exist.");
                }
            }
            set
            {
                if (i > -1 && i < LEAVES)
                {
                    Leaves[i] = value;
                }
                else
                {
                    throw new IndexOutOfRangeException("OctreeNode " + i.ToString() + " does not exist.");
                }
            }
        }

        public OctreeNode(Octree octree, OctreeNode parent, Cube bounding_box)
        {
            Octree = octree;
            Parent = parent;
            Depth = (parent == null) ? 1 : parent.Depth + 1;
            Leaves = null;
            BoundingBox = bounding_box;
            Center = bounding_box.Center;
            // NodeItems stays null until StoreItem actually puts something here
        }

        /// <summary>
        /// Re-attaches this node under a new parent and re-stamps the cached depth of
        /// this node and its whole subtree. Used by Octree.Resize, which pushes the
        /// old root down a level under a new top node.
        /// </summary>
        public void Reparent(OctreeNode parent)
        {
            Parent = parent;
            Depth = (parent == null) ? 1 : parent.Depth + 1;

            if (Leaves != null)
            {
                foreach (var leaf in Leaves)
                {
                    if (leaf != null)
                        leaf.Reparent(this);
                }
            }
        }

        /// <summary>
        /// Adds an item into this node's subtree. A caller-facing failure (item outside
        /// the world, no object type) is an ArgumentException from Octree.AddItem, not a
        /// return value; a node that already holds the item just ignores the call.
        /// </summary>
        public void AddItem(IMapObject3d mapItem)
        {
            // read the item's bounding box once here and pass it down the routing
            // recursion - BoundingBox is typically a fresh allocation on every access.
            AddItem(mapItem, mapItem.BoundingBox);
        }

        /// <summary>
        /// Routing worker for <see cref="AddItem(IMapObject3d)"/>. Takes the item's
        /// bounding box as a parameter so a multi-level insert reads the property once
        /// instead of once (or twice) per level.
        /// </summary>
        public void AddItem(IMapObject3d mapItem, Cube itemBox)
        {
            if (NodeItems != null && NodeItems.Contains(mapItem))
                return;

            if (Leaves == null)
            {
                // split once this node is already holding MaxNodeObjects and another item
                // is arriving - so a leaf tops out at exactly MaxNodeObjects, not Max + 1.
                if ((NodeItems?.Count ?? 0) >= Octree.MaxNodeObjects && this.Depth < Octree.MaxDepth)
                {
                    Split();

                    // redistribute existing items into the new leaves. An item whose
                    // bounding box does not fit entirely inside a single child straddles
                    // an octant boundary and has to stay on this node, so pull everything
                    // off first and let RouteItem decide where each one lands. The scratch
                    // array is pooled so a bulk build's many splits don't each allocate.
                    int moved = NodeItems.Count;
                    IMapObject3d[] scratch = ArrayPool<IMapObject3d>.Shared.Rent(moved);
                    NodeItems.CopyTo(scratch);
                    NodeItems.Clear();
                    // these items are about to be re-stored (on a child, or back here if
                    // they straddle); drop them from the running totals first so the
                    // per-item StoreItem calls below re-add them exactly once.
                    AdjustSubtreeCount(-moved);

                    for (int i = 0; i < moved; i++)
                        RouteItem(scratch[i], scratch[i].BoundingBox);

                    ArrayPool<IMapObject3d>.Shared.Return(scratch, clearArray: true);

                    RouteItem(mapItem, itemBox);
                }
                else
                {
                    StoreItem(mapItem);
                }
            }
            else
            {
                RouteItem(mapItem, itemBox);
            }
        }

        /// <summary>
        /// Routes an item into the child leaf that fully contains its bounding box. If no
        /// single child contains it (the item straddles an octant boundary) the item is
        /// stored on this node instead, so that collision queries touching only one of the
        /// neighbouring octants still find it. Assumes this node has been split.
        /// </summary>
        public void RouteItem(IMapObject3d mapItem, Cube itemBox)
        {
            OctreeNode leaf = FindContainingLeaf(itemBox);

            if (leaf == null)
                StoreItem(mapItem);
            else
                leaf.AddItem(mapItem, itemBox);
        }

        /// <summary>
        /// Returns the child leaf whose bounding box fully contains <paramref name="itemBox"/>,
        /// creating that child if it does not exist yet, or null when the box straddles an
        /// octant boundary (in which case no child is created). Assumes this node has been split.
        /// </summary>
        public OctreeNode FindContainingLeaf(Cube itemBox)
        {
            eOctant octant = FindOctant(Center, itemBox.Center);
            OctreeNode leaf = Leaves[(int)octant];

            Cube childBox = leaf != null ? leaf.BoundingBox : ChildBox(octant);
            if (!childBox.Contains(itemBox))
                return null;

            return leaf ?? (Leaves[(int)octant] = new OctreeNode(Octree, this, childBox));
        }

        /// <summary>
        /// The bounding box of child octant <paramref name="octant"/>, computed from this
        /// node's bounds whether or not that child has been created yet.
        /// </summary>
        protected Cube ChildBox(eOctant octant)
        {
            float x1 = BoundingBox.X1, y1 = BoundingBox.Y1, z1 = BoundingBox.Z1;
            float x2 = BoundingBox.X2, y2 = BoundingBox.Y2, z2 = BoundingBox.Z2;
            float cx = Center.X, cy = Center.Y, cz = Center.Z;

            return octant switch
            {
                eOctant.UpperRightNear => new Cube(cx, y1, z1, x2, cy, cz),
                eOctant.LowerRightNear => new Cube(cx, cy, z1, x2, y2, cz),
                eOctant.LowerLeftNear => new Cube(x1, cy, z1, cx, y2, cz),
                eOctant.UpperLeftNear => new Cube(x1, y1, z1, cx, cy, cz),
                eOctant.UpperRightFar => new Cube(cx, y1, cz, x2, cy, z2),
                eOctant.LowerRightFar => new Cube(cx, cy, cz, x2, y2, z2),
                eOctant.LowerLeftFar => new Cube(x1, cy, cz, cx, y2, z2),
                _ => new Cube(x1, y1, cz, cx, cy, z2),
            };
        }

        /// <summary>
        /// Stores an item directly on this node and points the tree's object index at
        /// this node.
        /// </summary>
        public void StoreItem(IMapObject3d mapItem)
        {
            (NodeItems ??= new List<IMapObject3d>()).Add(mapItem);
            AdjustSubtreeCount(1);

            if (Octree.ObjectIndex.ContainsKey(mapItem))
                Octree.ObjectIndex[mapItem] = this;
            else
                Octree.ObjectIndex.Add(mapItem, this);
        }

        /// <summary>
        /// Removes an item stored directly on this node and keeps the subtree counts in
        /// step. Returns false if the item was not held here. The tree's object index is
        /// the caller's responsibility.
        /// </summary>
        public bool RemoveStoredItem(IMapObject3d mapItem)
        {
            if (NodeItems == null || !NodeItems.Remove(mapItem))
                return false;

            AdjustSubtreeCount(-1);
            return true;
        }

        /// <summary>
        /// Adds <paramref name="delta"/> to this node's SubtreeCount and every ancestor's.
        /// </summary>
        public void AdjustSubtreeCount(int delta)
        {
            for (OctreeNode node = this; node != null; node = node.Parent)
                node.SubtreeCount += delta;
        }

        /// <summary>
        /// Recomputes this node's SubtreeCount from its own items plus its childrens'
        /// counts (assumed already correct). O(children); used by Octree.Resize.
        /// </summary>
        public void RefreshSubtreeCount()
        {
            int total = NodeItems?.Count ?? 0;

            if (Leaves != null)
            {
                foreach (var leaf in Leaves)
                {
                    if (leaf != null)
                        total += leaf.SubtreeCount;
                }
            }

            SubtreeCount = total;
        }

        public void RemoveAllLeafItems(bool recursive)
        {
            NodeItems?.Clear();
            SubtreeCount = 0;

            if (recursive && Leaves != null)
            {
                foreach (var leaf in Leaves)
                {
                    if (leaf != null)
                        leaf.RemoveAllLeafItems(true);
                }
            }
        }

        /// <summary>
        /// Object-type filter for collision queries. An item matches when it carries at
        /// least one of the requested type bits, so a query mask combining several types
        /// returns items of any of those types. A mask of 0 matches nothing.
        /// </summary>
        public static bool MatchesObjectTypes(int queryMask, int itemObjectTypes)
        {
            return (queryMask & itemObjectTypes) != 0;
        }

        /// <summary>
        /// returns a list of unique items that are colliding with the item that is passed in.
        /// </summary>
        public void GetCollidingItems(Cube collisionBox, int objectTypes, ref List<IMapObject3d> itemsFound)
        {
            if (!BoundingBox.Intersects(collisionBox))
                return;

            if (collisionBox.Contains(BoundingBox))
            {
                // the query region fully contains this node, so it contains this node's
                // whole subtree - collect everything below with no further geometry tests.
                CollectAll(objectTypes, ref itemsFound);
                return;
            }

            if (NodeItems is { Count: > 0 })
            {
                // test each item in this node
                foreach (var item in NodeItems)
                {
                    if (collisionBox.Intersects(item.BoundingBox) && MatchesObjectTypes(objectTypes, item.ObjectTypes))
                    {
                        itemsFound.Add(item);
                    }
                }
            }

            if (Leaves != null)
            {
                foreach (var leaf in Leaves)
                {
                    if (leaf != null)
                        leaf.GetCollidingItems(collisionBox, objectTypes, ref itemsFound);
                }
            }
        }

        /// <summary>
        /// returns a list of unique items that are colliding with the item that is passed in.
        /// </summary>
        public void GetCollidingItems(Sphere collisionSphere, int objectTypes, ref List<IMapObject3d> itemsFound)
        {
            if (!BoundingBox.Intersects(collisionSphere))
                return;

            if (collisionSphere.Contains(BoundingBox))
            {
                // the query region fully contains this node, so it contains this node's
                // whole subtree - collect everything below with no further geometry tests.
                CollectAll(objectTypes, ref itemsFound);
                return;
            }

            if (NodeItems is { Count: > 0 })
            {
                // test each item in this node
                foreach (var item in NodeItems)
                {
                    if (collisionSphere.Intersects(item.BoundingBox) && MatchesObjectTypes(objectTypes, item.ObjectTypes))
                    {
                        itemsFound.Add(item);
                    }
                }
            }

            if (Leaves != null)
            {
                foreach (var leaf in Leaves)
                {
                    if (leaf != null)
                        leaf.GetCollidingItems(collisionSphere, objectTypes, ref itemsFound);
                }
            }
        }

        /// <summary>
        /// Adds every type-matching item in this node's whole subtree to the result set
        /// with no spatial tests. Used by GetCollidingItems once a query region is known
        /// to fully contain this node.
        /// </summary>
        public void CollectAll(int objectTypes, ref List<IMapObject3d> itemsFound)
        {
            if (NodeItems != null)
            {
                foreach (var item in NodeItems)
                {
                    if (MatchesObjectTypes(objectTypes, item.ObjectTypes))
                    {
                        itemsFound.Add(item);
                    }
                }
            }

            if (Leaves != null)
            {
                foreach (var leaf in Leaves)
                {
                    if (leaf != null)
                        leaf.CollectAll(objectTypes, ref itemsFound);
                }
            }
        }

        public void Split()
        {
            if (Leaves != null)
                throw new Exception("Node already split");

            // child nodes are materialised lazily by FindContainingLeaf as items route
            // into each octant; an empty octant costs only its null array slot.
            Leaves = new OctreeNode[LEAVES];
        }

        /// <summary>
        /// Number of items held anywhere in this node's subtree. O(1) - returns the
        /// running <see cref="SubtreeCount"/>.
        /// </summary>
        public int GetChildObjectCount()
        {
            return SubtreeCount;
        }

        /// <summary>
        /// Called after an item is removed from this node. Walks toward the root and
        /// collapses the highest ancestor whose whole subtree now holds no more than
        /// MaxNodeObjects items back into a single leaf, so the tree does not stay
        /// permanently over-subdivided once items are taken back out of it.
        /// </summary>
        public void CollapseUpward()
        {
            OctreeNode cursor = this;
            OctreeNode target = null;

            while (cursor != null)
            {
                if (cursor.Leaves != null)
                {
                    // counts only grow as we move up, so once a node is over the limit
                    // no ancestor of it can be collapsible either.
                    if (cursor.GetChildObjectCount() <= Octree.MaxNodeObjects)
                        target = cursor;
                    else
                        break;
                }

                cursor = cursor.Parent;
            }

            target?.Collapse();
        }

        /// <summary>
        /// Pulls every item held anywhere in this node's subtree up into this node and
        /// discards the child leaves, turning this node back into a leaf.
        /// </summary>
        public void Collapse()
        {
            if (Leaves == null)
                return;

            foreach (var leaf in Leaves)
            {
                if (leaf != null)
                    leaf.MergeInto(this);
            }

            Leaves = null;
        }

        /// <summary>
        /// Moves this node's items, and recursively its descendants', into 'ancestor',
        /// repointing the tree's object index at 'ancestor' as it goes.
        /// </summary>
        public void MergeInto(OctreeNode ancestor)
        {
            if (NodeItems != null)
            {
                foreach (var item in NodeItems)
                {
                    (ancestor.NodeItems ??= new List<IMapObject3d>()).Add(item);
                    Octree.ObjectIndex[item] = ancestor;
                }

                NodeItems.Clear();
            }

            if (Leaves != null)
            {
                foreach (var leaf in Leaves)
                {
                    if (leaf != null)
                        leaf.MergeInto(ancestor);
                }

                Leaves = null;
            }
        }

        /// <summary>
        /// Which child octant <paramref name="point"/> falls in, relative to
        /// <paramref name="nodeCenter"/> (this node's cached centre).
        /// </summary>
        protected static eOctant FindOctant(Point3 nodeCenter, Point3 point)
        {
            if (point.X > nodeCenter.X)
            {
                if (point.Y > nodeCenter.Y)
                    return point.Z > nodeCenter.Z ? eOctant.LowerRightFar : eOctant.LowerRightNear;
                else
                    return point.Z > nodeCenter.Z ? eOctant.UpperRightFar : eOctant.UpperRightNear;
            }
            else
            {
                if (point.Y > nodeCenter.Y)
                    return point.Z > nodeCenter.Z ? eOctant.LowerLeftFar : eOctant.LowerLeftNear;
                else
                    return point.Z > nodeCenter.Z ? eOctant.UpperLeftFar : eOctant.UpperLeftNear;
            }
        }

        public override string ToString()
        {
            return $"Node depth: {Depth}, Center: {BoundingBox.Center}, {GetChildObjectCount()} items";
        }
    }
}
