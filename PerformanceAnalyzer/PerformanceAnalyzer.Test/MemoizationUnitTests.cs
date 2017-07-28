// <copyright file="MemoizationUnitTests.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer.Test
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using PerformanceAnalyzer;
    using TestHelper;

    [TestClass]
    public class MemoizationUnitTests : DiagnosticVerifier<MemoizationAnalyzer>
    {
        /// <summary>
        /// Checks that if we read from the list twice with the same key, we get an error.
        /// </summary>
        [TestMethod]
        public void TestDoubleRead()
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
            var expected = CreateExpectation("Collection localList is searched multiple times with key i.", row: 9, line: 14);
            this.VerifyDiagnostic(testCode, null, expected);
        }

        /// <summary>
        /// Checks that if we read from the list twice with the same key, but on different code paths we don't get an error.
        /// </summary>
        [TestMethod]
        public void TestDictionarySearchOnDifferentCodePaths()
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
            this.VerifyDiagnostic(testCode);
        }

        /// <summary>
        /// No diagnostics expected to show up.
        /// </summary>
        [TestMethod]
        public void TestForEmptyInput()
        {
            var testCode = string.Empty;

            this.VerifyDiagnostic(testCode);
        }

        /// <summary>
        /// Completely invalid source code. Fail silently, so also no diagnostics expected to show up.
        /// </summary>
        [TestMethod]
        public void TestForInvalidInput()
        {
            var testCode = "This is not a valid source code.";
            this.VerifyDiagnostic(testCode);
        }
    }
}