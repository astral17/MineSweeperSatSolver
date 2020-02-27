using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using WindowsInput;

namespace MineSweeperSatSolver
{
    internal class Program
    {
        private static readonly Random random = new Random();

        private static void Main(string[] args)
        {
            //UniversalMinesweeper.FindPattern("cells");
            //return;
            var game = new WindowsXpMinesweeper();
            //var game = new UniversalMinesweeper();
            var solver = new MixedSolver();

            game.Reset();

            while (true)
            {
                while (game.FetchState() && !game.IsDead() && !game.IsReady())
                {
                    if (!solver.Solve(game))
                    {
                        throw new Exception("no mines was clicked");
                    }
                }

                if (game.IsReady())
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

    internal enum CellState // Mb remove unuseful states
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

    internal struct MinesweeperCell
    {        
        public CellState State { get; set; }

        public int MinesAround { get; set; }

        public override string ToString() => $"{nameof(State)}: {State}, {nameof(MinesAround)}: {MinesAround}";
    }

    internal interface IMinesweeper // TODO: Move to other file
    {
        MinesweeperCell[,] GetField();

        bool FetchState();

        //TODO: int TotalMines();

        bool IsDead();

        bool IsReady(); // IsSolved

        void Click(int x, int y);

        void Mark(int x, int y);

        void Reset();
    }

    internal class WindowsXpMinesweeper : IMinesweeper
    {
        private readonly IntPtr windowHandle;

        private readonly InputSimulator inputSimulator = new InputSimulator();

        public Bitmap windowScreenShot = null;

        public WindowsXpMinesweeper()
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

            if (!WinApi.SetForegroundWindow(foundWindowHandle))
                throw new Exception("Could not set foreground window");
            windowHandle = foundWindowHandle;
        }

        public bool FetchState()
        {
            if (!WinApi.SetForegroundWindow(windowHandle))
                throw new Exception("Could not set foreground window");
            if (!WinApi.GetClientRect(windowHandle, out var rect))
                throw new Exception("Could not get window rect");
            var point = new WinApi.Point
            {
                X = rect.Left,
                Y = rect.Top
            };
            if (!WinApi.ClientToScreen(windowHandle, ref point))
                throw new Exception("Could not get client point");

            windowScreenShot =
                new Bitmap(rect.Right - rect.Left, rect.Bottom - rect.Top);
            using var screenGraphics = Graphics.FromImage(windowScreenShot);

            screenGraphics.CopyFromScreen(point.X, point.Y,
                0, 0, new Size(windowScreenShot.Width, windowScreenShot.Height),
                CopyPixelOperation.SourceCopy);
            //windowScreenShot.Save("temp.png");
            return true;
        }

        private static MinesweeperCell ParseCell(int cellHash)
        {
            var cell = new MinesweeperCell();
            
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

        public MinesweeperCell[,] GetField()
        {
            var cells = new MinesweeperCell[(windowScreenShot.Width - 20) / 16,
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

        public bool IsDead()
        {
            return windowScreenShot.GetPixel(windowScreenShot.Width / 2, 24).R == 0;
        }

        public bool IsReady()
        {
            return windowScreenShot.GetPixel(windowScreenShot.Width / 2 - 5, 28).R == 0;
        }

        public void Click(int x, int y)
        {
            if (!WinApi.SetForegroundWindow(windowHandle))
                throw new Exception("Could not set foreground window");
            if (!WinApi.GetClientRect(windowHandle, out var rect))
                throw new Exception("Could not get window rect");
            var point = new WinApi.Point
            {
                X = rect.Left,
                Y = rect.Top
            };
            if (!WinApi.ClientToScreen(windowHandle, ref point))
                throw new Exception("Could not get client point");
            WinApi.SetCursorPos(point.X + 12 + x * 16 + 8, 
                point.Y + 55 + y * 16 + 8);
            inputSimulator.Mouse.LeftButtonClick();
        }

        public void Mark(int x, int y)
        {
            if (!WinApi.SetForegroundWindow(windowHandle))
                throw new Exception("Could not set foreground window");
            if (!WinApi.GetClientRect(windowHandle, out var rect))
                throw new Exception("Could not get window rect");
            var point = new WinApi.Point
            {
                X = rect.Left,
                Y = rect.Top
            };
            if (!WinApi.ClientToScreen(windowHandle, ref point))
                throw new Exception("Could not get client point");
            WinApi.SetCursorPos(point.X + 12 + x * 16 + 8,
                point.Y + 55 + y * 16 + 8);
            inputSimulator.Mouse.RightButtonClick();
        }

        public void Reset()
        {
            if (!WinApi.SetForegroundWindow(windowHandle))
                throw new Exception("Could not set foreground window");
            if (!WinApi.GetClientRect(windowHandle, out var rect))
                throw new Exception("Could not get window rect");
            var point = new WinApi.Point
            {
                X = rect.Left,
                Y = rect.Top
            };
            if (!WinApi.ClientToScreen(windowHandle, ref point))
                throw new Exception("Could not get client point");
            WinApi.SetCursorPos(point.X + (rect.Right - rect.Left) / 2, point.Y + 28);
            inputSimulator.Mouse.LeftButtonClick();
        }
    }

    internal class UniversalMinesweeper : IMinesweeper
    {
        private readonly InputSimulator inputSimulator = new InputSimulator();

        public Bitmap windowScreenShot = null;
        private static int cellSize = 16; // TODO: remove static and load from config file
        private static int width = 30;
        private static int height = 16;

        private static int offsetX = 8; // from left top rect to first pixel of field
        private static int offsetY = 50;

        private WinApi.Point point = new WinApi.Point { X = 660, Y = 259 };
        //private WinApi.Point point = new WinApi.Point { X = 713, Y = 97 };
        //private WinApi.Point point = new WinApi.Point { X = 703, Y = 97 };
        private WinApi.Rect rect = new WinApi.Rect { Left = 0, Top = 0, Right = width * cellSize + offsetX, Bottom = height * cellSize + offsetY };
        public UniversalMinesweeper() // TODO: constructor from file
        {
            
        }

        public bool FetchState()
        {

            windowScreenShot = new Bitmap(rect.Right - rect.Left, rect.Bottom - rect.Top);
            using var screenGraphics = Graphics.FromImage(windowScreenShot);

            screenGraphics.CopyFromScreen(point.X, point.Y,
                0, 0, new Size(windowScreenShot.Width, windowScreenShot.Height),
                CopyPixelOperation.SourceCopy);
            windowScreenShot.Save("temp.png"); // Debug only
            return true;
        }
        public static void FindPattern(string path) // Not Ready
        {
            //Bitmap saved = null;
            //bool[,] good = null;
            //foreach (string name in System.IO.Directory.GetFiles(path))
            //{
            //Bitmap bitmap = new Bitmap(Image.FromFile(name));
            //if (saved == null)
            //{
            //    saved = bitmap;
            //    good = new bool[bitmap.Width, bitmap.Height];
            //    for (int x = 0; x < bitmap.Width; x++)
            //        for (int y = 0; y < bitmap.Height; y++)
            //            good[x, y] = true;
            //    continue;
            //}
            //for (int x = 0; x < bitmap.Width; x++)
            //    for (int y = 0; y < bitmap.Height; y++)
            //    {
            //if (saved.GetPixel(x, y) != bitmap.GetPixel(x, y))
            //    good[x, y] = false;

            //        }
            //}
            //int ans = 0;
            //for (int x = 0; x < saved.Width; x++)
            //    for (int y = 0; y < saved.Height; y++)
            //    {
            //        if (good[x, y])
            //            ans++;
            //    }
            //Console.WriteLine($"Count: {ans}/{saved.Width * saved.Height}");
            List<HashSet<Color>> list = new List<HashSet<Color>>();
            foreach (string dir in System.IO.Directory.GetDirectories(path))
            {
                HashSet<Color> rColors = null;
                foreach (string filename in System.IO.Directory.GetFiles(dir))
                {
                    Bitmap bitmap = new Bitmap(Image.FromFile(filename));
                    HashSet<Color> colors = new HashSet<Color>();
                    for (int x = 0; x < bitmap.Width; x++)
                        for (int y = 0; y < bitmap.Height; y++)
                            colors.Add(bitmap.GetPixel(x, y));
                    if (rColors == null)
                        rColors = colors;
                    else
                        rColors.IntersectWith(colors);
                }
                list.Add(rColors);
            }
            List<HashSet<Color>> result = new List<HashSet<Color>>();
            for (int i = 0; i < list.Count; i++)
            {
                HashSet<Color> cur = new HashSet<Color>(list[i]);
                Console.WriteLine($"i = {i}, BSize = {cur.Count}");
                for (int j = 0; j < list.Count; j++)
                    if (i != j)
                        cur.ExceptWith(list[j]);
                result.Add(cur);
                Console.WriteLine($"i = {i}, ASize = {cur.Count}");
            }
        }
        private static MinesweeperCell ParseCell(int cellHash) // TODO: Remove
        {
            var cell = new MinesweeperCell();

            switch (cellHash)
            {
                case 560552706:
                case 869293688:
                    cell.State = CellState.Closed;
                    break;
                case -91164671:
                case 969498094:
                    cell.State = CellState.Marked;
                    break;
                case -194541566:
                    cell.State = CellState.QuestionMarked;
                    break;
                case -1208559324:
                case 1608391983:
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
                case 683720862:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 5;
                    break;
                case -1475292224:
                case -1926047138:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 4;
                    break;
                case -1079452576:
                case 1693587486:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 3;
                    break;
                case -1150141824:
                case 1763429158:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 2;
                    break;
                case 607173218:
                case -2143063590:
                case 1999011529:
                case -795896909:
                case -952361618:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 1;
                    break;
                case -2009844800:
                case -140630690:
                case 1446996886:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 0;
                    break;
                default:
                    throw new Exception($"Unknown cell hash: {cellHash}");
            }

            return cell;
        }

        public MinesweeperCell[,] GetField()
        {
            var cells = new MinesweeperCell[width, height];

            var imageData = new byte[sizeof(byte) * 3 * windowScreenShot.Width * windowScreenShot.Height];
            var bitmapData = windowScreenShot.LockBits(new Rectangle(0, 0, windowScreenShot.Width, windowScreenShot.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            Marshal.Copy(bitmapData.Scan0, imageData, 0, imageData.Length);
            windowScreenShot.UnlockBits(bitmapData);

            //var width = cells.GetLength(0);
            //var height = cells.GetLength(1);
            //Bitmap textures = new Bitmap(Image.FromFile("410.bmp"));
            //int texCount = textures.Height / cellSize;
            for (var x = 0; x < width; x++)
                for (var y = 0; y < height; y++)
                {
                    var cellHash = 0;
                    //int[] diffs = new int[texCount];
                    for (var cellX = 0; cellX < cellSize; cellX++)
                        for (var cellY = 0; cellY < cellSize; cellY++)
                        {
                            cellHash = 31 * cellHash + imageData[(offsetX + x * cellSize + cellX + (offsetY + y * cellSize + cellY)
                                                                  * bitmapData.Width) * 3];
                            cellHash = 31 * cellHash + imageData[(offsetX + x * cellSize + cellX + (offsetY + y * cellSize + cellY)
                                                                  * bitmapData.Width) * 3 + 1];
                            cellHash = 31 * cellHash + imageData[(offsetX + x * cellSize + cellX + (offsetY + y * cellSize + cellY)
                                                                  * bitmapData.Width) * 3 + 2];
                            /*for (int i = 0; i < texCount; i++)
                            {
                                Color col1 = Color.FromArgb(255, imageData[(offsetX + x * cellSize + cellX + (offsetY + y * cellSize + cellY) * bitmapData.Width) * 3],
                                                  imageData[(offsetX + x * cellSize + cellX + (offsetY + y * cellSize + cellY) * bitmapData.Width) * 3 + 1],
                                                  imageData[(offsetX + x * cellSize + cellX + (offsetY + y * cellSize + cellY) * bitmapData.Width) * 3 + 2]);
                                Color col2 = textures.GetPixel(cellX, cellY + i * cellSize);
                                if (Math.Abs(col1.R - col2.R) + Math.Abs(col1.G - col2.G)+ Math.Abs(col1.B - col2.B) > 5)
                                {
                                    diffs[i]++;
                                }
                            }*/
                        }
                    /*
                    int best = int.MaxValue, id = 0;
                    if (cellHash == 1693587486 || cellHash == -2143063590)
                        Console.WriteLine($"hash: {cellHash}");
                    for (int i = 0; i < texCount; i++)
                    {
                        if (cellHash == 1693587486 || cellHash == -2143063590)
                            Console.WriteLine($"i = {i}, diff[i] = {diffs[i]}");
                        if (diffs[i] < best)
                        {
                            best = diffs[i];
                            id = i;
                        }
                    }
                    if (!System.IO.File.Exists($"cells2/{cellHash}_{id}.png"))
                        windowScreenShot.Clone(new Rectangle(x * cellSize + offsetX, y * cellSize + offsetY, cellSize, cellSize), System.Drawing.Imaging.PixelFormat.DontCare).Save($"cells2/{cellHash}_{id}.png");*/
                    if (!System.IO.File.Exists($"cells2/{cellHash}.png"))
                        windowScreenShot.Clone(new Rectangle(x * cellSize + offsetX, y * cellSize + offsetY, cellSize, cellSize), System.Drawing.Imaging.PixelFormat.DontCare).Save($"cells2/{cellHash}.png");
                    cells[x, y] = ParseCell(cellHash);
                }
                /*
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    Bitmap cell = windowScreenShot.Clone(new Rectangle(x * cellSize, y * cellSize, cellSize, cellSize), System.Drawing.Imaging.PixelFormat.DontCare);
                    int diff = 0;
                    for (int cellX = 0; cellX < cellSize; cellX++)
                        for (int cellY = 0; cellY < cellSize; cellY++)
                            if ()
                }*/

            return cells;
        }

        public bool IsDead()
        {
            throw new NotImplementedException();
            //return windowScreenShot.GetPixel(windowScreenShot.Width / 2, 24).R == 0;
        }

        public bool IsReady()
        {
            throw new NotImplementedException();
            //return windowScreenShot.GetPixel(windowScreenShot.Width / 2 - 5, 28).R == 0;
        }

        public void Click(int x, int y)
        {
            // TODO: wait while mouse not in rect
            Console.WriteLine($"Click at {x} {y}");
            WinApi.SetCursorPos(point.X + offsetX + x * cellSize + cellSize / 2, point.Y + offsetY + y * cellSize + cellSize / 2);
            inputSimulator.Mouse.LeftButtonClick();
        }

        public void Mark(int x, int y)
        {
            // TODO: wait while mouse not in rect
            Console.WriteLine($"Mark at {x} {y}");
            WinApi.SetCursorPos(point.X + offsetX + x * cellSize + cellSize / 2, point.Y + offsetY + y * cellSize + cellSize / 2);
            inputSimulator.Mouse.RightButtonClick();
        }

        public void Reset()
        {
            throw new NotImplementedException();
            //WinApi.SetCursorPos(point.X + (rect.Right - rect.Left) / 2, point.Y + 28);
            //inputSimulator.Mouse.LeftButtonClick();
        }
    }
}
