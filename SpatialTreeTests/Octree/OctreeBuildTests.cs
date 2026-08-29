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
using SpatialTrees.Octrees;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpatialTreesTests
{
    [TestFixture]
    [Category("Octree")]
    public class OctreeBuildTests
    {
        private static readonly Cube World = new Cube(0, 0, 0, 1000, 1000, 1000);

        [Test]
        public void Build_IndexesEveryItem()
        {
            var items = MakePopulation(500, seed: 1);

            var tree = Octree.Build(World, 8, 16, items);

            Assert.That(tree.ObjectIndex, Has.Count.EqualTo(items.Count));
            foreach (var item in items)
                Assert.That(tree.ObjectIndex.ContainsKey(item), Is.True);
        }

        [Test]
        public void Build_RootSubtreeCountEqualsItemCount()
        {
            var items = MakePopulation(500, seed: 2);

            var tree = Octree.Build(World, 8, 16, items);

            Assert.That(tree.TopNode.GetChildObjectCount(), Is.EqualTo(items.Count));
        }

        // The bulk build must produce a tree that answers queries identically to one
        // built by adding the same items one at a time.
        [Test]
        public void Build_QueryResultsMatchIncrementalBuild()
        {
            var items = MakePopulation(2000, seed: 3);

            var bulk = Octree.Build(World, 8, 16, items);

            var incremental = new Octree(World, 8, 16);
            foreach (var item in items)
                incremental.AddItem(item);

            var rng = new Random(99);
            var bulkHits = new List<IMapObject3d>();
            var incHits = new List<IMapObject3d>();

            for (int q = 0; q < 200; q++)
            {
                float x = rng.Next(0, 900);
                float y = rng.Next(0, 900);
                float z = rng.Next(0, 900);
                var box = new Cube(x, y, z, x + rng.Next(1, 200), y + rng.Next(1, 200), z + rng.Next(1, 200));

                bulk.GetCollidingItems(box, (int)TestVolumeItem.Properties.All, ref bulkHits);
                incremental.GetCollidingItems(box, (int)TestVolumeItem.Properties.All, ref incHits);

                Assert.That(
                    bulkHits.OrderBy(NameOf),
                    Is.EqualTo(incHits.OrderBy(NameOf)).AsCollection,
                    $"query {q} box {box}");
            }
        }

        [Test]
        public void Build_StraddlingItemsLandOnAnInteriorNode()
        {
            var items = new List<IMapObject3d>();

            for (int i = 0; i < 40; i++)
                items.Add(new TestVolumeItem($"small{i}", 100 + i, 100, 100, (int)TestVolumeItem.Properties.Property1));

            var straddler = new TestVolumeItem("big", 500, 500, 500, 400f, 400f, 400f, (int)TestVolumeItem.Properties.Property2);
            items.Add(straddler);

            var tree = Octree.Build(World, 8, 4, items);

            Assert.That(tree.ObjectIndex[straddler], Is.EqualTo(tree.TopNode));
            Assert.That(tree.TopNode.NodeItems, Does.Contain(straddler));
            Assert.That(tree.TopNode.IsSplit, Is.True);
        }

        [Test]
        public void Build_AllItemsStraddle_RootStaysALeaf()
        {
            var items = new List<IMapObject3d>();
            for (int i = 0; i < 20; i++)
                items.Add(new TestVolumeItem($"big{i}", 500, 500, 500, 600f, 600f, 600f, (int)TestVolumeItem.Properties.Property1));

            var tree = Octree.Build(World, 8, 4, items);

            Assert.That(tree.TopNode.IsSplit, Is.False);
            Assert.That(tree.TopNode.NodeItems, Has.Count.EqualTo(20));
            Assert.That(tree.TopNode.GetChildObjectCount(), Is.EqualTo(20));
        }

        [Test]
        public void Build_EmptyCollection_ReturnsUsableEmptyTree()
        {
            var tree = Octree.Build(World, 8, 16, new List<IMapObject3d>());

            Assert.That(tree.ObjectIndex, Is.Empty);
            Assert.That(tree.TopNode.IsSplit, Is.False);

            var item = new TestVolumeItem("late", 10, 10, 10, (int)TestVolumeItem.Properties.Property1);
            tree.AddItem(item);
            Assert.That(tree.ObjectIndex.ContainsKey(item), Is.True);
        }

        [Test]
        public void Build_ItemOutsideWorld_ThrowsArgumentException()
        {
            var items = new List<IMapObject3d>
            {
                new TestVolumeItem("ok", 10, 10, 10, (int)TestVolumeItem.Properties.Property1),
                new TestVolumeItem("bad", 5000, 5000, 5000, (int)TestVolumeItem.Properties.Property1),
            };

            Assert.Throws<ArgumentException>(() => Octree.Build(World, 8, 16, items));
        }

        [Test]
        public void Build_ItemWithNoObjectType_ThrowsArgumentException()
        {
            var items = new List<IMapObject3d>
            {
                new TestVolumeItem("ok", 10, 10, 10, (int)TestVolumeItem.Properties.Property1),
                new TestVolumeItem("typeless", 20, 20, 20, 0),
            };

            Assert.Throws<ArgumentException>(() => Octree.Build(World, 8, 16, items));
        }

        [Test]
        public void Build_NullItems_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Octree.Build(World, 8, 16, null));
        }

        [Test]
        public void Build_ItemsRemainRemovableAndMovable()
        {
            var items = MakePopulation(300, seed: 7);
            var tree = Octree.Build(World, 8, 16, items);

            var victim = items[0];
            Assert.That(tree.RemoveItem(victim), Is.True);
            Assert.That(tree.ObjectIndex.ContainsKey(victim), Is.False);

            var mover = (TestVolumeItem)items[1];
            mover.Location = new Point3(mover.Location.X + 300, mover.Location.Y + 300, mover.Location.Z + 300);
            tree.MoveItem(mover);

            var hits = new List<IMapObject3d>();
            tree.GetCollidingItems(
                new Cube(mover.Location.X - 2, mover.Location.Y - 2, mover.Location.Z - 2,
                         mover.Location.X + 2, mover.Location.Y + 2, mover.Location.Z + 2),
                (int)TestVolumeItem.Properties.All, ref hits);
            Assert.That(hits, Does.Contain(mover));
        }

        [Test]
        public void Build_DefaultOverload_UsesDefaultDepthAndObjectLimit()
        {
            var tree = Octree.Build(World, MakePopulation(50, seed: 11));

            Assert.Multiple(() =>
            {
                Assert.That(tree.MaxDepth, Is.EqualTo(8));
                Assert.That(tree.MaxNodeObjects, Is.EqualTo(16));
                Assert.That(tree.ObjectIndex, Has.Count.EqualTo(50));
            });
        }

        [Test]
        public void Build_SingleItem_StoredOnRootAsLeaf()
        {
            var only = new TestVolumeItem("solo", 123, 456, 789, (int)TestVolumeItem.Properties.Property1);

            var tree = Octree.Build(World, 8, 16, new List<IMapObject3d> { only });

            Assert.Multiple(() =>
            {
                Assert.That(tree.TopNode.IsSplit, Is.False);
                Assert.That(tree.TopNode.NodeItems, Is.EqualTo(new[] { only }));
                Assert.That(tree.TopNode.GetChildObjectCount(), Is.EqualTo(1));
                Assert.That(tree.ObjectIndex[only], Is.EqualTo(tree.TopNode));
            });
        }

        // Bulk build tops a leaf out at exactly MaxNodeObjects, same as incremental AddItem.
        [Test]
        public void Build_ExactlyMaxObjectsNonStraddling_RootStaysALeaf()
        {
            var items = new List<IMapObject3d>
            {
                new TestVolumeItem("o1", 100, 100, 100, (int)TestVolumeItem.Properties.Property1),
                new TestVolumeItem("o2", 900, 100, 100, (int)TestVolumeItem.Properties.Property1),
                new TestVolumeItem("o3", 100, 900, 100, (int)TestVolumeItem.Properties.Property1),
                new TestVolumeItem("o4", 900, 900, 900, (int)TestVolumeItem.Properties.Property1),
            };

            var tree = Octree.Build(World, 8, 4, items);

            Assert.That(tree.TopNode.IsSplit, Is.False);
            Assert.That(tree.TopNode.NodeItems, Has.Count.EqualTo(4));
        }

        [Test]
        public void Build_OneOverMaxObjectsNonStraddling_RootSplits()
        {
            var items = new List<IMapObject3d>
            {
                new TestVolumeItem("o1", 100, 100, 100, (int)TestVolumeItem.Properties.Property1),
                new TestVolumeItem("o2", 900, 100, 100, (int)TestVolumeItem.Properties.Property1),
                new TestVolumeItem("o3", 100, 900, 100, (int)TestVolumeItem.Properties.Property1),
                new TestVolumeItem("o4", 900, 900, 900, (int)TestVolumeItem.Properties.Property1),
                new TestVolumeItem("o5", 250, 250, 250, (int)TestVolumeItem.Properties.Property1),
            };

            var tree = Octree.Build(World, 8, 4, items);

            Assert.That(tree.TopNode.IsSplit, Is.True);
            Assert.That(tree.TopNode.GetChildObjectCount(), Is.EqualTo(5));
        }

        // A bulk-built tree keeps AddItem working: the incremental path's parent-walking
        // SubtreeCount updates must line up with the counts the bulk loader stamped.
        [Test]
        public void Build_ThenAddAndRemove_SubtreeCountStaysConsistentWithIndex()
        {
            var items = MakePopulation(200, seed: 13);
            var tree = Octree.Build(World, 8, 8, items);

            for (int i = 0; i < 50; i++)
                tree.AddItem(new TestVolumeItem($"extra{i}", 400 + i, 400, 400, (int)TestVolumeItem.Properties.Property1));

            Assert.That(tree.TopNode.GetChildObjectCount(), Is.EqualTo(tree.ObjectIndex.Count));

            foreach (var victim in items.GetRange(0, 40))
                tree.RemoveItem(victim);

            Assert.That(tree.TopNode.GetChildObjectCount(), Is.EqualTo(tree.ObjectIndex.Count));
        }

        private static string NameOf(IMapObject3d item) => ((TestVolumeItem)item).Name;

        private static List<IMapObject3d> MakePopulation(int count, int seed)
        {
            var rng = new Random(seed);
            var list = new List<IMapObject3d>(count);

            for (int i = 0; i < count; i++)
            {
                list.Add(new TestVolumeItem(
                    $"i{i}",
                    (float)rng.NextDouble() * 1000f,
                    (float)rng.NextDouble() * 1000f,
                    (float)rng.NextDouble() * 1000f,
                    (int)TestVolumeItem.Properties.Property1));
            }

            return list;
        }
    }
}
