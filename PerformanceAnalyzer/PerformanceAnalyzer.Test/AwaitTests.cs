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
            int awaitIndex = source.IndexOf("await");
            Assert.AreEqual(awaitIndex, diagnostics[0].Location.SourceSpan.Start); // Diagnostic location should start from the await
            Assert.AreEqual(source.IndexOf(";", awaitIndex), diagnostics[0].Location.SourceSpan.End); // Diagnostic location should end before next ";" from await.
        }

        [TestMethod]
        public async Task TestForAwaitInForEachInsideExpression()
        {
            string source = @"using System.Threading.Tasks;
internal static class TestClass
{
    public static async Task Test(System.Collection.IEnumerable<int> list, Task<int> task, System.Action<int> callback)
    {
        foreach (int i in list) {
            callback(await task);
        }
    }
}";

            Diagnostic[] diagnostics = await DiagnosticVerifier.CreateAndRunAnalyzerAsync<AwaitInLoopAnalyzer>(source);
            Assert.AreEqual(1, diagnostics.Length);
            Assert.AreEqual("A task was awaited within a loop.", diagnostics[0].GetMessage());
            int awaitIndex = source.IndexOf("await");
            Assert.AreEqual(awaitIndex, diagnostics[0].Location.SourceSpan.Start); // Diagnostic location should start from the await
            Assert.AreEqual(source.IndexOf(");", awaitIndex), diagnostics[0].Location.SourceSpan.End); // Diagnostic location should end before next ";" from await.
        }
    }
}
