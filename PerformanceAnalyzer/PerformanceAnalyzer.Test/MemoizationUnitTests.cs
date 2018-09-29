// <copyright file="MemoizationUnitTests.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer.Test
{
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using PerformanceAnalyzer;
    using TestHelper;

    [TestClass]
    public class MemoizationUnitTests
    {
        /// <summary>
        /// Checks that if we read from the list twice with the same key, we get an error.
        /// </summary>
        [TestMethod]
        public async Task TestDoubleRead()
        {
            var testCode = @"using System.Collections.Generic;

internal class TestClass
{
    public void Test()
    {
        int i = 4;
        IList<double> localList = new List<double>();
        localList[i].ToString();
        localList[i].ToString();
        i++;
        localList[i].ToString();
        i++;
        localList[i].ToString();
    }
}";

            Diagnostic[] diagnostics = await DiagnosticVerifier.CreateAndRunAnalyzerAsync<MemoizationAnalyzer>(testCode);
            Assert.AreEqual(1, diagnostics.Length);
            Assert.AreEqual("Collection localList is searched multiple times with key i.", diagnostics[0].GetMessage());
        }

        /// <summary>
        /// Checks that if we read from the list twice with the same key, but on different code paths we don't get an error.
        /// </summary>
        [TestMethod]
        public async Task TestDictionarySearchOnDifferentCodePaths()
        {
            var testCode = @"using System.Collections.Generic;

internal class TestClass
{
    public void Test()
    {
        int j = 4;
        int i = 4;
        IList<double> localList = new List<double>();
        if (j < 5) {
            localList[i].ToString();
        } else {
            localList[i].ToString();
        }
    }
}";
            Diagnostic[] diagnostics = await DiagnosticVerifier.CreateAndRunAnalyzerAsync<MemoizationAnalyzer>(testCode);
            Assert.AreEqual(0, diagnostics.Length); // There shouldn't be any warnings. Two reads are on different code paths
        }

        /// <summary>
        /// Checks that if we read from the list twice with the same key, but on different code paths we don't get an error.
        /// </summary>
        [TestMethod]
        public async Task TestDictionarySearchOnLoop1()
        {
            var testCode = @"using System.Collections.Generic;

internal class TestClass
{
    public void Test(IDictionary<double, double> localList)
    {
        for (int i = 0; i < 5; i++) {
            localList[3].ToString();
        }
    }
}";
            Diagnostic[] diagnostics = await DiagnosticVerifier.CreateAndRunAnalyzerAsync<MemoizationAnalyzer>(testCode);
            Assert.AreEqual(1, diagnostics.Length); // There should be one warning.
            Assert.AreEqual("Collection localList is searched multiple times with key 3.", diagnostics[0].GetMessage());
        }

        /// <summary>
        /// Checks that if we read from the list twice with the same key, but on different code paths we don't get an error.
        /// </summary>
        [TestMethod]
        public async Task TestDictionarySearchOnLoop2()
        {
            var testCode = @"using System.Collections.Generic;

internal class TestClass
{
    public void Test(IDictionary<double, double> localList)
    {
        for (int i = 0; i < 5; i++) {
            localList[i].ToString();
        }
    }
}";
            Diagnostic[] diagnostics = await DiagnosticVerifier.CreateAndRunAnalyzerAsync<MemoizationAnalyzer>(testCode);
            Assert.AreEqual(0, diagnostics.Length); // There shouldn't be any warnings. Read value is different each time.
        }

        /// <summary>
        /// No diagnostics expected to show up.
        /// </summary>
        [TestMethod]
        public async Task TestForEmptyInput()
        {
            var testCode = string.Empty;

            Diagnostic[] diagnostics = await DiagnosticVerifier.CreateAndRunAnalyzerAsync<MemoizationAnalyzer>(testCode);
            Assert.AreEqual(0, diagnostics.Length); // There shouldn't be any warnings. Failing to parse the code isn't our responsibility.
        }

        /// <summary>
        /// Completely invalid source code. Fail silently, so also no diagnostics expected to show up.
        /// </summary>
        [TestMethod]
        public async Task TestForInvalidInput()
        {
            var testCode = "This is not a valid source code.";
            Diagnostic[] diagnostics = await DiagnosticVerifier.CreateAndRunAnalyzerAsync<MemoizationAnalyzer>(testCode);
            Assert.AreEqual(0, diagnostics.Length); // There shouldn't be any warnings. Failing to parse the code isn't our responsibility.
        }
    }
}