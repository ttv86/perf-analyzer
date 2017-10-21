// <copyright file="CodePathTests.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Text;

    public class PathAnalyzerTestClass : PathAnalyzer
    {
        private static DiagnosticDescriptor descriptor = new DiagnosticDescriptor(nameof(PathAnalyzer), "Test", "{0}", "Test", DiagnosticSeverity.Warning, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create<DiagnosticDescriptor>(descriptor);

        protected override void AnalyzeMethod(MethodDeclarationSyntax method, ExecutionPath path, Action<Diagnostic> callback)
        {
            //this.AnalyzePath(path);
            //var location = Location.Create(string.Empty, TextSpan.FromBounds(0, 0), new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 0)));
            //callback(Diagnostic.Create(descriptor, location, "Juttujuttu"));
        }
    }
}