using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using WindowsInput;

namespace MineSweeperSatSolver.Adapters
{

    internal class SgTathamMinesweeper : IMinesweeperAdapter // https://www.chiark.greenend.org.uk/~sgtatham/puzzles/js/mines.html .exe version
    {
        private readonly IntPtr windowHandle; // TODO: Mb add WindowsMinesweeper parent class

        private readonly InputSimulator inputSimulator = new InputSimulator();

        private const int CellSize = 20;
        private const int OffsetX = 30;
        private const int OffsetY = 30;
        private const int ExtraWidth = OffsetX + 30;
        private const int ExtraHeight = OffsetY + 52;
        public Bitmap windowScreenShot = null;

        public SgTathamMinesweeper()
        {
            var foundWindowHandle = IntPtr.Zero;

            foreach (var process in Process.GetProcesses())
            {
                if (process.MainWindowTitle == "Mines")
                {
                    if (foundWindowHandle != IntPtr.Zero)
                        throw new Exception("Multiple windows with minesweeper was found");
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
            //windowScreenShot.Save("temp.png"); // Debug only
            return true;
        }

        private static MinesweeperCell ParseCell(int cellHash)
        {
            var cell = new MinesweeperCell();

            switch (cellHash)
            {
                case -1353029886:
                    cell.State = CellState.Closed;
                    break;
                case 2003012210:
                    cell.State = CellState.Marked;
                    break;
                case -1838076266:
                    cell.State = CellState.BlownMine;
                    break;
                //case -1668850496:
                //    cell.State = CellState.Mine;
                //    break;
                case -892136958:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 8;
                    break;
                case 1923235105:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 7;
                    break;
                case -1480603107:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 6;
                    break;
                case 1024099743:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 5;
                    break;
                case 1927950246:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 4;
                    break;
                case 71982584:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 3;
                    break;
                case 1659313554:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 2;
                    break;
                case -277149828:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 1;
                    break;
                case 355102988:
                    cell.State = CellState.Opened;
                    cell.MinesAround = 0;
                    break;
                default:
                    cell.State = CellState.Unknown;
                    break;
                    //throw new Exception($"Unknown cell hash: {cellHash}");
            }

            return cell;
        }

        public MinesweeperCell[,] GetField()
        {
            var cells = new MinesweeperCell[(windowScreenShot.Width - ExtraWidth) / CellSize,
                (windowScreenShot.Height - ExtraHeight) / CellSize];

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
                    for (var cellX = 0; cellX < CellSize; cellX++)
                        for (var cellY = 0; cellY < CellSize; cellY++)
                        {
                            cellHash = 31 * cellHash + imageData[(OffsetX + x * CellSize + cellX + (OffsetY + y * CellSize + cellY)
                                                                  * bitmapData.Width) * 3];
                            cellHash = 31 * cellHash + imageData[(OffsetX + x * CellSize + cellX + (OffsetY + y * CellSize + cellY)
                                                                  * bitmapData.Width) * 3 + 1];
                            cellHash = 31 * cellHash + imageData[(OffsetX + x * CellSize + cellX + (OffsetY + y * CellSize + cellY)
                                                                  * bitmapData.Width) * 3 + 2];
                        }
                    //if (!Directory.Exists($"cells_tmp/"))
                    //    Directory.CreateDirectory($"cells_tmp");
                    //windowScreenShot.Clone(new Rectangle(x * CellSize + OffsetX, y * CellSize + OffsetY, CellSize, CellSize),
                    //        System.Drawing.Imaging.PixelFormat.DontCare).Save($"cells_tmp/{cellHash}.png");
                    cells[x, y] = ParseCell(cellHash);
                }

            return cells;
        }

        public bool IsDead()
        {
            var field = GetField();
            var width = field.GetLength(0);
            var height = field.GetLength(1);
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (field[x, y].State == CellState.BlownMine || field[x, y].State == CellState.NoMine || field[x, y].State == CellState.Mine)
                    {
                        Console.WriteLine("Dead!");
                        return true;
                    }
            return false;
        }

        public bool IsReady()
        {
            var field = GetField();
            var width = field.GetLength(0);
            var height = field.GetLength(1);
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (field[x, y].State == CellState.Closed)
                        return false;
            return true;
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
            WinApi.SetCursorPos(point.X + OffsetX + x * CellSize + CellSize / 2,
                point.Y + OffsetY + y * CellSize + CellSize / 2);
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
            WinApi.SetCursorPos(point.X + OffsetX + x * CellSize + CellSize / 2,
                point.Y + OffsetY + y * CellSize + CellSize / 2);
            inputSimulator.Mouse.RightButtonClick();
        }

        public void Reset()
        {
            //throw new NotImplementedException();
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
            WinApi.SetCursorPos(point.X + 15, point.Y - 5);
            inputSimulator.Mouse.LeftButtonClick();
            System.Threading.Thread.Sleep(10);
            WinApi.SetCursorPos(point.X + 15, point.Y + 5);
            inputSimulator.Mouse.LeftButtonClick();
            System.Threading.Thread.Sleep(200);
            // Solve Button
            //System.Threading.Thread.Sleep(50);
            //WinApi.SetCursorPos(point.X + 35, point.Y + 35);
            //inputSimulator.Mouse.LeftButtonClick();
            //System.Threading.Thread.Sleep(50);
            //WinApi.SetCursorPos(point.X + 15, point.Y - 5);
            //inputSimulator.Mouse.LeftButtonClick();
            //System.Threading.Thread.Sleep(50);
            //WinApi.SetCursorPos(point.X + 15, point.Y + 250);
            //inputSimulator.Mouse.LeftButtonClick();
            //System.Threading.Thread.Sleep(200);
        }
    }
}
