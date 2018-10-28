// <copyright file="PathAnalyzer.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    internal static class TreeGenerator
    {
        /// <summary>
        /// Splits method execution to a path where code execution order is uniformally defined.
        /// </summary>
        /// <param name="method">Method to be split.</param>
        /// <returns>Execution path for the method.</returns>
        internal static ExecutionPath SplitMethod(MethodDeclarationSyntax method)
        {
            ExecutionPath path = new ExecutionPath();
            ExecutionPathNode startOfMethod = path.Root;
            startOfMethod.Name = "Start of method";
            if (method.Body != null)
            {
                ExecutionPathNode endOfMethod = new ExecutionPathNode()
                {
                    Name = "End of method"
                };

                SplitInfo splitInfo = new SplitInfo() { MethodEndNode = endOfMethod };
                ExecutionPathNode lastNode = TreeGenerator.SplitAndAnalyzeBlock(startOfMethod, method.Body, splitInfo);
                if (lastNode != null)
                {
                    lastNode.CreatePathTo(endOfMethod);
                }
            }

            return path;
        }

        /*internal void AnalyzePath(ExecutionPath path)
        {
            // First we check which lines are within some loop.
            var cycles = GraphHelper.FindAllCycles<ExecutionPathNode>(path);
            IGraphNode<ExecutionPathNode>[] cycledNodes = cycles.SelectMany(x => x).ToArray();

            HashSet<ExecutionPathNode> analyzedNodes = new HashSet<ExecutionPathNode>();
            List<ExecutionPathNode> analyzableNodes = new List<ExecutionPathNode>();
            analyzableNodes.Add(path.Root);
            while (analyzableNodes.Count > 0)
            {
                var first = analyzableNodes[0];
                analyzableNodes.RemoveAt(0);
                if (analyzedNodes.Contains(first))
                {
                    // This node was already analyzed. We can skip it.
                    continue;
                }

                Debug.WriteLine(first.ToString());
                analyzedNodes.Add(first); // Mark this node as analyzed.
                analyzableNodes.AddRange(first.NextNodes);
            }
        }*/

        /// <summary>
        /// Analyzes either a single statement or a group of statements within a block.
        /// </summary>
        private static ExecutionPathNode SplitAndAnalyzeBlock(ExecutionPathNode prevNode, StatementSyntax statement, SplitInfo splitInfo)
        {
            if (statement is BlockSyntax block)
            {
                return TreeGenerator.SplitAndAnalyzeBlock(prevNode, block.Statements, splitInfo);
            }
            else
            {
                return TreeGenerator.SplitAndAnalyzeBlock(prevNode, new StatementSyntax[] { statement }, splitInfo);
            }
        }

        /// <summary>
        /// Returns new end node for a block of statements, or a null if all code paths leave the block.
        /// </summary>
        private static ExecutionPathNode SplitAndAnalyzeBlock(ExecutionPathNode startNode, IEnumerable<StatementSyntax> statements, SplitInfo splitInfo)
        {
            ExecutionPathNode prevNode = startNode;
            foreach (StatementSyntax statement in statements)
            {
                ExecutionPathNode statementNode;
                if (statement is ExpressionStatementSyntax expressionStatementSyntax)
                {
                    statementNode = TreeGenerator.SplitExpression(expressionStatementSyntax.Expression, prevNode);
                }
                else
                {
                    statementNode = new ExecutionPathNode() { SyntaxNode = statement };
                    prevNode.CreatePathTo(statementNode);
                    switch (statement)
                    {
                        // Branching (If & switch):
                        case IfStatementSyntax ifStatement:
                            statementNode = TreeGenerator.SplitIf(ifStatement, statementNode, splitInfo);
                            break;
                        case SwitchStatementSyntax switchStatement:
                            statementNode = TreeGenerator.SplitSwitch(switchStatement, statementNode, splitInfo);
                            break;

                        // Loops:
                        case ForStatementSyntax forStatement:
                            statementNode = TreeGenerator.SplitFor(forStatement, statementNode, splitInfo);
                            break;
                        case ForEachStatementSyntax forEachStatement:
                            statementNode = TreeGenerator.SplitForeach(forEachStatement, statementNode, splitInfo);
                            break;
                        ////case ForEachVariableStatementSyntax // Special case of foreach
                        case DoStatementSyntax doStatement:
                            statementNode = TreeGenerator.SplitDo(doStatement, statementNode, splitInfo);
                            break;
                        case WhileStatementSyntax whileStatement:
                            statementNode = TreeGenerator.SplitWhile(whileStatement, statementNode, splitInfo);
                            break;
                        case TryStatementSyntax tryStatement:
                            statementNode = TreeGenerator.SplitTry(tryStatement, statementNode, splitInfo);
                            break;

                        // Continue & Break:
                        case ContinueStatementSyntax continueStatement:
                            statementNode.CreatePathTo(splitInfo.LoopStartNode);
                            return null;
                        case BreakStatementSyntax breakStatement:
                            statementNode.CreatePathTo(splitInfo.LoopOrSwitchEndNode);
                            return null;

                        // Return:
                        case ReturnStatementSyntax returnStatement:
                            statementNode.CreatePathTo(splitInfo.MethodEndNode);
                            return null;
                        ////case YieldStatementSyntax

                        // Inline:
                        ////case ExpressionStatementSyntax expressionStatement:
                        ////    statementNode = TreeGenerator.SplitExpression(expressionStatement.Expression, statementNode);
                        ////    break;
                        case LocalDeclarationStatementSyntax localDeclarationStatement:
                            statementNode = TreeGenerator.SplitLocalDeclaration(localDeclarationStatement.Declaration.Variables, statementNode);
                            break;

                        // Blocks:
                        ////case BlockSyntax
                        ////case CheckedStatementSyntax
                        ////case FixedStatementSyntax
                        ////case LockStatementSyntax
                        ////case UnsafeStatementSyntax
                        ////case UsingStatementSyntax

                        // Simple statements:
                        ////case ExpressionStatementSyntax
                        ////case ThrowStatementSyntax
                        ////case GotoStatementSyntax
                        ////case LabeledStatementSyntax
                        ////case LocalDeclarationStatementSyntax

                        // Handled elesewhere:
                        ////case LocalFunctionStatementSyntax

                        case EmptyStatementSyntax _:
                            break; // Do nothing
#if DEBUG
                        default:
                            ////System.Diagnostics.Debug.WriteLine("Unsupported statement type: " + statement.GetType().Name);
                            break; // Do nothing
#endif
                    }
                }

                if ((statementNode == null) || (statementNode.PreviousNodes.Count == 0))
                {
                    return null; // No execution paths left. Leave current block.
                }
                else
                {
                    prevNode = statementNode;
                }
            }

            return prevNode;
        }

        private static ExecutionPathNode SplitLocalDeclaration(IEnumerable<VariableDeclaratorSyntax> variables, ExecutionPathNode statementNode)
        {
            ExecutionPathNode result = statementNode;
            result.SyntaxNode = null;
            foreach (VariableDeclaratorSyntax declaration in variables)
            {
                if (declaration.Initializer != null)
                {
                    // Variable was initialized at declaration.
                    var valueNode = TreeGenerator.SplitExpression(declaration.Initializer.Value, result);
                    valueNode.SyntaxNode = declaration;
                    result = valueNode;
                }
            }

            return result;
        }

        private static ExecutionPathNode SplitExpression(ExpressionSyntax expression, ExecutionPathNode previousPathNode)
        {
            ExecutionPathNode nextNode = new ExecutionPathNode() { SyntaxNode = expression };
            previousPathNode.CreatePathTo(nextNode);
            switch (expression)
            {
                // Some expressions may contain complex equations within.
                case AwaitExpressionSyntax awaitExpression:                    
                    return TreeGenerator.SplitExpression(awaitExpression.Expression, nextNode);
                case ParenthesizedExpressionSyntax parenthesizedExpressionSyntax:
                    return TreeGenerator.SplitExpression(parenthesizedExpressionSyntax.Expression, nextNode);
                case BinaryExpressionSyntax binaryExpressionSyntax:
                    var leftNode = TreeGenerator.SplitExpression(binaryExpressionSyntax.Left, nextNode);
                    return TreeGenerator.SplitExpression(binaryExpressionSyntax.Right, leftNode);
////                case ConditionalAccessExpressionSyntax conditionalAccessExpression:
////#if DEBUG
////                    throw new NotImplementedException("ConditionalAccessExpressionSyntax is not supported. " + expression.ToFullString());
////#endif

                // Most expressions are so simple, we can just leave now.
                default:
                    return nextNode;
            }
        }

        /// <summary>
        /// Analyses if-else -block.
        /// </summary>
        /// <param name="ifStatement">Current statement</param>
        /// <param name="statementNode">Previous known execution path node.</param>
        /// <param name="splitInfo">Analyser context.</param>
        /// <returns>Final execution path node after the code block.</returns>
        private static ExecutionPathNode SplitIf(IfStatementSyntax ifStatement, ExecutionPathNode statementNode, SplitInfo splitInfo)
        {
            ExecutionPathNode beginIfNode = statementNode;
            beginIfNode.SyntaxNode = ifStatement.Condition;
            statementNode.Name = "Begin if";
            ExecutionPathNode endIfNode = new ExecutionPathNode() { Name = "End if" };

            ExecutionPathNode ifBodyEndNode = TreeGenerator.SplitAndAnalyzeBlock(beginIfNode, ifStatement.Statement, splitInfo);
            ifBodyEndNode?.CreatePathTo(endIfNode);

            if (ifStatement.Else?.Statement != null)
            {
                ExecutionPathNode elseBodyEndNode = TreeGenerator.SplitAndAnalyzeBlock(beginIfNode, ifStatement.Else.Statement, splitInfo);
                elseBodyEndNode?.CreatePathTo(endIfNode);
            }
            else
            {
                // If there is no else statement, jump from the if to the end of its body.
                beginIfNode.CreatePathTo(endIfNode);
            }

            return endIfNode;
        }

        /// <summary>
        /// Analyses switch-block.
        /// </summary>
        /// <param name="switchStatement">Current statement</param>
        /// <param name="statementNode">Previous known execution path node.</param>
        /// <param name="splitInfo">Analyser context.</param>
        /// <returns>Final execution path node after the code block.</returns>
        private static ExecutionPathNode SplitSwitch(SwitchStatementSyntax switchStatement, ExecutionPathNode statementNode, SplitInfo splitInfo)
        {
            ExecutionPathNode beginSwitchNode = statementNode;
            beginSwitchNode.SyntaxNode = switchStatement.Expression;
            statementNode.Name = "Begin switch";
            bool hasDefault = false;
            ExecutionPathNode endSwitchNode = new ExecutionPathNode() { Name = "End switch" };
            foreach (SwitchSectionSyntax section in switchStatement.Sections)
            {
                ExecutionPathNode startOfBlock = beginSwitchNode;
                foreach (SwitchLabelSyntax label in section.Labels)
                {
                    switch (label)
                    {
                        case CaseSwitchLabelSyntax caseLabel:
                            break;
                        case CasePatternSwitchLabelSyntax caseWhenLabel:
                            break;
                        case DefaultSwitchLabelSyntax defaultLabel:
                            hasDefault = true;
                            break;
                    }

                    SplitInfo sectionSplitInfo = splitInfo.Clone();
                    sectionSplitInfo.LoopOrSwitchEndNode = endSwitchNode;
                    var endOfSection = TreeGenerator.SplitAndAnalyzeBlock(startOfBlock, section.Statements, sectionSplitInfo);
                }
            }

            if (!hasDefault)
            {
                // If there is no default, jump possibly streight to end.
                beginSwitchNode.CreatePathTo(endSwitchNode);
            }

            return endSwitchNode;
        }

        /// <summary>
        /// Analyses for-block.
        /// </summary>
        /// <param name="forStatement">Current statement</param>
        /// <param name="statementNode">Previous known execution path node.</param>
        /// <param name="splitInfo">Analyser context.</param>
        /// <returns>Final execution path node after the code block.</returns>
        private static ExecutionPathNode SplitFor(ForStatementSyntax forStatement, ExecutionPathNode statementNode, SplitInfo splitInfo)
        {
            ExecutionPathNode beginForNode = statementNode;
            beginForNode.Name = "Begin for";
            beginForNode.SyntaxNode = forStatement.Declaration;

            ExecutionPathNode endForNode = new ExecutionPathNode() { Name = "End for" };

            ExecutionPathNode forTestNode = new ExecutionPathNode() { SyntaxNode = forStatement.Condition };
            beginForNode.CreatePathTo(forTestNode);

            SplitInfo forSplitInfo = splitInfo.Clone();
            forSplitInfo.LoopStartNode = beginForNode;
            forSplitInfo.LoopOrSwitchEndNode = endForNode;

            ExecutionPathNode forBodyEndNode = TreeGenerator.SplitAndAnalyzeBlock(forTestNode, forStatement.Statement, forSplitInfo);
            if (forBodyEndNode == null)
            {
                return null; // No executions paths left.
            }

            ExecutionPathNode forIncrementNode = TreeGenerator.ExpressionSyntaxListToNodes(forStatement.Incrementors, forBodyEndNode);
            forIncrementNode?.CreatePathTo(forTestNode);

            forTestNode.CreatePathTo(endForNode);
            return endForNode;
        }

        /// <summary>
        /// Analyses foreach-block.
        /// </summary>
        /// <param name="forEachStatement">Current statement</param>
        /// <param name="statementNode">Previous known execution path node.</param>
        /// <param name="splitInfo">Analyser context.</param>
        /// <returns>Final execution path node after the code block.</returns>
        private static ExecutionPathNode SplitForeach(ForEachStatementSyntax forEachStatement, ExecutionPathNode statementNode, SplitInfo splitInfo)
        {
            ExecutionPathNode beginForEachNode = statementNode;
            beginForEachNode.Name = "Begin foreach";
            beginForEachNode.SyntaxNode = forEachStatement.Expression;

            ExecutionPathNode beginForEachBody = new ExecutionPathNode() { Name = "Begin foreach body", SyntaxNode = forEachStatement };
            beginForEachNode.CreatePathTo(beginForEachBody);

            ExecutionPathNode endForEachNode = new ExecutionPathNode() { Name = "End foreach" };
            beginForEachBody.CreatePathTo(endForEachNode);

            SplitInfo forEachSplitInfo = splitInfo.Clone();
            forEachSplitInfo.LoopStartNode = beginForEachBody;
            forEachSplitInfo.LoopOrSwitchEndNode = endForEachNode;

            ExecutionPathNode forEachBodyEndNode = TreeGenerator.SplitAndAnalyzeBlock(beginForEachBody, forEachStatement.Statement, forEachSplitInfo);
            if (forEachBodyEndNode == null)
            {
                return null; // No executions paths left.
            }
            else
            {
                forEachBodyEndNode.CreatePathTo(beginForEachBody);
            }

            return endForEachNode;
        }

        /// <summary>
        /// Analyses do-block.
        /// </summary>
        /// <param name="doStatement">Current statement</param>
        /// <param name="statementNode">Previous known execution path node.</param>
        /// <param name="splitInfo">Analyser context.</param>
        /// <returns>Final execution path node after the code block.</returns>
        private static ExecutionPathNode SplitDo(DoStatementSyntax doStatement, ExecutionPathNode statementNode, SplitInfo splitInfo)
        {
            ExecutionPathNode beginDoNode = statementNode;
            beginDoNode.Name = "Begin do";

            ExecutionPathNode endDoNode = new ExecutionPathNode();
            endDoNode.Name = "end do";

            SplitInfo doSplitInfo = splitInfo.Clone();
            doSplitInfo.LoopStartNode = beginDoNode;
            doSplitInfo.LoopOrSwitchEndNode = endDoNode;

            ExecutionPathNode bodyEnd = TreeGenerator.SplitAndAnalyzeBlock(beginDoNode, doStatement.Statement, doSplitInfo);

            ExecutionPathNode testNode = new ExecutionPathNode();
            testNode.SyntaxNode = doStatement.Condition;
            bodyEnd?.CreatePathTo(testNode);

            testNode.CreatePathTo(endDoNode);
            testNode.CreatePathTo(beginDoNode);
            return endDoNode;
        }

        /// <summary>
        /// Analyses do-while -block.
        /// </summary>
        /// <param name="whileStatement">Current statement</param>
        /// <param name="statementNode">Previous known execution path node.</param>
        /// <param name="splitInfo">Analyser context.</param>
        /// <returns>Final execution path node after the code block.</returns>
        private static ExecutionPathNode SplitWhile(WhileStatementSyntax whileStatement, ExecutionPathNode statementNode, SplitInfo splitInfo)
        {
            ExecutionPathNode beginWhileNode = statementNode;
            beginWhileNode.Name = "Begin while";
            beginWhileNode.SyntaxNode = whileStatement.Condition;

            ExecutionPathNode endDoNode = new ExecutionPathNode();
            endDoNode.Name = "end while";

            SplitInfo doSplitInfo = splitInfo.Clone();
            doSplitInfo.LoopStartNode = beginWhileNode;
            doSplitInfo.LoopOrSwitchEndNode = endDoNode;

            ExecutionPathNode bodyEnd = TreeGenerator.SplitAndAnalyzeBlock(beginWhileNode, whileStatement.Statement, doSplitInfo);
            bodyEnd?.CreatePathTo(beginWhileNode);

            beginWhileNode.CreatePathTo(endDoNode);
            return endDoNode;
        }

        /// <summary>
        /// Analyses try-catch-finally -block.
        /// </summary>
        /// <param name="tryStatement">Current statement</param>
        /// <param name="statementNode">Previous known execution path node.</param>
        /// <param name="splitInfo">Analyser context.</param>
        /// <returns>Final execution path node after the code block.</returns>
        private static ExecutionPathNode SplitTry(TryStatementSyntax tryStatement, ExecutionPathNode statementNode, SplitInfo splitInfo)
        {
            ExecutionPathNode beginTryNode = statementNode;
            beginTryNode.Name = "Begin try";

            ExecutionPathNode endTryNode = new ExecutionPathNode();
            endTryNode.Name = "End try";

            ExecutionPathNode lastNode;
            ExecutionPathNode endTryOrFinallyNode = endTryNode; // Initially assume the end try-block node.
            if (tryStatement.Finally != null)
            {
                ExecutionPathNode finallyNode = new ExecutionPathNode();
                finallyNode.Name = "finally";
                lastNode = TreeGenerator.SplitAndAnalyzeBlock(finallyNode, tryStatement.Finally.Block, splitInfo);
                lastNode?.CreatePathTo(endTryNode);
                endTryOrFinallyNode = finallyNode; // There was a finally block. Go there instead.
            }

            lastNode = TreeGenerator.SplitAndAnalyzeBlock(beginTryNode, tryStatement.Block, splitInfo);
            lastNode?.CreatePathTo(endTryOrFinallyNode); // After all statements, go to finally or if it does not exist, go to end.

            // There might be 0 or more catch blocks.
            if (tryStatement.Catches.Count > 0)
            {
                foreach (var catchBlock in tryStatement.Catches)
                {
                    lastNode = TreeGenerator.SplitAndAnalyzeBlock(beginTryNode, catchBlock.Block, splitInfo);
                    lastNode?.CreatePathTo(endTryOrFinallyNode); // After all statements, go to finally or if it does not exist, go to end.
                }
            }
            else if (endTryOrFinallyNode != endTryNode)
            {
                // When there are no catch blocks, but there is a finally block, move execution straight to finally block.
                beginTryNode.CreatePathTo(endTryOrFinallyNode);
            }

            return endTryNode;
        }

        private static ExecutionPathNode ExpressionSyntaxListToNodes(IEnumerable<ExpressionSyntax> statements, ExecutionPathNode previousNode)
        {
            var result = previousNode;
            foreach (var expressionSyntax in statements)
            {
                result = TreeGenerator.SplitExpression(expressionSyntax, result);
                ////ExecutionPathNode node = new ExecutionPathNode() { SyntaxNode = expressionSyntax };
                ////result.CreatePathTo(node);
                ////result = node;
            }

            return result;
        }

        private class SplitInfo
        {
            public ExecutionPathNode MethodEndNode { get; set; }

            public ExecutionPathNode LoopStartNode { get; set; }

            public ExecutionPathNode LoopOrSwitchEndNode { get; set; }

            internal SplitInfo Clone()
            {
                return new SplitInfo()
                {
                    MethodEndNode = this.MethodEndNode,
                    LoopStartNode = this.LoopStartNode,
                    LoopOrSwitchEndNode = this.LoopOrSwitchEndNode
                };
            }
        }
    }
}