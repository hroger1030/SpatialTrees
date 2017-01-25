//////////////////////////////////////////////////////////////////////////////////
// Copyright (C) Global Conquest Games, LLC - All Rights Reserved               //
// Unauthorized copying of this file, via any medium is strictly prohibited     //
// Proprietary and confidential                                                 //
//////////////////////////////////////////////////////////////////////////////////

using Geometry;
using System;
using System.Collections.Generic;

namespace SpacialTrees
{
    public class QuadtreeNode
    {
        private static readonly int LEAVES = 4;

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
        public bool AddItem(IMapObject new_item)
        {
            if (_NodeItems.Contains(new_item))
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

                    quadrant = FindQuadrant(_BoundingBox.Center, new_item.BoundingBox.Center);
                    _Leaves[(int)quadrant].AddItem(new_item);
                }
                else
                {
                    _NodeItems.Add(new_item);

                    if (_Quadtree.ObjectIndex.ContainsKey(new_item))
                        _Quadtree.ObjectIndex[new_item] = this;
                    else
                        _Quadtree.ObjectIndex.Add(new_item, this);
                }

                return true;
            }
            else
            {
                eQuadrant quadrant = FindQuadrant(_BoundingBox.Center, new_item.BoundingBox.Center);
                return _Leaves[(int)quadrant].AddItem(new_item);
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
        public void GetCollidingItems(Rectangle collision_box, ref HashSet<IMapObject> items_found)
        {
            if (!_BoundingBox.Intersects(collision_box))
                return;

            if (collision_box.Contains(_BoundingBox))
            {
                foreach (var item in _NodeItems)
                {
                    items_found.Add(item);
                }
            }
            else
            {
                // test each item in this node
                foreach (var item in _NodeItems)
                {
                    if (collision_box.Intersects(item.BoundingBox))
                    {
                        items_found.Add(item);
                    }
                }
            }

            if (_Leaves != null)
            {
                foreach (var leaf in _Leaves)
                {
                    if (leaf != null)
                        leaf.GetCollidingItems(collision_box, ref items_found);
                }
            }
        }

        /// <summary>
        /// returns a list of unique items that are colliding with the item that is passed in.
        /// </summary>
        public void GetCollidingItems(Circle collision_circle, ref HashSet<IMapObject> items_found)
        {
            if (!_BoundingBox.Intersects(collision_circle))
                return;

            if (collision_circle.Contains(_BoundingBox))
            {
                foreach (var item in _NodeItems)
                {
                    items_found.Add(item);
                }
            }
            else
            {
                // test each item in this node
                foreach (var item in _NodeItems)
                {
                    if (collision_circle.Intersects(item.BoundingBox))
                    {
                        items_found.Add(item);
                    }
                }
            }

            if (_Leaves != null)
            {
                foreach (var leaf in _Leaves)
                {
                    if (leaf != null)
                        leaf.GetCollidingItems(collision_circle, ref items_found);
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

        protected eQuadrant FindQuadrant(Point2 bounding_box_center, Point2 point)
        {
            if (point.X > bounding_box_center.X)
            {
                if (point.Y > bounding_box_center.Y)
                    return eQuadrant.LowerRightQuadrant;
                else
                    return eQuadrant.UpperRightQuadrant;
            }
            else
            {
                if (point.Y > bounding_box_center.Y)
                    return eQuadrant.LowerLeftQuadrant;
                else
                    return eQuadrant.UpperLeftQuadrant;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (this.GetType() != obj.GetType())
                return false;

            QuadtreeNode other = (QuadtreeNode)obj;

            return this.Equals(other);
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
