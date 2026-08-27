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
using System.Collections.Generic;
using System.Diagnostics;

namespace SpatialTrees
{
    /// <summary>
    /// Octree represents a three dimensional tree structure, and all the
    /// objects that it contains. 
    ///
    /// The octants are numbered using EQuadrant's X/Y layout (top right, clockwise)
    /// for the near half (Z below center), then the same four for the far half.
    ///
    /// The octants are stored in an leaf array, with the index of a given octant
    /// as _leaf[octant_index-1]
    ///
    /// note that this object supports non-balanced nodes.
    ///
    /// THREADING: this class is not thread safe and is intended for single-threaded
    /// use only. Callers that touch it from multiple threads must serialize access
    /// themselves. Thread-safe variants of both spatial trees are planned.
    /// </summary>
    [DebuggerDisplay("Octree {WorldCube.Width} x {WorldCube.Height} x {WorldCube.Depth}, {ObjectIndex.Count} items")]
    public class Octree
    {
        // a leaf scans its items linearly before a query can prune past it, so a lower
        // per-node cap keeps those scans short; the deeper tree that allows is cheap now
        // that interior nodes defer their item-list allocation.
        protected readonly static int DEFAULT_MAX_DEPTH = 8;
        protected readonly static int DEFAULT_MAX_OBJECTS = 16;
        protected readonly static int DEFAULT_COLLECTION_SIZE = 1000;

        public Dictionary<IMapObject3d, OctreeNode> ObjectIndex { get; protected set; }
        public OctreeNode TopNode { get; protected set; }
        public int MaxDepth { get; protected set; }
        public int MaxNodeObjects { get; protected set; }

        public Cube WorldCube
        {
            get { return TopNode.BoundingBox; }
        }

        // default world is the unit cube (0,0,0)-(1,1,1); Quadtree() mirrors this with the unit rectangle
        public Octree() : this(new Cube(0f, 0f, 0f, 1f, 1f, 1f), DEFAULT_MAX_DEPTH, DEFAULT_MAX_OBJECTS) { }

        public Octree(Cube volume) : this(volume, DEFAULT_MAX_DEPTH, DEFAULT_MAX_OBJECTS) { }

        /// <param name="expectedItemCount">
        /// Hint for how many items the tree will hold, used to pre-size the internal
        /// item -> node index so a bulk build does not repeatedly grow it. 0 uses a
        /// default; the tree still works correctly with any number of items.
        /// </param>
        public Octree(Cube boundingBox, int maxDepth, int maxObjects, int expectedItemCount = 0)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxDepth, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(maxObjects, 1);
            ArgumentOutOfRangeException.ThrowIfNegative(expectedItemCount);

            ObjectIndex = new Dictionary<IMapObject3d, OctreeNode>(expectedItemCount > 0 ? expectedItemCount : DEFAULT_COLLECTION_SIZE);
            TopNode = new OctreeNode(this, null, boundingBox);
            MaxDepth = maxDepth;
            MaxNodeObjects = maxObjects;
        }

        /// <summary>
        /// Resizes world by adding a new top level node. Calling this will increase volume by 8x.
        /// Old top node becomes the upper left near node, since the cube keeps its X1,Y1,Z1 corner fixed.
        /// </summary>
        public void Resize()
        {
            // create new bounding box. note cube keeps its X1,Y1,Z1 corner and grows along X2,Y2,Z2
            var new_boundingbox = new Cube(TopNode.BoundingBox * 2);

            // save top object refrence
            var old_top_node = TopNode;

            // replace upper left near branch of octree with old tree
            TopNode = new OctreeNode(this, null, new_boundingbox);
            MaxDepth++;

            // Generate new leaves
            TopNode.Split();

            // replace old branches, then fix up the old subtree's parent link and its
            // now-stale cached depths (everything below it just dropped a level)
            TopNode[(int)eOctant.UpperLeftNear] = old_top_node;
            old_top_node.Reparent(TopNode);

            // the new top node inherits the old tree's whole item count (its seven new
            // sibling leaves are empty)
            TopNode.RefreshSubtreeCount();
        }

        /// <summary>
        /// Throws ArgumentException if an item cannot be placed in the tree: its
        /// bounding-box centre is outside the world, or it carries no object type bits
        /// (a zero mask matches no query, so the item would be invisible dead weight).
        /// Shared by AddItem and MoveItem so a rejected move can bail out before it has
        /// touched the tree.
        /// </summary>
        public void ValidateForInsert(IMapObject3d item)
        {
            ValidateForInsert(item, item.BoundingBox);
        }

        /// <summary>
        /// As <see cref="ValidateForInsert(IMapObject3d)"/>, but takes the item's bounding
        /// box so a caller that already has it does not re-read the property.
        /// </summary>
        public void ValidateForInsert(IMapObject3d item, Cube itemBox)
        {
            // route and range-check off the same reference point: the tree places items by
            // BoundingBox.Center (see OctreeNode.FindOctant), so that is what has to be
            // inside the world, not the item's Location which may not track the box.
            var center = itemBox.Center;

            if (!WorldCube.Contains(center))
                throw new ArgumentException($"{center} is outside the octree world cube {WorldCube}", nameof(item));

            if (item.ObjectTypes == 0)
                throw new ArgumentException($"{item} has no object type flags set and could never be returned by a query", nameof(item));
        }

        /// <summary>
        /// Adds an item to the octree, or re-places it if it is already present. The
        /// operation always succeeds unless it throws ArgumentException: the item's
        /// bounding-box centre is outside the world, or it has no object type bits set.
        /// </summary>
        public void AddItem(IMapObject3d item)
        {
            var itemBox = item.BoundingBox;
            ValidateForInsert(item, itemBox);

            if (ObjectIndex.ContainsKey(item))
            {
                // already here, treat this as a move/update. Pull it out completely -
                // both the node list and the object index - so the re-add below starts
                // from a clean state instead of leaving a stale index entry. No collapse
                // pass here: we are about to re-insert, so the item count is unchanged.
                DetachItem(item);
            }

            TopNode.AddItem(item, itemBox);
        }

        /// <summary>
        /// Re-places an item after its position or size changed; if it was never tracked
        /// it is added. Same throwing contract as AddItem for an item now outside the
        /// world - and a rejected move leaves the item where it was, tree unchanged.
        /// </summary>
        public void MoveItem(IMapObject3d item)
        {
            if (ObjectIndex.ContainsKey(item))
            {
                var current_node = ObjectIndex[item];
                var itemBox = item.BoundingBox;

                if (current_node.BoundingBox.Contains(itemBox))
                {
                    // still spatially inside its current node. If that node has children,
                    // the item may have shrunk enough to now fit entirely in one of them -
                    // push it down so a query against that child alone can prune to it.
                    if (current_node.IsSplit)
                    {
                        var target_leaf = current_node.FindContainingLeaf(itemBox);

                        if (target_leaf != null)
                        {
                            current_node.RemoveStoredItem(item);
                            target_leaf.AddItem(item, itemBox);
                        }
                    }

                    // otherwise it belongs exactly where it is (a leaf, or still
                    // straddling a child boundary) - nothing to do.
                    return;
                }

                // the item has to move. Reject an out-of-world / typeless target now,
                // before detaching anything, so a failed move leaves the tree intact.
                ValidateForInsert(item, itemBox);

                // still here? remove item entry from node list, then collapse any
                // now-underfull ancestors before re-inserting from the top.
                ObjectIndex.Remove(item);
                current_node.RemoveStoredItem(item);
                current_node.CollapseUpward();
            }

            // no longer fits or never existed. Yank it out and start from top.
            // cant assume that just going up a level in the tree is going to fit
            // as current bounding box may be way different than prior one.
            AddItem(item);
        }

        /// <summary>
        /// Removes an item from its node and the object index with no tree maintenance.
        /// Used by the AddItem update path, which re-inserts the item straight away.
        /// </summary>
        public bool DetachItem(IMapObject3d item)
        {
            if (!ObjectIndex.TryGetValue(item, out var node))
                return false;

            node.RemoveStoredItem(item);
            ObjectIndex.Remove(item);

            return true;
        }

        public bool RemoveItem(IMapObject3d item)
        {
            if (ObjectIndex.TryGetValue(item, out var node))
            {
                node.RemoveStoredItem(item);
                ObjectIndex.Remove(item);

                // pull any ancestors that are now underfull back into a single leaf
                node.CollapseUpward();

                return true;
            }
            else
            {
                // nothing found to remove
                return false;
            }
        }

        /// <summary>
        /// Removes every item from the tree. The world cube and MaxDepth are left as they
        /// are - any Resize() calls stay in effect, since the enlarged world is still the
        /// world; only the contents are cleared.
        /// </summary>
        public void Clear()
        {
            ObjectIndex.Clear();

            if (TopNode != null)
            {
                TopNode.RemoveAllLeafItems(true);

                // drop the now-empty subdivision so the tree starts fresh
                TopNode.Collapse();
            }
        }

        /// <summary>
        /// Fills <paramref name="itemsFound"/> with every unique item whose bounding box
        /// overlaps <paramref name="collisionBox"/> and whose object type matches the mask.
        /// Returns true if anything was found.
        /// </summary>
        public bool GetCollidingItems(Cube collisionBox, int objectTypes, ref List<IMapObject3d> itemsFound)
        {
            if (itemsFound == null)
                itemsFound = new List<IMapObject3d>();
            else
                itemsFound.Clear();

            TopNode.GetCollidingItems(collisionBox, objectTypes, ref itemsFound);

            return itemsFound.Count > 0;
        }

        /// <summary>
        /// Fills <paramref name="itemsFound"/> with every unique item whose bounding box
        /// overlaps <paramref name="collisionSphere"/> and whose object type matches the
        /// mask. Returns true if anything was found.
        /// </summary>
        public bool GetCollidingItems(Sphere collisionSphere, int objectTypes, ref List<IMapObject3d> itemsFound)
        {
            if (itemsFound == null)
                itemsFound = new List<IMapObject3d>();
            else
                itemsFound.Clear();

            TopNode.GetCollidingItems(collisionSphere, objectTypes, ref itemsFound);

            return itemsFound.Count > 0;
        }

        public override string ToString()
        {
            return $"Octree {WorldCube.Width} x {WorldCube.Height} x {WorldCube.Depth}, {ObjectIndex.Count} items";
        }
    }
}
