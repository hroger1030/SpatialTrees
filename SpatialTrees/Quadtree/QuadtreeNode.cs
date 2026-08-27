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
        protected int _Depth;

        public Rectangle BoundingBox
        {
            get { return _BoundingBox; }
        }

        public HashSet<IMapObject2d> NodeItems
        {
            get { return _NodeItems; }
        }

        /// <summary>
        /// True once this node has been subdivided into child quadrants.
        /// </summary>
        public bool IsSplit
        {
            get { return _Leaves != null; }
        }

        /// <summary>
        /// Depth of this node, root = 1. Cached at construction (and re-stamped by
        /// Reparent) rather than walked to the root on every access.
        /// </summary>
        public int Depth
        {
            get { return _Depth; }
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
            _Depth = (parent == null) ? 1 : parent._Depth + 1;
            _Leaves = null;
            _BoundingBox = bounding_box;
            _NodeItems = new HashSet<IMapObject2d>();
        }

        /// <summary>
        /// Re-attaches this node under a new parent and re-stamps the cached depth of
        /// this node and its whole subtree. Used by Quadtree.Resize, which pushes the
        /// old root down a level under a new top node.
        /// </summary>
        public void Reparent(QuadtreeNode parent)
        {
            _Parent = parent;
            _Depth = (parent == null) ? 1 : parent._Depth + 1;

            if (_Leaves != null)
            {
                foreach (var leaf in _Leaves)
                {
                    if (leaf != null)
                        leaf.Reparent(this);
                }
            }
        }

        /// <summary>
        /// Adds an item into this node's subtree. A caller-facing failure (item outside
        /// the world, no object type) is a thrown exception from Quadtree.AddItem, not a
        /// return value; a node that already holds the item just ignores the call.
        /// </summary>
        public void AddItem(IMapObject2d mapItem)
        {
            if (_NodeItems.Contains(mapItem))
                return;

            if (_Leaves == null)
            {
                // split once this node is already holding MaxNodeObjects and another item
                // is arriving - so a leaf tops out at exactly MaxNodeObjects, not Max + 1.
                if (_NodeItems.Count >= _Quadtree.MaxNodeObjects && this.Depth < _Quadtree.MaxDepth)
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
            }
            else
            {
                RouteItem(mapItem);
            }
        }

        /// <summary>
        /// Routes an item into the child leaf that fully contains its bounding box. If no
        /// single child contains it (the item straddles a quadrant boundary) the item is
        /// stored on this node instead, so that collision queries touching only one of the
        /// neighbouring quadrants still find it. Assumes this node has been split.
        /// </summary>
        public void RouteItem(IMapObject2d mapItem)
        {
            QuadtreeNode leaf = FindContainingLeaf(mapItem);

            if (leaf == null)
                StoreItem(mapItem);
            else
                leaf.AddItem(mapItem);
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
        /// Object-type filter for collision queries. An item matches when it carries at
        /// least one of the requested type bits, so a query mask combining several types
        /// returns items of any of those types. A mask of 0 matches nothing.
        /// </summary>
        public static bool MatchesObjectTypes(int queryMask, int itemObjectTypes)
        {
            return (queryMask & itemObjectTypes) != 0;
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
                        if (MatchesObjectTypes(objectTypes, item.ObjectTypes))
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
                        if (collisionBox.Intersects(item.BoundingBox) && MatchesObjectTypes(objectTypes, item.ObjectTypes))
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
                        if (MatchesObjectTypes(objectTypes, item.ObjectTypes))
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
                        if (collisionCircle.Intersects(item.BoundingBox) && MatchesObjectTypes(objectTypes, item.ObjectTypes))
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
                if (cursor._Leaves != null)
                {
                    // counts only grow as we move up, so once a node is over the limit
                    // no ancestor of it can be collapsible either.
                    if (cursor.GetChildObjectCount() <= _Quadtree.MaxNodeObjects)
                        target = cursor;
                    else
                        break;
                }

                cursor = cursor._Parent;
            }

            target?.Collapse();
        }

        /// <summary>
        /// Pulls every item held anywhere in this node's subtree up into this node and
        /// discards the child leaves, turning this node back into a leaf.
        /// </summary>
        public void Collapse()
        {
            if (_Leaves == null)
                return;

            foreach (var leaf in _Leaves)
            {
                if (leaf != null)
                    leaf.MergeInto(this);
            }

            _Leaves = null;
        }

        /// <summary>
        /// Moves this node's items, and recursively its descendants', into 'ancestor',
        /// repointing the tree's object index at 'ancestor' as it goes.
        /// </summary>
        public void MergeInto(QuadtreeNode ancestor)
        {
            foreach (var item in _NodeItems)
            {
                ancestor._NodeItems.Add(item);
                _Quadtree.ObjectIndex[item] = ancestor;
            }

            _NodeItems.Clear();

            if (_Leaves != null)
            {
                foreach (var leaf in _Leaves)
                {
                    if (leaf != null)
                        leaf.MergeInto(ancestor);
                }

                _Leaves = null;
            }
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

        public override string ToString()
        {
            return $"Node depth: {Depth}, Center: {_BoundingBox.Center}, {GetChildObjectCount()} items";
        }
    }
}
