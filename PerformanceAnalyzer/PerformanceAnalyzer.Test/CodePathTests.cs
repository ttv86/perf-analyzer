using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace PerformanceAnalyzer.Test
{
    [TestClass]
    public class CodePathTests : DiagnosticVerifier<PathAnalyzerTestClass>
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
            this.RunTest(source, null);
        }

        [TestMethod]
        public void TestSwitchPath()
        {
            string source = @"internal static class TestClass
{
    public static int Test()
    {
        int i = 0;
        int j = 0;
        switch (i)
            case 0:
                j = 4;
                break;
            case 1:
                return 2;
            case h when h = 4:
                j = 1;
                break;
            default:
                j = 0;
                break;
        }

        return j;
    }
}";
            this.RunTest(source, null);
        }

        [TestMethod]
        public void TestForPath()
        {
            string source = @"internal static class TestClass
{
    public static int Test()
    {
        for (int i = 0, j = 2; i < 10; i++, j--) {
            j.ToString();
        }

        return 0;
    }
}";
            this.RunTest(source, null);
        }

        [TestMethod]
        public void TestForeachPath()
        {
            string source = @"internal static class TestClass
{
    public static int Test()
    {
        System.IEnumerable<int> values = null;
        foreach (var value in values) {
            value.ToString();
        }

        return 0;
    }
}";
            this.RunTest(source, null);
        }

        [TestMethod]
        public void TestDoPath()
        {
            string source = @"internal static class TestClass
{
    public static int Test()
    {
        int i = 0;
        do {
            i++;
        } while (i < 10);

        return i;
    }
}";
            this.RunTest(source, null);
        }

        [TestMethod]
        public void TestWhilePath()
        {
            string source = @"internal static class TestClass
{
    public static int Test()
    {
        int i = 0;
        while (i < 10) {
            i++;
        }

        return i;
    }
}";
            this.RunTest(source, null);
        }

        [TestMethod]
        public void TestTryPath()
        {
            string source = @"internal static class TestClass
{
    public static int Test()
    {
        try {
            (0).ToString();
        } catch {
            (1).ToString();
        }

        try {
            (0).ToString();
        } catch (System.InvalidOperationException error) {
            (1).ToString();
        } catch (System.Exception error) {
            return 1;
        } finally {
            (3).ToString();
        }

        return 0;
    }
}";
            this.RunTest(source, null);
        }

        private void RunTest(string source, object expected)
        {
            PathAnalyzerTestClass analyzer = null;
            Action<PathAnalyzerTestClass> created = new Action<PathAnalyzerTestClass>((createdAnalyzer) =>
            {
                analyzer = createdAnalyzer;
            });

            this.VerifyDiagnostic(source, created);
            Assert.IsNotNull(analyzer);
            Assert.AreEqual(analyzer.MethodPaths.Count, 1); // There should be only 1 element.
            ExecutionPath path = analyzer.MethodPaths.First().Value;
            // TODO: Create a test to verify node structure.
        }
    }

    public class PathAnalyzerTestClass : PathAnalyzer
    {
        private static DiagnosticDescriptor descriptor = new DiagnosticDescriptor(nameof(PathAnalyzer), "Test", "Test", "Test", DiagnosticSeverity.Warning, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create<DiagnosticDescriptor>(descriptor);

        public IDictionary<MethodDeclarationSyntax, ExecutionPath> MethodPaths { get; } = new Dictionary<MethodDeclarationSyntax, ExecutionPath>();

        protected override void AnalyzeMethod(MethodDeclarationSyntax method, ExecutionPath path, Action<Diagnostic> callback)
        {
            this.MethodPaths[method] = path;
        }
    }
}