using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using Google.OrTools.Sat;
using MineSweeperSatSolver.Adapters;

namespace MineSweeperSatSolver.Solvers
{
    internal interface IMinesweeperSolver
    {
        bool Solve(IMinesweeperAdapter game);
    }
    internal class GroupSolver : IMinesweeperSolver
    {
        private class CellGroup
        {
            public readonly List<Point> cells = new List<Point>();
            public int mineCount;
            public CellGroup(int mineCount)
            {
                this.mineCount = mineCount;
            }
        }
        public bool Solve(IMinesweeperAdapter game)
        {
            Console.WriteLine("GroupSolver");
            MinesweeperCell[,] field = game.GetField();
            int width = field.GetLength(0);
            int height = field.GetLength(1);
            bool changed = false;
            for (int i = 0; i < width; i++) // TODO: Move to main logic
                for (int j = 0; j < height; j++)
                {
                    if (field[i, j].State == CellState.Opened && field[i, j].MinesAround == 0)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                            for (int dy = -1; dy <= 1; dy++)
                                if (0 <= i + dx && i + dx < width && 0 <= j + dy && j + dy < height && field[i + dx, j + dy].State == CellState.Closed)
                                {
                                    Console.WriteLine($"Not Expanded at {i + dx} {i + dy}"); // If around zero cell has closed cell
                                    return false;
                                }
                    }

                }
            List<CellGroup> groups = new List<CellGroup>();
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    if (field[i, j].State == CellState.Opened && field[i, j].MinesAround > 0)
                    {
                        CellGroup group = new CellGroup(field[i, j].MinesAround);
                        for (int x = -1; x <= 1; x++)
                            for (int y = -1; y <= 1; y++)
                                if (i + x >= 0 && i + x < width && j + y >= 0 && j + y < height)
                                {

                                    if (field[i + x, j + y].State == CellState.Closed)
                                        group.cells.Add(new Point(i + x, j + y));
                                    if (field[i + x, j + y].State == CellState.Marked)
                                        group.mineCount--;
                                }
                        if (group.cells.Count > 0)
                            groups.Add(group);
                    }
                }
            }
            bool repeat;
            do
            {
                repeat = false;
                for (int i = 0; i < groups.Count - 1; i++)
                {
                    CellGroup groupI = groups[i];
                    for (int j = i + 1; j < groups.Count; j++)
                    {
                        CellGroup groupJ = groups[j];
                        bool equal = true;
                        if (groupI.cells.Count == groupJ.cells.Count)
                        {
                            for (int k = 0; k < groupI.cells.Count; k++)
                                if (!groupI.cells[k].Equals(groupJ.cells[k]))
                                {
                                    equal = false;
                                    break;
                                }
                            if (equal)
                            {
                                groups.RemoveAt(j);
                                j--;
                                continue;
                            }
                        }

                        CellGroup groupBig;
                        CellGroup groupLittle;
                        CellGroup groupOverlap = new CellGroup(0);
                        if (groupI.cells.Count > groupJ.cells.Count)
                        {
                            groupBig = groupI;
                            groupLittle = groupJ;
                        }
                        else
                        {
                            groupBig = groupJ;
                            groupLittle = groupI;
                        }

                        bool fullyIn = true;
                        for (int l = 0; l < groupLittle.cells.Count; l++)
                        {
                            bool found = false;
                            for (int r = 0; r < groupBig.cells.Count; r++)
                                if (groupLittle.cells[l].Equals(groupBig.cells[r]))
                                {
                                    found = true;
                                    groupOverlap.cells.Add(groupBig.cells[r]);
                                    break;
                                }
                            if (!found)
                                fullyIn = false;
                        }
                        if (fullyIn) // TODO: Old group can be useful
                        {
                            for (int l = 0, r = 0; r < groupLittle.cells.Count; r++)
                            {
                                while (l < groupBig.cells.Count && !groupBig.cells[l].Equals(groupLittle.cells[r]))
                                    l++;
                                if (l >= groupBig.cells.Count)
                                    break;
                                groupBig.cells.RemoveAt(l);
                            }
                            groupBig.mineCount -= groupLittle.mineCount;
                            repeat = true;
                            continue;
                        }
                        if (groupOverlap.cells.Count == 0)
                            continue;

                        //int minCount = Math.Max(0, Math.Max(groupLittle.mineCount - (groupLittle.cells.Count - groupOverlap.cells.Count), groupBig.mineCount - (groupBig.cells.Count - groupOverlap.cells.Count)));
                        int maxCount = Math.Min(groupOverlap.cells.Count, Math.Min(groupLittle.mineCount, groupBig.mineCount));
                        if (groupLittle.cells.Count - groupOverlap.cells.Count == groupLittle.mineCount - maxCount)
                        {
                            for (int l = 0, r = 0; r < groupOverlap.cells.Count; r++)
                            {
                                while (l < groupLittle.cells.Count && !groupOverlap.cells[r].Equals(groupLittle.cells[l]))
                                    l++;
                                if (l >= groupLittle.cells.Count)
                                    break;
                                groupLittle.cells.RemoveAt(l);
                            }
                            groupLittle.mineCount -= maxCount;
                            repeat = true;
                            continue;
                        }
                        if (groupBig.cells.Count - groupOverlap.cells.Count == groupBig.mineCount - maxCount)
                        {
                            for (int l = 0, r = 0; r < groupOverlap.cells.Count; r++)
                            {
                                while (l < groupBig.cells.Count && !groupOverlap.cells[r].Equals(groupBig.cells[l]))
                                    l++;
                                if (l >= groupBig.cells.Count)
                                    break;
                                groupBig.cells.RemoveAt(l);
                            }
                            groupBig.mineCount -= maxCount;
                            repeat = true;
                            continue;
                        }
                        //if (minCount == maxCount)
                    }
                }
            } while (repeat);

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].mineCount == 0)
                {
                    for (int j = 0; j < groups[i].cells.Count; j++)
                    {
                        game.Click(groups[i].cells[j].X, groups[i].cells[j].Y);
                        changed = true;
                    }
                }
                if (groups[i].mineCount == groups[i].cells.Count)
                {
                    for (int j = 0; j < groups[i].cells.Count; j++)
                    {
                        if (field[groups[i].cells[j].X, groups[i].cells[j].Y].State != CellState.Marked)
                        {
                            game.Mark(groups[i].cells[j].X, groups[i].cells[j].Y);
                            field[groups[i].cells[j].X, groups[i].cells[j].Y].State = CellState.Marked;
                            changed = true;
                        }
                    }
                }
            }
            return changed;
        }
    }
    internal class SatSolver : IMinesweeperSolver
    {
        private static readonly Random random = new Random();
        private class Solutions
        {
            public IntVar[,] Variables;
            public long TotalSolutions;
            public long[,] MineHits;
        }

        private class MinesweeperSolutionCallback : CpSolverSolutionCallback
        {
            private readonly Solutions solutions;

            public MinesweeperSolutionCallback(Solutions solutions)
            {
                this.solutions = solutions ?? throw new ArgumentNullException(nameof(solutions));
            }

            public override void OnSolutionCallback()
            {
                solutions.TotalSolutions++;
                for (var x = 0; x < solutions.Variables.GetLength(0); x++)
                    for (var y = 0; y < solutions.Variables.GetLength(1); y++)
                    {
                        if (solutions.Variables[x, y] != null)
                            solutions.MineHits[x, y] += Value(solutions.Variables[x, y]);
                    }
            }
        }
        private readonly bool safeMode;
        public SatSolver(bool safeMode = false)
        {
            this.safeMode = safeMode;
        }
        public bool Solve(IMinesweeperAdapter game)
        {
            Console.WriteLine("SatSolver");
            var field = game.GetField();

            var width = field.GetLength(0);
            var height = field.GetLength(1);

            var solutions = new Solutions
            {
                MineHits = new long[width, height],
                Variables = new IntVar[width, height]
            };

            var model = new CpModel();
            var equations = 0;
            for (var x = 0; x < width; x++)
                for (var y = 0; y < height; y++)
                {
                    if (field[x, y].State != CellState.Opened)
                        continue;
                    if (field[x, y].MinesAround == 0)
                        continue;

                    LinearExpr cellEquation = null;
                    var minesAround = field[x, y].MinesAround;

                    for (var offsetX = -1; offsetX <= 1; offsetX++)
                        for (var offsetY = -1; offsetY <= 1; offsetY++)
                        {
                            if (offsetX == 0 && offsetY == 0)
                                continue;
                            if (offsetX + x < 0 || offsetX + x >= width || offsetY + y < 0 || offsetY + y >= height)
                                continue;
                            if (field[offsetX + x, offsetY + y].State == CellState.Closed)
                            {
                                var currentCellKey = $"{offsetX + x}:{offsetY + y}";
                                if (solutions.Variables[offsetX + x, offsetY + y] == null)
                                    solutions.Variables[offsetX + x, offsetY + y] =
                                        model.NewIntVar(0, 1, currentCellKey);

                                if (cellEquation == null)
                                    cellEquation = solutions.Variables[offsetX + x, offsetY + y];
                                else
                                    cellEquation += solutions.Variables[offsetX + x, offsetY + y];
                            }

                            if (field[offsetX + x, offsetY + y].State == CellState.Marked)
                                minesAround--;
                        }

                    if (cellEquation == null)
                        continue;
                    model.Add(cellEquation == minesAround);
                    equations++;
                }

            var solver = new CpSolver();
            {
                var variables = 0;
                for (var x = 0; x < width; x++)
                    for (var y = 0; y < height; y++)
                    {
                        variables += solutions.Variables[x, y] != null ? 1 : 0;
                    }
                // TODO: set time limit
                Console.Write($"Variables: {variables}, Equations: {equations}, Result: ");
                Console.Out.Flush();
            }
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            Console.Write(solver.SearchAllSolutions(model, new MinesweeperSolutionCallback(solutions)));
            stopwatch.Stop();
            Console.WriteLine($", Time consumed: {stopwatch.ElapsedMilliseconds} ms");

            var wasChanged = false;

            for (var x = 0; x < width; x++)
                for (var y = 0; y < height; y++)
                {
                    if (solutions.Variables[x, y] == null)
                        continue;
                    if (solutions.MineHits[x, y] == 0)
                    {
                        game.Click(x, y);
                        wasChanged = true;
                    }

                    if (solutions.MineHits[x, y] != solutions.TotalSolutions)
                        continue;

                    game.Mark(x, y);
                    wasChanged = true;
                }

            if (wasChanged)
                return true;

            var minimalPoint = new Point(-1, -1);
            var maximalPoint = new Point(-1, -1);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (solutions.Variables[x, y] == null)
                        continue;
                    if (minimalPoint.X == -1 || solutions.MineHits[minimalPoint.X, minimalPoint.Y] > solutions.MineHits[x, y]) // TODO: Or Mark point if chance better
                        minimalPoint = new Point(x, y);
                    if (maximalPoint.X == -1 || solutions.MineHits[maximalPoint.X, maximalPoint.Y] < solutions.MineHits[x, y]) // TODO: Or Mark point if chance better
                        maximalPoint = new Point(x, y);
                }
            }

            if (minimalPoint.X != -1)
                Console.WriteLine(
                    $"Chance hit at {minimalPoint.X}:{minimalPoint.Y} {100.0 * solutions.MineHits[minimalPoint.X, minimalPoint.Y] / solutions.TotalSolutions:0.00}%");
            if (maximalPoint.X != -1)
                Console.WriteLine($"And max chance is {100.0 * solutions.MineHits[maximalPoint.X, maximalPoint.Y] / solutions.TotalSolutions:0.00}%");

            if (safeMode)
                return false;

            if (minimalPoint.X == -1)
            {
                var possiblePoints = new List<Point>();
                for (var x = 0; x < width; x++)
                    for (var y = 0; y < height; y++)
                    {
                        if (field[x, y].State == CellState.Closed)
                            possiblePoints.Add(new Point(x, y));
                    }

                if (!possiblePoints.Any())
                    return false;

                var point = possiblePoints[random.Next(possiblePoints.Count)];
                game.Click(point.X, point.Y);
            }
            else
            {
                if ((1 - solutions.MineHits[maximalPoint.X, maximalPoint.Y] * 1.0 / solutions.TotalSolutions) * 2 <= solutions.MineHits[minimalPoint.X, minimalPoint.Y] * 1.0 / solutions.TotalSolutions)
                    game.Mark(maximalPoint.X, maximalPoint.Y);
                else
                    game.Click(minimalPoint.X, minimalPoint.Y);
            }
            return true;
        }
    }
    internal class MixedSolver : IMinesweeperSolver // Or move execute GroupSolver to SatSolver
    {
        private readonly GroupSolver groupSolver;
        private readonly SatSolver satSolver;
        public MixedSolver(bool safeMode = false)
        {
            groupSolver = new GroupSolver();
            satSolver = new SatSolver(safeMode);
        }
        public bool Solve(IMinesweeperAdapter game)
        {
            if (groupSolver.Solve(game))
                return true;
            game.FetchState(); // TODO: Sync marked cells
            return satSolver.Solve(game);
        }
    }
}
