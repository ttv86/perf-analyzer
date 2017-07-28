using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace PerformanceAnalyzer
{
    public class ExecutionPath
    {
        public Node Root { get; } = new Node();

        public sealed class Node
        {
            public string Name { get; set; }

            public SyntaxNode SyntaxNode { get; set; }

            public IList<Node> NextNodes { get; } = new List<Node>();

            public IList<Node> PreviousNodes { get; } = new List<Node>();

            internal void CreatePathTo(Node nextNode)
            {
                if (nextNode == null)
                {
                    throw new ArgumentNullException(nameof(nextNode));
                }

                this.NextNodes.Add(nextNode);
                nextNode.PreviousNodes.Add(this);
            }

            public override string ToString()
            {
                return this.Name ?? this?.SyntaxNode.ToString() ?? base.ToString();
            }
        }

        public sealed class NodePair
        {
            public NodePair(Node node1, Node node2)
            {
                this.Node1 = node1;
                this.Node2 = node2;
            }

            public Node Node1 { get; }

            public Node Node2 { get; }
        }

        //public class Edge
        //{
        //    public Node StartNode { get; internal set; }

        //    public Node EndNode { get; internal set; }

        //    public IList<SyntaxNode> Statements { get; } = new List<SyntaxNode>();

        //    public override string ToString()
        //    {
        //        return string.Join("; ", Statements);
        //    }
        //}
    }
}
