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

        /// <summary>
        /// Executes a given method once for every node in path.
        /// </summary>
        /// <param name="callback">Method to be executed.</param>
        public void ForEachNode(Action<ExecutionPathNode> callback)
        {
            HashSet<ExecutionPathNode> analyzedNodes = new HashSet<ExecutionPathNode>();
            List<ExecutionPathNode> analyzableNodes = new List<ExecutionPathNode>();
            analyzableNodes.Add(this.Root);
            while (analyzableNodes.Count > 0)
            {
                var first = analyzableNodes[0];
                analyzableNodes.RemoveAt(0);
                if (analyzedNodes.Contains(first))
                {
                    // This node was already analyzed. We can skip it.
                    continue;
                }

                callback(first);
                analyzedNodes.Add(first); // Mark this node as analyzed.
                analyzableNodes.AddRange(first.NextNodes);
            }
        }

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
