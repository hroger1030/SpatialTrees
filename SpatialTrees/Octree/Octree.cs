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
    [DebuggerDisplay("Octree {WorldCube.Width} x {WorldCube.Height} x {WorldCube.Depth}, {_ObjectIndex.Count} items")]
    public class Octree
    {
        protected readonly static int DEFAULT_MAX_DEPTH = 5;
        protected readonly static int DEFAULT_MAX_OBJECTS = 100;
        protected readonly static int DEFAULT_COLLECTION_SIZE = 1000;

        protected IDictionary<IMapObject3d, OctreeNode> _ObjectIndex;
        protected OctreeNode _TopNode;
        protected int _MaxDepth;
        protected int _MaxNodeObjects;

        public IDictionary<IMapObject3d, OctreeNode> ObjectIndex
        {
            get { return _ObjectIndex; }
        }

        public OctreeNode TopNode
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

        public Cube WorldCube
        {
            get { return _TopNode.BoundingBox; }
        }

        public Octree() : this(new Cube(0f, 0f, 0f, 1f, 1f, 1f), DEFAULT_MAX_DEPTH, DEFAULT_MAX_OBJECTS) { }

        public Octree(Cube volume) : this(volume, DEFAULT_MAX_DEPTH, DEFAULT_MAX_OBJECTS) { }

        public Octree(Cube boundingBox, int maxDepth, int maxObjects)
        {
            if (boundingBox == null)
                throw new Exception("Bounding Box cannot be null");

            if (maxDepth < 1)
                throw new Exception("Max depth must be greater than zero.");

            if (maxObjects < 1)
                throw new Exception("Max Objects must be greater than zero.");

            _ObjectIndex = new Dictionary<IMapObject3d, OctreeNode>(DEFAULT_COLLECTION_SIZE);
            _TopNode = new OctreeNode(this, null, boundingBox);
            _MaxDepth = maxDepth;
            _MaxNodeObjects = maxObjects;
        }

        /// <summary>
        /// Resizes world by adding a new top level node. Calling this will increase volume by 8x.
        /// Old top node becomes the upper left near node, since the cube keeps its X1,Y1,Z1 corner fixed.
        /// </summary>
        public bool Resize()
        {
            // create new bounding box. note cube keeps its X1,Y1,Z1 corner and grows along X2,Y2,Z2
            var new_boundingbox = new Cube(_TopNode.BoundingBox * 2);

            // save top object refrence
            var old_top_node = _TopNode;

            // replace upper left near branch of octree with old tree
            _TopNode = new OctreeNode(this, null, new_boundingbox);
            _MaxDepth++;

            // Generate new leaves
            _TopNode.Split();

            // replace old branches, then fix up the old subtree's parent link and its
            // now-stale cached depths (everything below it just dropped a level)
            _TopNode[(int)eOctant.UpperLeftNear] = old_top_node;
            old_top_node.Reparent(_TopNode);

            return true;
        }

        /// <summary>
        /// Attempts to add an item to the octree. Returns true if the item was added,
        /// false if the item faild to be added.
        /// </summary>
        public bool AddItem(IMapObject3d item)
        {
            // route and range-check off the same reference point: the tree places items by
            // BoundingBox.Center (see OctreeNode.FindOctant), so that is what has to be
            // inside the world, not the item's Location which may not track the box.
            if (!WorldCube.Contains(item.BoundingBox.Center))
                throw new ArgumentException($"{item.BoundingBox.Center} is outside the octree world cube {WorldCube}");

            if (item.ObjectTypes == 0)
                throw new Exception("Object w/o properties is being added:");

            if (_ObjectIndex.ContainsKey(item))
            {
                // already here, treat this as a move/update. Pull it out completely -
                // both the node list and the object index - so the re-add below starts
                // from a clean state instead of leaving a stale index entry. No collapse
                // pass here: we are about to re-insert, so the item count is unchanged.
                DetachItem(item);
            }

            return _TopNode.AddItem(item);
        }

        /// <summary>
        /// Moves item in tree. Does checks for collisions. This can be called if the
        /// bounding box has changed in size, too. Returns true if item was moved,
        /// false if item could not be moved.
        /// </summary>
        public bool MoveItem(IMapObject3d item)
        {
            if (_ObjectIndex.ContainsKey(item))
            {
                var current_node = _ObjectIndex[item];

                if (current_node.BoundingBox.Contains(item.BoundingBox))
                {
                    // we are still in the same node spatially.
                    return true;
                }
                else
                {
                    // still here? remove item entry from node list, then collapse any
                    // now-underfull ancestors before re-inserting from the top.
                    _ObjectIndex.Remove(item);
                    current_node.NodeItems.Remove(item);
                    current_node.CollapseUpward();
                }
            }

            // no longer fits or never existed. Yank it out and start from top.
            // cant assume that just going up a level in the tree is going to fit
            // as current bounding box may be way different than prior one.
            return AddItem(item);
        }

        /// <summary>
        /// Removes an item from its node and the object index with no tree maintenance.
        /// Used by the AddItem update path, which re-inserts the item straight away.
        /// </summary>
        public bool DetachItem(IMapObject3d item)
        {
            if (!_ObjectIndex.TryGetValue(item, out var node))
                return false;

            node.NodeItems.Remove(item);
            _ObjectIndex.Remove(item);

            return true;
        }

        public bool RemoveItem(IMapObject3d item)
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
        public bool GetCollidingItems(Cube collisionBox, int objectTypes, ref HashSet<IMapObject3d> itemsFound)
        {
            if (itemsFound == null)
                itemsFound = new HashSet<IMapObject3d>();
            else
                itemsFound.Clear();

            _TopNode.GetCollidingItems(collisionBox, objectTypes, ref itemsFound);

            return (itemsFound.Count > 0);
        }

        /// <summary>
        /// returns a list of unique items that are colliding with the item that is passed in.
        /// </summary>
        public bool GetCollidingItems(Sphere collisionSphere, int objectPoperties, ref HashSet<IMapObject3d> itemsFound)
        {
            if (itemsFound == null)
                itemsFound = new HashSet<IMapObject3d>();
            else
                itemsFound.Clear();

            _TopNode.GetCollidingItems(collisionSphere, objectPoperties, ref itemsFound);

            return (itemsFound.Count > 0);
        }

        public override string ToString()
        {
            return $"Octree {WorldCube.Width} x {WorldCube.Height} x {WorldCube.Depth}, {_ObjectIndex.Count} items";
        }
    }
}
