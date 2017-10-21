/*
// <copyright file="TestBase.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.Diagnostics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using TestHelper;

    public abstract class TestBase<T>
        where T : DiagnosticAnalyzer
    {
        protected async Task RunTestAsync(string source)
        {
            //Assert.IsNotNull(analyzer);
            //ExecutionPath path = analyzer.MethodPaths.First().Value; // Take the first method should be only 1 element.
            ////analyzer.AnalyzePath(path);

            // TODO: Create a test to verify node structure.
        }
    }
}
*/