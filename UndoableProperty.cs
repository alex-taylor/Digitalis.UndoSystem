/*
 * UndoableProperty.cs
 *
 * Copyright (C) 2010-2012 Alex Taylor.  All Rights Reserved.
 *
 */

using System;
using System.Runtime.Serialization;


namespace Digitalis.UndoSystem
{
    [Flags]
    public enum PropertyFlags
    {
        None = 0,

        DoNotSerializeValue = 1 << 0
    }

    /// <summary>
    /// Represents a property which supports undo/redo functionality.
    /// </summary>
    /// <typeparam name="T">The type of property.</typeparam>
    /// <remarks>
    /// To use, declare a private instance of this as the backing-store for your property. When your property's
    /// getter and setter are invoked, have them get/set <see cref="Value"/>.
    /// <para>If the property's <see cref="Value"/> is set multiple times consecutively within a single command,
    /// the changes will be concatenated into a single <see cref="IAction"/> for efficiency.</para>
    /// <code>
    ///    private int UndoableProperty&lt;int&gt; myPropertyStore = new UndoableProperty&lt;int&gt;(myPropertyInitialValue);
    ///    public int MyProperty { get { return myPropertyStore.Value; } set { myPropertyStore.Value = value; } }
    /// </code>
    /// </remarks>
    [Serializable]
    public class UndoableProperty<T>
    {
        private PropertyFlags _flags;
        private T _initialValue;

        /// <summary>
        /// Occurs when <see cref="Value"/> changes.
        /// </summary>
        public event PropertyChangedEventHandler<T> ValueChanged;

        /// <summary>
        /// Gets or sets the value of the <see cref="UndoableProperty{T}"/>.
        /// </summary>
        /// <remarks>
        /// Raises the <see cref="ValueChanged"/> event when its value changes, either directly or via <see cref="IAction.Apply"/> or <see cref="IAction.Revert"/>.
        /// </remarks>
        public T Value
        {
            get { return _value; }
            set
            {
                Action action = UndoStack.LastAction() as Action;

                if (null == action || this != action.Target)
                {
                    UndoStack.AddAction(new Action(this, value, _value));
                }
                else
                {
                    action.NewValue = value;
                    action.Apply();
                }
            }
        }
        private T _value;

        //////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Initializes an instance of the <see cref="UndoableProperty{T}"/> class with default values.
        /// </summary>
        public UndoableProperty()
            : this(default(T))
        {
        }

        /// <summary>
        /// Initializes an instance of the <see cref="UndoableProperty{T}"/> class with the specified value.
        /// </summary>
        /// <param name="initialValue">The initial value of the <see cref="UndoableProperty{T}"/>.</param>
        public UndoableProperty(T initialValue)
            : this(initialValue, PropertyFlags.None)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UndoableProperty{T}"/> class with the specified values.
        /// </summary>
        /// <param name="initialValue">The initial value of the <see cref="UndoableProperty{T}"/>.</param>
        /// <param name="flags">The flags.</param>
        public UndoableProperty(T initialValue, PropertyFlags flags)
        {
            _initialValue = initialValue;
            _flags        = flags;
            _value        = initialValue;
        }

        [OnSerializing]
        private void OnSerializing(StreamingContext sc)
        {
            if (0 != (PropertyFlags.DoNotSerializeValue & _flags))
                _value = _initialValue;
        }

        //////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Raises the <see cref="ValueChanged"/> event.
        /// </summary>
        /// <param name="e">A <see cref="PropertyChangedEventArgs{T}"/> that contains the event data.</param>
        protected virtual void OnValueChanged(PropertyChangedEventArgs<T> e)
        {
            if (null != ValueChanged)
                ValueChanged(this, e);
        }

        //////////////////////////////////////////////////////////////////////////////////////

        private class Action : IAction
        {
            public UndoableProperty<T> Target;
            public T                   NewValue;
            public T                   OldValue;

            public Action(UndoableProperty<T> target, T newValue, T oldValue)
            {
                Target   = target;
                NewValue = newValue;
                OldValue = oldValue;
            }

            public void Apply()
            {
                Target._value = NewValue;

                try
                {
                    Target.OnValueChanged(new PropertyChangedEventArgs<T>(OldValue, NewValue));
                }
                catch
                {
                    Target._value = OldValue;
                    throw;
                }
            }

            public void Revert()
            {
                Target._value = OldValue;

                try
                {
                    Target.OnValueChanged(new PropertyChangedEventArgs<T>(NewValue, OldValue));
                }
                catch
                {
                    Target._value = NewValue;
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Represents a method which is invoked when the value of an <see cref="UndoableProperty{T}"/> changes.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">A <see cref="PropertyChangedEventArgs{T}"/> that contains the event data.</param>
    /// <typeparam name="T">The type of the <see cref="UndoableProperty{T}"/>.</typeparam>
    public delegate void PropertyChangedEventHandler<T>(object sender, PropertyChangedEventArgs<T> e);

    /// <summary>
    /// Provides data for the <see cref="UndoableProperty{T}.ValueChanged"/> event.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="UndoableProperty{T}"/>.</typeparam>
    public sealed class PropertyChangedEventArgs<T> : EventArgs
    {
        /// <summary>
        /// Gets the previous value of the <see cref="UndoableProperty{T}"/>.
        /// </summary>
        public T OldValue { get; private set; }

        /// <summary>
        /// Gets the current value of the <see cref="UndoableProperty{T}"/>.
        /// </summary>
        public T NewValue{ get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyChangedEventArgs{T}"/> class with the specified values.
        /// </summary>
        /// <param name="oldValue">The previous value of the <see cref="UndoableProperty{T}"/>.</param>
        /// <param name="newValue">The new value of the <see cref="UndoableProperty{T}"/>.</param>
        public PropertyChangedEventArgs(T oldValue, T newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
