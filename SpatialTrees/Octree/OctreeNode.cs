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
        public Cube BoundingBox { get; protected set; }
        protected HashSet<IMapObject3d> _NodeItems;
        protected int _Depth;

        // bounding-box centre, cached as scalars at construction so routing does not
        // allocate a Point3 (Cube.Center) on every level of every insert.
        protected float _CenterX;
        protected float _CenterY;
        protected float _CenterZ;

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

        /// <summary>
        /// Depth of this node, root = 1. Cached at construction (and re-stamped by
        /// Reparent) rather than walked to the root on every access.
        /// </summary>
        public int Depth
        {
            get { return _Depth; }
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
            _Depth = (parent == null) ? 1 : parent._Depth + 1;
            _Leaves = null;
            BoundingBox = bounding_box;
            _CenterX = (bounding_box.X1 + bounding_box.X2) * 0.5f;
            _CenterY = (bounding_box.Y1 + bounding_box.Y2) * 0.5f;
            _CenterZ = (bounding_box.Z1 + bounding_box.Z2) * 0.5f;
            _NodeItems = new HashSet<IMapObject3d>();
        }

        /// <summary>
        /// Re-attaches this node under a new parent and re-stamps the cached depth of
        /// this node and its whole subtree. Used by Octree.Resize, which pushes the
        /// old root down a level under a new top node.
        /// </summary>
        public void Reparent(OctreeNode parent)
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
        /// the world, no object type) is an ArgumentException from Octree.AddItem, not a
        /// return value; a node that already holds the item just ignores the call.
        /// </summary>
        public void AddItem(IMapObject3d mapItem)
        {
            // read the item's bounding box once here and pass it down the routing
            // recursion - BoundingBox is typically a fresh allocation on every access.
            AddItem(mapItem, mapItem.BoundingBox);
        }

        /// <summary>
        /// Routing worker for <see cref="AddItem(IMapObject3d)"/>. Takes the item's
        /// bounding box as a parameter so a multi-level insert reads the property once
        /// instead of once (or twice) per level.
        /// </summary>
        public void AddItem(IMapObject3d mapItem, Cube itemBox)
        {
            if (_NodeItems.Contains(mapItem))
                return;

            if (_Leaves == null)
            {
                // split once this node is already holding MaxNodeObjects and another item
                // is arriving - so a leaf tops out at exactly MaxNodeObjects, not Max + 1.
                if (_NodeItems.Count >= _Octree.MaxNodeObjects && this.Depth < _Octree.MaxDepth)
                {
                    Split();

                    // redistribute existing items into the new leaves. An item whose
                    // bounding box does not fit entirely inside a single child straddles
                    // an octant boundary and has to stay on this node, so pull everything
                    // off first and let RouteItem decide where each one lands.
                    var items_to_route = new List<IMapObject3d>(_NodeItems);
                    _NodeItems.Clear();

                    foreach (var item in items_to_route)
                        RouteItem(item, item.BoundingBox);

                    RouteItem(mapItem, itemBox);
                }
                else
                {
                    StoreItem(mapItem);
                }
            }
            else
            {
                RouteItem(mapItem, itemBox);
            }
        }

        /// <summary>
        /// Routes an item into the child leaf that fully contains its bounding box. If no
        /// single child contains it (the item straddles an octant boundary) the item is
        /// stored on this node instead, so that collision queries touching only one of the
        /// neighbouring octants still find it. Assumes this node has been split.
        /// </summary>
        public void RouteItem(IMapObject3d mapItem, Cube itemBox)
        {
            OctreeNode leaf = FindContainingLeaf(itemBox);

            if (leaf == null)
                StoreItem(mapItem);
            else
                leaf.AddItem(mapItem, itemBox);
        }

        /// <summary>
        /// Returns the child leaf whose bounding box fully contains <paramref name="itemBox"/>,
        /// or null when the box straddles an octant boundary. Assumes this node has been split.
        /// </summary>
        public OctreeNode FindContainingLeaf(Cube itemBox)
        {
            // item centre as scalars - matches Cube.Center without the allocation
            float itemCenterX = (itemBox.X1 + itemBox.X2) * 0.5f;
            float itemCenterY = (itemBox.Y1 + itemBox.Y2) * 0.5f;
            float itemCenterZ = (itemBox.Z1 + itemBox.Z2) * 0.5f;

            eOctant octant = FindOctant(itemCenterX, itemCenterY, itemCenterZ);
            OctreeNode leaf = _Leaves[(int)octant];

            if (leaf.BoundingBox.Contains(itemBox))
                return leaf;

            return null;
        }

        /// <summary>
        /// Stores an item directly on this node and points the tree's object index at
        /// this node.
        /// </summary>
        public void StoreItem(IMapObject3d mapItem)
        {
            _NodeItems.Add(mapItem);

            if (_Octree.ObjectIndex.ContainsKey(mapItem))
                _Octree.ObjectIndex[mapItem] = this;
            else
                _Octree.ObjectIndex.Add(mapItem, this);
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
            if (!BoundingBox.Intersects(collisionBox))
                return;

            if (collisionBox.Contains(BoundingBox))
            {
                // the query region fully contains this node, so it contains this node's
                // whole subtree - collect everything below with no further geometry tests.
                CollectAll(objectTypes, ref itemsFound);
                return;
            }

            if (_NodeItems.Count > 0)
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
            if (!BoundingBox.Intersects(collisionSphere))
                return;

            if (collisionSphere.Contains(BoundingBox))
            {
                // the query region fully contains this node, so it contains this node's
                // whole subtree - collect everything below with no further geometry tests.
                CollectAll(objectTypes, ref itemsFound);
                return;
            }

            if (_NodeItems.Count > 0)
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

            if (_Leaves != null)
            {
                foreach (var leaf in _Leaves)
                {
                    if (leaf != null)
                        leaf.GetCollidingItems(collisionSphere, objectTypes, ref itemsFound);
                }
            }
        }

        /// <summary>
        /// Adds every type-matching item in this node's whole subtree to the result set
        /// with no spatial tests. Used by GetCollidingItems once a query region is known
        /// to fully contain this node.
        /// </summary>
        public void CollectAll(int objectTypes, ref HashSet<IMapObject3d> itemsFound)
        {
            foreach (var item in _NodeItems)
            {
                if (MatchesObjectTypes(objectTypes, item.ObjectTypes))
                {
                    itemsFound.Add(item);
                }
            }

            if (_Leaves != null)
            {
                foreach (var leaf in _Leaves)
                {
                    if (leaf != null)
                        leaf.CollectAll(objectTypes, ref itemsFound);
                }
            }
        }

        public void Split()
        {
            if (_Leaves != null)
                throw new Exception("Node already split");

            _Leaves = new OctreeNode[LEAVES];

            float x1 = BoundingBox.X1, y1 = BoundingBox.Y1, z1 = BoundingBox.Z1;
            float x2 = BoundingBox.X2, y2 = BoundingBox.Y2, z2 = BoundingBox.Z2;
            float cx = _CenterX, cy = _CenterY, cz = _CenterZ;

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

        /// <summary>
        /// Which child octant the point (px, py, pz) falls in, relative to this node's
        /// cached centre. Takes scalars rather than a Point3 so the hot routing path
        /// stays allocation-free.
        /// </summary>
        protected eOctant FindOctant(float px, float py, float pz)
        {
            if (px > _CenterX)
            {
                if (py > _CenterY)
                    return pz > _CenterZ ? eOctant.LowerRightFar : eOctant.LowerRightNear;
                else
                    return pz > _CenterZ ? eOctant.UpperRightFar : eOctant.UpperRightNear;
            }
            else
            {
                if (py > _CenterY)
                    return pz > _CenterZ ? eOctant.LowerLeftFar : eOctant.LowerLeftNear;
                else
                    return pz > _CenterZ ? eOctant.UpperLeftFar : eOctant.UpperLeftNear;
            }
        }

        public override string ToString()
        {
            return $"Node depth: {Depth}, Center: {BoundingBox.Center}, {GetChildObjectCount()} items";
        }
    }
}
