// <copyright file="AsyncInLoopAnalyzer.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
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
            var cycles = GraphHelper.FindAllCycles<ExecutionPathNode>(path);
            foreach (var awaitInCycle in cycles     // For each cycle:
                .SelectMany(x => x)                 // Select all nodes from all cycles.
                .Distinct()                         // But only once per each (in case of inner loops).
                .Select(x => x.SyntaxNode)          // Select syntaxNode for each node.
                .Where(x => x != null)              // We can skip nodes where there is no syntax attached.
                .SelectMany(n => n.DescendantNodesAndSelf()) // Process also everything within expressions.
                .OfType<AwaitExpressionSyntax>())   // And check if it is an await.
            {
                var location = awaitInCycle.GetLocation();
                base.CreateUniqueDiagnostic(callback, descriptor, location);
            }
        }
    }
}
