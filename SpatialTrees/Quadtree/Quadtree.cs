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

namespace SpatialTrees.Quadtrees
{
    /// <summary>
    /// Quadtree represents a two dimensional tree structure, and all the
    /// objects that it contains. We will assume that we are working off of
    /// a screen co-ordinate centric system. (0,0 is in top left)
    ///
    /// The quadants are numbered looking on the plane from the top, 0,1,2,3 in
    /// a clockwise fashion from the top right quadrant.
    ///
    /// The quadants are stored in an leaf array, with the index of a given quadrant
    /// as _leaf[quadants_index-1]
    ///
    /// This object supports non-balanced nodes.
    /// </summary>
    [DebuggerDisplay("Quadtree {WorldRectangle.Width} x {WorldRectangle.Height}, {ObjectIndex.Count} items")]
    public class Quadtree
    {
        private const int DEFAULT_MAX_DEPTH = 8;
        private const int DEFAULT_MAX_OBJECTS = 16;
        private const int DEFAULT_COLLECTION_SIZE = 1000;

        public Dictionary<IMapObject2d, QuadtreeNode> ObjectIndex { get; protected set; }
        public QuadtreeNode TopNode { get; protected set; }
        public int MaxDepth { get; protected set; }
        public int MaxNodeObjects { get; protected set; }

        public Rectangle WorldRectangle
        {
            get { return TopNode.BoundingBox; }
        }

        public Quadtree() : this(Rectangle.UnitRectangle, DEFAULT_MAX_DEPTH, DEFAULT_MAX_OBJECTS) { }

        public Quadtree(Rectangle area) : this(area, DEFAULT_MAX_DEPTH, DEFAULT_MAX_OBJECTS) { }

        public Quadtree(Rectangle boundingBox, int maxDepth, int maxObjects, int expectedItemCount = 0)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxDepth, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(maxObjects, 1);
            ArgumentOutOfRangeException.ThrowIfNegative(expectedItemCount);

            // the tree keys items by reference identity (a bulk build even requires
            // distinct references), so pin the comparer to reference equality instead of
            // letting a caller's Equals/GetHashCode override run on every index operation.
            ObjectIndex = new Dictionary<IMapObject2d, QuadtreeNode>(expectedItemCount > 0 ? expectedItemCount : DEFAULT_COLLECTION_SIZE, ReferenceEqualityComparer.Instance);
            TopNode = new QuadtreeNode(this, null, boundingBox);
            MaxDepth = maxDepth;
            MaxNodeObjects = maxObjects;
        }

        public static Quadtree Build(Rectangle boundingBox, int maxDepth, int maxObjects, IReadOnlyCollection<IMapObject2d> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            int count = items.Count;
            var tree = new Quadtree(boundingBox, maxDepth, maxObjects, count);

            if (count == 0)
                return tree;

            // two ping-pong buffers (each level partitions one into the other) plus a
            // one-byte-per-item class cache so the scatter pass skips re-doing the
            // geometry.
            var front = new IMapObject2d[count];
            var back = new IMapObject2d[count];
            var buckets = new byte[count];

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
        /// As <see cref="Build(Rectangle, int, int, IReadOnlyCollection{IMapObject2d})"/>
        /// using the default depth and per-node object limits.
        /// </summary>
        public static Quadtree Build(Rectangle boundingBox, IReadOnlyCollection<IMapObject2d> items)
        {
            return Build(boundingBox, DEFAULT_MAX_DEPTH, DEFAULT_MAX_OBJECTS, items);
        }

        /// <summary>
        /// Resizes world by adding a new top level node. Calling this will increase map by 4x.
        /// Old top node becomes the upper left node, since rectangle is screen oriented.
        /// </summary>
        public void Resize()
        {
            var newBoundingbox = new Rectangle(TopNode.BoundingBox * 2);
            var oldTopNode = TopNode;

            TopNode = new QuadtreeNode(this, null, newBoundingbox);
            MaxDepth++;

            TopNode.Split();
            TopNode[(int)eQuadrant.UpperLeftQuadrant] = oldTopNode;
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
        public void ValidateForInsert(IMapObject2d item)
        {
            ValidateForInsert(item, item.BoundingBox);
        }

        /// <summary>
        /// As <see cref="ValidateForInsert(IMapObject2d)"/>, but takes the item's bounding
        /// box so a caller that already has it does not re-read the property.
        /// </summary>
        public void ValidateForInsert(IMapObject2d item, Rectangle itemBox)
        {
            var center = itemBox.Center;

            if (!WorldRectangle.Contains(center))
                throw new ArgumentException($"{center} is outside the quadtree world rectangle {WorldRectangle}", nameof(item));

            if (item.ObjectTypes == 0)
                throw new ArgumentException($"{item} has no object type flags set and could never be returned by a query", nameof(item));
        }

        /// <summary>
        /// Adds an item to the quadtree, or re-places it if it is already present. The
        /// operation always succeeds unless it throws ArgumentException: the item's
        /// bounding-box centre is outside the world, or it has no object type bits set.
        /// </summary>
        public void AddItem(IMapObject2d item)
        {
            var itemBox = item.BoundingBox;
            ValidateForInsert(item, itemBox);

            // DetachItem is a no-op (single TryGetValue) when the item is not already
            // indexed, so it does not need a ContainsKey guard in front of it.
            DetachItem(item);

            TopNode.AddItem(item, itemBox);
        }

        /// <summary>
        /// Replaces an item after its position or size changed; if it was never tracked
        /// it is added. Same throwing contract as AddItem for an item now outside the
        /// world and a rejected move leaves the item where it was, tree unchanged.
        /// </summary>
        public void MoveItem(IMapObject2d item)
        {
            if (ObjectIndex.TryGetValue(item, out var current_node))
            {
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
        public bool DetachItem(IMapObject2d item)
        {
            if (!ObjectIndex.TryGetValue(item, out var node))
                return false;

            node.RemoveStoredItem(item);
            ObjectIndex.Remove(item);

            return true;
        }

        public bool RemoveItem(IMapObject2d item)
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
                // nothing found to remove
                return false;
            }
        }

        /// <summary>
        /// Removes every item from the tree. The world rectangle and MaxDepth are left as
        /// they are - any Resize() calls stay in effect, since the enlarged world is still
        /// the world; only the contents are cleared.
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
        public bool GetCollidingItems(Rectangle collisionBox, int objectTypes, ref List<IMapObject2d> itemsFound)
        {
            if (itemsFound == null)
                itemsFound = new List<IMapObject2d>();
            else
                itemsFound.Clear();

            TopNode.GetCollidingItems(collisionBox, objectTypes, ref itemsFound);

            return itemsFound.Count > 0;
        }

        /// <summary>
        /// Fills <paramref name="itemsFound"/> with every unique item whose bounding box
        /// overlaps <paramref name="collisionCircle"/> and whose object type matches the
        /// mask. Returns true if anything was found.
        /// </summary>
        public bool GetCollidingItems(Circle collisionCircle, int objectTypes, ref List<IMapObject2d> itemsFound)
        {
            if (itemsFound == null)
                itemsFound = new List<IMapObject2d>();
            else
                itemsFound.Clear();

            TopNode.GetCollidingItems(collisionCircle, objectTypes, ref itemsFound);

            return itemsFound.Count > 0;
        }

        public override string ToString()
        {
            return $"Quadtree {WorldRectangle.Width} x {WorldRectangle.Height}, {ObjectIndex.Count} items";
        }
    }
}
