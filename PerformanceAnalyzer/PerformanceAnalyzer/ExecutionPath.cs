// <copyright file="ExecutionPath.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer
{
    using System;
    using System.Collections.Generic;
    using Microsoft.CodeAnalysis;

    /// <summary>
    /// A class that represents the code execution graph.
    /// </summary>
    public class ExecutionPath : IRootedGraph<ExecutionPathNode>
    {
        private ExecutionPathNode root = new ExecutionPathNode();

        /// <summary>
        /// Gets the root node (entry point) for the current execution path.
        /// </summary>
        public ExecutionPathNode Root => this.root;

        ////public sealed class NodePair
        ////{
        ////    public NodePair(Node node1, Node node2)
        ////    {
        ////        this.Node1 = node1;
        ////        this.Node2 = node2;
        ////    }

        ////    public Node Node1 { get; }

        ////    public Node Node2 { get; }
        ////}

        ////public class Edge
        ////{
        ////    public Node StartNode { get; internal set; }

        ////    public Node EndNode { get; internal set; }

        ////    public IList<SyntaxNode> Statements { get; } = new List<SyntaxNode>();

        ////    public override string ToString()
        ////    {
        ////        return string.Join("; ", Statements);
        ////    }
        ////}
    }
}
