// <copyright file="CodePathTests.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using TestHelper;

    public class PathAnalyzerTestClass : PathAnalyzer
    {
        private static DiagnosticDescriptor descriptor = new DiagnosticDescriptor(nameof(PathAnalyzer), "Test", "Test", "Test", DiagnosticSeverity.Warning, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create<DiagnosticDescriptor>(descriptor);

        public IDictionary<MethodDeclarationSyntax, ExecutionPath> MethodPaths { get; } = new Dictionary<MethodDeclarationSyntax, ExecutionPath>();

        protected override void AnalyzeMethod(MethodDeclarationSyntax method, ExecutionPath path, Action<Diagnostic> callback)
        {
            this.MethodPaths[method] = path;
        }

        internal void AnalyzePath(ExecutionPath path)
        {
            var cycles = GraphHelper.FindAllCycles<ExecutionPathNode>(path);
            HashSet<ExecutionPathNode> analyzedNodes = new HashSet<ExecutionPathNode>();
            List<ExecutionPathNode> analyzableNodes = new List<ExecutionPathNode>();
            analyzableNodes.Add(path.Root);
            while (analyzableNodes.Count > 0)
            {
                var first = analyzableNodes[0];
                analyzableNodes.RemoveAt(0);
                if (analyzedNodes.Contains(first))
                {
                    // This node was already analyzed. We can skip it.
                    continue;
                }

                analyzedNodes.Add(first); // Mark this node as analyzed.
                analyzableNodes.AddRange(first.NextNodes);
            }
        }
    }
}