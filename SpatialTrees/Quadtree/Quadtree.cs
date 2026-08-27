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
    /// note that this object supports non-balanced nodes.
    ///
    /// THREADING: this class is not thread safe and is intended for single-threaded
    /// use only. Callers that touch it from multiple threads must serialize access
    /// themselves. Thread-safe variants of both spatial trees are planned.
    /// </summary>
    [DebuggerDisplay("Quadtree {WorldRectangle.Width} x {WorldRectangle.Height}, {_ObjectIndex.Count} items")]
    public class Quadtree
    {
        protected readonly static int DEFAULT_MAX_DEPTH = 5;
        protected readonly static int DEFAULT_MAX_OBJECTS = 100;
        protected readonly static int DEFAULT_COLLECTION_SIZE = 1000;

        protected IDictionary<IMapObject2d, QuadtreeNode> _ObjectIndex;
        protected QuadtreeNode _TopNode;
        protected int _MaxDepth;
        protected int _MaxNodeObjects;

        public IDictionary<IMapObject2d, QuadtreeNode> ObjectIndex
        {
            get { return _ObjectIndex; }
        }

        public QuadtreeNode TopNode
        {
            get { return _TopNode; }
        }

        public int MaxDepth
        {
            get { return _MaxDepth; }
        }

        public int MaxNodeObjects
        {
            get { return _MaxNodeObjects; }
        }

        public Rectangle WorldRectangle
        {
            get { return _TopNode.BoundingBox; }
        }

        // default world is the unit rectangle (0,0)-(1,1); Octree() mirrors this with the unit cube
        public Quadtree() : this(new Rectangle(0f, 0f, 1f, 1f), DEFAULT_MAX_DEPTH, DEFAULT_MAX_OBJECTS) { }

        public Quadtree(Rectangle area) : this(area, DEFAULT_MAX_DEPTH, DEFAULT_MAX_OBJECTS) { }

        public Quadtree(Rectangle boundingBox, int maxDepth, int maxObjects)
        {
            ArgumentNullException.ThrowIfNull(boundingBox);
            ArgumentOutOfRangeException.ThrowIfLessThan(maxDepth, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(maxObjects, 1);

            _ObjectIndex = new Dictionary<IMapObject2d, QuadtreeNode>(DEFAULT_COLLECTION_SIZE);
            _TopNode = new QuadtreeNode(this, null, boundingBox);
            _MaxDepth = maxDepth;
            _MaxNodeObjects = maxObjects;
        }

        /// <summary>
        /// Resizes world by adding a new top level node. Calling this will increase map by 4x.
        /// Old top node becomes the upper left node, since rectangle is screen oriented.
        /// </summary>
        public void Resize()
        {
            // create new bounding box. note rectangle keeps its top-left corner and grows down and to the right
            var new_boundingbox = new Rectangle(_TopNode.BoundingBox * 2);

            // save top object refrence
            var old_top_node = _TopNode;

            // replace upper left branch of quadtree with old tree
            _TopNode = new QuadtreeNode(this, null, new_boundingbox);
            _MaxDepth++;

            // Generate new leaves
            _TopNode.Split();

            // replace old branches, then fix up the old subtree's parent link and its
            // now-stale cached depths (everything below it just dropped a level)
            _TopNode[(int)eQuadrant.UpperLeftQuadrant] = old_top_node;
            old_top_node.Reparent(_TopNode);
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
            // route and range-check off the same reference point: the tree places items by
            // BoundingBox.Center (see QuadtreeNode.FindQuadrant), so that is what has to be
            // inside the world, not the item's Location which may not track the box.
            if (!WorldRectangle.Contains(item.BoundingBox.Center))
                throw new ArgumentException($"{item.BoundingBox.Center} is outside the quadtree world rectangle {WorldRectangle}", nameof(item));

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
            ValidateForInsert(item);

            if (_ObjectIndex.ContainsKey(item))
            {
                // already here, treat this as a move/update. Pull it out completely -
                // both the node list and the object index - so the re-add below starts
                // from a clean state instead of leaving a stale index entry. No collapse
                // pass here: we are about to re-insert, so the item count is unchanged.
                DetachItem(item);
            }

            _TopNode.AddItem(item);
        }

        /// <summary>
        /// Re-places an item after its position or size changed; if it was never tracked
        /// it is added. Same throwing contract as AddItem for an item now outside the
        /// world - and a rejected move leaves the item where it was, tree unchanged.
        /// </summary>
        public void MoveItem(IMapObject2d item)
        {
            if (_ObjectIndex.ContainsKey(item))
            {
                var current_node = _ObjectIndex[item];

                if (current_node.BoundingBox.Contains(item.BoundingBox))
                {
                    // still spatially inside its current node. If that node has children,
                    // the item may have shrunk enough to now fit entirely in one of them -
                    // push it down so a query against that child alone can prune to it.
                    if (current_node.IsSplit)
                    {
                        var target_leaf = current_node.FindContainingLeaf(item);

                        if (target_leaf != null)
                        {
                            current_node.NodeItems.Remove(item);
                            target_leaf.AddItem(item);
                        }
                    }

                    // otherwise it belongs exactly where it is (a leaf, or still
                    // straddling a child boundary) - nothing to do.
                    return;
                }

                // the item has to move. Reject an out-of-world / typeless target now,
                // before detaching anything, so a failed move leaves the tree intact.
                ValidateForInsert(item);

                // still here? remove item entry from node list, then collapse any
                // now-underfull ancestors before re-inserting from the top.
                _ObjectIndex.Remove(item);
                current_node.NodeItems.Remove(item);
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
        public bool DetachItem(IMapObject2d item)
        {
            if (!_ObjectIndex.TryGetValue(item, out var node))
                return false;

            node.NodeItems.Remove(item);
            _ObjectIndex.Remove(item);

            return true;
        }

        public bool RemoveItem(IMapObject2d item)
        {
            if (_ObjectIndex.TryGetValue(item, out var node))
            {
                node.NodeItems.Remove(item);
                _ObjectIndex.Remove(item);

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
        /// Removes every item from the tree. The world rectangle and MaxDepth are left as
        /// they are - any Resize() calls stay in effect, since the enlarged world is still
        /// the world; only the contents are cleared.
        /// </summary>
        public void Clear()
        {
            _ObjectIndex.Clear();

            if (_TopNode != null)
            {
                _TopNode.RemoveAllLeafItems(true);

                // drop the now-empty subdivision so the tree starts fresh
                _TopNode.Collapse();
            }
        }

        /// <summary>
        /// returns a list of unique items that are colliding with the item that is passed in.
        /// </summary>
        public bool GetCollidingItems(Rectangle collisionBox, int objectTypes, ref HashSet<IMapObject2d> itemsFound)
        {
            if (itemsFound == null)
                itemsFound = new HashSet<IMapObject2d>();
            else
                itemsFound.Clear();

            _TopNode.GetCollidingItems(collisionBox, objectTypes, ref itemsFound);

            return (itemsFound.Count > 0);
        }

        /// <summary>
        /// returns a list of unique items that are colliding with the item that is passed in.
        /// </summary>
        public bool GetCollidingItems(Circle collisionCircle, int objectPoperties, ref HashSet<IMapObject2d> itemsFound)
        {
            if (itemsFound == null)
                itemsFound = new HashSet<IMapObject2d>();
            else
                itemsFound.Clear();

            _TopNode.GetCollidingItems(collisionCircle, objectPoperties, ref itemsFound);

            return (itemsFound.Count > 0);
        }

        public override string ToString()
        {
            return $"Quadtree {WorldRectangle.Width} x {WorldRectangle.Height}, {_ObjectIndex.Count} items";
        }
    }
}
