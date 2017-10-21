// <copyright file="AsyncInLoopAnalyzer.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer
{
    using System;
    using System.Collections.Immutable;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AwaitInLoopAnalyzer : PathAnalyzer
    {
        private const string TitleText = "A task was awaited within a loop.";
        private const string MessageText = "A task was awaited within a loop.";

        private static DiagnosticDescriptor descriptor = new DiagnosticDescriptor(nameof(MemoizationAnalyzer), TitleText, MessageText, "Performance", DiagnosticSeverity.Warning, true);

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
            path.ForEachNode(n => TestNode(n, callback));
        }

        private static void TestNode(ExecutionPathNode node, Action<Diagnostic> callback)
        {
            if (node.IsInCycle && node.SyntaxNode is AwaitExpressionSyntax awaitNode)
            {
                var location = awaitNode.GetLocation();
                callback?.Invoke(Diagnostic.Create(descriptor, location));
            }
        }
    }
}
