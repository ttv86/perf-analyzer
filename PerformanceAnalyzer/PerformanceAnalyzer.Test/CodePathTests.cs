using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace PerformanceAnalyzer.Test
{
    [TestClass]
    public class CodePathTests : DiagnosticVerifier<PathAnalyzer>
    {
        [TestMethod]
        public void TestIfPath()
        {
            string source = @"internal static class TestClass
{
    public static int Test()
    {
        int i;
        if (1 < 5) {
            return 1;
        } else {
            return 5;
        }
    }
}";

            this.VerifyDiagnostic(source);
        }
    }

    public class PathAnalyzer : AnalyzerBase
    {
        private static DiagnosticDescriptor descriptor = new DiagnosticDescriptor(nameof(PathAnalyzer), "Test", "Test", "Test", DiagnosticSeverity.Warning, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create<DiagnosticDescriptor>(descriptor);

        protected override void AnalyzeMethod(MethodDeclarationSyntax method, Action<Diagnostic> callback)
        {
        }
    }
}
