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
using System.Collections.Generic;

namespace SpatialTreesTests
{
    [TestFixture]
    [Category("Quadtree")]
    public class QuadtreeRemoveItemTests
    {
        private Quadtree _Quadtree;

        [SetUp]
        public void Setup()
        {
            _Quadtree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);
        }

        [Test]
        public void RemoveItem_ExistingItem_ReturnsTrueAndRemovesFromIndexAndSearchResults()
        {
            var item = new TestItem("Removable", 10, 10, (int)TestItem.Properties.Property1);
            _Quadtree.AddItem(item);

            var result = _Quadtree.RemoveItem(item);

            var itemsFound = new HashSet<IMapObject>();
            _Quadtree.GetCollidingItems(new Rectangle(0, 0, 100, 100), (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(_Quadtree.ObjectIndex.ContainsKey(item), Is.False);
                Assert.That(itemsFound, Does.Not.Contain(item));
            });
        }

        [Test]
        public void RemoveItem_ItemNeverAdded_ReturnsFalse()
        {
            var item = new TestItem("NeverAdded", 10, 10, (int)TestItem.Properties.Property1);

            var result = _Quadtree.RemoveItem(item);

            Assert.That(result, Is.False);
        }
    }

    [TestFixture]
    [Category("Quadtree")]
    public class QuadtreeClearTests
    {
        [Test]
        public void Clear_RemovesAllItemsFromIndexAndSearchResults()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);
            tree.AddItem(new TestItem("A", 10, 10, (int)TestItem.Properties.Property1));
            tree.AddItem(new TestItem("B", 50, 50, (int)TestItem.Properties.Property1));

            tree.Clear();

            var itemsFound = new HashSet<IMapObject>();
            var anyFound = tree.GetCollidingItems(new Rectangle(0, 0, 100, 100), (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.Multiple(() =>
            {
                Assert.That(tree.ObjectIndex, Is.Empty);
                Assert.That(anyFound, Is.False);
                Assert.That(itemsFound, Is.Empty);
            });
        }
    }

    [TestFixture]
    [Category("Quadtree")]
    public class QuadtreeResizeTests
    {
        [Test]
        public void Resize_DoublesWorldRectangleAndIncrementsMaxDepth()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);

            var result = tree.Resize();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(tree.WorldRectangle.Width, Is.EqualTo(200f));
                Assert.That(tree.WorldRectangle.Height, Is.EqualTo(200f));
                Assert.That(tree.MaxDepth, Is.EqualTo(6));
            });
        }

        [Test]
        public void Resize_PreservesAbilityToFindPreviouslyAddedItems()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);
            var item = new TestItem("Survivor", 10, 10, (int)TestItem.Properties.Property1);
            tree.AddItem(item);

            tree.Resize();

            var itemsFound = new HashSet<IMapObject>();
            tree.GetCollidingItems(new Rectangle(9, 9, 2, 2), (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound, Does.Contain(item));
        }
    }
}
