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

namespace SpatialTrees.Quadtrees
{
    [DebuggerDisplay("Node depth: {Depth}, Center: {BoundingBox.Center}, {GetChildObjectCount()} items")]
    public class QuadtreeNode
    {
        public const int LEAVES = 4;

        public Quadtree Quadtree { get; protected set; }
        public QuadtreeNode Parent { get; protected set; }
        public QuadtreeNode[] Leaves { get; protected set; }
        public Rectangle BoundingBox { get; protected set; }
        public List<IMapObject2d> NodeItems { get; protected set; }
        public int Depth { get; protected set; }

        // number of items held anywhere in this node's subtree (this node's NodeItems
        // plus every descendant's). Maintained incrementally by StoreItem / RemoveStoredItem
        // so CollapseUpward does not have to re-walk the subtree on every remove.
        public int SubtreeCount { get; protected set; }

        // bounding-box centre, cached at construction so routing does not recompute it
        // on every level of every insert.
        public Point2 Center { get; protected set; }

        /// <summary>
        /// True once this node has been subdivided into child quadrants.
        /// </summary>
        public bool IsSplit
        {
            get { return Leaves != null; }
        }

        public QuadtreeNode this[int i]
        {
            get
            {
                if (i > -1 && i < LEAVES)
                {
                    return Leaves[i];
                }
                else
                {
                    throw new IndexOutOfRangeException($"QuadtreePointNode {i} does not exist.");
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
                    throw new IndexOutOfRangeException($"QuadtreePointNode {i} does not exist.");
                }
            }
        }

        public QuadtreeNode(Quadtree quadtree, QuadtreeNode parent, Rectangle bounding_box)
        {
            Quadtree = quadtree;
            Parent = parent;
            Depth = (parent == null) ? 1 : parent.Depth + 1;
            Leaves = null;
            BoundingBox = bounding_box;
            Center = bounding_box.Center;
            // NodeItems stays null until StoreItem actually puts something here
        }

        /// <summary>
        /// Re-attaches this node under a new parent and re-stamps the cached depth of
        /// this node and its whole subtree. Used by Quadtree.Resize, which pushes the
        /// old root down a level under a new top node.
        /// </summary>
        public void Reparent(QuadtreeNode parent)
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
        /// the world, no object type) is an ArgumentException from Quadtree.AddItem, not a
        /// return value; a node that already holds the item just ignores the call.
        /// </summary>
        public void AddItem(IMapObject2d mapItem)
        {
            // read the item's bounding box once here and pass it down the routing
            // recursion - BoundingBox is an interface call the implementer may compute
            // fresh each time, so we don't want to re-read it at every level.
            AddItem(mapItem, mapItem.BoundingBox);
        }

        /// <summary>
        /// Routing worker for <see cref="AddItem(IMapObject2d)"/>. Takes the item's
        /// bounding box as a parameter so a multi-level insert reads the property once
        /// instead of once (or twice) per level.
        /// </summary>
        public void AddItem(IMapObject2d mapItem, Rectangle itemBox)
        {
            // no dup guard here: Quadtree.AddItem already does ContainsKey -> DetachItem,
            // so a routed item is never already somewhere in the tree by the time it
            // reaches a node. The redistribute path below only re-routes items it just
            // pulled off this node, so those cannot collide either.

            if (Leaves == null)
            {
                // split once this node is already holding MaxNodeObjects and another item
                // is arriving - so a leaf tops out at exactly MaxNodeObjects, not Max + 1.
                if ((NodeItems?.Count ?? 0) >= Quadtree.MaxNodeObjects && this.Depth < Quadtree.MaxDepth)
                {
                    Split();

                    // redistribute existing items into the new leaves. An item whose
                    // bounding box does not fit entirely inside a single child straddles
                    // a quadrant boundary and has to stay on this node, so pull everything
                    // off first and let RouteItem decide where each one lands. The scratch
                    // array is pooled so a bulk build's many splits don't each allocate.
                    int moved = NodeItems.Count;
                    IMapObject2d[] scratch = ArrayPool<IMapObject2d>.Shared.Rent(moved);
                    NodeItems.CopyTo(scratch);
                    NodeItems.Clear();
                    // these items are about to be re-stored (on a child, or back here if
                    // they straddle); drop them from the running totals first so the
                    // per-item StoreItem calls below re-add them exactly once.
                    AdjustSubtreeCount(-moved);

                    for (int i = 0; i < moved; i++)
                        RouteItem(scratch[i], scratch[i].BoundingBox);

                    ArrayPool<IMapObject2d>.Shared.Return(scratch, clearArray: true);

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
        /// single child contains it (the item straddles a quadrant boundary) the item is
        /// stored on this node instead, so that collision queries touching only one of the
        /// neighbouring quadrants still find it. Assumes this node has been split.
        /// </summary>
        public void RouteItem(IMapObject2d mapItem, Rectangle itemBox)
        {
            QuadtreeNode leaf = FindContainingLeaf(itemBox);

            if (leaf == null)
                StoreItem(mapItem);
            else
                leaf.AddItem(mapItem, itemBox);
        }

        /// <summary>
        /// Returns the child leaf whose bounding box fully contains <paramref name="itemBox"/>,
        /// creating that child if it does not exist yet, or null when the box straddles a
        /// quadrant boundary (in which case no child is created). Assumes this node has been split.
        /// </summary>
        public QuadtreeNode FindContainingLeaf(Rectangle itemBox)
        {
            eQuadrant quadrant = FindQuadrant(Center, itemBox.Center);
            QuadtreeNode leaf = Leaves[(int)quadrant];

            Rectangle childBox = leaf != null ? leaf.BoundingBox : ChildBox(quadrant);
            if (!childBox.Contains(itemBox))
                return null;

            return leaf ?? (Leaves[(int)quadrant] = new QuadtreeNode(Quadtree, this, childBox));
        }

        /// <summary>
        /// The bounding box of child quadrant <paramref name="quadrant"/>, computed from this
        /// node's bounds whether or not that child has been created yet.
        /// </summary>
        protected Rectangle ChildBox(eQuadrant quadrant)
        {
            float halfWidth = BoundingBox.Width * 0.5f;
            float halfHeight = BoundingBox.Height * 0.5f;

            return quadrant switch
            {
                eQuadrant.UpperRightQuadrant => new Rectangle(Center.X, BoundingBox.Top, halfWidth, halfHeight),
                eQuadrant.LowerRightQuadrant => new Rectangle(Center.X, Center.Y, halfWidth, halfHeight),
                eQuadrant.LowerLeftQuadrant => new Rectangle(BoundingBox.Left, Center.Y, halfWidth, halfHeight),
                _ => new Rectangle(BoundingBox.Left, BoundingBox.Top, halfWidth, halfHeight),
            };
        }

        /// <summary>
        /// Stores an item directly on this node and points the tree's object index at
        /// this node.
        /// </summary>
        public void StoreItem(IMapObject2d mapItem)
        {
            (NodeItems ??= new List<IMapObject2d>()).Add(mapItem);
            AdjustSubtreeCount(1);

            // upsert: the indexer setter adds a new entry or repoints an existing one,
            // so this is a single hash lookup whether or not the item was already indexed.
            Quadtree.ObjectIndex[mapItem] = this;
        }

        /// <summary>
        /// Removes an item stored directly on this node and keeps the subtree counts in
        /// step. Returns false if the item was not held here. 
        /// </summary>
        public bool RemoveStoredItem(IMapObject2d mapItem)
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
            for (QuadtreeNode node = this; node != null; node = node.Parent)
                node.SubtreeCount += delta;
        }

        /// <summary>
        /// Recomputes this node's SubtreeCount from its own items plus its childrens'
        /// counts (assumed already correct). O(children); used by Quadtree.Resize.
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
        public void GetCollidingItems(Rectangle collisionBox, int objectTypes, ref List<IMapObject2d> itemsFound)
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
        public void GetCollidingItems(Circle collisionCircle, int objectTypes, ref List<IMapObject2d> itemsFound)
        {
            var nodeBox = BoundingBox;
            if (!nodeBox.Intersects(collisionCircle))
                return;

            if (collisionCircle.Contains(nodeBox))
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
                    if (MatchesObjectTypes(objectTypes, item.ObjectTypes) && collisionCircle.Intersects(item.BoundingBox))
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
                        leaf.GetCollidingItems(collisionCircle, objectTypes, ref itemsFound);
                }
            }
        }

        /// <summary>
        /// Adds every type-matching item in this node's whole subtree to the result set
        /// with no spatial tests. Used by GetCollidingItems once a query region is known
        /// to fully contain this node.
        /// </summary>
        public void CollectAll(int objectTypes, ref List<IMapObject2d> itemsFound)
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
            // into each quadrant; an empty quadrant costs only its null array slot.
            Leaves = new QuadtreeNode[LEAVES];
        }

        /// <summary>
        /// Bottom-up bulk load for <see cref="Quadtree.Build"/>. Partitions src[lo..hi)
        /// by child quadrant into dst, keeps straddlers here, recurses into each quadrant
        /// with the buffers swapped. <paramref name="buckets"/> is shared recursion
        /// scratch: the count pass writes each item's class, the scatter pass reads it.
        /// Returns this subtree's item count and sets <see cref="SubtreeCount"/>.
        /// </summary>
        public int BulkLoad(IMapObject2d[] src, IMapObject2d[] dst, byte[] buckets, int lo, int hi)
        {
            int count = hi - lo;

            // bucket 0 = straddles a quadrant line (stays here); 1..LEAVES = fits that child
            Span<int> counts = stackalloc int[LEAVES + 1];

            for (int i = lo; i < hi; i++)
            {
                byte bucket = (byte)BulkBucket(src[i].BoundingBox);
                buckets[i] = bucket;
                counts[bucket]++;
            }

            // leaf: under the cap, at the depth limit, or nothing a split could separate
            if (count <= Quadtree.MaxNodeObjects || Depth >= Quadtree.MaxDepth || counts[0] == count)
            {
                if (count > 0)
                {
                    NodeItems = new List<IMapObject2d>(count);

                    for (int i = lo; i < hi; i++)
                        StoreForBulk(src[i]);
                }

                SubtreeCount = count;
                return count;
            }

            Split();

            // counting sort src[lo..hi) -> dst[lo..hi): straddlers, then one run per quadrant
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
                NodeItems = new List<IMapObject2d>(counts[0]);
                for (int i = runStart[0]; i < runStart[1]; i++)
                    StoreForBulk(dst[i]);
            }

            // recurse into each non-empty child, src/dst swapped
            for (int q = 0; q < LEAVES; q++)
            {
                int childCount = counts[q + 1];

                if (childCount == 0)
                    continue;

                var child = new QuadtreeNode(Quadtree, this, ChildBox((eQuadrant)q));
                Leaves[q] = child;
                total += child.BulkLoad(dst, src, buckets, runStart[q + 1], runStart[q + 1] + childCount);
            }

            SubtreeCount = total;
            return total;
        }

        /// <summary>
        /// Bulk-load classifier: 0 if <paramref name="itemBox"/> straddles a quadrant
        /// boundary (stays on this node), else the 1-based index of the child that contains it.
        /// </summary>
        public int BulkBucket(Rectangle itemBox)
        {
            eQuadrant quadrant = FindQuadrant(Center, itemBox.Center);
            return ChildBox(quadrant).Contains(itemBox) ? (int)quadrant + 1 : 0;
        }

        /// <summary>
        /// Appends an already-validated item to the pre-sized NodeItems list and points
        /// the object index here. SubtreeCount is the bulk loader's job, not this method's.
        /// </summary>
        public void StoreForBulk(IMapObject2d mapItem)
        {
            NodeItems.Add(mapItem);
            Quadtree.ObjectIndex[mapItem] = this;
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
            QuadtreeNode cursor = this;
            QuadtreeNode target = null;

            while (cursor != null)
            {
                if (cursor.Leaves != null)
                {
                    // counts only grow as we move up, so once a node is over the limit
                    // no ancestor of it can be collapsible either.
                    if (cursor.GetChildObjectCount() <= Quadtree.MaxNodeObjects)
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
        public void MergeInto(QuadtreeNode ancestor)
        {
            var nodeItems = NodeItems;
            if (nodeItems != null)
            {
                for (int i = 0, n = nodeItems.Count; i < n; i++)
                {
                    var item = nodeItems[i];
                    (ancestor.NodeItems ??= new List<IMapObject2d>()).Add(item);
                    Quadtree.ObjectIndex[item] = ancestor;
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
        /// Which child quadrant <paramref name="point"/> falls in, relative to
        /// <paramref name="nodeCenter"/> (this node's cached centre).
        /// </summary>
        protected static eQuadrant FindQuadrant(Point2 nodeCenter, Point2 point)
        {
            if (point.X > nodeCenter.X)
            {
                if (point.Y > nodeCenter.Y)
                    return eQuadrant.LowerRightQuadrant;
                else
                    return eQuadrant.UpperRightQuadrant;
            }
            else
            {
                if (point.Y > nodeCenter.Y)
                    return eQuadrant.LowerLeftQuadrant;
                else
                    return eQuadrant.UpperLeftQuadrant;
            }
        }

        public override string ToString()
        {
            return $"Node depth: {Depth}, Center: {BoundingBox.Center}, {GetChildObjectCount()} items";
        }
    }
}
