using System;

namespace Geometry
{
    public class Point3
    {
        protected float _X;
        protected float _Y;
        protected float _Z;

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

        public float Z
        {
            get { return _Z; }
            set { _Z = value; }
        }

        public Point3() : this(0f, 0f, 0f) { }

        public Point3(float x, float y, float z)
        {
            _X = x;
            _Y = y;
            _Z = z;
        }

        public static Point3 operator +(Point3 p1, Point3 p2)
        {
            Point3 output = new Point3();

            output._X = p1._X + p2._X;
            output._Y = p1._Y + p2._Y;
            output._Z = p1._Z + p2._Z;

            return output;
        }

        public static Point3 operator -(Point3 p1, Point3 p2)
        {
            Point3 output = new Point3();

            output._X = p1._X - p2._X;
            output._Y = p1._Y - p2._Y;
            output._Z = p1._Z - p2._Z;

            return output;
        }

        public static Point3 operator *(Point3 p1, Point3 p2)
        {
            Point3 output = new Point3();

            output._X = p1._X * p2._X;
            output._Y = p1._Y * p2._Y;
            output._Z = p1._Z * p2._Z;

            return output;
        }

        public static Point3 operator /(Point3 p1, Point3 p2)
        {
            Point3 output = new Point3();

            output._X = p1._X / p2._X;
            output._Y = p1._Y / p2._Y;
            output._Z = p1._Z / p2._Z;

            return output;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (this.GetType() != obj.GetType())
                return false;

            Point3 other = (Point3)obj;

            return this.Equals(other);
        }

        public bool Equals(Point3 obj)
        {
            if (_X != obj._X)
                return false;

            if (_Y != obj._Y)
                return false;

            if (_Z != obj._Z)
                return false;

            return true;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return _X.GetHashCode() ^ (_Y.GetHashCode() * 7) ^ (_Z.GetHashCode() * 17);
            }
        }

        public override string ToString()
        {
            return $"Point3 ({_X},{_Y},{_Z})";
        }
    }
}
