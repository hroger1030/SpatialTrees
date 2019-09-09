//////////////////////////////////////////////////////////////////////////////////
// Copyright (C) Global Conquest Games, LLC - All Rights Reserved               //
// Unauthorized copying of this file, via any medium is strictly prohibited     //
// Proprietary and confidential                                                 //
//////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Geometry;

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
    /// </summary>
    [DebuggerDisplay("Quadtree {WorldRectangle.Width} x {WorldRectangle.Height}, {_ObjectIndex.Count} items")]
    public class Quadtree
    {
        protected readonly static int DEFAULT_MAX_DEPTH = 5;
        protected readonly static int DEFAULT_MAX_OBJECTS = 100;
        protected readonly static int DEFAULT_COLLECTION_SIZE = 1000;

        protected IDictionary<IMapObject, QuadtreeNode> _ObjectIndex;
        protected QuadtreeNode _TopNode;
        protected int _MaxDepth;
        protected int _MaxNodeObjects;
        protected object _LockObject;

        public IDictionary<IMapObject, QuadtreeNode> ObjectIndex
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

        public Quadtree() : this(new Rectangle(), DEFAULT_MAX_DEPTH, DEFAULT_MAX_OBJECTS) { }

        public Quadtree(Rectangle area) : this(area, DEFAULT_MAX_DEPTH, DEFAULT_MAX_OBJECTS) { }

        public Quadtree(Rectangle bounding_box, int max_depth, int max_objects)
        {
            if (bounding_box == null)
                throw new Exception("Bounding Box cannot be null");

            if (max_depth < 1)
                throw new Exception("Max depth must be greater than zero.");

            if (max_objects < 1)
                throw new Exception("Max Objects must be greater than zero.");

            _ObjectIndex = new Dictionary<IMapObject, QuadtreeNode>(DEFAULT_COLLECTION_SIZE);
            _TopNode = new QuadtreeNode(this, null, bounding_box);
            _MaxDepth = max_depth;
            _MaxNodeObjects = max_objects;
            _LockObject = new object();
        }

        /// <summary>
        /// Resizes world by adding a new top level node. Calling this will increase map by 4x. 
        /// Old top node becomes bottom left node, since rectangle is screen oriented.
        /// </summary>
        public bool Resize()
        {
            lock (_LockObject)
            {
                // create new bounding box. note rectangle is scaled down and to the right
                Rectangle new_boundingbox = new Rectangle(_TopNode.BoundingBox * 2);

                // save top object refrence
                var old_top_node = _TopNode;

                // replace bottom right branch of quadtree with old tree
                _TopNode = new QuadtreeNode(this, null, new_boundingbox);
                _MaxDepth++;

                // Generate new leaves
                _TopNode.Split();

                // replace old branches
                _TopNode[(int)eQuadrant.UpperLeftQuadrant] = old_top_node;

                return true;
            }
        }

        /// <summary>
        /// Attempts to add an item to the quadtree. Returns true if the item was added,
        /// false if the item faild to be added.
        /// </summary>
        /// 
        public bool AddItem(IMapObject item)
        {
            if (!WorldRectangle.Contains(item.Location))
                throw new ArgumentException($"{item.Location} is outside the quadtree world rectangle {WorldRectangle}");

            if (item.ObjectTypes == 0)
                throw new Exception("Object w/o properties is being added:");

            if (_ObjectIndex.ContainsKey(item))
            {
                // already here, treat this as a move/update
                _ObjectIndex[item].NodeItems.Remove(item);
            }

            return _TopNode.AddItem(item);
        }

        /// <summary>
        /// Moves item in tree. Does checks for collisions. This can be called if the 
        /// bounding box has changed in size, too. Returns true if item was moved,
        /// false if item could not be moved.
        /// </summary>
        public bool MoveItem(IMapObject item)
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
                    // still here? remove item entry from node list
                    _ObjectIndex.Remove(item);
                    current_node.NodeItems.Remove(item);
                }
            }

            // no longer fits or never existed. Yank it out and start from top.
            // cant assume that just going up a level in the tree is going to fit
            // as current bounding box may be way different than prior one.
            return AddItem(item);
        }

        public bool RemoveItem(IMapObject item)
        {
            if (_ObjectIndex.ContainsKey(item))
            {
                _ObjectIndex[item].NodeItems.Remove(item);
                _ObjectIndex.Remove(item);

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
                _TopNode.RemoveAllLeafItems(true);
        }

        /// <summary>
        /// returns a list of unique items that are colliding with the item that is passed in.
        /// </summary>
        public bool GetCollidingItems(Rectangle collisionBox, int objectTypes, ref HashSet<IMapObject> itemsFound)
        {
            if (itemsFound == null)
                itemsFound = new HashSet<IMapObject>();
            else
                itemsFound.Clear();

            _TopNode.GetCollidingItems(collisionBox, objectTypes, ref itemsFound);

            return (itemsFound.Count > 0);
        }

        /// <summary>
        /// returns a list of unique items that are colliding with the item that is passed in.
        /// </summary>
        public bool GetCollidingItems(Circle collisionCircle, int objectPoperties, ref HashSet<IMapObject> itemsFound)
        {
            if (itemsFound == null)
                itemsFound = new HashSet<IMapObject>();
            else
                itemsFound.Clear();

            _TopNode.GetCollidingItems(collisionCircle, objectPoperties, ref itemsFound);

            return (itemsFound.Count > 0);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (GetType() != obj.GetType()) return false;

            var new_obj = (Quadtree)obj;
            return Equals(new_obj);
        }

        public bool Equals(Quadtree obj)
        {
            return _TopNode.Equals(obj._TopNode);
        }

        public override int GetHashCode()
        {
            return _TopNode.GetHashCode();
        }

        public override string ToString()
        {
            return $"Quadtree {WorldRectangle.Width} x {WorldRectangle.Height}, {_ObjectIndex.Count} items";
        }
    }
}
