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
using SpatialTrees.Quadtrees;
using System;

namespace SpatialTreesTests
{
    [TestFixture]
    [Category("Quadtree")]
    public class QuadtreeConstructorTests
    {
        [Test]
        public void Constructor_Default_UsesDefaultRectangleDepthAndObjectLimit()
        {
            var tree = new Quadtree();

            Assert.Multiple(() =>
            {
                Assert.That(tree.MaxDepth, Is.EqualTo(8));
                Assert.That(tree.MaxNodeObjects, Is.EqualTo(16));
                Assert.That(tree.WorldRectangle.Width, Is.EqualTo(1f));
                Assert.That(tree.WorldRectangle.Height, Is.EqualTo(1f));
            });
        }

        [Test]
        public void Constructor_WithAreaOnly_UsesDefaultDepthAndObjectLimit()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 50, 25));

            Assert.Multiple(() =>
            {
                Assert.That(tree.MaxDepth, Is.EqualTo(8));
                Assert.That(tree.MaxNodeObjects, Is.EqualTo(16));
                Assert.That(tree.WorldRectangle.Width, Is.EqualTo(50f));
                Assert.That(tree.WorldRectangle.Height, Is.EqualTo(25f));
            });
        }

        [Test]
        public void Constructor_WithAllArguments_UsesProvidedValues()
        {
            var boundingBox = new Rectangle(0, 0, 200, 200);
            var tree = new Quadtree(boundingBox, 3, 7);

            Assert.Multiple(() =>
            {
                Assert.That(tree.MaxDepth, Is.EqualTo(3));
                Assert.That(tree.MaxNodeObjects, Is.EqualTo(7));
                Assert.That(tree.WorldRectangle.Width, Is.EqualTo(200f));
            });
        }

        [Test]
        public void Constructor_MaxDepthLessThanOne_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Quadtree(new Rectangle(0, 0, 100, 100), 0, 10));
        }

        [Test]
        public void Constructor_MaxObjectsLessThanOne_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Quadtree(new Rectangle(0, 0, 100, 100), 5, 0));
        }

        [Test]
        public void Constructor_NegativeExpectedItemCount_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10, -1));
        }

        [Test]
        public void Constructor_ExpectedItemCountHint_ProducesAWorkingTree()
        {
            var tree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 4, expectedItemCount: 500);

            for (int i = 0; i < 200; i++)
                tree.AddItem(new TestItem($"i{i}", i % 100, (i * 7) % 100, (int)TestItem.Properties.Property1));

            Assert.That(tree.ObjectIndex, Has.Count.EqualTo(200));
            Assert.That(tree.TopNode.GetChildObjectCount(), Is.EqualTo(200));
        }
    }
}
