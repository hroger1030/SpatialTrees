using System;

namespace Geometry
{
    public class Cube
    {
        public static readonly Cube UnitCube = new Cube(0f, 0f, 0f, 1f, 1f, 1f);

        protected float _X1;
        protected float _X2;
        protected float _Y1;
        protected float _Y2;
        protected float _Z1;
        protected float _Z2;

        public float X1
        {
            get { return _X1; }
            set { _X1 = value; }
        }

        public float X2
        {
            get { return _X2; }
            set { _X2 = value; }
        }

        public float Y1
        {
            get { return _Y1; }
            set { _Y1 = value; }
        }

        public float Y2
        {
            get { return _Y2; }
            set { _Y2 = value; }
        }

        public float Z1
        {
            get { return _Z1; }
            set { _Z1 = value; }
        }

        public float Z2
        {
            get { return _Z2; }
            set { _Z2 = value; }
        }

        /// <summary>
        /// Returns a Point3 object coresponding to the 3d coordinates of 
        /// a corner of the cube object. 
        /// </summary>
        public Point3 this[int i]
        {
            get
            {
                if (i > -1 && i < 8)
                {
                    switch (i)
                    {
                        // top face
                        case 0: return new Point3(_X1, _Y1, _Z1);
                        case 1: return new Point3(_X1, _Y2, _Z1);
                        case 2: return new Point3(_X2, _Y1, _Z1);
                        case 3: return new Point3(_X2, _Y2, _Z1);

                        // bottom face
                        case 4: return new Point3(_X1, _Y1, _Z2);
                        case 5: return new Point3(_X1, _Y2, _Z2);
                        case 6: return new Point3(_X2, _Y1, _Z2);
                        case 7: return new Point3(_X2, _Y2, _Z2);

                        default:
                            throw new Exception("Unknown index " + i.ToString());
                    }
                }
                else
                {
                    throw new IndexOutOfRangeException("Vertex " + i.ToString() + " does not exist.");
                }
            }
        }

        public float Volume
        {
            get
            {
                return Math.Abs(_X2 - _X1) *
                       Math.Abs(_Y2 - _Y1) *
                       Math.Abs(_Z2 - _Z1);
            }
        }

        public float SurfaceArea
        {
            get
            {
                float x = Math.Abs(_X2 - _X1);
                float y = Math.Abs(_Y2 - _Y1);
                float z = Math.Abs(_Z2 - _Z1);

                return (x * y * 2) + (y * z * 2) + (z * x * 2);
            }
        }
        // longest axis
        // shortest axis
        // surface area
        // intersection with another cube?

        public Cube(Point3 p1, Point3 p2)
        {
            if (p1.X < p2.X)
            {
                _X1 = p1.X;
                _X2 = p2.X;
            }
            else
            {
                _X1 = p2.X;
                _X2 = p1.X;
            }

            if (p1.Y < p2.Y)
            {
                _Y1 = p1.Y;
                _Y2 = p2.Y;
            }
            else
            {
                _Y1 = p2.Y;
                _Y2 = p1.Y;
            }

            if (p1.Z < p2.Z)
            {
                _Z1 = p1.Z;
                _Z2 = p2.Z;
            }
            else
            {
                _Z1 = p2.Z;
                _Z2 = p1.Z;
            }
        }

        public Cube(float x, float y, float z, float i, float j, float k)
        {
            if (x < i)
            {
                _X1 = x;
                _X2 = i;
            }
            else
            {
                _X1 = i;
                _X2 = x;
            }

            if (y < j)
            {
                _Y1 = y;
                _Y2 = j;
            }
            else
            {
                _Y1 = j;
                _Y2 = y;
            }

            if (z < k)
            {
                _Z1 = z;
                _Z2 = k;
            }
            else
            {
                _Z1 = k;
                _Z2 = z;
            }
        }

        public bool Intersects(Cube other)
        {
            return !(X1 > other.X2 || X2 < other.X1 || Y1 > other.Y2 || Y2 < other.Y1 || Z1 > other.Z2 || X2 < other.Z1);
            //TODO: Check all cases...

            // check to see if other completely surrounds this

            return false;
        }

    public override bool Equals(object obj)
    {
        if (obj == null)
            return false;

        if (this.GetType() != obj.GetType())
            return false;

        Cube other = (Cube)obj;

        if (_X1 != other._X1)
            return false;

        if (_X2 != other._X2)
            return false;

        if (_Y1 != other._Y1)
            return false;

        if (_Y2 != other._Y2)
            return false;

        if (_Z1 != other._Z1)
            return false;

        if (_Z2 != other._Z2)
            return false;

        return true;
    }

    public override string ToString()
    {
        return $"X1:{_X1}, X2:{_X2}, Y1:{_Y1}, Y2:{_Y2}, Z1:{_Z1}, Z2:{_Z2}";
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return _X1.GetHashCode() ^ (_X2.GetHashCode() * 7) ^ (_Y1.GetHashCode() * 17)
                ^ (_Y2.GetHashCode() * 13) ^ (_Z1.GetHashCode() * 47) ^ (_Z2.GetHashCode() * 37);
        }
    }
}
}