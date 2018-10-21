// <copyright file="ElementMatcher.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer
{
    using System;
    using System.Collections.Generic;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    internal sealed class ElementMatcher : IEqualityComparer<ExpressionSyntax>, IEqualityComparer<Tuple<ExpressionSyntax, ExpressionSyntax>>
    {
        private MemoizationAnalyzer analyzer;

        public ElementMatcher(MemoizationAnalyzer analyzer)
        {
            this.analyzer = analyzer;
        }

        public bool Equals(ExpressionSyntax x, ExpressionSyntax y)
        {
            if (x == null)
            {
                if (y == null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else if (y == null)
            {
                return false;
            }
            else
            {
                var symbol1 = this.analyzer.SemanticModel.GetSymbolInfo(x).Symbol;
                var symbol2 = this.analyzer.SemanticModel.GetSymbolInfo(y).Symbol;
                if ((symbol1 != null) && (symbol2 != null))
                {
                    return symbol1.Equals(symbol2);
                }
                else
                {
                    return x.ToString().Equals(y.ToString());
                }
            }
        }

        public bool Equals(ExpressionSyntax x, SyntaxToken y)
        {
            if (x == null)
            {
                if (y == null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else if (y == null)
            {
                return false;
            }
            else
            {
                var symbol1 = this.analyzer.SemanticModel.GetSymbolInfo(x).Symbol;

                // Both are declared in same place. Consider as same.
                return symbol1.DeclaringSyntaxReferences[0].GetSyntax(System.Threading.CancellationToken.None) == y.Parent;
            }
        }

        public int GetHashCode(ExpressionSyntax obj)
        {
            if (obj.SyntaxTree == this.analyzer.SemanticModel.SyntaxTree)
            {
                return this.analyzer.SemanticModel.GetSymbolInfo(obj).Symbol.GetHashCode();
            }
            else
            {
                return obj.GetHashCode();
            }
        }

        public bool Equals(Tuple<ExpressionSyntax, ExpressionSyntax> x, Tuple<ExpressionSyntax, ExpressionSyntax> y)
        {
            return this.Equals(x.Item1, y.Item1) && this.Equals(x.Item2, y.Item2);
        }

        public int GetHashCode(Tuple<ExpressionSyntax, ExpressionSyntax> obj)
        {
            return this.GetHashCode(obj.Item1);
        }
    }
}
