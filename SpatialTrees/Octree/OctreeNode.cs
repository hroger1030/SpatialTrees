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
using System.Runtime.CompilerServices;

namespace SpatialTrees.Octrees
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
        public int SubtreeCount { get; protected set; }
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

            var leaves = Leaves;
            if (leaves != null)
            {
                for (int i = 0; i < leaves.Length; i++)
                {
                    var leaf = leaves[i];
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
            // recursion - BoundingBox is an interface call the implementer may compute
            // fresh each time, so we don't want to re-read it at every level.
            AddItem(mapItem, mapItem.BoundingBox);
        }

        /// <summary>
        /// Routing worker for <see cref="AddItem(IMapObject3d)"/>. Takes the item's
        /// bounding box as a parameter so a multi-level insert reads the property once
        /// instead of once (or twice) per level.
        /// </summary>
        public void AddItem(IMapObject3d mapItem, Cube itemBox)
        {
            // no dup guard here: Octree.AddItem already does ContainsKey -> DetachItem, so
            // a routed item is never already somewhere in the tree by the time it reaches
            // a node. The redistribute path below only re-routes items it just pulled off
            // this node, so those cannot collide either.

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

                    // these items are about to be re-stored drop them from the running totals.
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

            // upsert: the indexer setter adds a new entry or repoints an existing one,
            // so this is a single hash lookup whether or not the item was already indexed.
            Octree.ObjectIndex[mapItem] = this;
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

            var leaves = Leaves;
            if (leaves != null)
            {
                for (int i = 0; i < leaves.Length; i++)
                {
                    var leaf = leaves[i];
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

            var leaves = Leaves;
            if (recursive && leaves != null)
            {
                for (int i = 0; i < leaves.Length; i++)
                {
                    var leaf = leaves[i];
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool MatchesObjectTypes(int queryMask, int itemObjectTypes)
        {
            return (queryMask & itemObjectTypes) != 0;
        }

        /// <summary>
        /// returns a list of unique items that are colliding with the item that is passed in.
        /// </summary>
        public void GetCollidingItems(Cube collisionBox, int objectTypes, ref List<IMapObject3d> itemsFound)
        {
            var nodeBox = BoundingBox;
            if (!nodeBox.Intersects(collisionBox))
                return;

            if (collisionBox.Contains(nodeBox))
            {
                // the query region fully contains this node, so it contains this node's
                // whole subtree - collect everything below with no further geometry tests.
                CollectAll(objectTypes, ref itemsFound);
                return;
            }

            var items = NodeItems;
            if (items != null)
            {
                // test each item in this node - cheap type mask first, then the geometry
                for (int i = 0, n = items.Count; i < n; i++)
                {
                    var item = items[i];
                    if (MatchesObjectTypes(objectTypes, item.ObjectTypes) && collisionBox.Intersects(item.BoundingBox))
                        itemsFound.Add(item);
                }
            }

            var leaves = Leaves;
            if (leaves != null)
            {
                for (int i = 0; i < leaves.Length; i++)
                {
                    var leaf = leaves[i];
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
            var nodeBox = BoundingBox;
            if (!nodeBox.Intersects(collisionSphere))
                return;

            if (collisionSphere.Contains(nodeBox))
            {
                // the query region fully contains this node, so it contains this node's
                // whole subtree - collect everything below with no further geometry tests.
                CollectAll(objectTypes, ref itemsFound);
                return;
            }

            var items = NodeItems;
            if (items != null)
            {
                // test each item in this node - cheap type mask first, then the geometry
                for (int i = 0, n = items.Count; i < n; i++)
                {
                    var item = items[i];
                    if (MatchesObjectTypes(objectTypes, item.ObjectTypes) && collisionSphere.Intersects(item.BoundingBox))
                        itemsFound.Add(item);
                }
            }

            var leaves = Leaves;
            if (leaves != null)
            {
                for (int i = 0; i < leaves.Length; i++)
                {
                    var leaf = leaves[i];
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
            var items = NodeItems;
            if (items != null)
            {
                for (int i = 0, n = items.Count; i < n; i++)
                {
                    var item = items[i];
                    if (MatchesObjectTypes(objectTypes, item.ObjectTypes))
                        itemsFound.Add(item);
                }
            }

            var leaves = Leaves;
            if (leaves != null)
            {
                for (int i = 0; i < leaves.Length; i++)
                {
                    var leaf = leaves[i];
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
        /// Bottom-up bulk load for <see cref="Octree.Build"/>. Partitions src[lo..hi) by
        /// child octant into dst, keeps straddlers here, recurses into each octant with
        /// the buffers swapped. <paramref name="buckets"/> is shared recursion scratch:
        /// the count pass writes each item's class, the scatter pass reads it. Returns
        /// this subtree's item count and sets <see cref="SubtreeCount"/>.
        /// </summary>
        public int BulkLoad(IMapObject3d[] src, IMapObject3d[] dst, byte[] buckets, int lo, int hi)
        {
            int count = hi - lo;

            // bucket 0 = straddles an octant plane (stays here); 1..LEAVES = fits that child
            Span<int> counts = stackalloc int[LEAVES + 1];
            for (int i = lo; i < hi; i++)
            {
                byte bucket = (byte)BulkBucket(src[i].BoundingBox);
                buckets[i] = bucket;
                counts[bucket]++;
            }

            // leaf: under the cap, at the depth limit, or nothing a split could separate
            if (count <= Octree.MaxNodeObjects || Depth >= Octree.MaxDepth || counts[0] == count)
            {
                if (count > 0)
                {
                    NodeItems = new List<IMapObject3d>(count);
                    for (int i = lo; i < hi; i++)
                        StoreForBulk(src[i]);
                }

                SubtreeCount = count;
                return count;
            }

            Split();

            // counting sort src[lo..hi) -> dst[lo..hi): straddlers, then one run per octant
            Span<int> runStart = stackalloc int[LEAVES + 1];
            runStart[0] = lo;
            for (int b = 1; b <= LEAVES; b++)
                runStart[b] = runStart[b - 1] + counts[b - 1];

            Span<int> cursor = stackalloc int[LEAVES + 1];
            runStart.CopyTo(cursor);

            for (int i = lo; i < hi; i++)
                dst[cursor[buckets[i]]++] = src[i];

            // straddlers stay on this node
            int total = counts[0];
            if (counts[0] > 0)
            {
                NodeItems = new List<IMapObject3d>(counts[0]);
                for (int i = runStart[0]; i < runStart[1]; i++)
                    StoreForBulk(dst[i]);
            }

            // recurse into each non-empty child, src/dst swapped
            for (int o = 0; o < LEAVES; o++)
            {
                int childCount = counts[o + 1];
                if (childCount == 0)
                    continue;

                var child = new OctreeNode(Octree, this, ChildBox((eOctant)o));
                Leaves[o] = child;
                total += child.BulkLoad(dst, src, buckets, runStart[o + 1], runStart[o + 1] + childCount);
            }

            SubtreeCount = total;
            return total;
        }

        /// <summary>
        /// Bulk-load classifier: 0 if <paramref name="itemBox"/> straddles an octant
        /// plane (stays on this node), else the 1-based index of the child that contains it.
        /// </summary>
        public int BulkBucket(Cube itemBox)
        {
            eOctant octant = FindOctant(Center, itemBox.Center);
            return ChildBox(octant).Contains(itemBox) ? (int)octant + 1 : 0;
        }

        /// <summary>
        /// Appends an already-validated item to the pre-sized NodeItems list and points
        /// the object index here. SubtreeCount is the bulk loader's job, not this method's.
        /// </summary>
        public void StoreForBulk(IMapObject3d mapItem)
        {
            NodeItems.Add(mapItem);
            Octree.ObjectIndex[mapItem] = this;
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

            var leaves = Leaves;
            for (int i = 0; i < leaves.Length; i++)
            {
                var leaf = leaves[i];
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
            var nodeItems = NodeItems;
            if (nodeItems != null)
            {
                for (int i = 0, n = nodeItems.Count; i < n; i++)
                {
                    var item = nodeItems[i];
                    (ancestor.NodeItems ??= new List<IMapObject3d>()).Add(item);
                    Octree.ObjectIndex[item] = ancestor;
                }

                nodeItems.Clear();
            }

            var leaves = Leaves;
            if (leaves != null)
            {
                for (int i = 0; i < leaves.Length; i++)
                {
                    var leaf = leaves[i];
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
