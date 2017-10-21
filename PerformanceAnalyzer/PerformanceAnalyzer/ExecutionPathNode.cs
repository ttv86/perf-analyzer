// <copyright file="ExecutionPath.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer
{
    using System;
    using System.Collections.Generic;
    using Microsoft.CodeAnalysis;

    /// <summary>
    /// A class that represents a node in execution path graph.
    /// </summary>
    public sealed class ExecutionPathNode : IGraphNode<ExecutionPathNode>
    {
        private List<ExecutionPathNode> nextNodes = new List<ExecutionPathNode>();
        private List<ExecutionPathNode> previousNodes = new List<ExecutionPathNode>();

        /// <summary>
        /// Gets or sets a human readble name for the node.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets reference to the original Roslyn SyntaxNode on which this node is based on.
        /// </summary>
        public SyntaxNode SyntaxNode { get; set; }

        public bool IsInCycle { get; set; }

        /// <summary>
        /// Gets a list of nodes where code execution might move to.
        /// </summary>
        public IReadOnlyCollection<ExecutionPathNode> NextNodes => this.nextNodes;

        /// <summary>
        /// Gets a list of nodes where code execution might come from.
        /// </summary>
        public IReadOnlyCollection<ExecutionPathNode> PreviousNodes => this.previousNodes;

        /// <summary>
        /// Returns a string that represents the current node.
        /// </summary>
        /// <returns>A string that represents the current node.</returns>
        public override string ToString()
        {
            return this.Name ?? this?.SyntaxNode.ToString() ?? base.ToString();
        }

        /// <summary>
        /// Creates path to another node from the current node.
        /// </summary>
        /// <param name="nextNode"></param>
        internal void CreatePathTo(ExecutionPathNode nextNode)
        {
            if (nextNode == null)
            {
                throw new ArgumentNullException(nameof(nextNode));
            }

            this.nextNodes.Add(nextNode);
            nextNode.previousNodes.Add(this);
        }
    }
}
