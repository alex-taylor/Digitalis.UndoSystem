/*
 * IAction.cs
 *
 * Copyright (C) 2010 Alex Taylor.  All Rights Reserved.
 *
 */

using System;


namespace Digitalis.UndoSystem
{
    /// <summary>
    /// Defines an object which represents a change to an undoable object.
    /// </summary>
    public interface IAction
    {
        /// <summary>
        /// Applies the <see cref="IAction"/>.
        /// </summary>
        void Apply();

        /// <summary>
        /// Reverts the <see cref="IAction"/>.
        /// </summary>
        void Revert();
    }
}
