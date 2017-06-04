using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace PerformanceAnalyzer
{
    internal class ExecutionPath
    {
        public Node Root { get; } = new Node();

        internal class Node
        {
            public IList<Edge> LeavingEdges { get; } = new List<Edge>();

            public IList<Edge> ArrivingEdges { get; } = new List<Edge>();

            internal Edge CreatePathTo(Node otherNode)
            {
                if (otherNode == null)
                {
                    throw new ArgumentNullException(nameof(otherNode));
                }

                Edge newEdge = new Edge() { StartNode = this, EndNode = otherNode };
                this.LeavingEdges.Add(newEdge);
                otherNode.ArrivingEdges.Add(newEdge);
                return newEdge;
            }
        }

        internal class Edge
        {
            public Node StartNode { get; internal set; }

            public Node EndNode { get; internal set; }

            public IList<SyntaxNode> Statements { get; } = new List<SyntaxNode>();
        }
    }
}
