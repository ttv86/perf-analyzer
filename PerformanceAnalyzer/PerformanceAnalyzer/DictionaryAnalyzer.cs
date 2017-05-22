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

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DictionaryAnalyzer : DiagnosticAnalyzer
    {
        private const string TitleText = "Dictionary should not be searched multiple times";
        private const string MessageText = "Dictionary {0} is searched multiple times with key {1}.";

        private static DiagnosticDescriptor descriptor = new DiagnosticDescriptor(nameof(DictionaryAnalyzer), TitleText, MessageText, "Performance", DiagnosticSeverity.Warning, true);
        private Dictionary<Tuple<ExpressionSyntax, ExpressionSyntax>, Counter> readCounts = null;
        private ElementMatcher matcher;

        public DictionaryAnalyzer()
        {
            matcher = new ElementMatcher(this);
        }

        public DictionaryAnalyzer(SemanticModel semModel)
            : this()
        {
            this.SemanticModel = semModel;
        }

        internal SemanticModel SemanticModel { get; set; }

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
        /// Checks one given node from the syntax tree. If errors are found, call an optional callback method.
        /// </summary>
        public void AnalyzeMethod(MethodDeclarationSyntax method, Action<Diagnostic> callback = null)
        {
            readCounts = new Dictionary<Tuple<ExpressionSyntax, ExpressionSyntax>, Counter>(matcher);
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
                    if (IsDictionaryMethod(memberAccess, SemanticModel, "ContainsKey", "TryGetValue"))
                    {
                        // These methods do not change the state of the dictionary. We shouldn't keep calling them again with same parameters until the state has changed.
                        if ((memberAccess.Parent is InvocationExpressionSyntax invocation) && (invocation.ArgumentList.Arguments.Count >= 1))
                        {
                            ReadDictionary(memberAccess.Expression, invocation.ArgumentList.Arguments.First().Expression);
                            continue;
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                    else if (IsDictionaryMethod(memberAccess, SemanticModel, "Add"))
                    {
                        // Add changes the state of the dictionary for 1 item.
                        if ((memberAccess.Parent is InvocationExpressionSyntax invocation) && (invocation.ArgumentList.Arguments.Count == 2))
                        {
                            // Add should have 2 parameters
                            WriteDictionary(memberAccess.Expression, invocation.ArgumentList.Arguments.First().Expression);
                            continue;
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                    else if (IsDictionaryMethod(memberAccess, SemanticModel, "Clear"))
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
                    if (IsDictionary(assignmentExpression.Left))
                    {
                        ClearDictionary(assignmentExpression.Left);
                    }
                    else
                    {
                        this.VariableChanged(assignmentExpression.Left);
                    }
                }

                // Element access
                // var value = dictionary[key];
                if (node is ElementAccessExpressionSyntax elementAccess)
                {
                    if (IsDictionary(elementAccess.Expression))
                    {
                        if (elementAccess.ArgumentList.Arguments.Count() == 1)
                        {
                            ReadDictionary(elementAccess.Expression, elementAccess.ArgumentList.Arguments.First().Expression);
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
                    if (IsDictionary(assignment.Left))
                    {
                        var tuple = GetDictionaryAccess(assignment.Left);
                        if (tuple != null)
                        {
                            WriteDictionary(tuple.Item1, tuple.Item2);
                            skipped.Add(assignment.Left);
                            continue;
                        }
                    }
                }

                // Unary methods - prefix
                // ++dict[key];
                if ((node is PrefixUnaryExpressionSyntax prefixUnary) && ((prefixUnary.OperatorToken.Text == "++") || (prefixUnary.OperatorToken.Text == "--")))
                {
                    if (IsDictionary(prefixUnary.Operand))
                    {
                        var tuple = GetDictionaryAccess(prefixUnary.Operand);
                        if (tuple != null)
                        {
                            WriteDictionary(tuple.Item1, tuple.Item2);
                            skipped.Add(prefixUnary.Operand);
                            continue;
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

                // Unary methods - postfix
                // dict[key]++;
                if ((node is PostfixUnaryExpressionSyntax postfixUnary) && ((postfixUnary.OperatorToken.Text == "++") || (postfixUnary.OperatorToken.Text == "--")))
                {
                    if (IsDictionary(postfixUnary.Operand))
                    {
                        var tuple = GetDictionaryAccess(postfixUnary.Operand);
                        if (tuple != null)
                        {
                            WriteDictionary(tuple.Item1, tuple.Item2);
                            skipped.Add(postfixUnary.Operand);
                            continue;
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
        /// A dictionary value was read. Increase the needed value counter.
        /// </summary>
        private void ReadDictionary(ExpressionSyntax expression, ExpressionSyntax key)
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
        /// A dictionary value was changed. Reset the needed value counter.
        /// </summary>
        private void WriteDictionary(ExpressionSyntax expression, ExpressionSyntax key)
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

        private bool IsDictionary(ExpressionSyntax expression)
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
                        if (IsDictionary(typeSymbol))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        internal static bool IsDictionary(ITypeSymbol typeSymbol)
        {
            var orig = typeSymbol.OriginalDefinition;
            if (orig.ContainingAssembly == null)
            {
                return false;
            }

            if ((orig.ContainingAssembly.Name == "mscorlib") && ((orig.Name == "Dictionary") || (orig.Name == "IDictionary") || (orig.Name == "IReadOnlyDictionary")))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the MemberAccess is calling any of the specified methods for the dictionary.
        /// </summary>
        internal bool IsDictionaryMethod(MemberAccessExpressionSyntax access, SemanticModel semModel, params string[] methodNames)
        {
            if (!IsDictionary(access.Expression))
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

            public int Current { get; private set; }

            public int Max { get; private set; }

            public void Increase(SyntaxNode node)
            {
                Current++;
                if (Current > Max)
                {
                    Max = Current;
                }

                if (start < 0)
                {
                    start = node.Span.Start;
                    end = node.Span.End;
                }
                else
                {
                    start = Math.Min(node.SpanStart, start);
                    end = Math.Max(node.Span.End, start);
                }
            }

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
