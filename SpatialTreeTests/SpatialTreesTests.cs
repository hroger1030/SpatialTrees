using Geometry;
using NUnit.Framework;
using SpatialTrees;
using System.Collections.Generic;

namespace SpatialTreesTests
{
    [TestFixture]
    public class SpatialTreesTests
    {
        private Quadtree _Quadtree;

        [OneTimeSetUp]
        public void Init()
        {
            _Quadtree = new Quadtree(new Rectangle(0, 0, 100, 100), 5, 10);

            _Quadtree.AddItem(new TestItem() { Name = "TestItem1", Location = new Point2(1, 1), ObjectTypes = (int)TestItem.Properties.Property1 });
            _Quadtree.AddItem(new TestItem() { Name = "TestItem2", Location = new Point2(5, 5), ObjectTypes = (int)TestItem.Properties.Property2 });
            _Quadtree.AddItem(new TestItem() { Name = "TestItem3", Location = new Point2(25, 25), ObjectTypes = (int)TestItem.Properties.Property2 });
            _Quadtree.AddItem(new TestItem() { Name = "TestItem4", Location = new Point2(50, 50), ObjectTypes = (int)TestItem.Properties.Property1 });
            _Quadtree.AddItem(new TestItem() { Name = "TestItem5", Location = new Point2(75, 75), ObjectTypes = (int)TestItem.Properties.Property3 });
            _Quadtree.AddItem(new TestItem() { Name = "TestItem6", Location = new Point2(100, 100), ObjectTypes = (int)TestItem.Properties.All });
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsBasicSquare_Passes()
        {
            var itemsFound = new HashSet<IMapObject>();
            var searchArea = new Rectangle(1, 1, 1, 1);
            _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound.Count == 1, Is.True);
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsBasicSquareOversized_Passes()
        {
            var itemsFound = new HashSet<IMapObject>();
            var searchArea = new Rectangle(-1, -1, 102, 102);
            _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound.Count == 3, Is.True);
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsBasicCircle_Passes()
        {
            var itemsFound = new HashSet<IMapObject>();
            var searchArea = new Circle(1, 1, 1);
            _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound.Count == 1, Is.True);
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsBasicCircleOversized_Passes()
        {
            var itemsFound = new HashSet<IMapObject>();
            var searchArea = new Circle(50, 50, 100);
            _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound.Count == 3, Is.True);
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsWithMatchingProperty_Passes()
        {
            var itemsFound = new HashSet<IMapObject>();
            var searchArea = new Circle(3, 3, 5);
            _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property2, ref itemsFound);

            Assert.That(itemsFound.Count == 1, Is.True);
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsBasicSquareTangent_Passes()
        {
            var itemsFound = new HashSet<IMapObject>();
            var searchArea = new Rectangle(0, 0, 1, 1);
            _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound.Count == 1, Is.True);
        }

        [Test]
        [Category("Quadtree")]
        public void Quadtree_FindItemsBasicCircleTangent_Passes()
        {
            var itemsFound = new HashSet<IMapObject>();
            var searchArea = new Circle(1, 2, 1);
            _Quadtree.GetCollidingItems(searchArea, (int)TestItem.Properties.Property1, ref itemsFound);

            Assert.That(itemsFound.Count == 1, Is.True);
        }
    }
}
