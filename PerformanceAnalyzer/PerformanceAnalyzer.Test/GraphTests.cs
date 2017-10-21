using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PerformanceAnalyzer.Test
{
    [TestClass]
    public class GraphTests
    {
        [TestMethod]
        [TestCategory("Graph")]
        public void TestCycleFind_Tree()
        {
            /*        A
             *      /   \
             *    B       C
             *   / \     / \
             *  D   E   F   G
             * / \ / \ / \ / \
             * H I J K L M N O
             */
            Node root = new Node("A",
                new Node("B",
                    new Node("D",
                        new Node("H"),
                        new Node("I")),
                    new Node("E",
                        new Node("J"),
                        new Node("K"))),
                new Node("C",
                    new Node("F",
                        new Node("L"),
                        new Node("M")),
                    new Node("G",
                        new Node("N"),
                        new Node("O"))));

            var actual = GraphHelper.FindAllCycles<Node>(new RootedGraph(root));
            var expected = 0;
            Assert.AreEqual(expected, actual.Count);
        }

        [TestMethod]
        [TestCategory("Graph")]
        public void TestCycleFind_SplitAndMerge()
        {
            /*
             *   A
             *  / \
             * B   C
             *  \ /
             *   D
             */
            var a = new Node("A");
            var b = new Node("B");
            var c = new Node("C");
            var d = new Node("D");
            a.NextNodes.Add(b);
            a.NextNodes.Add(c);
            b.NextNodes.Add(d);
            c.NextNodes.Add(d);

            var actual = GraphHelper.FindAllCycles<Node>(new RootedGraph(a));
            var expected = 0;
            Assert.AreEqual(expected, actual.Count);
        }

        [TestMethod]
        [TestCategory("Graph")]
        public void TestCycleFind_OneCycle()
        {
            /* A -> B
             * ^    |
             * |    v
             * D <- C
             */
            var a = new Node("A");
            var b = new Node("B");
            var c = new Node("C");
            var d = new Node("D");
            a.NextNodes.Add(b);
            b.NextNodes.Add(c);
            c.NextNodes.Add(d);
            d.NextNodes.Add(a);

            var actual = GraphHelper.FindAllCycles<Node>(new RootedGraph(a));
            var expectedCount = 1;
            Assert.AreEqual(expectedCount, actual.Count);

            var expectedFirst = new Node[] { a, b, c, d };
            AssertCollection.AreEqual(expectedFirst, actual.First());
        }

        [TestMethod]
        [TestCategory("Graph")]
        public void TestCycleFind_TwoCycles()
        {
            /* A -> B -> C -> F -> G
             *      ^    |    ^    |
             *      |    v    |    v
             *      E <- D    H <- I -> J
             */
            var a = new Node("A");
            var b = new Node("B");
            var c = new Node("C");
            var d = new Node("D");
            var e = new Node("E");
            var f = new Node("F");
            var g = new Node("G");
            var h = new Node("H");
            var i = new Node("I");
            var j = new Node("J");
            a.NextNodes.Add(b);
            b.NextNodes.Add(c);
            c.NextNodes.Add(f);
            c.NextNodes.Add(d);
            d.NextNodes.Add(e);
            e.NextNodes.Add(b);
            f.NextNodes.Add(g);
            g.NextNodes.Add(i);
            i.NextNodes.Add(j);
            i.NextNodes.Add(h);
            h.NextNodes.Add(f);

            var actual = GraphHelper.FindAllCycles<Node>(new RootedGraph(a));
            var expectedCount = 2;
            Assert.AreEqual(expectedCount, actual.Count);

            var expectedFirst = new Node[] { b, c, d, e };
            AssertCollection.AreEqual(expectedFirst, actual.First());

            var expectedSecond = new Node[] { f, g, i, h };
            AssertCollection.AreEqual(expectedFirst, actual.Skip(1).First());
        }

        [TestMethod]
        [TestCategory("Graph")]
        public void TestCycleFind_NestedCycles()
        {
            /* A -> B -> C -> D -> E -> F
             *      ^    ^    |    |
             *      |    |    v    |
             *      |    G <- H    |
             *      |              |
             *      I <----------- J
             */
            var a = new Node("A");
            var b = new Node("B");
            var c = new Node("C");
            var d = new Node("D");
            var e = new Node("E");
            var f = new Node("F");
            var g = new Node("G");
            var h = new Node("H");
            var i = new Node("I");
            var j = new Node("J");
            a.NextNodes.Add(b);
            b.NextNodes.Add(c);
            c.NextNodes.Add(d);
            d.NextNodes.Add(e);
            d.NextNodes.Add(h);
            e.NextNodes.Add(f);
            e.NextNodes.Add(j);
            g.NextNodes.Add(c);
            i.NextNodes.Add(b);
            h.NextNodes.Add(g);
            j.NextNodes.Add(i);

            var actual = GraphHelper.FindAllCycles<Node>(new RootedGraph(a));
            var expectedCount = 2;
            Assert.AreEqual(expectedCount, actual.Count);

            var expectedFirst = new Node[] { c, d, h, g };
            AssertCollection.AreEqual(expectedFirst, actual.First());

            var expectedSecond = new Node[] { b, c, d, e, j, i };
            AssertCollection.AreEqual(expectedFirst, actual.Skip(1).First());
        }

        private class RootedGraph : IRootedGraph<Node>
        {
            public RootedGraph(Node root)
            {
                this.Root = root;
            }

            public Node Root { get; }
        }

        private class Node : IGraphNode<Node>
        {
            private List<Node> prev;
            private ObservableCollection<Node> next;

            internal Node(string name, params Node[] next)
            {
                this.Name = name ?? throw new ArgumentNullException(nameof(name));
                this.prev = new List<Node>();
                this.next = new ObservableCollection<Node>(next);
                foreach (var node in next)
                {
                    node.prev.Add(this);
                }

                this.next.CollectionChanged += this.NextCollectionChanged;
            }

            private void NextCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
            {
                if (e.NewItems != null)
                {
                    foreach (Node node in e.NewItems)
                    {
                        node.prev.Add(this);
                    }
                }

                if (e.OldItems != null)
                {
                    foreach (Node node in e.OldItems)
                    {
                        node.prev.Remove(this);
                    }
                }
            }

            public string Name { get; }

            public IReadOnlyCollection<Node> PreviousNodes => this.prev;

            public ICollection<Node> NextNodes => this.next;

            IReadOnlyCollection<Node> IGraphNode<Node>.NextNodes => this.next;

            public override string ToString()
            {
                return this.Name;
            }
        }
    }
}
