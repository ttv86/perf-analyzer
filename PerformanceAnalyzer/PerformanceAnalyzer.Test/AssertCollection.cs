using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PerformanceAnalyzer.Test
{
    internal class AssertCollection
    {
        internal static void AreEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        {
            Assert.AreEqual(expected == null, actual == null);
            using (var enumerator1 = expected.GetEnumerator())
            {
                using (var enumerator2 = expected.GetEnumerator())
                {
                    bool end1 = enumerator1.MoveNext();
                    bool end2 = enumerator2.MoveNext();
                    while (end1 && end2)
                    {
                        Assert.AreEqual(enumerator1.Current, enumerator2.Current);
                        end1 = enumerator1.MoveNext();
                        end2 = enumerator2.MoveNext();
                    }

                    Assert.AreEqual(end1, end2);
                }
            }
        }
    }
}