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
        internal ReadCounter Increment(Tuple<ExpressionSyntax, ExpressionSyntax> key, Location location)
        {
            Counter newCounter;
            if (this.dataStore.TryGetValue(key, out Counter old))
            {
                newCounter = old.Increment(location);
            }
            else
            {
                // This value hasn't been used before. Add it.
                newCounter = new Counter(1, location);
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
                newCounter = old.Reset();
            }
            else
            {
                newCounter = new Counter(0, null);
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
                    newData[kvp.Key] = kvp.Value.Merge(oldValue);
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
        private Location currentLocation;
        private Location maxLocation;

        private Counter(int current, int max)
        {
            this.Current = current;
            this.Max = max;
        }

        public Counter(int current, Location location)
        {
            this.Current = current;
            this.Max = current;
            this.currentLocation = location;
            this.maxLocation = location;
        }

        /// <summary>
        /// Gets the current value of the counter.
        /// </summary>
        public int Current { get; }

        /// <summary>
        /// Gets the maximum value the counter ever has had.
        /// </summary>
        public int Max { get; }

        public Counter Increment(Location location)
        {
            Location newCurrentLocation, newMaxLocation;

            if (this.currentLocation != null)
            {
                newCurrentLocation = Location.Create(
                    this.currentLocation.SourceTree,
                    TextSpan.FromBounds(
                        Math.Min(location.SourceSpan.Start, this.currentLocation.SourceSpan.Start),
                        Math.Max(location.SourceSpan.End, this.currentLocation.SourceSpan.End)));
            }
            else
            {
                newCurrentLocation = location;
            }

            int newCurrent = this.Current + 1;
            int newMax;
            if (newCurrent > this.Max)
            {
                newMaxLocation = newCurrentLocation;
                newMax = newCurrent;
            }
            else
            {
                newMax = this.Max;
                newMaxLocation = this.maxLocation;
            }

            var result = new Counter(newCurrent, newMax);
            result.currentLocation = newCurrentLocation;
            result.maxLocation = newMaxLocation;
            return result;
        }

        internal Location GetLocation(SyntaxTree syntaxTree)
        {
            return maxLocation;
        }

        internal Counter Reset()
        {
            var result = new Counter(0, this.Max);
            result.currentLocation = null;
            result.maxLocation = this.maxLocation;
            return result;
        }

        internal Counter Merge(Counter oldValue)
        {
            int max;
            Location maxLocation;
            if (this.Max > oldValue.Max)
            {
                max = this.Max;
                maxLocation = this.maxLocation;
            }
            else
            {
                max = oldValue.Max;
                maxLocation = oldValue.maxLocation;
            }

            var result = new Counter(Math.Max(oldValue.Current, this.Current), max);
            result.maxLocation = maxLocation;
            if (this.currentLocation == null)
            {
                result.currentLocation = oldValue.currentLocation;
            }
            else if (oldValue.currentLocation == null)
            {
                result.currentLocation = this.currentLocation;
            }
            else
            {
                result.currentLocation = Location.Create(
                    this.currentLocation.SourceTree,
                    TextSpan.FromBounds(
                        Math.Min(currentLocation.SourceSpan.Start, oldValue.currentLocation.SourceSpan.Start),
                        Math.Max(currentLocation.SourceSpan.End, oldValue.currentLocation.SourceSpan.End)));
            }

            return result;
        }
    }
}
