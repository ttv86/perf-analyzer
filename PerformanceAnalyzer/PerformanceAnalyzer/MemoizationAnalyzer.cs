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
            AnalyzeNonCyclic(method, path, callback);
            AnalyzeCyclic(method, path, callback);
        }

        private void AnalyzeNonCyclic(MethodDeclarationSyntax method, ExecutionPath path, Action<Diagnostic> callback)
        {
            // First go throught codes ignoring cycles.
            HashSet<ExecutionPathNode> analyzedNodes = new HashSet<ExecutionPathNode>();
            Queue<ExecutionPathNode> analyzeNodes = new Queue<ExecutionPathNode>(); // Normal list is First-In-First-Out
            Stack<ExecutionPathNode> analyzeNodesLowPriority = new Stack<ExecutionPathNode>(); // Low priorities are First-In-Last-Out
            analyzeNodes.Enqueue(path.Root);
            // A counter of how many times each collection is read using a given variable.
            path.Root.ReadCounts = new ReadCounter(this.matcher);
            var tails = new List<ExecutionPathNode>();
            while ((analyzeNodes.Count > 0) || (analyzeNodesLowPriority.Count > 0))
            {
                ExecutionPathNode first;
                if (analyzeNodes.Count > 0)
                {
                    first = analyzeNodes.Dequeue();
                    if (first.PreviousNodes.Count > 1)
                    {
                        // Sent item to low-priority queue, so if there are any other paths leading here, we process them first.
                        analyzeNodesLowPriority.Push(first);
                        continue;
                    }
                }
                else
                {
                    first = analyzeNodesLowPriority.Pop();
                }

                if (analyzedNodes.Contains(first))
                {
                    // This node was already analyzed. We can skip it.
                    continue;
                }

                var readCounts = first.ReadCounts;
                if (first.SyntaxNode != null)
                {
                    // Foreach statements have a special case below.
                    if (!(first.SyntaxNode is ForEachStatementSyntax))
                    {
                        foreach (var statementPart in first.SyntaxNode.DescendantNodesAndSelf())
                        {
                            this.ProcessNode(statementPart, ref readCounts);
                        }
                    }
                }

                analyzedNodes.Add(first); // Mark this node as analyzed.
                if (first.NextNodes.Count > 0)
                {
                    foreach (var node in first.NextNodes)
                    {
                        node.ReadCounts = readCounts.Merge(node.ReadCounts);
                        analyzeNodes.Enqueue(node);
                    }
                }
                else
                {
                    tails.Add(first);
                }
            }

            // If any dictionary was read more then once with same parameters, raise an error.
            foreach (var tail in tails)
            {
                ReportFindings(method, callback, tail.ReadCounts);
            }
        }

        private void AnalyzeCyclic(MethodDeclarationSyntax method, ExecutionPath path, Action<Diagnostic> callback)
        {
            ReadCounter readCounts = new ReadCounter(this.matcher);
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
                        if (node.SyntaxNode is ForEachStatementSyntax forEachStatementSyntax)
                        {
                            // Foreach statements are a special case, since there is no assignment operation in code.
                            // If cycle contains a foreach statement, reset read counter for variable.
                            this.VariableChanged(forEachStatementSyntax.Identifier, ref readCounts);
                        }
                        else if (node.SyntaxNode != null)
                        {
                            foreach (var statementPart in node.SyntaxNode.DescendantNodesAndSelf())
                            {
                                this.ProcessNode(statementPart, ref readCounts);
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

        private void ProcessNode(SyntaxNode node, ref ReadCounter readCounts)
        {
            // When variable is used either with ref or out, mark it as possible changed.
            if ((node is ArgumentSyntax argument) && !string.IsNullOrEmpty(argument.RefOrOutKeyword.Text))
            {
                this.VariableChanged(argument.Expression, ref readCounts);
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
                        this.ReadValue(memberAccess.Expression, invocation.ArgumentList.Arguments.First().Expression, ref readCounts);
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
                        this.WriteValue(memberAccess.Expression, invocation.ArgumentList.Arguments.First().Expression, ref readCounts);
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
                        this.ClearDictionary(memberAccess.Expression, ref readCounts);
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
                    this.ClearDictionary(assignmentExpression.Left, ref readCounts);
                }
                else
                {
                    // Value of a method was changed. It can be now considered as a new value.
                    this.VariableChanged(assignmentExpression.Left, ref readCounts);
                }
            }

            if (node is VariableDeclaratorSyntax variableDeclaratorSyntax)
            {
                ////if (this.IsDictionaryOrList(variableDeclaratorSyntax.Identifier))
                ////{
                ////    // Reference to a dictionary was changed. No need to track previous instance anymore.
                ////    this.ClearDictionary(variableDeclaratorSyntax.Identifier, ref readCounts);
                ////}
                ////else
                ////{
                    // Value of a method was changed. It can be now considered as a new value.
                    this.VariableChanged(variableDeclaratorSyntax.Identifier, ref readCounts);
                ////}
            }

            // Element access
            // var value = dictionary[key];
            if (node is ElementAccessExpressionSyntax elementAccess)
            {
                if (this.IsDictionaryOrList(elementAccess.Expression))
                {
                    if (elementAccess.ArgumentList.Arguments.Count() == 1)
                    {
                        this.ReadValue(elementAccess.Expression, elementAccess.ArgumentList.Arguments.First().Expression, ref readCounts);
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
                        this.WriteValue(tuple.Item1, tuple.Item2, ref readCounts);
                        return;
                    }
                }
            }

            // Unary operations execute both read and write actions
            if (this.TestUnary(node, ref readCounts))
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
        private bool TestUnary(SyntaxNode node, ref ReadCounter readCounts)
        {
            // Unary prefix (++x, --x)
            if ((node is PrefixUnaryExpressionSyntax prefixUnary) && ((prefixUnary.OperatorToken.Text == "++") || (prefixUnary.OperatorToken.Text == "--")))
            {
                if (this.IsDictionaryOrList(prefixUnary.Operand))
                {
                    var tuple = this.GetDictionaryAccess(prefixUnary.Operand);
                    if (tuple != null)
                    {
                        this.WriteValue(tuple.Item1, tuple.Item2, ref readCounts);
                        return true;
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
                else
                {
                    this.VariableChanged(prefixUnary.Operand, ref readCounts);
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
                        this.WriteValue(tuple.Item1, tuple.Item2, ref readCounts);
                        return true;
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
                else
                {
                    this.VariableChanged(postfixUnary.Operand, ref readCounts);
                }
            }

            return false;
        }

        private void VariableChanged(ExpressionSyntax expression, ref ReadCounter readCounts)
        {
            foreach (var kvp in readCounts)
            {
                // If the varible matches to some of our dictionary search keys, reset that dictionary search counter.
                if (this.matcher.Equals(kvp.Key.Item2, expression) || this.matcher.Equals(kvp.Key.Item1, expression))
                {
                    readCounts = readCounts.Reset(kvp.Key);
                    break;
                }
            }
        }

        private void VariableChanged(SyntaxToken token, ref ReadCounter readCounts)
        {
            foreach (var kvp in readCounts)
            {
                // If the varible matches to some of our dictionary search keys, reset that dictionary search counter.
                if (this.matcher.Equals(kvp.Key.Item2, token) || this.matcher.Equals(kvp.Key.Item1, token))
                {
                    readCounts = readCounts.Reset(kvp.Key);
                    break;
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
        private void ReadValue(ExpressionSyntax expression, ExpressionSyntax key, ref ReadCounter readCounts)
        {
            System.Diagnostics.Debug.WriteLine($"Reading {expression} by key {key} ({this.GetFirstLine(expression.Parent.Parent.Parent.ToString())})");
            Tuple<ExpressionSyntax, ExpressionSyntax> searchKey = new Tuple<ExpressionSyntax, ExpressionSyntax>(expression, key);
            readCounts = readCounts.Increment(searchKey, expression.GetLocation());
        }

        /// <summary>
        /// A collection value was changed. Reset the needed value counter.
        /// </summary>
        private void WriteValue(ExpressionSyntax expression, ExpressionSyntax key, ref ReadCounter readCounts)
        {
            System.Diagnostics.Debug.WriteLine($"Writing {expression} by key {key} ({this.GetFirstLine(expression.Parent.Parent.Parent.ToString())})");
            Tuple<ExpressionSyntax, ExpressionSyntax> searchKey = new Tuple<ExpressionSyntax, ExpressionSyntax>(expression, key);

            // Try to reset value counter if it has been set.
            readCounts = readCounts.Reset(searchKey);
        }

        /// <summary>
        /// Dictionary is cleared. Reset all value counters on the given dictionary.
        /// </summary>
        private void ClearDictionary(ExpressionSyntax expression, ref ReadCounter readCounts)
        {
            System.Diagnostics.Debug.WriteLine($"Clearing {expression} ({this.GetFirstLine(expression.Parent.Parent.Parent.ToString())})");
            foreach (var kvp in readCounts)
            {
                if (this.matcher.Equals(kvp.Key.Item1, expression))
                {
                    readCounts = readCounts.Reset(kvp.Key);
                    break;
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

        ////private bool IsDictionaryOrList(SyntaxNode syntaxToken)
        ////{
        ////    this.SemanticModel.GetSymbolInfo(syntaxToken);
        ////    throw new NotImplementedException();
        ////}

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
    }
}
