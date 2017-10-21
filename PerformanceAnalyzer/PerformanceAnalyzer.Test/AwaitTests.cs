// <copyright file="AwaitTests.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using TestHelper;

    [TestClass]
    public class AwaitTests
    {
        [TestMethod]
        public async Task TestForAwaitInForEach()
        {
            string source = @"internal static class TestClass
{
    public static async System.Threading.Tasks.Task Test(System.Collection.IEnumerable<int> list, Task task)
    {
        foreach (int i in list) {
            await task;
        }
    }
}";

            Diagnostic[] diagnostics = await DiagnosticVerifier.CreateAndRunAnalyzerAsync<AwaitInLoopAnalyzer>(source);
            Assert.AreEqual(1, diagnostics.Length);
            Assert.AreEqual("A task was awaited within a loop.", diagnostics[0].GetMessage());
        }
    }
}
