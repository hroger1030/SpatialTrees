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
    [DebuggerDisplay("Node depth: {Depth}, Center: {_BoundingBox.Center}, {GetChildObjectCount()} items")]
    public class QuadtreeNode
    {
        public const int LEAVES = 4;

        protected Quadtree _Quadtree;
        protected QuadtreeNode _Parent;
        protected QuadtreeNode[] _Leaves;
        protected Rectangle _BoundingBox;
        protected HashSet<IMapObject> _NodeItems;

        public Rectangle BoundingBox
        {
            get { return _BoundingBox; }
        }

        public HashSet<IMapObject> NodeItems
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
            _NodeItems = new HashSet<IMapObject>();
        }

        /// <summary>
        /// Attempts to add an item to the quadtree. Returns true if the item was added,
        /// false if the item faild to be added.
        /// </summary>
        public bool AddItem(IMapObject mapItem)
        {
            if (_NodeItems.Contains(mapItem))
                return false;

            if (_Leaves == null)
            {
                if (_NodeItems.Count > _Quadtree.MaxNodeObjects && this.Depth < _Quadtree.MaxDepth)
                {
                    Split();

                    eQuadrant quadrant;

                    foreach (var item in _NodeItems)
                    {
                        quadrant = FindQuadrant(_BoundingBox.Center, item.BoundingBox.Center);
                        _Leaves[(int)quadrant].AddItem(item);
                    }

                    _NodeItems.Clear();

                    quadrant = FindQuadrant(_BoundingBox.Center, mapItem.BoundingBox.Center);
                    _Leaves[(int)quadrant].AddItem(mapItem);
                }
                else
                {
                    _NodeItems.Add(mapItem);

                    if (_Quadtree.ObjectIndex.ContainsKey(mapItem))
                        _Quadtree.ObjectIndex[mapItem] = this;
                    else
                        _Quadtree.ObjectIndex.Add(mapItem, this);
                }

                return true;
            }
            else
            {
                eQuadrant quadrant = FindQuadrant(_BoundingBox.Center, mapItem.BoundingBox.Center);
                return _Leaves[(int)quadrant].AddItem(mapItem);
            }
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
        public void GetCollidingItems(Rectangle collisionBox, int objectTypes, ref HashSet<IMapObject> itemsFound)
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
                        if (collisionBox.Intersects(item.BoundingBox) && ((objectTypes & item.ObjectTypes) == item.ObjectTypes))
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
        public void GetCollidingItems(Circle collisionCircle, int objectTypes, ref HashSet<IMapObject> itemsFound)
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
                        if (collisionCircle.Intersects(item.BoundingBox) && ((objectTypes & item.ObjectTypes) == item.ObjectTypes))
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
