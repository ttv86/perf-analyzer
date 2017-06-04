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
        /// No diagnostics expected to show up.
        /// </summary>
        [TestMethod]
        public void TestMethod1()
        {
            var test = string.Empty;

            this.VerifyDiagnostic(test);
        }

        /// <summary>
        /// Diagnostic and CodeFix both triggered and checked for.
        /// </summary>
        [TestMethod]
        public void TestMethod2()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
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

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class TYPENAME
        {   
        }
    }";
            this.VerifyCSharpFix(test, fixtest);
        }

        protected override CodeFixProvider GetCodeFixProvider()
        {
            return new PerformanceAnalyzerCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetDiagnosticAnalyzer()
        {
            return new PerformanceAnalyzer.DictionaryAnalyzer();
        }
    }
}