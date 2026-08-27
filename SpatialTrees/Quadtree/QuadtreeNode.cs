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
    [DebuggerDisplay("Node depth: {Depth}, Center: {BoundingBox.Center}, {GetChildObjectCount()} items")]
    public class QuadtreeNode
    {
        public const int LEAVES = 4;

        public Quadtree Quadtree { get; protected set; }
        public QuadtreeNode Parent { get; protected set; }
        public QuadtreeNode[] Leaves { get; protected set; }
        public Rectangle BoundingBox { get; protected set; }
        // items held directly on this node. A List (not a HashSet): iteration during
        // collision queries is the hot path and wins from contiguous storage; duplicate
        // inserts are already prevented by the ContainsKey guard in Quadtree.AddItem and
        // the Contains guard in AddItem below. Null until the first item is stored here -
        // most interior nodes never hold anything directly, so the allocation is deferred.
        public List<IMapObject2d> NodeItems { get; protected set; }
        public int Depth { get; protected set; }

        // bounding-box centre, cached as scalars at construction so routing does not
        // allocate a Point2 (Rectangle.Center) on every level of every insert.
        public float CenterX { get; protected set; }
        public float CenterY { get; protected set; }

        /// <summary>
        /// True once this node has been subdivided into child quadrants.
        /// </summary>
        public bool IsSplit
        {
            get { return Leaves != null; }
        }

        public QuadtreeNode this[int i]
        {
            get
            {
                if (i > -1 && i < LEAVES)
                {
                    return Leaves[i];
                }
                else
                {
                    throw new IndexOutOfRangeException($"QuadtreePointNode {i} does not exist.");
                }
            }
            set
            {
                if (i > -1 && i < LEAVES)
                {
                    Leaves[i] = value;
                }
                else
                {
                    throw new IndexOutOfRangeException($"QuadtreePointNode {i} does not exist.");
                }
            }
        }

        public QuadtreeNode(Quadtree quadtree, QuadtreeNode parent, Rectangle bounding_box)
        {
            Quadtree = quadtree;
            Parent = parent;
            Depth = (parent == null) ? 1 : parent.Depth + 1;
            Leaves = null;
            BoundingBox = bounding_box;
            CenterX = (bounding_box.Left + bounding_box.Right) * 0.5f;
            CenterY = (bounding_box.Top + bounding_box.Bottom) * 0.5f;
            // NodeItems stays null until StoreItem actually puts something here
        }

        /// <summary>
        /// Re-attaches this node under a new parent and re-stamps the cached depth of
        /// this node and its whole subtree. Used by Quadtree.Resize, which pushes the
        /// old root down a level under a new top node.
        /// </summary>
        public void Reparent(QuadtreeNode parent)
        {
            Parent = parent;
            Depth = (parent == null) ? 1 : parent.Depth + 1;

            if (Leaves != null)
            {
                foreach (var leaf in Leaves)
                {
                    if (leaf != null)
                        leaf.Reparent(this);
                }
            }
        }

        /// <summary>
        /// Adds an item into this node's subtree. A caller-facing failure (item outside
        /// the world, no object type) is an ArgumentException from Quadtree.AddItem, not a
        /// return value; a node that already holds the item just ignores the call.
        /// </summary>
        public void AddItem(IMapObject2d mapItem)
        {
            // read the item's bounding box once here and pass it down the routing
            // recursion - BoundingBox is typically a fresh allocation on every access.
            AddItem(mapItem, mapItem.BoundingBox);
        }

        /// <summary>
        /// Routing worker for <see cref="AddItem(IMapObject2d)"/>. Takes the item's
        /// bounding box as a parameter so a multi-level insert reads the property once
        /// instead of once (or twice) per level.
        /// </summary>
        public void AddItem(IMapObject2d mapItem, Rectangle itemBox)
        {
            if (NodeItems != null && NodeItems.Contains(mapItem))
                return;

            if (Leaves == null)
            {
                // split once this node is already holding MaxNodeObjects and another item
                // is arriving - so a leaf tops out at exactly MaxNodeObjects, not Max + 1.
                if ((NodeItems?.Count ?? 0) >= Quadtree.MaxNodeObjects && this.Depth < Quadtree.MaxDepth)
                {
                    Split();

                    // redistribute existing items into the new leaves. An item whose
                    // bounding box does not fit entirely inside a single child straddles
                    // a quadrant boundary and has to stay on this node, so pull everything
                    // off first and let RouteItem decide where each one lands.
                    var items_to_route = new List<IMapObject2d>(NodeItems);
                    NodeItems.Clear();

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
        /// single child contains it (the item straddles a quadrant boundary) the item is
        /// stored on this node instead, so that collision queries touching only one of the
        /// neighbouring quadrants still find it. Assumes this node has been split.
        /// </summary>
        public void RouteItem(IMapObject2d mapItem, Rectangle itemBox)
        {
            QuadtreeNode leaf = FindContainingLeaf(itemBox);

            if (leaf == null)
                StoreItem(mapItem);
            else
                leaf.AddItem(mapItem, itemBox);
        }

        /// <summary>
        /// Returns the child leaf whose bounding box fully contains <paramref name="itemBox"/>,
        /// or null when the box straddles a quadrant boundary. Assumes this node has been split.
        /// </summary>
        public QuadtreeNode FindContainingLeaf(Rectangle itemBox)
        {
            // item centre as scalars - matches Rectangle.Center without the allocation
            float itemCenterX = (itemBox.Left + itemBox.Right) * 0.5f;
            float itemCenterY = (itemBox.Top + itemBox.Bottom) * 0.5f;

            eQuadrant quadrant = FindQuadrant(itemCenterX, itemCenterY);
            QuadtreeNode leaf = Leaves[(int)quadrant];

            if (leaf.BoundingBox.Contains(itemBox))
                return leaf;

            return null;
        }

        /// <summary>
        /// Stores an item directly on this node and points the tree's object index at
        /// this node.
        /// </summary>
        public void StoreItem(IMapObject2d mapItem)
        {
            (NodeItems ??= new List<IMapObject2d>()).Add(mapItem);

            if (Quadtree.ObjectIndex.ContainsKey(mapItem))
                Quadtree.ObjectIndex[mapItem] = this;
            else
                Quadtree.ObjectIndex.Add(mapItem, this);
        }

        public void RemoveAllLeafItems(bool recursive)
        {
            NodeItems?.Clear();

            if (recursive && Leaves != null)
            {
                foreach (var leaf in Leaves)
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
            if (!BoundingBox.Intersects(collisionBox))
                return;

            if (collisionBox.Contains(BoundingBox))
            {
                // the query region fully contains this node, so it contains this node's
                // whole subtree - collect everything below with no further geometry tests.
                CollectAll(objectTypes, ref itemsFound);
                return;
            }

            if (NodeItems is { Count: > 0 })
            {
                // test each item in this node
                foreach (var item in NodeItems)
                {
                    if (collisionBox.Intersects(item.BoundingBox) && MatchesObjectTypes(objectTypes, item.ObjectTypes))
                    {
                        itemsFound.Add(item);
                    }
                }
            }

            if (Leaves != null)
            {
                foreach (var leaf in Leaves)
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
            if (!BoundingBox.Intersects(collisionCircle))
                return;

            if (collisionCircle.Contains(BoundingBox))
            {
                // the query region fully contains this node, so it contains this node's
                // whole subtree - collect everything below with no further geometry tests.
                CollectAll(objectTypes, ref itemsFound);
                return;
            }

            if (NodeItems is { Count: > 0 })
            {
                // test each item in this node
                foreach (var item in NodeItems)
                {
                    if (collisionCircle.Intersects(item.BoundingBox) && MatchesObjectTypes(objectTypes, item.ObjectTypes))
                    {
                        itemsFound.Add(item);
                    }
                }
            }

            if (Leaves != null)
            {
                foreach (var leaf in Leaves)
                {
                    if (leaf != null)
                        leaf.GetCollidingItems(collisionCircle, objectTypes, ref itemsFound);
                }
            }
        }

        /// <summary>
        /// Adds every type-matching item in this node's whole subtree to the result set
        /// with no spatial tests. Used by GetCollidingItems once a query region is known
        /// to fully contain this node.
        /// </summary>
        public void CollectAll(int objectTypes, ref HashSet<IMapObject2d> itemsFound)
        {
            if (NodeItems != null)
            {
                foreach (var item in NodeItems)
                {
                    if (MatchesObjectTypes(objectTypes, item.ObjectTypes))
                    {
                        itemsFound.Add(item);
                    }
                }
            }

            if (Leaves != null)
            {
                foreach (var leaf in Leaves)
                {
                    if (leaf != null)
                        leaf.CollectAll(objectTypes, ref itemsFound);
                }
            }
        }

        public void Split()
        {
            if (Leaves != null)
                throw new Exception("Node already split");

            Leaves = new QuadtreeNode[LEAVES];

            float new_width = BoundingBox.Width / 2;
            float new_height = BoundingBox.Height / 2;

            Leaves[(int)eQuadrant.UpperRightQuadrant] = new QuadtreeNode(Quadtree, this, new Rectangle(CenterX, BoundingBox.Top, new_width, new_height));
            Leaves[(int)eQuadrant.LowerRightQuadrant] = new QuadtreeNode(Quadtree, this, new Rectangle(CenterX, CenterY, new_width, new_height));
            Leaves[(int)eQuadrant.LowerLeftQuadrant] = new QuadtreeNode(Quadtree, this, new Rectangle(BoundingBox.Left, CenterY, new_width, new_height));
            Leaves[(int)eQuadrant.UpperLeftQuadrant] = new QuadtreeNode(Quadtree, this, new Rectangle(BoundingBox.Left, BoundingBox.Top, new_width, new_height));
        }

        public int GetChildObjectCount()
        {
            int total = NodeItems?.Count ?? 0;

            if (Leaves != null)
            {
                foreach (var leaf in Leaves)
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
                if (cursor.Leaves != null)
                {
                    // counts only grow as we move up, so once a node is over the limit
                    // no ancestor of it can be collapsible either.
                    if (cursor.GetChildObjectCount() <= Quadtree.MaxNodeObjects)
                        target = cursor;
                    else
                        break;
                }

                cursor = cursor.Parent;
            }

            target?.Collapse();
        }

        /// <summary>
        /// Pulls every item held anywhere in this node's subtree up into this node and
        /// discards the child leaves, turning this node back into a leaf.
        /// </summary>
        public void Collapse()
        {
            if (Leaves == null)
                return;

            foreach (var leaf in Leaves)
            {
                if (leaf != null)
                    leaf.MergeInto(this);
            }

            Leaves = null;
        }

        /// <summary>
        /// Moves this node's items, and recursively its descendants', into 'ancestor',
        /// repointing the tree's object index at 'ancestor' as it goes.
        /// </summary>
        public void MergeInto(QuadtreeNode ancestor)
        {
            if (NodeItems != null)
            {
                foreach (var item in NodeItems)
                {
                    (ancestor.NodeItems ??= new List<IMapObject2d>()).Add(item);
                    Quadtree.ObjectIndex[item] = ancestor;
                }

                NodeItems.Clear();
            }

            if (Leaves != null)
            {
                foreach (var leaf in Leaves)
                {
                    if (leaf != null)
                        leaf.MergeInto(ancestor);
                }

                Leaves = null;
            }
        }

        /// <summary>
        /// Which child quadrant the point (px, py) falls in, relative to this node's
        /// cached centre. Takes scalars rather than a Point2 so the hot routing path
        /// stays allocation-free.
        /// </summary>
        protected eQuadrant FindQuadrant(float px, float py)
        {
            if (px > CenterX)
            {
                if (py > CenterY)
                    return eQuadrant.LowerRightQuadrant;
                else
                    return eQuadrant.UpperRightQuadrant;
            }
            else
            {
                if (py > CenterY)
                    return eQuadrant.LowerLeftQuadrant;
                else
                    return eQuadrant.UpperLeftQuadrant;
            }
        }

        public override string ToString()
        {
            return $"Node depth: {Depth}, Center: {BoundingBox.Center}, {GetChildObjectCount()} items";
        }
    }
}
