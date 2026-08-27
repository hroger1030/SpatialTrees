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
using NUnit.Framework;
using SpatialTrees;
using System;
using System.Collections.Generic;

namespace SpatialTreesTests
{
    [TestFixture]
    [Category("Quadtree")]
    public class QuadtreeMoveItemTests
    {
        private Quadtree _Quadtree;

        [SetUp]
        public void Setup()
        {
            _Quadtree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);
        }

        [Test]
        public void MoveItem_NotPreviouslyTracked_AddsItAsNew()
        {
            var item = new TestItem("Fresh", 10, 10, (int)TestItem.Properties.Property1);

            _Quadtree.MoveItem(item);

            Assert.That(_Quadtree.ObjectIndex.ContainsKey(item), Is.True);
        }

        [Test]
        public void MoveItem_WithinSameNode_IsFindableAtNewLocation()
        {
            var item = new TestItem("Mover", 10, 10, (int)TestItem.Properties.Property1);
            _Quadtree.AddItem(item);

            item.Location = new Point2(20, 20);
            _Quadtree.MoveItem(item);

            var itemsFound = new HashSet<IMapObject2d>();
            _Quadtree.GetCollidingItems(new Rectangle(19, 19, 2, 2), (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Does.Contain(item));
        }

        [Test]
        public void MoveItem_ToLocationOutsideWorldRectangle_ThrowsArgumentException()
        {
            var item = new TestItem("Wanderer", 10, 10, (int)TestItem.Properties.Property1);
            _Quadtree.AddItem(item);

            item.Location = new Point2(500, 500);

            Assert.Throws<ArgumentException>(() => _Quadtree.MoveItem(item));
        }

        [Test]
        public void MoveItem_ToLocationOutsideWorldRectangle_LeavesItemTracked()
        {
            var item = new TestItem("Wanderer", 10, 10, (int)TestItem.Properties.Property1);
            _Quadtree.AddItem(item);
            var original_node = _Quadtree.ObjectIndex[item];

            item.Location = new Point2(500, 500);

            Assert.Throws<ArgumentException>(() => _Quadtree.MoveItem(item));

            // the rejected move must not have dropped the item from the tree
            Assert.Multiple(() =>
            {
                Assert.That(_Quadtree.ObjectIndex.ContainsKey(item), Is.True);
                Assert.That(_Quadtree.ObjectIndex[item], Is.SameAs(original_node));
                Assert.That(original_node.NodeItems, Does.Contain(item));
            });
        }
    }
}
