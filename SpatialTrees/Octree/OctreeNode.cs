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
    public class OctreeNode
    {
        public const int LEAVES = 8;

        protected Octree _Octree;
        protected OctreeNode _Parent;
        protected OctreeNode[] _Leaves;
        protected Cube _BoundingBox;
        protected HashSet<IMapObject3d> _NodeItems;

        public Cube BoundingBox
        {
            get { return _BoundingBox; }
        }

        public HashSet<IMapObject3d> NodeItems
        {
            get { return _NodeItems; }
        }

        /// <summary>
        /// True once this node has been subdivided into child octants.
        /// </summary>
        public bool IsSplit
        {
            get { return _Leaves != null; }
        }

        public int Depth
        {
            get
            {
                int depth = 1;
                OctreeNode current_node = this;

                while (current_node._Parent != null)
                {
                    current_node = current_node._Parent;
                    depth++;
                }

                return depth;
            }
        }

        public OctreeNode this[int i]
        {
            get
            {
                if (i > -1 && i < LEAVES)
                {
                    return _Leaves[i];
                }
                else
                {
                    throw new IndexOutOfRangeException("OctreeNode " + i.ToString() + " does not exist.");
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
                    throw new IndexOutOfRangeException("OctreeNode " + i.ToString() + " does not exist.");
                }
            }
        }

        public OctreeNode(Octree octree, OctreeNode parent, Cube bounding_box)
        {
            _Octree = octree;
            _Parent = parent;
            _Leaves = null;
            _BoundingBox = bounding_box;
            _NodeItems = new HashSet<IMapObject3d>();
        }

        /// <summary>
        /// Attempts to add an item to the octree. Returns true if the item was added,
        /// false if the item faild to be added.
        /// </summary>
        public bool AddItem(IMapObject3d mapItem)
        {
            if (_NodeItems.Contains(mapItem))
                return false;

            if (_Leaves == null)
            {
                if (_NodeItems.Count > _Octree.MaxNodeObjects && this.Depth < _Octree.MaxDepth)
                {
                    Split();

                    eOctant octant;

                    foreach (var item in _NodeItems)
                    {
                        octant = FindOctant(_BoundingBox.Center, item.BoundingBox.Center);
                        _Leaves[(int)octant].AddItem(item);
                    }

                    _NodeItems.Clear();

                    octant = FindOctant(_BoundingBox.Center, mapItem.BoundingBox.Center);
                    _Leaves[(int)octant].AddItem(mapItem);
                }
                else
                {
                    _NodeItems.Add(mapItem);

                    if (_Octree.ObjectIndex.ContainsKey(mapItem))
                        _Octree.ObjectIndex[mapItem] = this;
                    else
                        _Octree.ObjectIndex.Add(mapItem, this);
                }

                return true;
            }
            else
            {
                eOctant octant = FindOctant(_BoundingBox.Center, mapItem.BoundingBox.Center);
                return _Leaves[(int)octant].AddItem(mapItem);
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
        public void GetCollidingItems(Cube collisionBox, int objectTypes, ref HashSet<IMapObject3d> itemsFound)
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
        public void GetCollidingItems(Sphere collisionSphere, int objectTypes, ref HashSet<IMapObject3d> itemsFound)
        {
            if (!_BoundingBox.Intersects(collisionSphere))
                return;

            if (_NodeItems.Count > 0)
            {
                if (collisionSphere.Contains(_BoundingBox))
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
                        if (collisionSphere.Intersects(item.BoundingBox) && MatchesObjectTypes(objectTypes, item.ObjectTypes))
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
                        leaf.GetCollidingItems(collisionSphere, objectTypes, ref itemsFound);
                }
            }
        }

        public void Split()
        {
            if (_Leaves != null)
                throw new Exception("Node already split");

            _Leaves = new OctreeNode[LEAVES];

            var center = _BoundingBox.Center;
            float x1 = _BoundingBox.X1, y1 = _BoundingBox.Y1, z1 = _BoundingBox.Z1;
            float x2 = _BoundingBox.X2, y2 = _BoundingBox.Y2, z2 = _BoundingBox.Z2;
            float cx = center.X, cy = center.Y, cz = center.Z;

            _Leaves[(int)eOctant.UpperRightNear] = new OctreeNode(_Octree, this, new Cube(cx, y1, z1, x2, cy, cz));
            _Leaves[(int)eOctant.LowerRightNear] = new OctreeNode(_Octree, this, new Cube(cx, cy, z1, x2, y2, cz));
            _Leaves[(int)eOctant.LowerLeftNear] = new OctreeNode(_Octree, this, new Cube(x1, cy, z1, cx, y2, cz));
            _Leaves[(int)eOctant.UpperLeftNear] = new OctreeNode(_Octree, this, new Cube(x1, y1, z1, cx, cy, cz));
            _Leaves[(int)eOctant.UpperRightFar] = new OctreeNode(_Octree, this, new Cube(cx, y1, cz, x2, cy, z2));
            _Leaves[(int)eOctant.LowerRightFar] = new OctreeNode(_Octree, this, new Cube(cx, cy, cz, x2, y2, z2));
            _Leaves[(int)eOctant.LowerLeftFar] = new OctreeNode(_Octree, this, new Cube(x1, cy, cz, cx, y2, z2));
            _Leaves[(int)eOctant.UpperLeftFar] = new OctreeNode(_Octree, this, new Cube(x1, y1, cz, cx, cy, z2));
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
            OctreeNode cursor = this;
            OctreeNode target = null;

            while (cursor != null)
            {
                if (cursor._Leaves != null)
                {
                    // counts only grow as we move up, so once a node is over the limit
                    // no ancestor of it can be collapsible either.
                    if (cursor.GetChildObjectCount() <= _Octree.MaxNodeObjects)
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
        public void MergeInto(OctreeNode ancestor)
        {
            foreach (var item in _NodeItems)
            {
                ancestor._NodeItems.Add(item);
                _Octree.ObjectIndex[item] = ancestor;
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

        protected eOctant FindOctant(Point3 boundingBoxCenter, Point3 point)
        {
            if (point.X > boundingBoxCenter.X)
            {
                if (point.Y > boundingBoxCenter.Y)
                    return point.Z > boundingBoxCenter.Z ? eOctant.LowerRightFar : eOctant.LowerRightNear;
                else
                    return point.Z > boundingBoxCenter.Z ? eOctant.UpperRightFar : eOctant.UpperRightNear;
            }
            else
            {
                if (point.Y > boundingBoxCenter.Y)
                    return point.Z > boundingBoxCenter.Z ? eOctant.LowerLeftFar : eOctant.LowerLeftNear;
                else
                    return point.Z > boundingBoxCenter.Z ? eOctant.UpperLeftFar : eOctant.UpperLeftNear;
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (GetType() != obj.GetType()) return false;

            var new_obj = (OctreeNode)obj;
            return Equals(new_obj);
        }

        public bool Equals(OctreeNode obj)
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
