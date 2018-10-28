// <copyright file="MemoizationAnalyzer.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Text;

    internal class ReadCounter : IEnumerable<KeyValuePair<Tuple<ExpressionSyntax, ExpressionSyntax>, Counter>>
    {
        private ImmutableDictionary<Tuple<ExpressionSyntax, ExpressionSyntax>, Counter> dataStore;

        public ReadCounter(IEqualityComparer<Tuple<ExpressionSyntax, ExpressionSyntax>> comparer)
        {
            this.dataStore = ImmutableDictionary.Create<Tuple<ExpressionSyntax, ExpressionSyntax>, Counter>(comparer);
        }

        private ReadCounter(ImmutableDictionary<Tuple<ExpressionSyntax, ExpressionSyntax>, Counter> dataStore)
        {
            this.dataStore = dataStore;
        }

        public IEnumerator<KeyValuePair<Tuple<ExpressionSyntax, ExpressionSyntax>, Counter>> GetEnumerator()
        {
            return this.dataStore.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.dataStore.GetEnumerator();
        }

        internal bool TryGetValue(Tuple<ExpressionSyntax, ExpressionSyntax> key, out Counter value)
        {
            return this.dataStore.TryGetValue(key, out value);
        }

        /// <summary>
        /// Increments active counter on a specified key by one. Increments max counter if active counter is larger than it.
        /// </summary>
        /// <param name="key">Key to read with.</param>
        /// <returns>New ReadCounter with updated data.</returns>
        internal ReadCounter Increment(Tuple<ExpressionSyntax, ExpressionSyntax> key)
        {
            Counter newCounter;
            if (this.dataStore.TryGetValue(key, out Counter old))
            {
                newCounter = new Counter(old.Current + 1, Math.Max(old.Max, old.Current + 1));
            }
            else
            {
                // This value hasn't been used before. Add it.
                newCounter = new Counter(1, 1);
            }

            return new ReadCounter(this.dataStore.SetItem(key, newCounter));
        }

        /// <summary>
        /// Resets active counter on a specified key. Does not reset max counter.
        /// </summary>
        /// <param name="key">Key to read with.</param>
        /// <returns>New ReadCounter with updated data.</returns>
        internal ReadCounter Reset(Tuple<ExpressionSyntax, ExpressionSyntax> key)
        {
            Counter newCounter;
            if (this.dataStore.TryGetValue(key, out Counter old))
            {
                newCounter = new Counter(0, old.Max);
            }
            else
            {
                // This value hasn't been used before. Add it.
                newCounter = new Counter(0, 0);
            }

            return new ReadCounter(this.dataStore.SetItem(key, newCounter));
        }

        internal ReadCounter Merge(ReadCounter other)
        {
            if (other == null)
            {
                return this;
            }

            var newData = new Dictionary<Tuple<ExpressionSyntax, ExpressionSyntax>, Counter>(this.dataStore.KeyComparer);
            foreach (var kvp in this.dataStore)
            {
                newData.Add(kvp.Key, kvp.Value);
            }

            foreach (var kvp in other.dataStore)
            {
                if (newData.TryGetValue(kvp.Key, out Counter oldValue))
                {
                    newData[kvp.Key] = new Counter(Math.Max(kvp.Value.Current, oldValue.Current), Math.Max(kvp.Value.Max, oldValue.Max));
                }
                else
                {
                    newData.Add(kvp.Key, kvp.Value);
                }
            }

            return new ReadCounter(newData.ToImmutableDictionary());
        }
    }

    internal class Counter
    {
        public Counter(int current, int max)
        {
            this.Current = current;
            this.Max = max;
            this.Start = -1;
            this.End = -1;
        }

        public Counter(int current, int max, int start, int end)
        {
            this.Current = current;
            this.Max = max;
            this.Start = start;
            this.End = end;
        }

        /// <summary>
        /// Gets the current value of the counter.
        /// </summary>
        public int Current { get; }

        /// <summary>
        /// Gets the maximum value the counter ever has had.
        /// </summary>
        public int Max { get; }

        public int Start { get; }

        public int End { get; }

        internal Location GetLocation(SyntaxTree syntaxTree)
        {
            if (this.Start == -1)
            {
                return null;
            }

            return Location.Create(syntaxTree, TextSpan.FromBounds(this.Start, this.End));
        }
    }
}
