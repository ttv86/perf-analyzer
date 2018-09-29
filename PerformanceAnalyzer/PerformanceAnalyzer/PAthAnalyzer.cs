// <copyright file="PathAnalyzer.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    public abstract class PathAnalyzer : AnalyzerBase
    {
        private HashSet<Tuple<int, int>> createdDiagnostics = new HashSet<Tuple<int, int>>();
        protected abstract void AnalyzeMethod(MethodDeclarationSyntax method, ExecutionPath path, Action<Diagnostic> callback);

        protected override void AnalyzeMethod(MethodDeclarationSyntax method, Action<Diagnostic> callback)
        {
            var executionPath = TreeGenerator.SplitMethod(method);
#if DEBUG
            GraphHelper.TreeToSvg(executionPath, System.IO.Path.GetTempPath() + @"\tree.svg");
#endif

            //// After we have made a graph from method contents, mark nodes that are in cycles.
            ////IReadOnlyCollection<IEnumerable<ExecutionPathNode>> cycles = GraphHelper.FindAllCycles<ExecutionPathNode>(executionPath);
            ////foreach (var cycle in cycles)
            ////{
            ////    foreach (var node in cycle)
            ////    {
            ////        node.IsInCycle = true;
            ////    }
            ////}

            this.AnalyzeMethod(method, executionPath, callback);
        }

        /// <summary>
        /// Creates a diagnostic event, but only if there isn't already one reported.
        /// </summary>
        internal void CreateUniqueDiagnostic(Action<Diagnostic> callback, DiagnosticDescriptor descriptor, Location location, params object[] args)
        {
            Tuple<int, int> key = new Tuple<int, int>(location.SourceSpan.Start, location.SourceSpan.End);
            if (this.createdDiagnostics.Contains(key))
            {
                return;
            }

            this.createdDiagnostics.Add(key);
            callback?.Invoke(Diagnostic.Create(descriptor, location, args));
        }
    }
}