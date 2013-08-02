/*
 * UndoStack.cs
 *
 * Copyright (C) 2010-2011 Alex Taylor.  All Rights Reserved.
 *
 */

using System;
using System.Collections.Generic;


namespace Digitalis.UndoSystem
{
    /// <summary>
    /// Represents a stack of undoable commands.
    /// </summary>
    /// <remarks>
    /// <b>UndoStack</b> provides a flexible undo/redo mechanism, based on the notion that each entry in the stack is a
    /// 'command' consisting of one or more 'actions'. Actions may be added to a command from disparate sources, and are
    /// guaranteed to be applied in the order they were added, and undone in the reverse of that order.
    /// <para>Multiple <b>UndoStack</b>s may be created, although only one stack may be active per thread, and only one
    /// command per stack. <see cref="StartCommand">Starting</see> a command on an <b>UndoStack</b> automatically makes
    /// it the active one for the thread.</para>
    /// <para><b>UndoStack</b> supports random-access to the array of stored commands, as well as traditional <see cref="Undo"/>
    /// and <see cref="Redo"/> functions. The <see cref="Size"/> of the array may be set, and events are available for
    /// all major actions.</para>
    /// <para>Adding undo/redo support to an application involves creating an <b>UndoStack</b> to manage the array, and
    /// <see cref="StartCommand">adding commands</see> to it. <see cref="IAction"/>s can be added to commands from any source;
    /// a typical pattern is to use <see cref="UndoableProperty{T}"/> to implement the properties of a class.</para>
    /// <para>Because <see cref="IAction"/>s are <see cref="IAction.Apply">applied</see> as they are added, the command is complete
    /// when <see cref="EndCommand()"/> is called. It can be abandoned by calling <see cref="CancelCommand"/> instead, which will roll
    /// back the <see cref="IAction"/>s in the reverse of the order they were added.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// UndoStack stack = new UndoStack();
    ///
    /// stack.StartCommand("Perform some function");        // initiate a new command
    /// UndoStack.AddAction(new Action());                  // add some action to it; this will execute immediately
    /// UndoStack.AddAction(new Action());                  // add another action
    /// stack.EndCommand();                                 // and complete the command
    ///
    /// stack.Undo();                                       // the last command is undone
    /// </code>
    /// </example>
    public class UndoStack
    {
        [ThreadStatic]
        private static UndoStack _currentStack;

        /// <summary>
        /// Adds an <see cref="IAction"/> to the current command.
        /// </summary>
        /// <param name="action">The <see cref="IAction"/> to add. It will be <see cref="IAction.Apply">applied</see> immediately, regardless of whether there is a current command.</param>
        /// <remarks>
        /// If there is no current command, or the current command is suspended, the method returns after applying the <see cref="IAction"/>. This simplifies the process of adding undo/redo support to a system:
        /// the application has the choice of whether or not to enable support without having to modify code which adds actions to handle both cases.
        /// </remarks>
        public static void AddAction(IAction action)
        {
            if (null == _currentStack || !_currentStack.IsCommandStarted || _currentStack.IsCommandSuspended)
            {
                action.Apply();
                return;
            }

            _currentStack._currentCommand.AddAction(action);
            action.Apply();
        }

        /// <summary>
        /// Returns the last <see cref="IAction"/> added to the current command.
        /// </summary>
        /// <returns>An <see cref="IAction"/>, or <c>null</c> if the current command is empty.</returns>
        /// <remarks>
        /// If there is no current command, or the current command is suspended, the method returns <c>null</c>.
        /// </remarks>
        public static IAction LastAction()
        {
            if (null == _currentStack || !_currentStack.IsCommandStarted || _currentStack.IsCommandSuspended)
                return null;

            return _currentStack._currentCommand.LastAction();
        }

        /// <summary>
        /// Returns the current <see cref="UndoStack"/>.
        /// </summary>
        public static UndoStack CurrentStack { get { return _currentStack; } }

        //////////////////////////////////////////////////////////////////////////////////////

        private List<Command> _commands       = new List<Command>();
        private Command       _currentCommand = null;
        private int           _suspendCount   = 0;
        private int           _lastSavePoint  = -1;

        //////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Occurs when a new command is started.
        /// </summary>
        public event EventHandler CommandStarted;

        /// <summary>
        /// Occurs when a command is ended.
        /// </summary>
        public event EventHandler CommandEnded;

        /// <summary>
        /// Occurs when a command is cancelled.
        /// </summary>
        public event EventHandler CommandCancelled;

        /// <summary>
        /// Occurs when a command is executed.
        /// </summary>
        public event EventHandler CommandExecuted;

        /// <summary>
        /// Occurs when a command is rolled back.
        /// </summary>
        public event EventHandler CommandRolledBack;

        /// <summary>
        /// Occurs when a command is discarded from the <see cref="UndoStack"/>.
        /// </summary>
        /// <remarks>
        /// If a new command is <see cref="EndCommand()">added</see> to an <see cref="UndoStack"/> which has reached its <see cref="Size">capacity</see>,
        /// the oldest command in the stack is discarded.
        /// </remarks>
        public event EventHandler CommandDiscarded;

        /// <summary>
        /// Gets the identifier of the current command, if any.
        /// </summary>
        /// <remarks>
        /// This is only valid while <see cref="IsCommandStarted"/> is <b>true</b>; otherwise it returns <c>null</c>.
        /// </remarks>
        public object CurrentCommand { get { if (null != _currentCommand) return _currentCommand.Identifier; return null; } }

        /// <summary>
        /// Gets the identifier of the specified command.
        /// </summary>
        /// <param name="index">The index of the command to get.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0 or greater than or equal to <see cref="Count"/>.</exception>
        public object this[int index] { get { return _commands[index].Identifier; } }

        /// <summary>
        /// Gets the number of commands in the <see cref="UndoStack"/>.
        /// </summary>
        public int Count { get { return _commands.Count; } }

        /// <summary>
        /// Gets a value indicating whether the <see cref="UndoStack"/> contains commands which may be undone.
        /// </summary>
        public bool CanUndo { get { return _position >= 0; } }

        /// <summary>
        /// Gets a value indicating whether the <see cref="UndoStack"/> contains commands which may be redone.
        /// </summary>
        public bool CanRedo { get { return _position < Count - 1; } }

        /// <summary>
        /// Gets a value indicating whether any commands have been executed, undone or redone since the last call to <see cref="SetSavePoint"/>.
        /// </summary>
        public bool HasUnsavedChanges { get { return _lastSavePoint != Position; } }

        /// <summary>
        /// Gets a value indicating whether there is a current command.
        /// </summary>
        /// <remarks>
        /// This returns <b>true</b> if called between <see cref="StartCommand"/> and <see cref="EndCommand()"/>/<see cref="CancelCommand"/>, and <b>false</b> otherwise.
        /// </remarks>
        public bool IsCommandStarted { get { return (null != _currentCommand); } }

        /// <summary>
        /// Gets a value indicating whether the current command is suspended.
        /// </summary>
        public bool IsCommandSuspended { get { return _suspendCount > 0; } }

        /// <summary>
        /// Gets a value indicating whether the <see cref="UndoStack"/> is <see cref="Undo">undoing</see> a command.
        /// </summary>
        public bool IsUndoing { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the <see cref="UndoStack"/> is <see cref="Redo">redoing</see> a command.
        /// </summary>
        public bool IsRedoing { get; private set; }

        /// <summary>
        /// Gets the list of command identifiers.
        /// </summary>
        /// <remarks>
        /// Commands are ordered from oldest to newest, with indices less than or equal to <see cref="Position"/> being those which are
        /// available for undoing and those greater than <see cref="Position"/> being available for redoing.
        /// </remarks>
        public object[] Commands
        {
            get
            {
                object[] _identifiers = new object[Count];

                for (int i = 0; i < Count; i++)
                {
                    _identifiers[i] = _commands[i].Identifier;
                }

                return _identifiers;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of commands which may be stored.
        /// </summary>
        /// <remarks>
        /// If this is set to a value less than the number of commands currently stored, the oldest will be discarded.
        /// <para>To have no limit on the number of commands, set to zero.</para>
        /// </remarks>
        public int Size
        {
            get { return _size; }
            set
            {
                if (value < 0)
                    return;

                _size = value;

                if (_size > 0 && Count > _size)
                {
                    int delta = Count - _size;

                    _commands.RemoveRange(0, delta);

                    if (_lastSavePoint < delta)
                        _lastSavePoint = -2;
                    else
                        _lastSavePoint -= delta;

                    if (_position >= Count)
                        _position = Count - 1;
                }
            }
        }
        private int _size;

        /// <summary>
        /// Gets or sets the current cursor position in the <see cref="UndoStack"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">A command is started on the <see cref="UndoStack"/>.</exception>
        /// <remarks>
        /// This is the index into the <see cref="UndoStack"/> of the command that will be undone on the next call to
        /// <see cref="Undo"/>. If there are no commands to undo, it returns -1.
        /// <para>Commands are ordered from oldest to newest, with indices less than or equal to <see cref="Position"/> being those which are
        /// available for undoing and those greater than <see cref="Position"/> being available for redoing.</para>
        /// <para>If successful, setting this property raises either the <see cref="CommandExecuted"/> or <see cref="CommandRolledBack"/> event.</para>
        /// </remarks>
        public int Position
        {
            get { return _position; }
            set
            {
                if (IsCommandStarted)
                    throw new InvalidOperationException();

                try
                {
                    _currentStack = this;

                    if (value >= Count)
                        value = Count - 1;
                    else if (value < -1)
                        value = -1;

                    int oldValue = _position;

                    _position = value;

                    if (value > oldValue)
                    {
                        for (int i = oldValue + 1; i <= value; i++)
                        {
                            _commands[i].Execute();
                            OnCommandExecuted(EventArgs.Empty);
                        }
                    }
                    else if (value < oldValue)
                    {
                        for (int i = oldValue; i > value; i--)
                        {
                            _commands[i].Rollback();
                            OnCommandRolledBack(EventArgs.Empty);
                        }
                    }
                }
                finally
                {
                    _currentStack = null;
                }
            }
        }
        private int _position = -1;

        //////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Initializes an instance of the <see cref="UndoStack"/> class with an unlimited initial <see cref="Size"/>.
        /// </summary>
        public UndoStack() { }

        /// <summary>
        /// Initializes an instance of the <see cref="UndoStack"/> class with the specified initial <see cref="Size"/>.
        /// </summary>
        /// <param name="initialSize">The initial size of the stack.</param>
        public UndoStack(int initialSize)
        {
            Size = initialSize;
        }

        //////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the <see cref="UndoStack"/> of all commands.
        /// </summary>
        public void Clear()
        {
            _commands.Clear();
            _position      = -1;
            _lastSavePoint = -1;
        }

        /// <summary>
        /// Initiates a new command.
        /// </summary>
        /// <param name="identifier">An optional identifier for the new command. If not required, pass <c>null</c>.</param>
        /// <exception cref="InvalidOperationException">A command is already started on an <see cref="UndoStack"/> in the same thread.</exception>
        /// <remarks>
        /// Commands are not added to the <see cref="UndoStack"/> until they are <see cref="EndCommand()">committed</see>. If successful, this method raises the <see cref="CommandStarted"/> event.
        /// <para><paramref name="identifier"/> may be used for anything you like. It is only used by <see cref="UndoStack"/> in order to determine whether two consecutive
        /// commands may be <see cref="EndCommand(bool)">merged</see>, so the only requirement is that <see cref="Object.Equals(object)"/> can be used to check whether two identifiers
        /// are equivalent. For example, <paramref name="identifier"/> might be used to hold a string describing the function of the command which can then be displayed to the user
        /// to inform them what they are about to undo or redo.</para>
        /// </remarks>
        public void StartCommand(object identifier)
        {
            if (null != _currentStack)
                throw new InvalidOperationException();

            try
            {
                _currentStack   = this;
                _currentCommand = new Command(identifier);
                OnCommandStarted(EventArgs.Empty);
            }
            catch
            {
                _currentStack = null;
                throw;
            }
        }

        /// <summary>
        /// Ends the current command and adds it to the <see cref="UndoStack"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">There is no command started on the <see cref="UndoStack"/>.</exception>
        /// <remarks>
        ///  If successful, this method raises the <see cref="CommandEnded"/> event, adds the command to the <see cref="UndoStack"/> and raises the <see cref="CommandExecuted"/> event.
        /// The <see cref="CommandEnded"/> event may add further <see cref="IAction"/>s to the command if it wishes.
        /// </remarks>
        public void EndCommand()
        {
            EndCommand(false);
        }

        /// <summary>
        /// Ends the current command and adds it to the <see cref="UndoStack"/>.
        /// </summary>
        /// <param name="mergeable">If <b>true</b> and the last command has the same identifier as the current one,
        /// the <see cref="IAction"/>s in this command are added to the previous one. Otherwise, the command is added to
        /// the end of the <see cref="UndoStack"/>.</param>
        /// <exception cref="InvalidOperationException">There is no command started on the <see cref="UndoStack"/>.</exception>
        /// <remarks>
        ///  If successful, this method raises the <see cref="CommandEnded"/> event, adds the command to the <see cref="UndoStack"/> and raises the <see cref="CommandExecuted"/> event.
        /// The <see cref="CommandEnded"/> event may add further <see cref="IAction"/>s to the command if it wishes.
        /// </remarks>
        public void EndCommand(bool mergeable)
        {
            if (!IsCommandStarted)
                throw new InvalidOperationException();

            try
            {
                if (_currentCommand.HasActions)
                {
                    if (mergeable && _position >= 0 && _commands[_position].Identifier.Equals(_currentCommand.Identifier))
                    {
                        _commands[_position].Merge(_currentCommand);
                    }
                    else
                    {
                        if (CanRedo)
                            _commands.RemoveRange(_position + 1, Count - _position - 1);

                        _commands.Add(_currentCommand);
                        _position = Count - 1;

                        if (Size > 0 && Count > Size)
                        {
                            if (_lastSavePoint < 1)
                                _lastSavePoint = -2;
                            else
                                _lastSavePoint--;

                            _commands.RemoveAt(0);
                            _position--;
                            OnCommandDiscarded(EventArgs.Empty);
                        }
                    }
                }

                OnCommandEnded(EventArgs.Empty);
                _currentCommand = null;
                _currentStack   = null;
                _suspendCount   = 0;
                OnCommandExecuted(EventArgs.Empty);
            }
            finally
            {
                _currentCommand = null;
                _currentStack   = null;
                _suspendCount   = 0;
            }
        }

        /// <summary>
        /// Abandons a command, reverting any changes it made.
        /// </summary>
        /// <exception cref="InvalidOperationException">There is no command started on the <see cref="UndoStack"/>.</exception>
        /// <remarks>
        /// The current command is rolled back, reverting its <see cref="IAction"/>s in the reverse order they were added.
        /// <para>If successful, this method raises the <see cref="CommandCancelled"/> event.</para>
        /// </remarks>
        public void CancelCommand()
        {
            if (!IsCommandStarted)
                throw new InvalidOperationException();

            OnCommandCancelled(EventArgs.Empty);

            try
            {
                Command cmd = _currentCommand;

                _currentCommand = null;
                cmd.Rollback();
            }
            finally
            {
                _currentStack = null;
                _suspendCount = 0;
            }
        }

        /// <summary>
        /// Suspends the current command.
        /// </summary>
        /// <remarks>
        /// While suspended, <see cref="IAction"/>s will not be added to the command, but will execute immediately and irrevocably.
        /// The purpose of this function is to allow operations to take place inside the context of a command but which should not
        /// be undoable - for example, creating a new object.
        /// <para>When done, the command can be resumed by calling <see cref="ResumeCommand"/>. The suspend/resume functionality
        /// 'nests', so further actions will only be added to the command once every call to <b>SuspendCommand()</b> is matched by
        /// exactly one call to <see cref="ResumeCommand"/>.</para>
        /// <para>If no command is started, this method has no effect.</para>
        /// </remarks>
        /// <seealso cref="ResumeCommand"/>.
        public void SuspendCommand()
        {
            if (IsCommandStarted)
                _suspendCount++;
        }

        /// <summary>
        /// Resumes the current command.
        /// </summary>
        /// <remarks>
        /// While suspended, <see cref="IAction"/>s will not be added to the command, but will execute immediately and irrevocably.
        /// The purpose of this function is to allow operations to take place inside the context of a command but which should not
        /// be undoable - for example, creating a new object.
        /// <para>The suspend/resume functionality 'nests', so further actions will only be added to the command once every call to
        /// <see cref="SuspendCommand"/> is matched by exactly one call to <b>ResumeCommand()</b>.</para>
        /// <para>If no command is started, or if the current command is not suspended, this method has no effect.</para>
        /// </remarks>
        /// <seealso cref="SuspendCommand"/>.
        public void ResumeCommand()
        {
            if (IsCommandStarted && _suspendCount > 0)
                _suspendCount--;
        }

        /// <summary>
        /// Undoes the last command on the <see cref="UndoStack"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">There are no commands to undo.</exception>
        /// <remarks>
        /// If successful, this method raises the <see cref="CommandRolledBack"/> event.
        /// </remarks>
        public void Undo()
        {
            if (!CanUndo)
                throw new InvalidOperationException();

            try
            {
                IsUndoing = true;
                Position--;
            }
            finally
            {
                IsUndoing = false;
            }
        }

        /// <summary>
        /// Redoes the last undone command on the <see cref="UndoStack"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">There are no commands to redo.</exception>
        /// <remarks>
        /// If successful, this method raises the <see cref="CommandExecuted"/> event.
        /// </remarks>
        public void Redo()
        {
            if (!CanRedo)
                throw new InvalidOperationException();

            try
            {
                IsRedoing = true;
                Position++;
            }
            finally
            {
                IsRedoing = false;
            }
        }

        /// <summary>
        /// Sets a marker on the current <see cref="Position"/>.
        /// </summary>
        /// <remarks>
        /// This method, along with the property <see cref="HasUnsavedChanges"/>, provides a simple mechanism for
        /// determining whether any commands have been executed, undone or redone since the last time the method
        /// was called.
        /// <para>While an application could achieve the same result by saving the value of <see cref="Position"/> itself,
        /// this method will take into account the fact that <see cref="Position"/> will change if the <see cref="UndoStack"/>
        /// reaches its <see cref="Size"/> and has to be trimmed.</para>
        /// </remarks>
        public void SetSavePoint()
        {
            _lastSavePoint = Position;
        }

        //////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Raises the <see cref="CommandStarted"/> event.
        /// </summary>
        /// <param name="e">An <see cref="EventArgs"/> that contains the event data.</param>
        protected virtual void OnCommandStarted(EventArgs e)
        {
            if (null != CommandStarted)
                CommandStarted(this, e);
        }

        /// <summary>
        /// Raises the <see cref="CommandEnded"/> event.
        /// </summary>
        /// <param name="e">An <see cref="EventArgs"/> that contains the event data.</param>
        protected virtual void OnCommandEnded(EventArgs e)
        {
            if (null != CommandEnded)
                CommandEnded(this, e);
        }

        /// <summary>
        /// Raises the <see cref="CommandCancelled"/> event.
        /// </summary>
        /// <param name="e">An <see cref="EventArgs"/> that contains the event data.</param>
        protected virtual void OnCommandCancelled(EventArgs e)
        {
            if (null != CommandCancelled)
                CommandCancelled(this, e);
        }

        /// <summary>
        /// Raises the <see cref="CommandExecuted"/> event.
        /// </summary>
        /// <param name="e">An <see cref="EventArgs"/> that contains the event data.</param>
        protected virtual void OnCommandExecuted(EventArgs e)
        {
            if (null != CommandExecuted)
                CommandExecuted(this, e);
        }

        /// <summary>
        /// Raises the <see cref="CommandRolledBack"/> event.
        /// </summary>
        /// <param name="e">An <see cref="EventArgs"/> that contains the event data.</param>
        protected virtual void OnCommandRolledBack(EventArgs e)
        {
            if (null != CommandRolledBack)
                CommandRolledBack(this, e);
        }

        /// <summary>
        /// Raises the <see cref="CommandDiscarded"/> event.
        /// </summary>
        /// <param name="e">An <see cref="EventArgs"/> that contains the event data.</param>
        protected virtual void OnCommandDiscarded(EventArgs e)
        {
            if (null != CommandDiscarded)
                CommandDiscarded(this, e);
        }

        //////////////////////////////////////////////////////////////////////////////////////

        private class Command
        {
            public object Identifier { get; private set; }
            public bool HasActions { get { return _actions.Count > 0; } }

            private List<IAction> _actions = new List<IAction>();

            public Command(object identifier)
            {
                Identifier = identifier;
            }

            public void AddAction(IAction action)
            {
                _actions.Add(action);
            }

            public IAction LastAction()
            {
                if (!HasActions)
                    return null;

                return _actions[_actions.Count - 1];
            }

            public void Execute()
            {
                int i = 0;

                try
                {
                    for (; i < _actions.Count; i++)
                    {
                        _actions[i].Apply();
                    }
                }
                catch
                {
                    i--;

                    for (; i >= 0; i--)
                    {
                        _actions[i].Revert();
                    }

                    throw;
                }
            }

            public void Rollback()
            {
                int i = 0;

                try
                {
                    for (i = _actions.Count - 1; i >= 0; i--)
                    {
                        _actions[i].Revert();
                    }
                }
                catch
                {
                    i++;

                    for (; i < _actions.Count; i++)
                    {
                        _actions[i].Apply();
                    }

                    throw;
                }
            }

            public void Merge(Command command)
            {
                foreach (IAction action in command._actions)
                {
                    _actions.Add(action);
                }
            }
        }
    }
}
