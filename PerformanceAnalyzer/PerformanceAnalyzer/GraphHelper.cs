// <copyright file="GraphHelper.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;

    internal static class GraphHelper
    {
        /// <summary>
        /// Finds a list of all the cycles in a directed graph.
        /// </summary>
        /// <param name="graph">A reference to a graph.</param>
        /// <returns>A list of all the cycles in a graph.</returns>
        internal static IReadOnlyCollection<IEnumerable<T>> FindAllCycles<T>(IGraph graph)
            where T : IGraphNode<T>
        {
            T root;
            if (graph is IRootedGraph<T> rooted)
            {
                root = rooted.Root;
            }
            else
            {
                throw new NotImplementedException("Invalid root element.");
            }

            List<IEnumerable<T>> result = new List<IEnumerable<T>>();
            HashSet<IGraphNode<T>> visitedNodes = new HashSet<IGraphNode<T>>();
            List<NodeVisitInfo<T>> nodesToVisit = new List<NodeVisitInfo<T>>();
            ////Dictionary<T, IImmutableList<IGraphNode<T>>> shortestPaths = new Dictionary<T, IImmutableList<IGraphNode<T>>>();
            nodesToVisit.Add(new NodeVisitInfo<T>(root, ImmutableList.Create<IGraphNode<T>>()));
            while (nodesToVisit.Count > 0)
            {
                NodeVisitInfo<T> first = nodesToVisit[0];
                nodesToVisit.RemoveAt(0);
                if (visitedNodes.Contains(first.Node))
                {
                    // We found a possible cycle.
                    int firstInstance = first.PathSoFar.IndexOf(first.Node);
                    if (firstInstance >= 0)
                    {
                        // Current node was found from a path here. Mark this as a cycle.
                        result.Add(new List<T>(first.PathSoFar.Skip(firstInstance).Cast<T>()));
                    }
                    else
                    {
                        // Node wasn't on path here. Possible branch and merge.
                    }

                    continue;
                }

                ////shortestPaths.Add(first.Node, first.PathSoFar);
                var extendedPath = first.PathSoFar.Add(first.Node);
                visitedNodes.Add(first.Node); // Mark this node as analyzed.
                nodesToVisit.AddRange(first.Node.NextNodes.Select(x => new NodeVisitInfo<T>(x, extendedPath)));
            }

            return result;
        }

        /// <summary>
        /// A debug helper that draws a tree to svg file.
        /// </summary>
        internal static void TreeToSvg(ExecutionPath executionPath, string filePath)
        {
            // TODO: If there's time
            /*
            ////Dictionary<ExecutionPathNode, Tuple<double, double>> visited = new Dictionary<ExecutionPathNode, Tuple<double, double>>();
            HashSet<ExecutionPathNode> visited = new HashSet<ExecutionPathNode>();
            Queue<Tuple<ExecutionPathNode, Lane>> unvisited = new Queue<Tuple<ExecutionPathNode, Lane>>();
            Lane rootLane = new Lane(null);
            unvisited.Enqueue(new Tuple<ExecutionPathNode, Lane>(executionPath.Root, rootLane));
            Tuple<ExecutionPathNode, Lane> current;
            while (unvisited.Count > 0)
            {
                current = unvisited.Dequeue();
                if (visited.Contains(current.Item1))
                {
                    continue;
                }

                if (current.Item1.PreviousNodes.Count > 1)
                {

                }

                visited.Add(current.Item1);
                int nodeCount = current.Item1.NextNodes.Count;
                if (nodeCount == 1)
                {
                    var onlyNode = current.Item1.NextNodes.First();
                    current.Item2.Children.Add(onlyNode);
                    unvisited.Enqueue(new Tuple<ExecutionPathNode, Lane>(onlyNode, current.Item2));
                }
                else
                {
                    current.Item2.Width += nodeCount - 1;
                    foreach (var node in current.Item1.NextNodes)
                    {
                        var newLane = new Lane(current.Item2);
                        current.Item2.Children.Add(newLane);
                        unvisited.Enqueue(new Tuple<ExecutionPathNode, Lane>(node, newLane));
                    }
                }
            }

            ////double height = y * 20;
            ////using (Stream stream = File.Create(filePath))
            ////{
            ////    using (XmlWriter writer = XmlWriter.Create(stream))
            ////    {
            ////        writer.WriteStartDocument(true);
            ////        writer.WriteStartElement(string.Empty, "svg", "http://www.w3.org/2000/svg");
            ////        writer.WriteAttributeString("version", "1.1");
            ////        writer.WriteAttributeString("width", "1000");
            ////        writer.WriteAttributeString("height", height.ToString(CultureInfo.InvariantCulture));
            ////        writer.WriteEndElement();
            ////        writer.WriteEndDocument();
            ////    }
            ////}
            */
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

        /*
        // TODO: If there's time
        private class Lane
        {
            private int width;

            public Lane(Lane parent)
            {
                this.Parent = parent;
                this.width = 1;
                this.Children = new List<object>();
            }

            public Lane Parent { get; }

            public List<object> Children { get; }

            public int Width
            {
                get
                {
                    return this.width;
                }

                set
                {
                    int delta = value - this.width;
                    if (delta != 0)
                    {
                        this.width = value;
                        if (this.Parent != null)
                        {
                            this.Parent.Width += delta;
                        }
                    }
                }
            }
        }*/
    }
}