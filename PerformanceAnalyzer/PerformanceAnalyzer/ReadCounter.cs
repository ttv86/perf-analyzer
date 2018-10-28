using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PerformanceAnalyzer
{
    internal class ReadCounter : Dictionary<Tuple<ExpressionSyntax, ExpressionSyntax>, Counter>
    {
        public ReadCounter(IEqualityComparer<Tuple<ExpressionSyntax, ExpressionSyntax>> comparer) : base(comparer)
        {
        }
    }

    internal class Counter
    {
        private int start = -1;
        private int end = -1;

        public Counter(int initialValue)
        {
            this.Max = this.Current = initialValue;
        }

        /// <summary>
        /// Gets the current value of the counter.
        /// </summary>
        public int Current { get; private set; }

        /// <summary>
        /// Gets the maximum value the counter ever has had.
        /// </summary>
        public int Max { get; private set; }

        /// <summary>
        /// Increases counter value by one. Stores also the span of the affected area.
        /// </summary>
        /// <param name="node"></param>
        public void Increase(SyntaxNode node)
        {
            this.Current++;
            if (this.Current > this.Max)
            {
                this.Max = this.Current;
            }

            if (this.start < 0)
            {
                // This is the first one. Use span as is.
                this.start = node.Span.Start;
                this.end = node.Span.End;
            }
            else
            {
                // There was already a span. Combine both spans to a new one.
                this.start = Math.Min(node.SpanStart, this.start);
                this.end = Math.Max(node.Span.End, this.end);
            }
        }

        /// <summary>
        /// Resets current count back to zero. Also reset span counter.
        /// </summary>
        public void Reset()
        {
            this.start = -1;
            this.end = -1;
            this.Current = 0;
        }

        internal Location GetLocation(SyntaxTree syntaxTree)
        {
            if (this.start == -1)
            {
                return null;
            }

            return Location.Create(syntaxTree, TextSpan.FromBounds(this.start, this.end));
        }
    }
}
