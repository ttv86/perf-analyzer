namespace PerformanceAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
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
    public sealed class MemoizationAnalyzer : DiagnosticAnalyzer
    {
        private const string TitleText = "Collection should not be searched multiple times";
        private const string MessageText = "Collection {0} is searched multiple times with key {1}.";

        private static DiagnosticDescriptor descriptor = new DiagnosticDescriptor(nameof(MemoizationAnalyzer), TitleText, MessageText, "Performance", DiagnosticSeverity.Warning, true);
        private Dictionary<Tuple<ExpressionSyntax, ExpressionSyntax>, Counter> readCounts = null;
        private ElementMatcher matcher;

        /// <summary>
        /// Creates new instance of MemoizationAnalyzer class.
        /// </summary>
        public MemoizationAnalyzer()
        {
            matcher = new ElementMatcher(this);
        }

        internal SemanticModel SemanticModel { get; set; }

        /// <summary>
        /// Returns an array of analyzer descriptions to be used in Visual Studio analyses.
        /// </summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(descriptor);
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSemanticModelAction(AnalyzeModel);
        }

        private void AnalyzeModel(SemanticModelAnalysisContext context)
        {
            this.SemanticModel = context.SemanticModel;
            var methods = context.SemanticModel.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                if (!context.CancellationToken.IsCancellationRequested)
                {
                    AnalyzeMethod(method, context.ReportDiagnostic);
                }
            }
        }

        /// <summary>
        /// Checks one method. If errors are found, call an optional callback method.
        /// </summary>
        public void AnalyzeMethod(MethodDeclarationSyntax method, Action<Diagnostic> callback = null)
        {
            // A dictionary of how many times each collection is read using a given variable.
            readCounts = new Dictionary<Tuple<ExpressionSyntax, ExpressionSyntax>, Counter>(matcher);

            // A list of already processed nodes. No need to process it more than once.
            List<SyntaxNode> skipped = new List<SyntaxNode>();

            foreach (var node in method.Body.DescendantNodes())
            {
                // This node was already processed. No need to process it again.
                if (skipped.Contains(node))
                {
                    continue;
                }

                // When variable is used either with ref or out, mark it as possible changed.
                if ((node is ArgumentSyntax argument) && !string.IsNullOrEmpty(argument.RefOrOutKeyword.Text))
                {
                    VariableChanged(argument.Expression);
                }

                // Member access aka method calls. 
                // method();
                if (node is MemberAccessExpressionSyntax memberAccess)
                {
                    if (IsDictionaryOrListMethod(memberAccess, SemanticModel, "Contains", "ContainsKey", "TryGetValue"))
                    {
                        // These methods do not change the state of the dictionary. We shouldn't keep calling them again with same parameters until the state has changed.
                        if ((memberAccess.Parent is InvocationExpressionSyntax invocation) && (invocation.ArgumentList.Arguments.Count >= 1))
                        {
                            ReadValue(memberAccess.Expression, invocation.ArgumentList.Arguments.First().Expression);
                            continue;
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                    else if (IsDictionaryOrListMethod(memberAccess, SemanticModel, "Add"))
                    {
                        // Add changes the state of the dictionary for 1 item.
                        if ((memberAccess.Parent is InvocationExpressionSyntax invocation) && (invocation.ArgumentList.Arguments.Count == 2))
                        {
                            // Add should have 2 parameters
                            WriteValue(memberAccess.Expression, invocation.ArgumentList.Arguments.First().Expression);
                            continue;
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                    else if (IsDictionaryOrListMethod(memberAccess, SemanticModel, "Clear"))
                    {
                        // Clear changes the state of the dictionary for all items.
                        if ((memberAccess.Parent is InvocationExpressionSyntax invocation) && (invocation.ArgumentList.Arguments.Count == 0))
                        {
                            // Clear should have 0 parameters.
                            ClearDictionary(memberAccess.Expression);
                            continue;
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                }

                if (node is AssignmentExpressionSyntax assignmentExpression)
                {
                    if (IsDictionaryOrList(assignmentExpression.Left))
                    {
                        // Reference to a dictionary was changed. No need to track previous instance anymore.
                        ClearDictionary(assignmentExpression.Left);
                    }
                    else
                    {
                        // Value of a method was changed. It can be now considered as a new value.
                        this.VariableChanged(assignmentExpression.Left);
                    }
                }

                // Element access
                // var value = dictionary[key];
                if (node is ElementAccessExpressionSyntax elementAccess)
                {
                    if (IsDictionaryOrList(elementAccess.Expression))
                    {
                        if (elementAccess.ArgumentList.Arguments.Count() == 1)
                        {
                            ReadValue(elementAccess.Expression, elementAccess.ArgumentList.Arguments.First().Expression);
                            continue;
                        }
                    }
                    else
                    {
                        //VariableChanged(postfixUnary.Operand);
                    }
                }

                // Element assignment
                // dictionary[key] = value;
                if (node is AssignmentExpressionSyntax assignment)
                {
                    var target = assignment.Left;
                    if (IsDictionaryOrList(assignment.Left))
                    {
                        var tuple = GetDictionaryAccess(assignment.Left);
                        if (tuple != null)
                        {
                            WriteValue(tuple.Item1, tuple.Item2);
                            skipped.Add(assignment.Left);
                            continue;
                        }
                    }
                }

                // Unary operations execute both read and write actions
                if (this.TestUnary(node, skipped))
                {
                    // Node was a unary operation. No need to process it more.
                    continue;
                }
            }

            // If any dictionary was read more then once with same parameters, raise an error.
            foreach (var x in readCounts)
            {
                if (x.Value.Max > 1)
                {
                    string dictName = x.Key.Item1.ToString();
                    var location = x.Value.GetLocation(method.SyntaxTree);
                    callback?.Invoke(Diagnostic.Create(descriptor, location, dictName, x.Key.Item2.ToString()));
                }
            }
        }

        /// <summary>
        /// Do actions on unary nodes (x++, x--, ++x it --x).
        /// </summary>
        /// <param name="node"></param>
        /// <param name="skippedNodes"></param>
        /// <returns></returns>
        private bool TestUnary(SyntaxNode node, ICollection<SyntaxNode> skippedNodes)
        {
            // Unary prefix (++x, --x)
            if ((node is PrefixUnaryExpressionSyntax prefixUnary) && ((prefixUnary.OperatorToken.Text == "++") || (prefixUnary.OperatorToken.Text == "--")))
            {
                if (IsDictionaryOrList(prefixUnary.Operand))
                {
                    var tuple = GetDictionaryAccess(prefixUnary.Operand);
                    if (tuple != null)
                    {
                        WriteValue(tuple.Item1, tuple.Item2);
                        skippedNodes.Add(prefixUnary.Operand);
                        return true;
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
                else
                {
                    VariableChanged(prefixUnary.Operand);
                }
            }

            // Unary postfix (x++, x--)
            if ((node is PostfixUnaryExpressionSyntax postfixUnary) && ((postfixUnary.OperatorToken.Text == "++") || (postfixUnary.OperatorToken.Text == "--")))
            {
                if (IsDictionaryOrList(postfixUnary.Operand))
                {
                    var tuple = GetDictionaryAccess(postfixUnary.Operand);
                    if (tuple != null)
                    {
                        WriteValue(tuple.Item1, tuple.Item2);
                        skippedNodes.Add(postfixUnary.Operand);
                        return true;
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
                else
                {
                    VariableChanged(postfixUnary.Operand);
                }
            }

            return false;
        }

        private void VariableChanged(ExpressionSyntax expression)
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
        private void ReadValue(ExpressionSyntax expression, ExpressionSyntax key)
        {
            System.Diagnostics.Debug.WriteLine($"Reading {expression} by key {key} ({GetFirstLine(expression.Parent.Parent.Parent.ToString())})");
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
        private void WriteValue(ExpressionSyntax expression, ExpressionSyntax key)
        {
            System.Diagnostics.Debug.WriteLine($"Writing {expression} by key {key} ({GetFirstLine(expression.Parent.Parent.Parent.ToString())})");
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
        private void ClearDictionary(ExpressionSyntax expression)
        {
            System.Diagnostics.Debug.WriteLine($"Clearing {expression} ({GetFirstLine(expression.Parent.Parent.Parent.ToString())})");
            foreach (var kvp in readCounts)
            {
                if (matcher.Equals(kvp.Key.Item1, expression))
                {
                    kvp.Value.Reset();
                }
            }
        }

        private string GetFirstLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            text = text.Trim();
            int index = text.IndexOf('\n');
            if (index > 0)
            {
                return text.Substring(0, index).Trim();
            }

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

            var symbol = SemanticModel.GetSymbolInfo(expression);
            if (symbol.Symbol != null)
            {
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

            // Test only types from 
            if (orig.ContainingAssembly.Name == "mscorlib")
            {
                if ((orig.Name == "List") || (orig.Name == "IList") || (orig.Name == "IReadOnlyList"))
                {
                    return true;
                }

                if ((orig.Name == "Dictionary") || (orig.Name == "IDictionary") || (orig.Name == "IReadOnlyDictionary"))
                {
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
            if (!IsDictionaryOrList(access.Expression))
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
                Current++;
                if (Current > Max)
                {
                    Max = Current;
                }

                if (start < 0)
                {
                    // This is the first one. Use span as is.
                    start = node.Span.Start;
                    end = node.Span.End;
                }
                else
                {
                    // There was already a span. Combine both spans to a new one.
                    start = Math.Min(node.SpanStart, start);
                    end = Math.Max(node.Span.End, start);
                }
            }

            /// <summary>
            /// Resets current count back to zero. Also reset span counter.
            /// </summary>
            public void Reset()
            {
                start = -1;
                end = -1;
                Current = 0;
            }

            internal Location GetLocation(SyntaxTree syntaxTree)
            {
                if (start == -1)
                {
                    return null;
                }

                return Location.Create(syntaxTree, TextSpan.FromBounds(this.start, this.end));
            }
        }
    }
}
