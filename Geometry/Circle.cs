using System;
using System.Diagnostics;

namespace Geometry
{
    [DebuggerDisplay("Circle (c: {_Center}, r: {_Radius})")]
    public class Circle
    {
        public static readonly Circle UnitCircle = new Circle(0f, 0f, 1f);

        protected Point2 _Center;
        protected float _Radius;

        public Point2 Center
        {
            get { return _Center; }
            set { _Center = value; }
        }

        public float Radius
        {
            get { return _Radius; }
            set { _Radius = value; }
        }

        public float Left
        {
            get { return _Center.X - _Radius; }
        }

        public float Right
        {
            get { return _Center.X + _Radius; }
        }

        public float Top
        {
            get { return _Center.Y - _Radius; }
        }

        public float Bottom
        {
            get { return _Center.Y + _Radius; }
        }

        public float Area
        {
            get { return (float)(Math.PI * _Radius * _Radius); }
        }

        public float Circumfrence
        {
            get { return (float)(Math.PI * 2 * _Radius); }
        }

        public Circle() : this(0f, 0f, 1f) { }

        public Circle(Point2 position) : this(position, 1f) { }

        public Circle(float radius) : this(0f, 0f, radius) { }

        public Circle(float x, float y, float radius)
        {
            _Center = new Point2(x, y);
            _Radius = radius;
        }

        public Circle(Point2 position, float radius)
        {
            _Center = position;
            _Radius = radius;
        }

        public Circle(Circle circle)
        {
            _Center = new Point2(circle.Center.X, circle.Center.Y);
            _Radius = circle._Radius;
        }

        public bool Intersects(Circle circle)
        {
            float distance_x = circle.Center.X - this.Center.X;
            float distance_y = circle.Center.Y - this.Center.Y;
            float sum_radius = this._Radius + circle._Radius;

            if ((sum_radius * sum_radius) < (distance_x * distance_x + distance_y * distance_y))
                return false;
            else
                return true;
        }

        public bool Intersects(Rectangle rectangle)
        {
            if (rectangle.Contains(_Center))
                return true;

            if (this.Contains(rectangle.TopLeftConrner) || this.Contains(rectangle.TopRightConrner) ||
                this.Contains(rectangle.BottomLeftConrner) || this.Contains(rectangle.BottomRightCorner))
                return true;

            return true;
        }

        public bool Contains(Point2 point)
        {
            float distance_x = point.X - this.Center.X;
            float distance_y = point.Y - this.Center.Y;

            if ((_Radius * _Radius) < Math.Abs(distance_x * distance_x + distance_y * distance_y))
                return false;
            else
                return true;
        }

        public bool Contains(Rectangle rectangle)
        {
            // A circle contains a rectangle if it contains all of the rectangle's corners.
            return this.Contains(rectangle.TopLeftConrner) && this.Contains(rectangle.TopRightConrner) &&
            this.Contains(rectangle.BottomRightCorner) && this.Contains(rectangle.BottomLeftConrner);
        }

        public static Circle operator +(Circle circle, Vector2 vector)
        {
            return new Circle(circle._Center.X + vector.X, circle._Center.Y + vector.Y, circle.Radius);
        }

        public static Circle operator -(Circle circle, Vector2 vector)
        {
            return new Circle(circle._Center.X - vector.X, circle._Center.Y - vector.Y, circle.Radius);
        }

        public static Circle operator *(Circle circle, float scalar)
        {
            return new Circle(circle._Center.X, circle._Center.Y, circle._Radius * scalar);
        }

        public static Circle operator /(Circle circle, float scalar)
        {
            return new Circle(circle._Center.X, circle._Center.Y, circle._Radius / scalar);
        }

        public static bool operator ==(Circle c1, Circle c2)
        {
            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(c1, c2))
                return true;

            // If one is null, but not both, return false.
            if (((object)c1 == null) || ((object)c2 == null))
                return false;

            return (c1._Center.X == c2._Center.X) && (c1._Center.Y == c2._Center.Y) && (c1._Radius == c2._Radius);
        }

        public static bool operator !=(Circle a, Circle b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return _Center.GetHashCode() ^ (_Radius.GetHashCode() * 691);
            }
        }

        public override string ToString()
        {
            return $"Circle (Center: {_Center}, Radius: {_Radius})";
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (this.GetType() != obj.GetType())
                return false;

            Circle other = (Circle)obj;

            return (_Center == other._Center && _Radius == other._Radius);
        }
    }
}
