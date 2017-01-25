using System;

namespace Geometry
{
    public class Point2
    {
        protected float _X;
        protected float _Y;

        public float X
        {
            get { return _X; }
            set { _X = value; }
        }

        public float Y
        {
            get { return _Y; }
            set { _Y = value; }
        }

        public Point2() : this(0f, 0f) { }

        public Point2(double x, double y) : this((float)x, (float)y) { }

        public Point2(int x, int y) : this((float)x, (float)y) { }

        public Point2(short x, short y) : this((float)x, (float)y) { }

        public Point2(float x, float y)
        {
            _X = x;
            _Y = y;
        }

        public float DistanceTo(Point2 other)
        {
            return Point2.DistanceTo(this, other);
        }

        public static float DistanceTo(Point2 p1, Point2 p2)
        {
            return (float)Math.Sqrt((p1.X - p2.X) * (p1.X - p2.X) + (p1.Y - p2.Y) * (p1.Y - p2.Y));
        }

        public static Point2 operator +(Point2 p1, Vector2 v1)
        {
            Point2 output = new Point2();

            output._X = p1._X + v1.X;
            output._Y = p1._Y + v1.Y;

            return output;
        }

        public static Point2 operator -(Point2 p1, Vector2 v1)
        {
            Point2 output = new Point2();

            output._X = p1._X - v1.X;
            output._Y = p1._Y - v1.Y;

            return output;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (this.GetType() != obj.GetType())
                return false;

            Point2 other = (Point2)obj;

            return this.Equals(other);
        }

        public bool Equals(Point2 obj)
        {
            if (_X != obj._X)
                return false;

            if (_Y != obj._Y)
                return false;

            return true;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return _X.GetHashCode() ^ (_Y.GetHashCode() * 149);
            }
        }

        public override string ToString()
        {
            return $"Point2({_X},{_Y})";
        }
    }
}
