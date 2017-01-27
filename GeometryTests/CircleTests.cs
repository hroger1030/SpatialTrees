using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Geometry;

namespace GeometryTests
{
    [TestClass]
    public class CircleTests
    {
        [TestMethod]
        [TestCategory("Area")]
        public void TestArea()
        {
            var circle = Circle.UnitCircle;
            Assert.IsTrue(circle.Area == (float)Math.PI * circle.Radius * circle.Radius);
        }

        [TestMethod]
        [TestCategory("Perimeter")]
        public void TestPerimeter()
        {
            var circle = Circle.UnitCircle;
            Assert.IsTrue(circle.Circumfrence == (float)Math.PI * circle.Radius * 2);
        }

        [TestMethod]
        [TestCategory("Intersects")]
        public void TestIntersectsCircle1()
        {
            // overlap
            var circle1 = Circle.UnitCircle;
            var circle2 = Circle.UnitCircle;
            Assert.IsTrue(circle1.Intersects(circle2));
        }

        [TestMethod]
        [TestCategory("Intersects")]
        public void TestIntersectsCircle2()
        {
            // doesn't intersect
            var circle1 = Circle.UnitCircle + Vector2.One;
            var circle2 = Circle.UnitCircle - Vector2.One;
            Assert.IsFalse(circle1.Intersects(circle2));
        }

        [TestMethod]
        [TestCategory("Intersects")]
        public void TestIntersectsCircle3()
        {
            // intersects
            var circle1 = Circle.UnitCircle;
            var circle2 = Circle.UnitCircle + new Vector2(0.5f, 0.5f);
            Assert.IsTrue(circle1.Intersects(circle2));
        }

        [TestMethod]
        [TestCategory("Intersects")]
        public void TestIntersectsRectangle1()
        {
            // overlap
            var rectangle1 = Rectangle.UnitRectangle;
            var rectangle2 = Rectangle.UnitRectangle;
            Assert.IsTrue(rectangle1.Intersects(rectangle2));
        }

        [TestMethod]
        [TestCategory("Intersects")]
        public void TestIntersectsRectangle2()
        {
            // doesn't intersect
            var rectangle1 = Rectangle.UnitRectangle + Vector2.One;
            var rectangle2 = Rectangle.UnitRectangle - Vector2.One;
            Assert.IsFalse(rectangle1.Intersects(rectangle2));
        }


        //public bool Intersects(Rectangle rectangle)
        //{
        //    if (rectangle.Contains(_Center))
        //        return true;

        //    if (this.Contains(rectangle.TopLeftConrner) || this.Contains(rectangle.TopRightConrner) ||
        //        this.Contains(rectangle.BottomLeftConrner) || this.Contains(rectangle.BottomRightCorner))
        //        return true;

        //    return true;
        //}

        //public bool Contains(Point2 point)
        //{
        //    float distance_x = point.X - this.Center.X;
        //    float distance_y = point.Y - this.Center.Y;

        //    if ((_Radius * _Radius) < Math.Abs(distance_x * distance_x + distance_y * distance_y))
        //        return false;
        //    else
        //        return true;
        //}

        //public bool Contains(Rectangle rectangle)
        //{
        //    A circle contains a rectangle if it contains all of the rectangle's corners.
        //    return this.Contains(rectangle.TopLeftConrner) && this.Contains(rectangle.TopRightConrner) &&
        //    this.Contains(rectangle.BottomRightCorner) && this.Contains(rectangle.BottomLeftConrner);
        //}

        //public static Circle operator +(Circle circle, Vector2 vector)
        //{
        //    return new Circle(circle._Center.X + vector.X, circle._Center.Y + vector.Y, circle.Radius);
        //}

        //public static Circle operator -(Circle circle, Vector2 vector)
        //{
        //    return new Circle(circle._Center.X - vector.X, circle._Center.Y - vector.Y, circle.Radius);
        //}

        //public static Circle operator *(Circle circle, float scalar)
        //{
        //    return new Circle(circle._Center.X, circle._Center.Y, circle._Radius * scalar);
        //}

        //public static Circle operator /(Circle circle, float scalar)
        //{
        //    return new Circle(circle._Center.X, circle._Center.Y, circle._Radius / scalar);
        //}

        //public static bool operator ==(Circle c1, Circle c2)
        //{
        //    If both are null, or both are same instance, return true.
        //    if (ReferenceEquals(c1, c2))
        //        return true;

        //    If one is null, but not both, return false.
        //    if (((object)c1 == null) || ((object)c2 == null))
        //        return false;

        //    return (c1._Center.X == c2._Center.X) && (c1._Center.Y == c2._Center.Y) && (c1._Radius == c2._Radius);
        //}

        //public static bool operator !=(Circle a, Circle b)
        //{
        //    return !(a == b);
        //}


        //public override bool Equals(object obj)
        //{
        //    if (obj == null)
        //        return false;

        //    if (this.GetType() != obj.GetType())
        //        return false;

        //    Circle other = (Circle)obj;

        //    return (_Center == other._Center && _Radius == other._Radius);
        //}



    }
}
