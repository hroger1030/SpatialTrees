using System;

namespace Geometry
{
    public class Vector3
    {
        public static readonly Vector3 Zero = new Vector3(0, 0, 0);
        public static readonly Vector3 One = new Vector3(1, 1, 1);

        private float _X;
        private float _Y;
        private float _Z;

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

        public Vector3() : this(0, 0, 0) { }

        public Vector3(Vector3 v1) : this(v1.X, v1.Y, v1.Z) { }

        public Vector3(Point3 p1) : this(p1.X, p1.Y, p1.Z) { }

        public Vector3(Point3 head, Point3 destination) : this(destination.X - head.X, destination.Y - head.Y, destination.Z - head.Z) { }

        public Vector3(float x, float y, float z)
        {
            _X = x;
            _Y = y;
            _Z = z;
        }

        public static Vector3 operator +(Vector3 v1, Vector3 v2)
        {
            return new Vector3
            (
                v1.X + v2.X,
                v1.Y + v2.Y,
                v1.Z + v2.Z
            );
        }

        public static Vector3 operator -(Vector3 v1, Vector3 v2)
        {
            return new Vector3
            (
                v1.X - v2.X,
                v1.Y - v2.Y,
                v1.Z - v2.Z
            );
        }

        public static Vector3 operator *(Vector3 v1, float s2)
        {
            return new Vector3
            (
                v1.X * s2,
                v1.Y * s2,
                v1.Z * s2
            );
        }

        public static Vector3 operator /(Vector3 v1, float s2)
        {
            return new Vector3
            (
                v1.X / s2,
                v1.Y / s2,
                v1.Z / s2
            );
        }

        public static Vector3 Normalize(Vector3 v1)
        {
            var length = v1.Length();

            if (length == 0)
                throw new DivideByZeroException("Cannot normalize a vector when it's magnitude is zero");

            float inverse = 1f / length;

            return new Vector3
            (
                v1.X * inverse,
                v1.Y * inverse,
                v1.Z * inverse
            );
        }

        public void Normalize()
        {
            float length = Length();

            if (length == 0)
                throw new DivideByZeroException("Cannot normalize a vector when it's magnitude is zero");

            float inverse = 1f / length;

            _X *= inverse;
            _Y *= inverse;
            _Z *= inverse;
        }

        public static float DistanceTo(Vector3 v1, Vector3 v2)
        {
            float delta_x = v1.X - v2.X;
            float delta_y = v1.Y - v2.Y;
            float delta_z = v1.Z - v2.Z;

            return (float)Math.Sqrt((delta_x * delta_x) + (delta_y * delta_y) + (delta_z * delta_z));
        }

        public float DistanceTo(Vector3 other)
        {
            return DistanceTo(this, other);
        }

        public float Length()
        {
            return (float)Math.Sqrt((_X * _X) + (_Y * _Y) + (_Z * _Z));
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return _X.GetHashCode() ^ (_Y.GetHashCode() * 13) ^ (_Z.GetHashCode() * 47);
            }
        }

        public override string ToString()
        {
            return $"({_X},{_Y},{_Z})";
        }

        public bool Equals(Vector3 other)
        {
            return other._X == _X && other._Y == _Y && other._Z == _Z;
        }
    }
}
