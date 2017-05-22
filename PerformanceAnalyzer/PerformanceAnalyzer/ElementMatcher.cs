namespace PerformanceAnalyzer
{
    using System;
    using System.Collections.Generic;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    internal sealed class ElementMatcher : IEqualityComparer<ExpressionSyntax>, IEqualityComparer<Tuple<ExpressionSyntax, ExpressionSyntax>>
    {
        private DictionaryAnalyzer analyzer;

        public ElementMatcher(DictionaryAnalyzer analyzer)
        {
            this.analyzer = analyzer;
        }

        public bool Equals(ExpressionSyntax x, ExpressionSyntax y)
        {
            if (null == x)
            {
                if (null == y)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else if (null == y)
            {
                return false;
            }
            else
            {
                var symbol1 = analyzer.SemanticModel.GetSymbolInfo(x).Symbol;
                var symbol2 = analyzer.SemanticModel.GetSymbolInfo(y).Symbol;
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

        public int GetHashCode(ExpressionSyntax obj)
        {
            if (obj.SyntaxTree == analyzer.SemanticModel.SyntaxTree)
            {
                return analyzer.SemanticModel.GetSymbolInfo(obj).Symbol.GetHashCode();
            }
            else
            {
                return obj.GetHashCode();
            }
        }

        public bool Equals(Tuple<ExpressionSyntax, ExpressionSyntax> x, Tuple<ExpressionSyntax, ExpressionSyntax> y)
        {
            return Equals(x.Item1, y.Item1) && Equals(x.Item2, y.Item2);
        }

        public int GetHashCode(Tuple<ExpressionSyntax, ExpressionSyntax> obj)
        {
            return GetHashCode(obj.Item1);
        }
    }
}
