// <copyright file="TestClass.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace ConsoleTester
{
    using System.Collections.Generic;

    internal class TestClass
    {
        ////IDictionary<string, double> fieldDict = null;

        public void Test()
        {
            /*
            double old;
            IDictionary<string, double> localDict = null;
            if (localDict.ContainsKey("Kissa"))
            {
                old = localDict["Kissa"];
                old++;
                localDict["Kissa"] = old;

                localDict["Kissa"]++;

                localDict["Kissa"] *= 4;

                ++localDict["Kissa"];

                int i = ~(int)localDict["Kissa"];
            }
            */
            {
                int i = 4;
                IDictionary<int, double> localDict = null;
                IDictionary<int, IDictionary<int, double>> localDict2 = null;
                localDict[i].ToString();
                localDict2.TryGetValue(2, out localDict);
                localDict[i].ToString();
                i++;
                localDict[i].ToString();
                i++;
                localDict[i].ToString();
            }

            {
                IDictionary<string, double> localDict = null;
                localDict["hiiri"].ToString();
                localDict["koira"].ToString();
                localDict["koira"]++;
                localDict["koira"].ToString();
                localDict["kissa"].ToString();
                localDict["hiiri"].ToString();
                localDict.Clear();
                localDict["kissa"].ToString();
                localDict["koira"].ToString();
                localDict["hiiri"].ToString();
            }

            /*{
                IDictionary<string, double> localDict = null;
                localDict["kissa"].ToString();
                localDict["kissa"].ToString();
                localDict["kissa"].ToString();
                localDict["kissa"] = 5;
                localDict["kissa"].ToString();
                localDict["kissa"].ToString();
            }*/
        }

        /*public void Test2()
        {
            if (fieldDict.ContainsKey("Kissa"))
            {
                double old = fieldDict["Kissa"];
                old++;
                fieldDict["Kissa"] = old;
            }
        }

        public void Test3(IDictionary<string, double> argumentDict)
        {
            if (argumentDict.ContainsKey("Kissa"))
            {
                double old = argumentDict["Kissa"];
                old++;
                argumentDict["Kissa"] = old;
            }
        }*/

        /*private class ContainerClass
        {
            public IDictionary<string, double> Other { get; } = new Dictionary<string, double>();
        }*/
    }
}
