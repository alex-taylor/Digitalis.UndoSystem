/*
 * UndoableList.cs
 *
 * Copyright (C) 2010-2012 Alex Taylor.  All Rights Reserved.
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;


namespace Digitalis.UndoSystem
{
    /// <summary>
    /// Defines a strongly-typed list which supports undo/redo functionality.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <remarks>
    /// <b>UndoableList</b> functions exactly like a normal <see cref="IList{T}"/> except that all modifications made to it
    /// are automatically added to the current command of the active <see cref="UndoStack"/>, if any.
    /// </remarks>
    [Serializable]
    public class UndoableList<T> : IList<T>, IList
    {
        /// <summary>
        /// Occurs when items are added to the <see cref="UndoableList{T}"/>.
        /// </summary>
        public event UndoableListChangedEventHandler<T> ItemsAdded;

        /// <summary>
        /// Occurs when items are removed from the <see cref="UndoableList{T}"/>.
        /// </summary>
        public event UndoableListChangedEventHandler<T> ItemsRemoved;

        /// <summary>
        /// Occurs when items in the <see cref="UndoableList{T}"/> are replaced.
        /// </summary>
        public event UndoableListReplacedEventHandler<T> ItemsReplaced;

        /// <summary>
        /// Occurs when the <see cref="UndoableList{T}"/> is cleared.
        /// </summary>
        public event UndoableListChangedEventHandler<T> ListCleared;

        //////////////////////////////////////////////////////////////////////////////////////

        private List<T> _list;

        //////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Initializes an instance of the <see cref="UndoableList{T}"/> class that is empty.
        /// </summary>
        /// <remarks>
        /// The <see cref="UndoableList{T}"/> will be created in read-write mode.
        /// </remarks>
        public UndoableList() : this(false) { }

        /// <summary>
        /// Initializes an instance of the <see cref="UndoableList{T}"/> class that is empty and has the specified access mode.
        /// </summary>
        /// <param name="readOnly">If <b>true</b>, the <see cref="UndoableList{T}"/> will be read-only; otherwise it is read-write.</param>
        public UndoableList(bool readOnly)
        {
            _list      = new List<T>();
            IsReadOnly = readOnly;
        }

        /// <summary>
        /// Initializes an instance of the <see cref="UndoableList{T}"/> class that contains elements copied from the specified collection and has the specified access mode.
        /// </summary>
        /// <param name="collection">The collection whose elements are to be copied to the new list.</param>
        /// <param name="readOnly">If <b>true</b>, the <see cref="UndoableList{T}"/> will be read-only; otherwise it is read-write.</param>
        /// <exception cref="ArgumentNullException"><paramref name="collection"/> is a null reference.</exception>
        public UndoableList(IEnumerable<T> collection, bool readOnly)
        {
            _list      = new List<T>(collection);
            IsReadOnly = readOnly;
        }

        //////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the elements of the specified collection to the end of the <see cref="UndoableList{T}"/>.
        /// </summary>
        /// <param name="items">The collection whose elements should be added to the end of the <see cref="UndoableList{T}"/>. The collection itself cannot be null, but it can contain elements that are null, if type T is a reference type.</param>
        public void AddRange(IEnumerable<T> items)
        {
            InsertRange(Count, items);
        }

        /// <summary>
        /// Inserts the elements of a collection into the <see cref="UndoableList{T}"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which the new elements should be inserted.</param>
        /// <param name="items">The collection whose elements should be inserted into the <see cref="UndoableList{T}"/>. The collection itself cannot be null, but it can contain elements that are null, if type T is a reference type.</param>
        public void InsertRange(int index, IEnumerable<T> items)
        {
            if (IsReadOnly)
                throw new NotSupportedException();

            UndoStack.AddAction(new ActionInsert(this, index, items));
        }

        /// <summary>
        /// Removes a range of elements from the <see cref="UndoableList{T}"/>.
        /// </summary>
        /// <param name="index">The zero-based starting index of the range of elements to remove.</param>
        /// <param name="count">The number of elements to remove.</param>
        public void RemoveRange(int index, int count)
        {
            if (IsReadOnly)
                throw new NotSupportedException();

            UndoStack.AddAction(new ActionRemove(this, index, count));
        }

        /// <summary>
        /// Replaces the entire contents of the <see cref="UndoableList{T}"/>.
        /// </summary>
        /// <param name="items">The collection whose elements should be inserted into the <see cref="UndoableList{T}"/>. The collection itself cannot be null, but it can contain elements that are null, if type T is a reference type.</param>
        /// <remarks>
        /// This is similar to calling <see cref="Clear()"/> followed by <see cref="AddRange()"/>, but raises the <see cref="ItemsReplaced"/> event instead of <see cref="ListCleared"/> and <see cref="ItemsAdded"/>.
        /// </remarks>
        public void ReplaceContents(IEnumerable<T> items)
        {
            if (IsReadOnly)
                throw new NotSupportedException();

            UndoStack.AddAction(new ActionReplaceList(this, items));
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // IList<T>

        /// <inheritdoc />
        public virtual T this[int index]
        {
            get { return _list[index]; }
            set
            {
                if (IsReadOnly)
                    throw new NotSupportedException();

                UndoStack.AddAction(new ActionReplaceItem(this, value, index));
            }
        }

        /// <inheritdoc />
        public virtual int IndexOf(T item)
        {
            return _list.IndexOf(item);
        }

        /// <inheritdoc />
        public virtual void Insert(int index, T item)
        {
            if (IsReadOnly)
                throw new NotSupportedException();

            UndoStack.AddAction(new ActionInsert(this, index, item));
        }

        /// <inheritdoc />
        public virtual void RemoveAt(int index)
        {
            if (IsReadOnly)
                throw new NotSupportedException();

            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException();

            UndoStack.AddAction(new ActionRemove(this, index));
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // IList

        /// <inheritdoc />
        public virtual void CopyTo(Array array, int index)
        {
            ((IList)_list).CopyTo(array, index);
        }

        /// <inheritdoc />
        public virtual bool IsSynchronized { get { return ((IList)_list).IsSynchronized; } }

        /// <inheritdoc />
        public virtual object SyncRoot { get { return ((IList)_list).SyncRoot; } }

        /// <inheritdoc />
        public virtual int Add(object value)
        {
            return ((IList)_list).Add((T)value);
        }

        /// <inheritdoc />
        public virtual bool Contains(object value)
        {
            return ((IList)_list).Contains((T)value);
        }

        /// <inheritdoc />
        public virtual int IndexOf(object value)
        {
            return ((IList)_list).IndexOf((T)value);
        }

        /// <inheritdoc />
        public virtual void Insert(int index, object value)
        {
            ((IList)_list).Insert(index, (T)value);
        }

        /// <inheritdoc />
        public virtual bool IsFixedSize { get { return ((IList)_list).IsFixedSize; } }

        /// <inheritdoc />
        public virtual void Remove(object value)
        {
            ((IList)_list).Remove((T)value);
        }

        /// <inheritdoc />
        object IList.this[int index] { get { return ((IList)_list)[index]; } set { ((IList)_list)[index] = (T)value; } }

        //////////////////////////////////////////////////////////////////////////////////////
        // ICollection<T>

        /// <inheritdoc />
        public virtual int Count { get { return _list.Count; } }

        /// <inheritdoc />
        public virtual bool IsReadOnly { get; private set; }

        /// <inheritdoc />
        public virtual void Add(T item)
        {
            Insert(Count, item);
        }

        /// <inheritdoc />
        public virtual void Clear()
        {
            if (IsReadOnly)
                throw new NotSupportedException();

            UndoStack.AddAction(new ActionClear(this));
        }

        /// <inheritdoc />
        public virtual bool Contains(T item)
        {
            return _list.Contains(item);
        }

        /// <inheritdoc />
        public virtual void CopyTo(T[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        /// <inheritdoc />
        public virtual bool Remove(T item)
        {
            if (IsReadOnly)
                throw new NotSupportedException();

            int index = IndexOf(item);

            if (-1 == index)
                return false;

            RemoveAt(index);
            return true;
        }

        /// <inheritdoc />
        public virtual T[] ToArray()
        {
            return _list.ToArray();
        }

        //////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Raises the <see cref="ItemsAdded"/> event.
        /// </summary>
        /// <param name="e">An <see cref="UndoableListChangedEventArgs{T}"/> that contains the event data.</param>
        protected virtual void OnItemsAdded(UndoableListChangedEventArgs<T> e)
        {
            if (null != ItemsAdded)
                ItemsAdded(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ItemsRemoved"/> event.
        /// </summary>
        /// <param name="e">An <see cref="UndoableListChangedEventArgs{T}"/> that contains the event data.</param>
        protected virtual void OnItemsRemoved(UndoableListChangedEventArgs<T> e)
        {
            if (null != ItemsRemoved)
                ItemsRemoved(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ItemsReplaced"/> event.
        /// </summary>
        /// <param name="e">An <see cref="UndoableListReplacedEventArgs{T}"/> that contains the event data.</param>
        protected virtual void OnItemsReplaced(UndoableListReplacedEventArgs<T> e)
        {
            if (null != ItemsReplaced)
                ItemsReplaced(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ListCleared"/> event.
        /// </summary>
        /// <param name="e">An <see cref="UndoableListChangedEventArgs{T}"/> that contains the event data.</param>
        protected virtual void OnListCleared(UndoableListChangedEventArgs<T> e)
        {
            if (null != ListCleared)
                 ListCleared(this, e);
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // IEnumerable<T>

        /// <inheritdoc />
        public virtual IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // IEnumerable

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        //////////////////////////////////////////////////////////////////////////////////////

        private class ActionInsert : IAction
        {
            private UndoableList<T> _list;
            private IEnumerable<T>  _items;
            private int             _index;
            private int             _count;

            public ActionInsert(UndoableList<T> list, int index, T item) : this(list, index, new T[] { item } ) { }

            public ActionInsert(UndoableList<T> list, int index, IEnumerable<T> items)
            {
                _list  = list;
                _index = index;
                _items = items;

                foreach (T item in items)
                {
                    _count++;
                }
            }

            public void Apply()
            {
                _list._list.InsertRange(_index, _items);

                try
                {
                    _list.OnItemsAdded(new UndoableListChangedEventArgs<T>(_items, _index, _count));
                }
                catch
                {
                    _list._list.RemoveRange(_index, _items.Count());
                    throw;
                }
            }

            public void Revert()
            {
                _list._list.RemoveRange(_index, _count);

                try
                {
                    _list.OnItemsRemoved(new UndoableListChangedEventArgs<T>(_items, _index, _count));
                }
                catch
                {
                    _list._list.InsertRange(_index, _items);
                    throw;
                }
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////

        private class ActionRemove : IAction
        {
            private UndoableList<T> _list;
            private ICollection<T>  _items;
            private int             _index;

            public ActionRemove(UndoableList<T> list, int index) : this (list, index, 1) { }

            public ActionRemove(UndoableList<T> list, int index, int count)
            {
                _list  = list;
                _index = index;
                _items = list._list.GetRange(index, count);
            }

            public void Apply()
            {
                _list._list.RemoveRange(_index, _items.Count);

                try
                {
                    _list.OnItemsRemoved(new UndoableListChangedEventArgs<T>(_items, _index, _items.Count));
                }
                catch
                {
                    _list._list.InsertRange(_index, _items);
                    throw;
                }
            }

            public void Revert()
            {
                _list._list.InsertRange(_index, _items);

                try
                {
                    _list.OnItemsAdded(new UndoableListChangedEventArgs<T>(_items, _index, _items.Count));
                }
                catch
                {
                    _list._list.RemoveRange(_index, _items.Count);
                    throw;
                }
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////

        private class ActionReplaceItem : IAction
        {
            private UndoableList<T> _list;
            private T               _newItem;
            private T               _oldItem;
            private int             _index;

            public ActionReplaceItem(UndoableList<T> list, T item, int index)
            {
                _list     = list;
                _newItem  = item;
                _oldItem  = list[index];
                _index    = index;
            }

            public void Apply()
            {
                _list._list[_index] = _newItem;

                try
                {
                    _list.OnItemsReplaced(new UndoableListReplacedEventArgs<T>(new UndoableListChangedEventArgs<T>(new T[] { _newItem }, _index, 1),
                                                                               new UndoableListChangedEventArgs<T>(new T[] { _oldItem }, _index, 1)));
                }
                catch
                {
                    _list._list[_index] = _oldItem;
                    throw;
                }
            }

            public void Revert()
            {
                _list._list[_index] = _oldItem;

                try
                {
                    _list.OnItemsReplaced(new UndoableListReplacedEventArgs<T>(new UndoableListChangedEventArgs<T>(new T[] { _oldItem }, _index, 1),
                                                                               new UndoableListChangedEventArgs<T>(new T[] { _newItem }, _index, 1)));
                }
                catch
                {
                    _list._list[_index] = _newItem;
                    throw;
                }
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////

        private class ActionReplaceList : IAction
        {
            private UndoableList<T> _list;
            private IEnumerable<T>  _items;
            private List<T>         _backup;
            private int             _count;

            public ActionReplaceList(UndoableList<T> list, IEnumerable<T> items)
            {
                _list   = list;
                _items  = items;
                _backup = new List<T>(_list);

                foreach (T item in items)
                {
                    _count++;
                }
            }

            public void Apply()
            {
                _list._list.Clear();
                _list._list.AddRange(_items);

                try
                {
                    _list.OnItemsReplaced(new UndoableListReplacedEventArgs<T>(new UndoableListChangedEventArgs<T>(_items, 0, _count),
                                                                               new UndoableListChangedEventArgs<T>(_backup, 0, _backup.Count)));
                }
                catch
                {
                    _list._list.Clear();
                    _list._list.AddRange(_backup);
                    throw;
                }
            }

            public void Revert()
            {
                _list._list.Clear();
                _list._list.AddRange(_backup);

                try
                {
                    _list.OnItemsReplaced(new UndoableListReplacedEventArgs<T>(new UndoableListChangedEventArgs<T>(_backup, 0, _backup.Count),
                                                                               new UndoableListChangedEventArgs<T>(_items, 0, _count)));
                }
                catch
                {
                    _list._list.Clear();
                    _list._list.AddRange(_items);
                    throw;
                }
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////

        private class ActionClear : IAction
        {
            private UndoableList<T> _list;
            private List<T>         _backup;

            public ActionClear(UndoableList<T> list)
            {
                _list    = list;
                _backup  = new List<T>(list);
            }

            public void Apply()
            {
                _list._list.Clear();

                try
                {
                    _list.OnListCleared(new UndoableListChangedEventArgs<T>(_backup.ToArray(), 0, _backup.Count));
                }
                catch
                {
                    _list._list.AddRange(_backup);
                    throw;
                }
            }

            public void Revert()
            {
                _list._list.AddRange(_backup);

                try
                {
                    _list.OnItemsAdded(new UndoableListChangedEventArgs<T>(_backup, 0, _backup.Count));
                }
                catch
                {
                    _list._list.Clear();
                }
            }
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Represents a method which is invoked when the contents of an <see cref="UndoableList{T}"/> are added to or removed.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An <see cref="UndoableListChangedEventArgs{T}"/> that contains the event data.</param>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    public delegate void UndoableListChangedEventHandler<T>(object sender, UndoableListChangedEventArgs<T> e);

    /// <summary>
    /// Provides data for the <see cref="UndoableList{T}.ItemsAdded"/>, <see cref="UndoableList{T}.ItemsRemoved"/> and <see cref="UndoableList{T}.ListCleared"/>events.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    public sealed class UndoableListChangedEventArgs<T> : EventArgs
    {
        /// <summary>
        /// Gets the items which were added or removed.
        /// </summary>
        public IEnumerable<T> Items { get; private set; }

        /// <summary>
        /// Gets the index of the first item which was added or removed.
        /// </summary>
        /// <remarks>
        /// In the case of a <see cref="UndoableList{T}.ItemsRemoved"/> event, this is the index at which the item
        /// was present prior to its removal.
        /// </remarks>
        public int FirstIndex { get; private set; }

        /// <summary>
        /// Gets the number of items which were added or removed.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UndoableListChangedEventArgs{T}"/> class with the specified values.
        /// </summary>
        /// <param name="items">The items which were added or removed.</param>
        /// <param name="firstIndex">The index of the first item.</param>
        /// <param name="count">The number of items.</param>
        public UndoableListChangedEventArgs(IEnumerable<T> items, int firstIndex, int count)
        {
            Items      = items;
            FirstIndex = firstIndex;
            Count      = count;
        }
    }

    /// <summary>
    /// Represents a method which is invoked when the contents of an <see cref="UndoableList{T}"/> are replaced.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An <see cref="UndoableListReplacedEventArgs{T}"/> that contains the event data.</param>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    public delegate void UndoableListReplacedEventHandler<T>(object sender, UndoableListReplacedEventArgs<T> e);

    /// <summary>
    /// Provides data for the <see cref="UndoableList{T}.ItemsReplaced"/> event.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    public sealed class UndoableListReplacedEventArgs<T> : EventArgs
    {
        /// <summary>
        /// Gets the items which were added.
        /// </summary>
        public UndoableListChangedEventArgs<T> ItemsAdded { get; private set; }

        /// <summary>
        /// Gets the items which were removed.
        /// </summary>
        public UndoableListChangedEventArgs<T> ItemsRemoved { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UndoableListReplacedEventArgs{T}"/> class with the specified values.
        /// </summary>
        /// <param name="itemsAdded">The items which were added.</param>
        /// <param name="itemsRemoved">The items which were removed.</param>
        public UndoableListReplacedEventArgs(UndoableListChangedEventArgs<T> itemsAdded, UndoableListChangedEventArgs<T> itemsRemoved)
        {
            ItemsAdded   = itemsAdded;
            ItemsRemoved = itemsRemoved;
        }
    }
}
