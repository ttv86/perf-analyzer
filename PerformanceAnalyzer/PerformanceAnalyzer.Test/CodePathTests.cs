// <copyright file="CodePathTests.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer.Test
{
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using TestHelper;

    [TestClass]
    public class CodePathTests
    {
        [TestMethod]
        public async Task TestIfPath()
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

            Diagnostic[] diagnostics = await DiagnosticVerifier.CreateAndRunAnalyzerAsync<PathAnalyzerTestClass>(source);
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