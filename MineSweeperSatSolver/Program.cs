using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using WindowsInput;
using Google.OrTools.Sat;

namespace MineSwepperSatSolver
{
    internal class Program
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

        private static void Main(string[] args)
        {
            var game = new WindowsXpMineswepper();

            game.Reset();

            while (true)
            {
                var state = game.FetchState();

                while (!game.IsDead(state) && !game.IsReady(state))
                {
                    state = game.FetchState();

                    var field = game.GetField(state);

                    var width = field.GetLength(0);
                    var height = field.GetLength(1);

                    var solutions = new Solutions
                    {
                        MineHits = new long[width, height],
                        Variables = new IntVar[width, height]
                    };

                    var changed = false;

                    for (var x = 0; x < width; x++)
                        for (var y = 0; y < height; y++)
                        {
                            if (field[x, y].State != CellState.Opened)
                                continue;
                            if (field[x, y].MinesAround == 0)
                                continue;

                            var minesAround = field[x, y].MinesAround;
                            var closedAround = 0;

                            for (var offsetX = -1; offsetX <= 1; offsetX++)
                                for (var offsetY = -1; offsetY <= 1; offsetY++)
                                {
                                    if (offsetX == 0 && offsetY == 0)
                                        continue;
                                    if (offsetX + x < 0 || offsetX + x >= width || offsetY + y < 0 || offsetY + y >= height)
                                        continue;
                                    if (field[offsetX + x, offsetY + y].State == CellState.Closed)
                                        closedAround++;
                                    if (field[offsetX + x, offsetY + y].State == CellState.Marked)
                                        minesAround--;
                                }

                            if (minesAround == 0)
                            {
                                for (var offsetX = -1; offsetX <= 1; offsetX++)
                                    for (var offsetY = -1; offsetY <= 1; offsetY++)
                                    {
                                        if (offsetX == 0 && offsetY == 0)
                                            continue;
                                        if (offsetX + x < 0 || offsetX + x >= width || offsetY + y < 0 ||
                                            offsetY + y >= height)
                                            continue;
                                        if (field[offsetX + x, offsetY + y].State != CellState.Closed) 
                                            continue;
                                        game.Click(offsetX + x, offsetY + y);
                                        field[offsetX + x, offsetY + y].State = CellState.Question;
                                        changed = true;
                                    }
                                continue;
                            }

                            if (minesAround != closedAround)
                                continue;

                            for (var offsetX = -1; offsetX <= 1; offsetX++)
                                for (var offsetY = -1; offsetY <= 1; offsetY++)
                                {
                                    if (offsetX == 0 && offsetY == 0)
                                        continue;
                                    if (offsetX + x < 0 || offsetX + x >= width || offsetY + y < 0 || offsetY + y >= height)
                                        continue;
                                    if (field[offsetX + x, offsetY + y].State != CellState.Closed) 
                                        continue;
                                    game.Mark(offsetX + x, offsetY + y);
                                    field[offsetX + x, offsetY + y].State = CellState.Marked;
                                    changed = true;
                                }
                        }

                    if (changed)
                    {
                        Console.WriteLine("Simple");
                        continue;
                    }

                    Console.WriteLine("Intellectual");

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
                        continue;

                    var minimalPoint = new Point(-1, -1);

                    for (var y = 0; y < height; y++)
                    {
                        for (var x = 0; x < width; x++)
                        {
                            if (solutions.Variables[x, y] == null) 
                                continue;
                            if (minimalPoint.X == -1 || solutions.MineHits[minimalPoint.X, minimalPoint.Y] >
                                solutions.MineHits[x, y])
                                minimalPoint = new Point(x, y);
                        }
                    }

                    if (minimalPoint.X != -1)
                        Console.WriteLine(
                            $"Chance hit at {minimalPoint.X}:{minimalPoint.Y} {100.0 * solutions.MineHits[minimalPoint.X, minimalPoint.Y] / solutions.TotalSolutions:0.00}%");

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
                            continue;

                        var point = possiblePoints[random.Next(possiblePoints.Count)];
                        game.Click(point.X, point.Y);
                    } 
                    else
                        game.Click(minimalPoint.X, minimalPoint.Y);
                }

                if (game.IsReady(state))
                    break;

                game.Reset();
            }

            /*
            var model = new CpModel();
            var var1 = model.NewIntVar(0, 1, "x");
            var var2 = model.NewIntVar(0, 1, "y");
            model.Add(var1 != var2);

            var solver = new CpSolver();
            var status = solver.Solve(model);
            if (status == CpSolverStatus.Feasible)
            {
                Console.WriteLine(solver.Value(var1));
                Console.WriteLine(solver.Value(var2));
            }*/
        }
    }

    internal enum CellState
    {
        Closed,
        Opened,
        Marked,
        QuestionMarked,
        Mine,
        BlownMine,
        NoMine,
        Question
    }

    internal struct MineswepperCell
    {        
        public CellState State { get; set; }

        public int MinesAround { get; set; }

        public override string ToString() => $"{nameof(State)}: {State}, {nameof(MinesAround)}: {MinesAround}";
    }

    internal interface IMineswepper<T>
    {
        MineswepperCell[,] GetField(T state);

        T FetchState();

        bool IsDead(T state);

        bool IsReady(T state);

        void Click(int x, int y);

        void Mark(int x, int y);

        void Reset();
    }

    internal class WindowsXpMineswepper : IMineswepper<Bitmap>
    {
        private readonly IntPtr windowHandle;

        private readonly InputSimulator inputSimulator = new InputSimulator();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);
        
        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int x, int y);

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        public WindowsXpMineswepper()
        {
            var foundWindowHandle = IntPtr.Zero;

            foreach (var process in Process.GetProcesses())
            {
                if (process.MainWindowTitle.Contains("Minesweeper"))
                {
                    foundWindowHandle = process.MainWindowHandle;
                }
            }

            if (foundWindowHandle == IntPtr.Zero)
                throw new Exception("Minesweeper window wasn't found");

            if (!SetForegroundWindow(foundWindowHandle))
                throw new Exception("Could not set foreground window");
            windowHandle = foundWindowHandle;
        }

        public Bitmap FetchState()
        {
            if (!SetForegroundWindow(windowHandle))
                throw new Exception("Could not set foreground window");
            if (!GetClientRect(windowHandle, out var rect))
                throw new Exception("Could not get window rect");
            var point = new Point
            {
                X = rect.Left,
                Y = rect.Top
            };
            if (!ClientToScreen(windowHandle, ref point))
                throw new Exception("Could not get client point");

            var windowScreenShot =
                new Bitmap(rect.Right - rect.Left, rect.Bottom - rect.Top);
            using var screenGraphics = Graphics.FromImage(windowScreenShot);

            screenGraphics.CopyFromScreen(point.X, point.Y,
                0, 0, new Size(windowScreenShot.Width, windowScreenShot.Height),
                CopyPixelOperation.SourceCopy);

            windowScreenShot.Save("temp.png");

            return windowScreenShot;
        }

        private static MineswepperCell ParseCell(int cellHash)
        {
            var cell = new MineswepperCell();
            
            switch (cellHash)
            {
                case 560552706:
                    cell.State = CellState.Closed;
                    break;
                case -91164671:
                    cell.State = CellState.Marked;
                    break;
                case -194541566:
                    cell.State = CellState.QuestionMarked;
                    break;
                case -1208559324:
                    cell.State = CellState.BlownMine;
                    break;
                case -1103832863:
                    cell.State = CellState.NoMine;
                    break;
                case -1668850496:
                    cell.State = CellState.Mine;
                    break;
                case -1887492416:
                    cell.State = CellState.Question;
                    break;
                case 2067284928:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 8;
                    break;
                case 1275880384:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 7;
                    break;
                case 139654080:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 6;
                    break;
                case 792206272:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 5;
                    break;
                case -1475292224:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 4;
                    break;
                case -1079452576:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 3;
                    break;
                case -1150141824:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 2;
                    break;
                case 607173218:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 1;
                    break;
                case -2009844800:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 0;
                    break;
                default:
                    throw new Exception($"Unknown cell hash: {cellHash}");
            }

            return cell;
        }

        public MineswepperCell[,] GetField(Bitmap state)
        {
            var windowScreenShot = state;

            var cells = new MineswepperCell[(windowScreenShot.Width - 20) / 16,
                (windowScreenShot.Height - 63) / 16];

            var imageData = new byte[sizeof(byte) * 3 * windowScreenShot.Width * windowScreenShot.Height];
            var bitmapData = windowScreenShot.LockBits(new Rectangle(0, 0, windowScreenShot.Width, windowScreenShot.Height), 
                System.Drawing.Imaging.ImageLockMode.ReadOnly, 
                System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            Marshal.Copy(bitmapData.Scan0, imageData, 0, imageData.Length);
            windowScreenShot.UnlockBits(bitmapData);

            var width = cells.GetLength(0);
            var height = cells.GetLength(1);

            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                var cellHash = 0;
                for (var cellX = 0; cellX < 16; cellX++)
                for (var cellY = 0; cellY < 16; cellY++)
                {
                    cellHash = 31 * cellHash + imageData[(12 + x * 16 + cellX + (55 + y * 16 + cellY)
                                                          * bitmapData.Width) * 3];
                    cellHash = 31 * cellHash + imageData[(12 + x * 16 + cellX + (55 + y * 16 + cellY)
                                                          * bitmapData.Width) * 3 + 1];
                    cellHash = 31 * cellHash + imageData[(12 + x * 16 + cellX + (55 + y * 16 + cellY)
                                                          * bitmapData.Width) * 3 + 2];
                }

                cells[x, y] = ParseCell(cellHash);
            }

            return cells;
        }

        public bool IsDead(Bitmap state)
        {
            var windowScreenShot = FetchState();
            return state.GetPixel(windowScreenShot.Width / 2, 24).R == 0;
        }

        public bool IsReady(Bitmap state)
        {
            var windowScreenShot = FetchState();
            return state.GetPixel(windowScreenShot.Width / 2 - 5, 28).R == 0;
        }

        public void Click(int x, int y)
        {
            if (!SetForegroundWindow(windowHandle))
                throw new Exception("Could not set foreground window");
            if (!GetClientRect(windowHandle, out var rect))
                throw new Exception("Could not get window rect");
            var point = new Point
            {
                X = rect.Left,
                Y = rect.Top
            };
            if (!ClientToScreen(windowHandle, ref point))
                throw new Exception("Could not get client point");
            SetCursorPos(point.X + 12 + x * 16 + 8, 
                point.Y + 55 + y * 16 + 8);
            inputSimulator.Mouse.LeftButtonClick();
        }

        public void Mark(int x, int y)
        {
            if (!SetForegroundWindow(windowHandle))
                throw new Exception("Could not set foreground window");
            if (!GetClientRect(windowHandle, out var rect))
                throw new Exception("Could not get window rect");
            var point = new Point
            {
                X = rect.Left,
                Y = rect.Top
            };
            if (!ClientToScreen(windowHandle, ref point))
                throw new Exception("Could not get client point");
            SetCursorPos(point.X + 12 + x * 16 + 8,
                point.Y + 55 + y * 16 + 8);
            inputSimulator.Mouse.RightButtonClick();
        }

        public void Reset()
        {
            if (!SetForegroundWindow(windowHandle))
                throw new Exception("Could not set foreground window");
            if (!GetClientRect(windowHandle, out var rect))
                throw new Exception("Could not get window rect");
            var point = new Point
            {
                X = rect.Left,
                Y = rect.Top
            };
            if (!ClientToScreen(windowHandle, ref point))
                throw new Exception("Could not get client point");
            SetCursorPos(point.X + (rect.Right - rect.Left) / 2, point.Y + 28);
            inputSimulator.Mouse.LeftButtonClick();
        }
    }
}
