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
        public void TestTryPath1()
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
            this.RunTest(source, (results) =>
            {
                Assert.AreEqual(0, results.Count); // There shouldn't be any warnings, since none of the above methods are on same execution path.
            });
        }

        [TestMethod]
        public void TestTryPath2()
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
            this.RunTest(source, (results) =>
            {
                Assert.AreEqual(1, results.Count); // There should be 1 warning, since LongRunningMethod could be called twice during the execution.
            });
        }

        private void RunTest(string source, Action<IReadOnlyCollection<object>> expected)
        {
            PathAnalyzerTestClass analyzer = null;
            Action<PathAnalyzerTestClass> created = new Action<PathAnalyzerTestClass>((createdAnalyzer) =>
            {
                analyzer = createdAnalyzer;
            });

            this.VerifyDiagnostic(source, created);
            Assert.IsNotNull(analyzer);
            ExecutionPath path = analyzer.MethodPaths.First().Value; // Take the first method should be only 1 element.
            analyzer.AnalyzePath(path);

            // TODO: Create a test to verify node structure.
        }
    }
}