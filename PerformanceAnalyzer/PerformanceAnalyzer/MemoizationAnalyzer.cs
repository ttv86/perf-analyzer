// <copyright file="MemoizationAnalyzer.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;
    using Microsoft.CodeAnalysis.Text;

    /// <summary>
    /// Makes sure that a collection (either a dictionary or a list) isn't searched multiple times with the same search value.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MemoizationAnalyzer : PathAnalyzer // AnalyzerBase
    {
        private const string TitleText = "Collection should not be searched multiple times";
        private const string MessageText = "Collection {0} is searched multiple times with key {1}.";

        private static DiagnosticDescriptor descriptor = new DiagnosticDescriptor(nameof(MemoizationAnalyzer), TitleText, MessageText, "Performance", DiagnosticSeverity.Warning, true);
        private ElementMatcher matcher;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoizationAnalyzer"/> class.
        /// </summary>
        public MemoizationAnalyzer()
        {
            this.matcher = new ElementMatcher(this);
        }

        /// <summary>
        /// Gets an array of analyzer descriptions to be used in Visual Studio analyses.
        /// </summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(descriptor);
            }
        }

        protected override void AnalyzeMethod(MethodDeclarationSyntax method, ExecutionPath path, Action<Diagnostic> callback)
        {
            // A counter of how many times each collection is read using a given variable.
            var readCounts = new ReadCounter(this.matcher);

            // First go throught codes ignoring cycles.
            HashSet<ExecutionPathNode> analyzedNodes = new HashSet<ExecutionPathNode>();
            Queue<ExecutionPathNode> analyzableNodes = new Queue<ExecutionPathNode>();
            analyzableNodes.Enqueue(path.Root);
            while (analyzableNodes.Count > 0)
            {
                var first = analyzableNodes.Dequeue();
                if (analyzedNodes.Contains(first))
                {
                    // This node was already analyzed. We can skip it.
                    continue;
                }

                if (first.SyntaxNode != null)
                {
                    foreach (var statementPart in first.SyntaxNode.DescendantNodesAndSelf())
                    {
                        this.ProcessNode(statementPart, readCounts);
                    }
                }

                analyzedNodes.Add(first); // Mark this node as analyzed.
                foreach (var node in first.NextNodes)
                {
                    analyzableNodes.Enqueue(node);
                }
            }

            // If any dictionary was read more then once with same parameters, raise an error.
            ReportFindings(method, callback, readCounts);

            // Then find all cycles, and check if there's anything interesting.
            var cycles = GraphHelper.FindAllCycles<ExecutionPathNode>(path);
            foreach (IEnumerable<ExecutionPathNode> cycle in cycles)
            {
                // Reset read counts for every cycle.
                readCounts = new ReadCounter(this.matcher);

                // Go each cycle through twice, so we get a match for a things that are done more than once.
                for (int i = 0; i < 2; i++)
                {
                    foreach (var node in cycle)
                    {
                        if (node.SyntaxNode is ExpressionSyntax expressionStatement)
                        {
                            foreach (var statementPart in expressionStatement.DescendantNodesAndSelf())
                            {
                                this.ProcessNode(statementPart, readCounts);
                            }
                        }
                    }
                }

                // If any dictionary was read more then once with same parameters, raise an error.
                ReportFindings(method, callback, readCounts);
            }
        }

        private void ReportFindings(MethodDeclarationSyntax method, Action<Diagnostic> callback, ReadCounter readCounts)
        {
            foreach (var x in readCounts)
            {
                if (x.Value.Max > 1)
                {
                    string dictName = x.Key.Item1.ToString();
                    var location = x.Value.GetLocation(method.SyntaxTree);
                    base.CreateUniqueDiagnostic(callback, descriptor, location, dictName, x.Key.Item2.ToString());
                }
            }
        }

        private void ProcessNode(SyntaxNode node, ReadCounter readCounts)
        {
            // When variable is used either with ref or out, mark it as possible changed.
            if ((node is ArgumentSyntax argument) && !string.IsNullOrEmpty(argument.RefOrOutKeyword.Text))
            {
                this.VariableChanged(argument.Expression, readCounts);
            }

            // Member access aka method calls.
            // method();
            if (node is MemberAccessExpressionSyntax memberAccess)
            {
                if (this.IsDictionaryOrListMethod(memberAccess, this.SemanticModel, "Contains", "ContainsKey", "TryGetValue"))
                {
                    // These methods do not change the state of the dictionary. We shouldn't keep calling them again with same parameters until the state has changed.
                    if ((memberAccess.Parent is InvocationExpressionSyntax invocation) && (invocation.ArgumentList.Arguments.Count >= 1))
                    {
                        this.ReadValue(memberAccess.Expression, invocation.ArgumentList.Arguments.First().Expression, readCounts);
                        return;
                    }
                    else
                    {
                        Debug.WriteLine("Unexpected amount of parameters");
                    }
                }
                else if (this.IsDictionaryOrListMethod(memberAccess, this.SemanticModel, "Add"))
                {
                    // Add changes the state of the dictionary for 1 item.
                    if ((memberAccess.Parent is InvocationExpressionSyntax invocation) && (invocation.ArgumentList.Arguments.Count == 2))
                    {
                        // Add should have 2 parameters
                        this.WriteValue(memberAccess.Expression, invocation.ArgumentList.Arguments.First().Expression, readCounts);
                        return;
                    }
                    else
                    {
                        Debug.WriteLine("Unexpected amount of parameters");
                    }
                }
                else if (this.IsDictionaryOrListMethod(memberAccess, this.SemanticModel, "Clear"))
                {
                    // Clear changes the state of the dictionary for all items.
                    if ((memberAccess.Parent is InvocationExpressionSyntax invocation) && (invocation.ArgumentList.Arguments.Count == 0))
                    {
                        // Clear should have 0 parameters.
                        this.ClearDictionary(memberAccess.Expression, readCounts);
                        return;
                    }
                    else
                    {
                        Debug.WriteLine("Unexpected amount of parameters");
                    }
                }
            }

            if (node is AssignmentExpressionSyntax assignmentExpression)
            {
                if (this.IsDictionaryOrList(assignmentExpression.Left))
                {
                    // Reference to a dictionary was changed. No need to track previous instance anymore.
                    this.ClearDictionary(assignmentExpression.Left, readCounts);
                }
                else
                {
                    // Value of a method was changed. It can be now considered as a new value.
                    this.VariableChanged(assignmentExpression.Left, readCounts);
                }
            }

            // Element access
            // var value = dictionary[key];
            if (node is ElementAccessExpressionSyntax elementAccess)
            {
                if (this.IsDictionaryOrList(elementAccess.Expression))
                {
                    if (elementAccess.ArgumentList.Arguments.Count() == 1)
                    {
                        this.ReadValue(elementAccess.Expression, elementAccess.ArgumentList.Arguments.First().Expression, readCounts);
                        return;
                    }
                    else
                    {
                        Debug.WriteLine("Unexpected amount of parameters");
                    }
                }
                else
                {
                    ////VariableChanged(postfixUnary.Operand);
                }
            }

            // Element assignment
            // dictionary[key] = value;
            if (node is AssignmentExpressionSyntax assignment)
            {
                var target = assignment.Left;
                if (this.IsDictionaryOrList(assignment.Left))
                {
                    var tuple = this.GetDictionaryAccess(assignment.Left);
                    if (tuple != null)
                    {
                        this.WriteValue(tuple.Item1, tuple.Item2, readCounts);
                        return;
                    }
                }
            }

            // Unary operations execute both read and write actions
            if (this.TestUnary(node, readCounts))
            {
                // Node was a unary operation. No need to process it more.
                return;
            }
        }

        /// <summary>
        /// Do actions on unary nodes (x++, x--, ++x it --x).
        /// </summary>
        /// <param name="node"></param>
        /// <param name="skippedNodes"></param>
        /// <returns></returns>
        private bool TestUnary(SyntaxNode node, ReadCounter readCounts)
        {
            // Unary prefix (++x, --x)
            if ((node is PrefixUnaryExpressionSyntax prefixUnary) && ((prefixUnary.OperatorToken.Text == "++") || (prefixUnary.OperatorToken.Text == "--")))
            {
                if (this.IsDictionaryOrList(prefixUnary.Operand))
                {
                    var tuple = this.GetDictionaryAccess(prefixUnary.Operand);
                    if (tuple != null)
                    {
                        this.WriteValue(tuple.Item1, tuple.Item2, readCounts);
                        return true;
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
                else
                {
                    this.VariableChanged(prefixUnary.Operand, readCounts);
                }
            }

            // Unary postfix (x++, x--)
            if ((node is PostfixUnaryExpressionSyntax postfixUnary) && ((postfixUnary.OperatorToken.Text == "++") || (postfixUnary.OperatorToken.Text == "--")))
            {
                if (this.IsDictionaryOrList(postfixUnary.Operand))
                {
                    var tuple = this.GetDictionaryAccess(postfixUnary.Operand);
                    if (tuple != null)
                    {
                        this.WriteValue(tuple.Item1, tuple.Item2, readCounts);
                        return true;
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
                else
                {
                    this.VariableChanged(postfixUnary.Operand, readCounts);
                }
            }

            return false;
        }

        private void VariableChanged(ExpressionSyntax expression, ReadCounter readCounts)
        {
            foreach (var kvp in readCounts)
            {
                // If the varible matches to some of our dictionary search keys, reset that dictionary search counter.
                if (this.matcher.Equals(kvp.Key.Item2, expression) || this.matcher.Equals(kvp.Key.Item1, expression))
                {
                    kvp.Value.Reset();
                }
            }
        }

        private Tuple<ExpressionSyntax, ExpressionSyntax> GetDictionaryAccess(ExpressionSyntax expression)
        {
            if (expression is ElementAccessExpressionSyntax access)
            {
                // Dictionaries always have only one index argument. Just make sure of it.
                if (access.ArgumentList.Arguments.Count() == 1)
                {
                    return new Tuple<ExpressionSyntax, ExpressionSyntax>(access.Expression, access.ArgumentList.Arguments.First().Expression);
                }
            }

            return null;
        }

        /// <summary>
        /// A collection value was read. Increase the needed value counter.
        /// </summary>
        private void ReadValue(ExpressionSyntax expression, ExpressionSyntax key, ReadCounter readCounts)
        {
            System.Diagnostics.Debug.WriteLine($"Reading {expression} by key {key} ({this.GetFirstLine(expression.Parent.Parent.Parent.ToString())})");
            Tuple<ExpressionSyntax, ExpressionSyntax> searchKey = new Tuple<ExpressionSyntax, ExpressionSyntax>(expression, key);
            if (!readCounts.TryGetValue(searchKey, out Counter old))
            {
                // This value hasn't been used before. Add it.
                readCounts.Add(searchKey, old = new Counter(0));
            }

            old.Increase(expression);
        }

        /// <summary>
        /// A collection value was changed. Reset the needed value counter.
        /// </summary>
        private void WriteValue(ExpressionSyntax expression, ExpressionSyntax key, ReadCounter readCounts)
        {
            System.Diagnostics.Debug.WriteLine($"Writing {expression} by key {key} ({this.GetFirstLine(expression.Parent.Parent.Parent.ToString())})");
            Tuple<ExpressionSyntax, ExpressionSyntax> searchKey = new Tuple<ExpressionSyntax, ExpressionSyntax>(expression, key);

            // Try to reset value counter if it has been set.
            if (readCounts.TryGetValue(searchKey, out Counter old))
            {
                old.Reset();
            }
        }

        /// <summary>
        /// Dictionary is cleared. Reset all value counters on the given dictionary.
        /// </summary>
        private void ClearDictionary(ExpressionSyntax expression, ReadCounter readCounts)
        {
            System.Diagnostics.Debug.WriteLine($"Clearing {expression} ({this.GetFirstLine(expression.Parent.Parent.Parent.ToString())})");
            foreach (var kvp in readCounts)
            {
                if (this.matcher.Equals(kvp.Key.Item1, expression))
                {
                    kvp.Value.Reset();
                }
            }
        }

        /// <summary>
        /// Returns the content of the string until the first newline character.
        /// </summary>
        /// <param name="text">Any string</param>
        /// <returns>First line of the string that was given.</returns>
        private string GetFirstLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            text = text.Trim();
            int index = text.IndexOf('\n');
            if (index > 0)
            {
                // A newline character was found. Return everything before it.
                return text.Substring(0, index).Trim();
            }

            // No newline. Return everything.
            return text;
        }

        /// <summary>
        /// Tests if given expression is a reference to a dictionary or list types.
        /// </summary>
        /// <param name="expression">Expression to be tested.</param>
        /// <returns>True is expression is a reference to a dictionary or a list, false otherwise.</returns>
        private bool IsDictionaryOrList(ExpressionSyntax expression)
        {
            if (expression is ElementAccessExpressionSyntax ea)
            {
                expression = ea.Expression;
            }

            var symbol = this.SemanticModel.GetSymbolInfo(expression);
            if (symbol.Symbol != null)
            {
                // Use reflection to get what we want.
                var orig = symbol.Symbol.OriginalDefinition;
                var typeInfo = orig.GetType().GetTypeInfo();
                var typeProperty = typeInfo.DeclaredProperties.FirstOrDefault(x => x.Name == "Type");
                if (typeProperty == null)
                {
                    typeInfo = typeInfo.BaseType.GetTypeInfo();
                    typeProperty = typeInfo.DeclaredProperties.FirstOrDefault(x => x.Name == "Type");
                }

                if (typeProperty != null)
                {
                    if (typeProperty.GetValue(orig) is ITypeSymbol typeSymbol)
                    {
                        if (IsDictionaryOrList(typeSymbol))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Tests if given symbol is a reference to a dictionary or list types.
        /// </summary>
        /// <param name="typeSymbol">Symbol to be tested.</param>
        /// <returns>True is symbol is a reference to a dictionary or a list, false otherwise.</returns>
        internal static bool IsDictionaryOrList(ITypeSymbol typeSymbol)
        {
            var orig = typeSymbol.OriginalDefinition;
            if (orig.ContainingAssembly == null)
            {
                return false;
            }

            // Test only types from mscorlib (.NET Framework) or System.Runtime (.Net standard) or System.Private.CoreLib (.Net Core)
            if ((orig.ContainingAssembly.Name == "mscorlib") || (orig.ContainingAssembly.Name == "System.Runtime") || (orig.ContainingAssembly.Name == "System.Private.CoreLib"))
            {
                if ((orig.Name == "List") || (orig.Name == "IList") || (orig.Name == "IReadOnlyList"))
                {
                    // It is a list
                    return true;
                }

                if ((orig.Name == "Dictionary") || (orig.Name == "IDictionary") || (orig.Name == "IReadOnlyDictionary"))
                {
                    // It is a Dictionary
                    return true;
                }

                if (orig.Name == "HashSet")
                {
                    // It is a HashSet
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if the MemberAccess is calling any of the specified methods for the dictionary.
        /// </summary>
        internal bool IsDictionaryOrListMethod(MemberAccessExpressionSyntax access, SemanticModel semModel, params string[] methodNames)
        {
            if (!this.IsDictionaryOrList(access.Expression))
            {
                return false;
            }

            return methodNames.Contains(access.Name.Identifier.Text);
        }

        private class Counter
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
                    this.end = Math.Max(node.Span.End, this.start);
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

        private class ReadCounter : Dictionary<Tuple<ExpressionSyntax, ExpressionSyntax>, Counter>
        {
            public ReadCounter(IEqualityComparer<Tuple<ExpressionSyntax, ExpressionSyntax>> comparer) : base(comparer)
            {
            }
        }
    }
}
