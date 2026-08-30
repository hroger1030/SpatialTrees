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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace SpatialTrees.Octrees
{
    /// <summary>
    /// A thread-safe facade over <see cref="Octree"/>.
    ///
    /// The plain <see cref="Octree"/> is not safe for concurrent use: a read that runs
    /// while another thread is splitting, collapsing or re-indexing a node can observe a
    /// half-mutated structure. This wrapper serialises every operation through a single
    /// <see cref="ReaderWriterLockSlim"/> - many collision queries may run in parallel,
    /// but a mutation (add / move / remove / clear / resize) takes the lock exclusively.
    ///
    /// Every method on this class takes the lock inline - enter, call straight through to
    /// the inner tree, exit in a finally - so the hot paths carry no delegate, closure or
    /// allocation of their own; the cost over the plain tree is just the lock itself.
    ///
    /// The wrapped tree is never handed out for unsynchronised access. Callers that need
    /// an operation this class does not expose use <see cref="Read{T}(Func{Octree, T})"/>
    /// or <see cref="Write(Action{Octree})"/>, which run a caller-supplied delegate against
    /// the inner tree while holding the appropriate lock. Do not call back into this
    /// wrapper from inside one of those delegates - the lock is non-recursive and will
    /// throw.
    /// </summary>
    [DebuggerDisplay("MultiThreadOctree {WorldCube.Width} x {WorldCube.Height} x {WorldCube.Depth}, {Count} items")]
    public sealed class MultiThreadOctree : IDisposable
    {
        private readonly Octree _Tree;
        private readonly ReaderWriterLockSlim _Lock;
        private bool _Disposed;

        public MultiThreadOctree() : this(new Octree()) { }

        public MultiThreadOctree(Cube volume) : this(new Octree(volume)) { }

        public MultiThreadOctree(Cube boundingBox, int maxDepth, int maxObjects, int expectedItemCount = 0)
            : this(new Octree(boundingBox, maxDepth, maxObjects, expectedItemCount)) { }

        /// <summary>
        /// Wraps an already-constructed <see cref="Octree"/>. The wrapper takes ownership
        /// of the instance: once wrapped, the tree must only be reached through this class
        /// (or the Read/Write delegates), never directly, or the locking guarantee is lost.
        /// This overload is the injection seam for tests.
        /// </summary>
        public MultiThreadOctree(Octree tree)
        {
            ArgumentNullException.ThrowIfNull(tree);

            _Tree = tree;
            _Lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        }

        /// <summary>
        /// Builds a bulk-loaded tree (see <see cref="Octree.Build(Cube, int, int, IReadOnlyCollection{IMapObject3d})"/>)
        /// and returns it wrapped for thread-safe use.
        /// </summary>
        public static MultiThreadOctree Build(Cube boundingBox, int maxDepth, int maxObjects, IReadOnlyCollection<IMapObject3d> items)
        {
            return new MultiThreadOctree(Octree.Build(boundingBox, maxDepth, maxObjects, items));
        }

        /// <summary>
        /// As <see cref="Build(Cube, int, int, IReadOnlyCollection{IMapObject3d})"/> using
        /// the default depth and per-node object limits.
        /// </summary>
        public static MultiThreadOctree Build(Cube boundingBox, IReadOnlyCollection<IMapObject3d> items)
        {
            return new MultiThreadOctree(Octree.Build(boundingBox, items));
        }

        /// <summary>The world cube. Only changes under an exclusive lock via <see cref="Resize"/>.</summary>
        public Cube WorldCube
        {
            get
            {
                _Lock.EnterReadLock();
                
                try
                {
                    return _Tree.WorldCube;
                }
                finally
                {
                    _Lock.ExitReadLock();
                }
            }
        }

        public int MaxDepth
        {
            get
            {
                _Lock.EnterReadLock();

                try
                {
                    return _Tree.MaxDepth;
                }
                finally
                {
                    _Lock.ExitReadLock();
                }
            }
        }

        public int MaxNodeObjects
        {
            get
            {
                _Lock.EnterReadLock();

                try
                {
                    return _Tree.MaxNodeObjects;
                }
                finally
                {
                    _Lock.ExitReadLock();
                }
            }
        }

        /// <summary>Number of items currently indexed by the tree.</summary>
        public int Count
        {
            get
            {
                _Lock.EnterReadLock();

                try
                {
                    return _Tree.ObjectIndex.Count;
                }
                finally
                {
                    _Lock.ExitReadLock();
                }
            }
        }

        /// <summary>Thread-safe <see cref="Octree.AddItem"/>.</summary>
        public void AddItem(IMapObject3d item)
        {
            _Lock.EnterWriteLock();

            try
            {
                _Tree.AddItem(item);
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Adds every item in <paramref name="items"/> under a single exclusive lock, so a
        /// bulk insert from one thread is not interleaved with reads from others and pays
        /// the lock cost once rather than per item.
        /// </summary>
        public void AddItems(IEnumerable<IMapObject3d> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            _Lock.EnterWriteLock();
            try
            {
                foreach (var item in items)
                    _Tree.AddItem(item);
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>Thread-safe <see cref="Octree.MoveItem"/>.</summary>
        public void MoveItem(IMapObject3d item)
        {
            _Lock.EnterWriteLock();

            try
            {
                _Tree.MoveItem(item);
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Re-places every item in <paramref name="items"/> under a single exclusive lock.
        /// </summary>
        public void MoveItems(IEnumerable<IMapObject3d> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            _Lock.EnterWriteLock();

            try
            {
                foreach (var item in items)
                    _Tree.MoveItem(item);
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>Thread-safe <see cref="Octree.RemoveItem"/>.</summary>
        public bool RemoveItem(IMapObject3d item)
        {
            _Lock.EnterWriteLock();

            try
            {
                return _Tree.RemoveItem(item);
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>Thread-safe <see cref="Octree.DetachItem"/>.</summary>
        public bool DetachItem(IMapObject3d item)
        {
            _Lock.EnterWriteLock();

            try
            {
                return _Tree.DetachItem(item);
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>Thread-safe <see cref="Octree.Clear"/>.</summary>
        public void Clear()
        {
            _Lock.EnterWriteLock();

            try
            {
                _Tree.Clear();
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>Thread-safe <see cref="Octree.Resize"/>.</summary>
        public void Resize()
        {
            _Lock.EnterWriteLock();

            try
            {
                _Tree.Resize();
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Thread-safe <see cref="Octree.GetCollidingItems(Cube, int, ref List{IMapObject3d})"/>.
        /// Runs under a shared read lock, so concurrent queries proceed in parallel. The
        /// caller must not share <paramref name="itemsFound"/> between threads.
        /// </summary>
        public bool GetCollidingItems(Cube collisionBox, int objectTypes, ref List<IMapObject3d> itemsFound)
        {
            _Lock.EnterReadLock();

            try
            {
                return _Tree.GetCollidingItems(collisionBox, objectTypes, ref itemsFound);
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Thread-safe <see cref="Octree.GetCollidingItems(Sphere, int, ref List{IMapObject3d})"/>.
        /// </summary>
        public bool GetCollidingItems(Sphere collisionSphere, int objectTypes, ref List<IMapObject3d> itemsFound)
        {
            _Lock.EnterReadLock();

            try
            {
                return _Tree.GetCollidingItems(collisionSphere, objectTypes, ref itemsFound);
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/> against the inner tree while holding a shared
        /// read lock. Use for read-only work this class does not expose directly. The
        /// delegate must not mutate the tree and must not call back into this wrapper.
        /// </summary>
        public void Read(Action<Octree> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            _Lock.EnterReadLock();

            try
            {
                action(_Tree);
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Runs <paramref name="func"/> against the inner tree while holding a shared read
        /// lock and returns its result. The delegate must not mutate the tree, must not
        /// call back into this wrapper, and must not let tree-owned references (nodes, the
        /// object index, the result list) escape for use after the lock is released.
        /// </summary>
        public T Read<T>(Func<Octree, T> func)
        {
            ArgumentNullException.ThrowIfNull(func);

            _Lock.EnterReadLock();
            
            try
            {
                return func(_Tree);
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/> against the inner tree while holding the
        /// exclusive write lock. Use for a mutation this class does not expose directly.
        /// The delegate must not call back into this wrapper.
        /// </summary>
        public void Write(Action<Octree> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            _Lock.EnterWriteLock();

            try
            {
                action(_Tree);
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// As <see cref="Write(Action{Octree})"/>, returning the delegate's result.
        /// </summary>
        public T Write<T>(Func<Octree, T> func)
        {
            ArgumentNullException.ThrowIfNull(func);

            _Lock.EnterWriteLock();

            try
            {
                return func(_Tree);
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        public void Dispose()
        {
            if (_Disposed)
                return;

            _Disposed = true;
            _Lock.Dispose();
        }

        public override string ToString()
        {
            return $"MultiThreadOctree {WorldCube.Width} x {WorldCube.Height} x {WorldCube.Depth}, {Count} items";
        }
    }
}
