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
using System.Linq;

namespace SpatialTreesTests
{
    [TestFixture]
    [Category("Quadtree")]
    public class QuadtreeBuildTests
    {
        private static readonly Rectangle World = new Rectangle(0, 0, 1000, 1000);

        [Test]
        public void Build_IndexesEveryItem()
        {
            var items = MakePopulation(500, seed: 1);

            var tree = Quadtree.Build(World, 8, 16, items);

            Assert.That(tree.ObjectIndex, Has.Count.EqualTo(items.Count));
            foreach (var item in items)
                Assert.That(tree.ObjectIndex.ContainsKey(item), Is.True);
        }

        [Test]
        public void Build_RootSubtreeCountEqualsItemCount()
        {
            var items = MakePopulation(500, seed: 2);

            var tree = Quadtree.Build(World, 8, 16, items);

            Assert.That(tree.TopNode.GetChildObjectCount(), Is.EqualTo(items.Count));
        }

        // The bulk build must produce a tree that answers queries identically to one
        // built by adding the same items one at a time.
        [Test]
        public void Build_QueryResultsMatchIncrementalBuild()
        {
            var items = MakePopulation(2000, seed: 3);

            var bulk = Quadtree.Build(World, 8, 16, items);

            var incremental = new Quadtree(World, 8, 16);
            foreach (var item in items)
                incremental.AddItem(item);

            var rng = new Random(99);
            var bulkHits = new List<IMapObject2d>();
            var incHits = new List<IMapObject2d>();

            for (int q = 0; q < 200; q++)
            {
                var box = new Rectangle(rng.Next(0, 900), rng.Next(0, 900), rng.Next(1, 200), rng.Next(1, 200));

                bulk.GetCollidingItems(box, (int)TestItem.Properties.All, ref bulkHits);
                incremental.GetCollidingItems(box, (int)TestItem.Properties.All, ref incHits);

                Assert.That(
                    bulkHits.OrderBy(NameOf),
                    Is.EqualTo(incHits.OrderBy(NameOf)).AsCollection,
                    $"query {q} box {box}");
            }
        }

        [Test]
        public void Build_StraddlingItemsLandOnAnInteriorNode()
        {
            var items = new List<IMapObject2d>();

            // enough small items in one quadrant to force several splits
            for (int i = 0; i < 40; i++)
                items.Add(new TestItem($"small{i}", 100 + i, 100, (int)TestItem.Properties.Property1));

            // one big item centred on the world's centre - straddles every top-level boundary
            var straddler = new TestItem("big", 500, 500, 400f, 400f, (int)TestItem.Properties.Property2);
            items.Add(straddler);

            var tree = Quadtree.Build(World, 8, 4, items);

            Assert.That(tree.ObjectIndex[straddler], Is.EqualTo(tree.TopNode));
            Assert.That(tree.TopNode.NodeItems, Does.Contain(straddler));
            Assert.That(tree.TopNode.IsSplit, Is.True);
        }

        [Test]
        public void Build_AllItemsStraddle_RootStaysALeaf()
        {
            var items = new List<IMapObject2d>();
            for (int i = 0; i < 20; i++)
                items.Add(new TestItem($"big{i}", 500, 500, 600f, 600f, (int)TestItem.Properties.Property1));

            var tree = Quadtree.Build(World, 8, 4, items);

            Assert.That(tree.TopNode.IsSplit, Is.False);
            Assert.That(tree.TopNode.NodeItems, Has.Count.EqualTo(20));
            Assert.That(tree.TopNode.GetChildObjectCount(), Is.EqualTo(20));
        }

        [Test]
        public void Build_EmptyCollection_ReturnsUsableEmptyTree()
        {
            var tree = Quadtree.Build(World, 8, 16, new List<IMapObject2d>());

            Assert.That(tree.ObjectIndex, Is.Empty);
            Assert.That(tree.TopNode.IsSplit, Is.False);

            // still works as a normal tree afterwards
            var item = new TestItem("late", 10, 10, (int)TestItem.Properties.Property1);
            tree.AddItem(item);
            Assert.That(tree.ObjectIndex.ContainsKey(item), Is.True);
        }

        [Test]
        public void Build_ItemOutsideWorld_ThrowsArgumentException()
        {
            var items = new List<IMapObject2d>
            {
                new TestItem("ok", 10, 10, (int)TestItem.Properties.Property1),
                new TestItem("bad", 5000, 5000, (int)TestItem.Properties.Property1),
            };

            Assert.Throws<ArgumentException>(() => Quadtree.Build(World, 8, 16, items));
        }

        [Test]
        public void Build_ItemWithNoObjectType_ThrowsArgumentException()
        {
            var items = new List<IMapObject2d>
            {
                new TestItem("ok", 10, 10, (int)TestItem.Properties.Property1),
                new TestItem("typeless", 20, 20, 0),
            };

            Assert.Throws<ArgumentException>(() => Quadtree.Build(World, 8, 16, items));
        }

        [Test]
        public void Build_NullItems_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Quadtree.Build(World, 8, 16, null));
        }

        [Test]
        public void Build_ItemsRemainRemovableAndMovable()
        {
            var items = MakePopulation(300, seed: 7);
            var tree = Quadtree.Build(World, 8, 16, items);

            var victim = items[0];
            Assert.That(tree.RemoveItem(victim), Is.True);
            Assert.That(tree.ObjectIndex.ContainsKey(victim), Is.False);

            var mover = (TestItem)items[1];
            mover.Location = new Point2(mover.Location.X + 300, mover.Location.Y + 300);
            tree.MoveItem(mover);

            var hits = new List<IMapObject2d>();
            tree.GetCollidingItems(new Rectangle(mover.Location.X - 2, mover.Location.Y - 2, 4, 4),
                (int)TestItem.Properties.All, ref hits);
            Assert.That(hits, Does.Contain(mover));
        }

        [Test]
        public void Build_DefaultOverload_UsesDefaultDepthAndObjectLimit()
        {
            var tree = Quadtree.Build(World, MakePopulation(50, seed: 11));

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
            var only = new TestItem("solo", 123, 456, (int)TestItem.Properties.Property1);

            var tree = Quadtree.Build(World, 8, 16, new List<IMapObject2d> { only });

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
            var items = new List<IMapObject2d>
            {
                new TestItem("q1", 100, 100, (int)TestItem.Properties.Property1),
                new TestItem("q2", 900, 100, (int)TestItem.Properties.Property1),
                new TestItem("q3", 100, 900, (int)TestItem.Properties.Property1),
                new TestItem("q4", 900, 900, (int)TestItem.Properties.Property1),
            };

            var tree = Quadtree.Build(World, 8, 4, items);

            Assert.That(tree.TopNode.IsSplit, Is.False);
            Assert.That(tree.TopNode.NodeItems, Has.Count.EqualTo(4));
        }

        [Test]
        public void Build_OneOverMaxObjectsNonStraddling_RootSplits()
        {
            var items = new List<IMapObject2d>
            {
                new TestItem("q1", 100, 100, (int)TestItem.Properties.Property1),
                new TestItem("q2", 900, 100, (int)TestItem.Properties.Property1),
                new TestItem("q3", 100, 900, (int)TestItem.Properties.Property1),
                new TestItem("q4", 900, 900, (int)TestItem.Properties.Property1),
                new TestItem("q5", 250, 250, (int)TestItem.Properties.Property1),
            };

            var tree = Quadtree.Build(World, 8, 4, items);

            Assert.That(tree.TopNode.IsSplit, Is.True);
            Assert.That(tree.TopNode.GetChildObjectCount(), Is.EqualTo(5));
        }

        // A bulk-built tree keeps AddItem working: the incremental path's parent-walking
        // SubtreeCount updates must line up with the counts the bulk loader stamped.
        [Test]
        public void Build_ThenAddAndRemove_SubtreeCountStaysConsistentWithIndex()
        {
            var items = MakePopulation(200, seed: 13);
            var tree = Quadtree.Build(World, 8, 8, items);

            for (int i = 0; i < 50; i++)
                tree.AddItem(new TestItem($"extra{i}", 400 + i, 400, (int)TestItem.Properties.Property1));

            Assert.That(tree.TopNode.GetChildObjectCount(), Is.EqualTo(tree.ObjectIndex.Count));

            foreach (var victim in items.GetRange(0, 40))
                tree.RemoveItem(victim);

            Assert.That(tree.TopNode.GetChildObjectCount(), Is.EqualTo(tree.ObjectIndex.Count));
        }

        private static string NameOf(IMapObject2d item) => ((TestItem)item).Name;

        private static List<IMapObject2d> MakePopulation(int count, int seed)
        {
            var rng = new Random(seed);
            var list = new List<IMapObject2d>(count);

            for (int i = 0; i < count; i++)
            {
                list.Add(new TestItem(
                    $"i{i}",
                    (float)rng.NextDouble() * 1000f,
                    (float)rng.NextDouble() * 1000f,
                    (int)TestItem.Properties.Property1));
            }

            return list;
        }
    }
}
