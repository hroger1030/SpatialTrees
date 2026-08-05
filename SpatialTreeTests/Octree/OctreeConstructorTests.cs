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

namespace SpatialTreesTests
{
    [TestFixture]
    [Category("Octree")]
    public class OctreeConstructorTests
    {
        [Test]
        public void Constructor_Default_UsesDefaultCubeDepthAndObjectLimit()
        {
            var tree = new Octree();

            Assert.Multiple(() =>
            {
                Assert.That(tree.MaxDepth, Is.EqualTo(5));
                Assert.That(tree.MaxNodeObjects, Is.EqualTo(100));
                Assert.That(tree.WorldCube.Width, Is.EqualTo(1f));
                Assert.That(tree.WorldCube.Height, Is.EqualTo(1f));
                Assert.That(tree.WorldCube.Depth, Is.EqualTo(1f));
            });
        }

        [Test]
        public void Constructor_WithVolumeOnly_UsesDefaultDepthAndObjectLimit()
        {
            var tree = new Octree(new Cube(0, 0, 0, 50, 25, 10));

            Assert.Multiple(() =>
            {
                Assert.That(tree.MaxDepth, Is.EqualTo(5));
                Assert.That(tree.MaxNodeObjects, Is.EqualTo(100));
                Assert.That(tree.WorldCube.Width, Is.EqualTo(50f));
                Assert.That(tree.WorldCube.Height, Is.EqualTo(25f));
                Assert.That(tree.WorldCube.Depth, Is.EqualTo(10f));
            });
        }

        [Test]
        public void Constructor_WithAllArguments_UsesProvidedValues()
        {
            var boundingBox = new Cube(0, 0, 0, 200, 200, 200);
            var tree = new Octree(boundingBox, 3, 7);

            Assert.Multiple(() =>
            {
                Assert.That(tree.MaxDepth, Is.EqualTo(3));
                Assert.That(tree.MaxNodeObjects, Is.EqualTo(7));
                Assert.That(tree.WorldCube.Width, Is.EqualTo(200f));
            });
        }

        [Test]
        public void Constructor_NullBoundingBox_Throws()
        {
            Assert.Throws<Exception>(() => new Octree(null, 5, 10));
        }

        [Test]
        public void Constructor_MaxDepthLessThanOne_Throws()
        {
            Assert.Throws<Exception>(() => new Octree(new Cube(0, 0, 0, 100, 100, 100), 0, 10));
        }

        [Test]
        public void Constructor_MaxObjectsLessThanOne_Throws()
        {
            Assert.Throws<Exception>(() => new Octree(new Cube(0, 0, 0, 100, 100, 100), 5, 0));
        }
    }
}
