// <copyright file="IGraphNode.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents a node in any graph.
    /// </summary>
    public interface IGraphNode<T>
        where T : IGraphNode<T>
    {
        /// <summary>
        /// Gets a list of previous nodes in the graph.
        /// </summary>
        IReadOnlyCollection<T> PreviousNodes { get; }

        /// <summary>
        /// Gets a list of next nodes in the graph.
        /// </summary>
        IReadOnlyCollection<T> NextNodes { get; }
    }
}