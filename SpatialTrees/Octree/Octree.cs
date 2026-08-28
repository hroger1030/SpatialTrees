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
    /// This object supports non-balanced nodes.
    [DebuggerDisplay("Octree {WorldCube.Width} x {WorldCube.Height} x {WorldCube.Depth}, {ObjectIndex.Count} items")]
    public class Octree
    {
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

        public Octree() : this(Cube.UnitCube, DEFAULT_MAX_DEPTH, DEFAULT_MAX_OBJECTS) { }

        public Octree(Cube volume) : this(volume, DEFAULT_MAX_DEPTH, DEFAULT_MAX_OBJECTS) { }

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

        public static Octree Build(Cube boundingBox, int maxDepth, int maxObjects, IReadOnlyCollection<IMapObject3d> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            var tree = new Octree(boundingBox, maxDepth, maxObjects, expectedItemCount: items.Count);

            if (items.Count == 0)
                return tree;

            // two ping-pong buffers (each level partitions one into the other) plus a
            // one-byte-per-item class cache so the scatter pass skips re-doing the
            // geometry. 
            var front = new IMapObject3d[items.Count];
            var back = new IMapObject3d[items.Count];
            var buckets = new byte[items.Count];

            int n = 0;
            foreach (var item in items)
            {
                tree.ValidateForInsert(item);
                front[n++] = item;
            }

            tree.TopNode.BulkLoad(front, back, buckets, 0, n);

            return tree;
        }

        /// <summary>
        /// As <see cref="Build(Cube, int, int, IReadOnlyCollection{IMapObject3d})"/>
        /// using the default depth and per-node object limits.
        /// </summary>
        public static Octree Build(Cube boundingBox, IReadOnlyCollection<IMapObject3d> items)
        {
            return Build(boundingBox, DEFAULT_MAX_DEPTH, DEFAULT_MAX_OBJECTS, items);
        }

        /// <summary>
        /// Resizes world by adding a new top level node. Calling this will increase volume by 8x.
        /// Old top node becomes the upper left near node, since the cube keeps its X1,Y1,Z1 corner fixed.
        /// </summary>
        public void Resize()
        {
            var newBoundingbox = new Cube(TopNode.BoundingBox * 2);
            var oldTopNode = TopNode;

            TopNode = new OctreeNode(this, null, newBoundingbox);
            MaxDepth++;

            TopNode.Split();
            TopNode[(int)eOctant.UpperLeftNear] = oldTopNode;
            oldTopNode.Reparent(TopNode);
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
                    if (current_node.IsSplit)
                    {
                        var target_leaf = current_node.FindContainingLeaf(itemBox);

                        if (target_leaf != null)
                        {
                            current_node.RemoveStoredItem(item);
                            target_leaf.AddItem(item, itemBox);
                        }
                    }
                    return;
                }

                ValidateForInsert(item, itemBox);
                ObjectIndex.Remove(item);
                current_node.RemoveStoredItem(item);
                current_node.CollapseUpward();
            }

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
                node.CollapseUpward();

                return true;
            }
            else
            {
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
