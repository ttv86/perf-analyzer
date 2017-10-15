// <copyright file="GraphHelper.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace PerformanceAnalyzer
{
    internal static class GraphHelper
    {
        /// <summary>
        /// Finds a list of all the cycles in a directed graph.
        /// </summary>
        /// <param name="graph">A reference to a graph.</param>
        /// <returns>A list of all the cycles in a graph.</returns>
        internal static IReadOnlyCollection<IEnumerable<IGraphNode<T>>> FindAllCycles<T>(IGraph graph)
            where T : IGraphNode<T>
        {
            var result = new List<IEnumerable<IGraphNode<T>>>();
            T root;
            if (graph is IRootedGraph<T> rooted)
            {
                root = rooted.Root;
            }
            else
            {
                throw new NotImplementedException();
            }

            HashSet<IGraphNode<T>> visitedNodes = new HashSet<IGraphNode<T>>();
            List<NodeVisitInfo<T>> nodesToVisit = new List<NodeVisitInfo<T>>();
            Dictionary<T, IImmutableList<IGraphNode<T>>> shortestPaths = new Dictionary<T, IImmutableList<IGraphNode<T>>>();
            nodesToVisit.Add(new NodeVisitInfo<T>(root, ImmutableList.Create<IGraphNode<T>>()));
            while (nodesToVisit.Count > 0)
            {
                var first = nodesToVisit[0];
                nodesToVisit.RemoveAt(0);
                if (visitedNodes.Contains(first.Node))
                {
                    // We found a possible cycle.
                    var firstInstance = first.PathSoFar.IndexOf(first.Node);
                    if (firstInstance >= 0)
                    {
                        // Current node was found from a path here. Mark this as a cycle.
                        result.Add(new List<IGraphNode<T>>(first.PathSoFar.Skip(firstInstance)));
                    }
                    else
                    {
                        // Node wasn't on path here. Possible branch and merge.
                    }

                    continue;
                }

                shortestPaths.Add(first.Node, first.PathSoFar);
                var extendedPath = first.PathSoFar.Add(first.Node);
                visitedNodes.Add(first.Node); // Mark this node as analyzed.
                nodesToVisit.AddRange(first.Node.NextNodes.Select(x => new NodeVisitInfo<T>(x, extendedPath)));
            }

            return result;
        }

        private sealed class NodeVisitInfo<T>
            where T : IGraphNode<T>
        {
            public NodeVisitInfo(T node, ImmutableList<IGraphNode<T>> pathSoFar)
            {
                this.Node = node;
                this.PathSoFar = pathSoFar;
            }

            public T Node { get; }

            public ImmutableList<IGraphNode<T>> PathSoFar { get; }
        }
    }
}
