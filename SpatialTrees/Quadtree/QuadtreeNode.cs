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
    [DebuggerDisplay("Node depth: {Depth}, Center: {_BoundingBox.Center}, {GetChildObjectCount()} items")]
    public class QuadtreeNode
    {
        public const int LEAVES = 4;

        protected Quadtree _Quadtree;
        protected QuadtreeNode _Parent;
        protected QuadtreeNode[] _Leaves;
        protected Rectangle _BoundingBox;
        protected HashSet<IMapObject2d> _NodeItems;

        public Rectangle BoundingBox
        {
            get { return _BoundingBox; }
        }

        public HashSet<IMapObject2d> NodeItems
        {
            get { return _NodeItems; }
        }

        public int Depth
        {
            get
            {
                int depth = 1;
                QuadtreeNode current_node = this;

                while (current_node._Parent != null)
                {
                    current_node = current_node._Parent;
                    depth++;
                }

                return depth;
            }
        }

        public QuadtreeNode this[int i]
        {
            get
            {
                if (i > -1 && i < LEAVES)
                {
                    return _Leaves[i];
                }
                else
                {
                    throw new IndexOutOfRangeException("QuadtreePointNode " + i.ToString() + " does not exist.");
                }
            }
            set
            {
                if (i > -1 && i < LEAVES)
                {
                    _Leaves[i] = value;
                }
                else
                {
                    throw new IndexOutOfRangeException("QuadtreePointNode " + i.ToString() + " does not exist.");
                }
            }
        }

        public QuadtreeNode(Quadtree quadtree, QuadtreeNode parent, Rectangle bounding_box)
        {
            _Quadtree = quadtree;
            _Parent = parent;
            _Leaves = null;
            _BoundingBox = bounding_box;
            _NodeItems = new HashSet<IMapObject2d>();
        }

        /// <summary>
        /// Attempts to add an item to the quadtree. Returns true if the item was added,
        /// false if the item faild to be added.
        /// </summary>
        public bool AddItem(IMapObject2d mapItem)
        {
            if (_NodeItems.Contains(mapItem))
                return false;

            if (_Leaves == null)
            {
                if (_NodeItems.Count > _Quadtree.MaxNodeObjects && this.Depth < _Quadtree.MaxDepth)
                {
                    Split();

                    // redistribute existing items into the new leaves. An item whose
                    // bounding box does not fit entirely inside a single child straddles
                    // a quadrant boundary and has to stay on this node, so pull everything
                    // off first and let RouteItem decide where each one lands.
                    var items_to_route = new List<IMapObject2d>(_NodeItems);
                    _NodeItems.Clear();

                    foreach (var item in items_to_route)
                        RouteItem(item);

                    RouteItem(mapItem);
                }
                else
                {
                    StoreItem(mapItem);
                }

                return true;
            }
            else
            {
                return RouteItem(mapItem);
            }
        }

        /// <summary>
        /// Routes an item into the child leaf that fully contains its bounding box. If no
        /// single child contains it (the item straddles a quadrant boundary) the item is
        /// stored on this node instead, so that collision queries touching only one of the
        /// neighbouring quadrants still find it. Assumes this node has been split.
        /// </summary>
        public bool RouteItem(IMapObject2d mapItem)
        {
            QuadtreeNode leaf = FindContainingLeaf(mapItem);

            if (leaf == null)
            {
                StoreItem(mapItem);
                return true;
            }

            return leaf.AddItem(mapItem);
        }

        /// <summary>
        /// Returns the child leaf whose bounding box fully contains the item's bounding
        /// box, or null when the item straddles a quadrant boundary. Assumes this node
        /// has been split.
        /// </summary>
        public QuadtreeNode FindContainingLeaf(IMapObject2d mapItem)
        {
            eQuadrant quadrant = FindQuadrant(_BoundingBox.Center, mapItem.BoundingBox.Center);
            QuadtreeNode leaf = _Leaves[(int)quadrant];

            if (leaf.BoundingBox.Contains(mapItem.BoundingBox))
                return leaf;

            return null;
        }

        /// <summary>
        /// Stores an item directly on this node and points the tree's object index at
        /// this node.
        /// </summary>
        public void StoreItem(IMapObject2d mapItem)
        {
            _NodeItems.Add(mapItem);

            if (_Quadtree.ObjectIndex.ContainsKey(mapItem))
                _Quadtree.ObjectIndex[mapItem] = this;
            else
                _Quadtree.ObjectIndex.Add(mapItem, this);
        }

        public void RemoveAllLeafItems(bool recursive)
        {
            _NodeItems.Clear();

            if (recursive && _Leaves != null)
            {
                foreach (var leaf in _Leaves)
                {
                    if (leaf != null)
                        leaf.RemoveAllLeafItems(true);
                }
            }
        }

        /// <summary>
        /// returns a list of unique items that are colliding with the item that is passed in.
        /// </summary>
        public void GetCollidingItems(Rectangle collisionBox, int objectTypes, ref HashSet<IMapObject2d> itemsFound)
        {
            if (!_BoundingBox.Intersects(collisionBox))
                return;

            if (_NodeItems.Count > 0)
            {
                if (collisionBox.Contains(_BoundingBox))
                {
                    foreach (var item in _NodeItems)
                    {
                        if ((objectTypes & item.ObjectTypes) == objectTypes)
                        {
                            itemsFound.Add(item);
                        }
                    }
                }
                else
                {
                    // test each item in this node
                    foreach (var item in _NodeItems)
                    {
                        if (collisionBox.Intersects(item.BoundingBox) && ((objectTypes & item.ObjectTypes) == objectTypes))
                        {
                            itemsFound.Add(item);
                        }
                    }
                }
            }

            if (_Leaves != null)
            {
                foreach (var leaf in _Leaves)
                {
                    if (leaf != null)
                        leaf.GetCollidingItems(collisionBox, objectTypes, ref itemsFound);
                }
            }
        }

        /// <summary>
        /// returns a list of unique items that are colliding with the item that is passed in.
        /// </summary>
        public void GetCollidingItems(Circle collisionCircle, int objectTypes, ref HashSet<IMapObject2d> itemsFound)
        {
            if (!_BoundingBox.Intersects(collisionCircle))
                return;

            if (_NodeItems.Count > 0)
            {
                if (collisionCircle.Contains(_BoundingBox))
                {
                    foreach (var item in _NodeItems)
                    {
                        if ((objectTypes & item.ObjectTypes) == objectTypes)
                        {
                            itemsFound.Add(item);
                        }
                    }
                }
                else
                {
                    // test each item in this node
                    foreach (var item in _NodeItems)
                    {
                        if (collisionCircle.Intersects(item.BoundingBox) && ((objectTypes & item.ObjectTypes) == objectTypes))
                        {
                            itemsFound.Add(item);
                        }
                    }
                }
            }

            if (_Leaves != null)
            {
                foreach (var leaf in _Leaves)
                {
                    if (leaf != null)
                        leaf.GetCollidingItems(collisionCircle, objectTypes, ref itemsFound);
                }
            }
        }

        public void Split()
        {
            if (_Leaves != null)
                throw new Exception("Node already split");

            _Leaves = new QuadtreeNode[LEAVES];

            float new_width = _BoundingBox.Width / 2;
            float new_height = _BoundingBox.Height / 2;

            _Leaves[(int)eQuadrant.UpperRightQuadrant] = new QuadtreeNode(_Quadtree, this, new Rectangle(_BoundingBox.Center.X, _BoundingBox.Top, new_width, new_height));
            _Leaves[(int)eQuadrant.LowerRightQuadrant] = new QuadtreeNode(_Quadtree, this, new Rectangle(_BoundingBox.Center.X, _BoundingBox.Center.Y, new_width, new_height));
            _Leaves[(int)eQuadrant.LowerLeftQuadrant] = new QuadtreeNode(_Quadtree, this, new Rectangle(_BoundingBox.Left, _BoundingBox.Center.Y, new_width, new_height));
            _Leaves[(int)eQuadrant.UpperLeftQuadrant] = new QuadtreeNode(_Quadtree, this, new Rectangle(_BoundingBox.Left, _BoundingBox.Top, new_width, new_height));
        }

        public int GetChildObjectCount()
        {
            int total = _NodeItems.Count;

            if (_Leaves != null)
            {
                foreach (var leaf in _Leaves)
                {
                    if (leaf != null)
                        total += leaf.GetChildObjectCount();
                }
            }

            return total;
        }

        protected eQuadrant FindQuadrant(Point2 boundingBoxCenter, Point2 point)
        {
            if (point.X > boundingBoxCenter.X)
            {
                if (point.Y > boundingBoxCenter.Y)
                    return eQuadrant.LowerRightQuadrant;
                else
                    return eQuadrant.UpperRightQuadrant;
            }
            else
            {
                if (point.Y > boundingBoxCenter.Y)
                    return eQuadrant.LowerLeftQuadrant;
                else
                    return eQuadrant.UpperLeftQuadrant;
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (GetType() != obj.GetType()) return false;

            var new_obj = (QuadtreeNode)obj;
            return Equals(new_obj);
        }

        public bool Equals(QuadtreeNode obj)
        {
            return GetHashCode() == obj.GetHashCode();
        }

        public override int GetHashCode()
        {
            return _BoundingBox.GetHashCode();
        }

        public override string ToString()
        {
            return $"Node depth: {Depth}, Center: {_BoundingBox.Center}, {GetChildObjectCount()} items";
        }
    }
}
