// <copyright file="UnitTests.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer.Test
{
    using System;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CodeFixes;
    using Microsoft.CodeAnalysis.Diagnostics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using PerformanceAnalyzer;
    using TestHelper;

    [TestClass]
    public class UnitTests : CodeFixVerifier
    {

        /// <summary>
        /// Diagnostic and CodeFix both triggered and checked for.
        /// </summary>
        [TestMethod]
        public void TestMethod2()
        {
            var test = @"using System.Collections.Generic;

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
            var expected = new DiagnosticResult
            {
                Id = "PerformanceAnalyzer",
                Message = string.Format("Type name '{0}' contains lowercase letters", "TypeName"),
                Severity = DiagnosticSeverity.Warning,
                Locations = new[] { new DiagnosticResultLocation("Test0.cs", 11, 15) }
            };

            this.VerifyDiagnostic(test, expected);
        }

        /// <summary>
        /// No diagnostics expected to show up.
        /// </summary>
        [TestMethod]
        public void TestForEmptyInput()
        {
            var test = string.Empty;

            this.VerifyDiagnostic(test);
        }

        protected override DiagnosticAnalyzer GetDiagnosticAnalyzer()
        {
            return new PerformanceAnalyzer.MemoizationAnalyzer();
        }
    }
}