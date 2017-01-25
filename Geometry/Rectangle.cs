using System;

namespace Geometry
{
    public class Rectangle : IEquatable<Rectangle>
    {
        protected float _X;
        protected float _Y;
        protected float _Width;
        protected float _Height;

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

        public float Width
        {
            get { return _Width; }
            set
            {
                if (value < float.Epsilon)
                    throw new ArgumentException("width must be greater than 0.");

                _Width = value;
            }
        }

        public float Height
        {
            get { return _Height; }
            set
            {
                if (value < float.Epsilon)
                    throw new ArgumentException("height must be greater than 0.");

                _Height = value;
            }
        }

        public Point2 TopLeftConrner
        {
            get { return new Point2(_X, _Y); }
        }

        public Point2 TopRightConrner
        {
            get { return new Point2(_X + _Width, _Y); }
        }

        public Point2 BottomLeftConrner
        {
            get { return new Point2(_X, _Y + _Height); }
        }

        public Point2 BottomRightCorner
        {
            get { return new Point2(_X + _Width, _Y + _Height); }
        }

        /// <summary>
        /// Returns the x coordinate of the left edge of this <see cref="Rectangle"/>.
        /// </summary>
        public float Left
        {
            get { return _X; }
            set { _X = value; }
        }

        /// <summary>
        /// Returns the x coordinate of the right edge of this <see cref="Rectangle"/>.
        /// </summary>
        public float Right
        {
            get { return _X + _Width; }
            set { _X = value - _Width; }
        }

        /// <summary>
        /// Returns the y coordinate of the top edge of this <see cref="Rectangle"/>.
        /// </summary>
        public float Top
        {
            get { return _Y; }
            set { _Y = value; }
        }

        /// <summary>
        /// Returns the y coordinate of the bottom edge of this <see cref="Rectangle"/>.
        /// </summary>
        public float Bottom
        {
            get { return (_Y + _Height); }
            set { _Y = value - _Height; }
        }

        /// <summary>
        /// The top-left coordinates of this <see cref="Rectangle"/>.
        /// </summary>
        public Point2 Location
        {
            get { return new Point2(_X, _Y); }
            set
            {
                _X = value.X;
                _Y = value.Y;
            }
        }

        /// <summary>
        /// The width-height coordinates of this <see cref="Rectangle"/>.
        /// </summary>
        public Point2 Size
        {
            get
            {
                return new Point2(_Width, _Height);
            }
            set
            {
                _Width = value.X;
                _Height = value.Y;
            }
        }

        /// <summary>
        /// A <see cref="Point2"/> located in the center of this <see cref="Rectangle"/>.
        /// </summary>
        public Point2 Center
        {
            get { return new Point2(_X + (_Width / 2), _Y + (_Height / 2)); }
            set
            {
                _X = value.X - (_Width / 2);
                _Y = value.Y - (_Height / 2);
            }
        }

        public float Area
        {
            get { return _Height * _Width; }
        }

        public float Perimeter
        {
            get { return (_Height + _Width) * 2f; }
        }

        public Rectangle() : this(0f, 0f, 0f, 0f) { }

        public Rectangle(float width, float height) : this(0f, 0f, width, height) { }

        public Rectangle(float x, float y, float width, float height)
        {
            if (width < float.Epsilon)
                throw new ArgumentException("Width must be greater than 0.");

            if (height < float.Epsilon)
                throw new ArgumentException("Height must be greater than 0.");

            _X = x;
            _Y = y;
            _Width = width;
            _Height = height;
        }

        public Rectangle(Rectangle rectangle)
        {
            _X = rectangle._X;
            _Y = rectangle._Y;
            _Width = rectangle._Width;
            _Height = rectangle._Height;
        }

        public bool Contains(int x, int y)
        {
            return ((((_X <= x) && (x < (_X + _Width))) && (_Y <= y)) && (y < (_Y + _Height)));
        }

        public bool Contains(float x, float y)
        {
            return (_X <= x) && (_Y <= y) && (x <= (_X + _Width)) && (y <= (_Y + _Height));
        }

        public bool Contains(Point2 point)
        {
            return (_X <= point.X) && (_Y <= point.Y) && (point.X <= (_X + _Width)) && (point.Y <= (_Y + _Height));
        }

        public bool Contains(Rectangle value)
        {
            return (_X <= value._X) && (value._X + value._Width) <= (_X + _Width) && (_Y <= value._Y) && (value._Y + value._Height) <= (_Y + _Height);
        }

        /// <summary>
        /// Adjusts the edges of this <see cref="Rectangle"/> by specified horizontal and vertical amounts. 
        /// Rectangle top left will remain in place, and values don't have to be symetrical.
        /// </summary>
        public void Scale(float width_scale, float height_scale)
        {
            if (height_scale < float.Epsilon)
                throw new ArgumentException("Height scale must be greater than 0");

            if (width_scale < float.Epsilon)
                throw new ArgumentException("Width scale must be greater than 0");

            _Width *= width_scale;
            _Height *= height_scale;
        }

        /// <summary>
        /// Gets whether or not a specified <see cref="Rectangle"/> intersects with this <see cref="Rectangle"/>.
        /// </summary>
        public bool Intersects(Rectangle other)
        {
            return (other.Left < Right) && (other.Right > Left) && (other.Top < Bottom) && (other.Bottom > Top);
        }

        /// <summary>
        /// Gets whether or not a specified <see cref="Circle"/> intersects with this <see cref="Rectangle"/>.
        /// </summary>
        public bool Intersects(Circle circle)
        {
            Point2 rectangle_center = Center;

            float circleDistance_x = Math.Abs(circle.Center.X - rectangle_center.X);
            float circleDistance_y = Math.Abs(circle.Center.Y - rectangle_center.Y);

            if (circleDistance_x > (_Width / 2f + circle.Radius))
                return false;

            if (circleDistance_y > (_Height / 2f + circle.Radius))
                return false;

            if (circleDistance_x <= (_Width / 2f))
                return true;

            if (circleDistance_y <= (_Height / 2f))
                return true;

            float cornerDistance_sq = (circleDistance_x - _Width / 2f) * (circleDistance_x - _Width / 2f) + (circleDistance_y - _Height / 2f) * (circleDistance_y - _Height / 2f);

            return (cornerDistance_sq <= (circle.Radius * circle.Radius));
        }

        /// <summary>
        /// Creates a new <see cref="Rectangle"/> that completely contains two other rectangles.
        /// </summary>
        public static Rectangle Union(Rectangle value1, Rectangle value2)
        {
            float x = Math.Min(value1._X, value2._X);
            float y = Math.Min(value1._Y, value2._Y);
            return new Rectangle(x, y, Math.Max(value1.Right, value2.Right) - x, Math.Max(value1.Bottom, value2.Bottom) - y);
        }

        public static Rectangle operator +(Rectangle rectangle, Vector2 vector)
        {
            return new Rectangle(rectangle.X + vector.X, rectangle.Y + vector.Y, rectangle.Width, rectangle.Height);
        }

        public static Rectangle operator -(Rectangle rectangle, Vector2 vector)
        {
            return new Rectangle(rectangle.X - vector.X, rectangle.Y - vector.Y, rectangle.Width, rectangle.Height);
        }

        public static Rectangle operator *(Rectangle rectangle, float scale)
        {
            if (scale < 0)
                throw new ArgumentException("Scale cannot be less than 0");

            return new Rectangle(rectangle.X, rectangle.Y, rectangle.Width * scale, rectangle.Height * scale);
        }

        public static Rectangle operator /(Rectangle rectangle, float scale)
        {
            if (scale == 0f)
                throw new ArgumentException("Scale cannot be zero, divide by zero errors will occur");

            return new Rectangle(rectangle.X, rectangle.Y, rectangle.Width / scale, rectangle.Height / scale);
        }

        public static bool operator ==(Rectangle r1, Rectangle r2)
        {
            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(r1, r2))
                return true;

            // If one is null, but not both, return false.
            if (((object)r1 == null) || ((object)r2 == null))
                return false;

            return (r1._X == r2._X) && (r1._Y == r2._Y) && (r1._Width == r2._Width) && (r1._Height == r2._Height);
        }

        public static bool operator !=(Rectangle a, Rectangle b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            return (obj != null) && (obj is Rectangle) && this == ((Rectangle)obj);
        }

        public bool Equals(Rectangle other)
        {
            return this == other;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return _X.GetHashCode() ^ (_Y.GetHashCode() * 11) ^ (_Width.GetHashCode() * 197) ^ (_Height.GetHashCode() << 79);
            }
        }

        public override string ToString()
        {
            return $"Rectangle(X:{_X}, Y:{_Y}, Width:{_Width}, Height:{_Height}";
        }
    }
}
