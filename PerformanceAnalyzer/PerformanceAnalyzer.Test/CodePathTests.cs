// <copyright file="CodePathTests.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer.Test
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using TestHelper;

    [TestClass]
    public class CodePathTests
    {
        /// <summary>
        /// Checks that if we read from the list twice with the same key, we get an error.
        /// </summary>
        [TestMethod]
        public async Task TestVariableDeclarations()
        {
            var source = @"using System.Collections.Generic;

internal class TestClass
{
    public void Test()
    {
        int a; // No initial value
        int b = 0; // Initial value
        int c, d, e; // Multiple variables at once without initial values
        int f = 1, g = 2, h = 3; // Multiple variables at once with initial values
        IList<double> i = new List<double>(); // New instance
        int j = (f * g * h) + b; // Formula
    }
}";

            IDictionary<string, ExecutionPath> methods = await DiagnosticVerifier.GetMethodTrees(source);
            var node = methods["Test"].Root;
            while (node.Name != "End of method")
            {
                Assert.AreEqual(1, node.NextNodes); // Every node should have 1 next node (no loops or switches).
                node = node.NextNodes.First();
            }
        }

        [TestMethod]
        public async Task TestTernaryAndMethodCall()
        {
            string source = @"internal static class TestClass
{
    public static int Test(bool x, IList<int> a, IList<int> b)
    {
        (x ? a : b).Add(1);
    }
}";

            IDictionary<string, ExecutionPath> methods = await DiagnosticVerifier.GetMethodTrees(source);
            var node = methods["Test"].Root;
        }

        [TestMethod]
        public async Task TestIfPath()
        {
            string source = @"internal static class TestClass
{
    public static int Test(bool x)
    {
        if (x) {
            return 1;
        } else {
            return 5;
        }
    }
}";

            IDictionary<string, ExecutionPath> methods = await DiagnosticVerifier.GetMethodTrees(source);
            var node = methods["Test"].Root;
        }

        [TestMethod]
        public async Task TestSwitchPath()
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
            Diagnostic[] diagnostics = await DiagnosticVerifier.CreateAndRunAnalyzerAsync<PathAnalyzerTestClass>(source);
        }

        [TestMethod]
        public async Task TestForPath()
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
            Diagnostic[] diagnostics = await DiagnosticVerifier.CreateAndRunAnalyzerAsync<PathAnalyzerTestClass>(source);
        }

        [TestMethod]
        public async Task TestForeachPath()
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
            Diagnostic[] diagnostics = await DiagnosticVerifier.CreateAndRunAnalyzerAsync<PathAnalyzerTestClass>(source);
        }

        [TestMethod]
        public async Task TestDoPath()
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
            Diagnostic[] diagnostics = await DiagnosticVerifier.CreateAndRunAnalyzerAsync<PathAnalyzerTestClass>(source);
        }

        [TestMethod]
        public async Task TestWhilePath()
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
            Diagnostic[] diagnostics = await DiagnosticVerifier.CreateAndRunAnalyzerAsync<PathAnalyzerTestClass>(source);
        }

        [TestMethod]
        public async Task TestTryPath1()
        {
            string source = @"internal static class TestClass
{
    public static void Test()
    {
        // Test a block without catch statement.
        try {
            LongRunningMethod1();
        } finally {
            LongRunningMethod1();
        }

        // Test a block with multiple catch statements.
        try {
            LongRunningMethod2();
        } catch (System.InvalidOperationException error) {
            LongRunningMethod2();
        } catch (System.Exception error) {
            LongRunningMethod2();
        }
    }

    private static void LongRunningMethod1() {
    }

    private static void LongRunningMethod2() {
    }
}";
            Diagnostic[] diagnostics = await DiagnosticVerifier.CreateAndRunAnalyzerAsync<PathAnalyzerTestClass>(source);
            Assert.AreEqual(0, diagnostics.Length); // There shouldn't be any warnings, since none of the above methods are on same execution path.
        }

        [TestMethod]
        public async Task TestTryPath2()
        {
            string source = @"internal static class TestClass
{
    public static void Test()
    {
        // Test a block without catch statement.
        try {
            LongRunningMethod();
        } catch (System.Exception error) {
            LongRunningMethod();
        } finally {
            LongRunningMethod();
        }
    }

    private static void LongRunningMethod() {
    }
}";
            Diagnostic[] diagnostics = await DiagnosticVerifier.CreateAndRunAnalyzerAsync<PathAnalyzerTestClass>(source);
            Assert.AreEqual(1, diagnostics.Length); // There should be 1 warning, since LongRunningMethod could be called twice during the execution.
        }
    }
}