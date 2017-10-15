// <copyright file="IGraph.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer
{
    /// <summary>
    /// Represents any graph.
    /// </summary>
    public interface IGraph
    {
    }

    /// <summary>
    /// Represents any graph that has a specific root node.
    /// </summary>
    public interface IRootedGraph<T> : IGraph
        where T : IGraphNode<T>
    {
        /// <summary>
        /// Gets the root node of the graph.
        /// </summary>
        T Root { get; }
    }
}