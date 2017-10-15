// <copyright file="AnalyzerBase.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer
{
    using System;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;

    public abstract class AnalyzerBase : DiagnosticAnalyzer
    {
        internal SemanticModel SemanticModel { get; set; }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSemanticModelAction(this.AnalyzeModel);
        }

        protected abstract void AnalyzeMethod(MethodDeclarationSyntax method, Action<Diagnostic> callback);

        private void AnalyzeModel(SemanticModelAnalysisContext context)
        {
            this.SemanticModel = context.SemanticModel;
            var methods = context.SemanticModel.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                if (!context.CancellationToken.IsCancellationRequested)
                {
                    this.AnalyzeMethod(method, context.ReportDiagnostic);
                }
            }
        }
    }
}
