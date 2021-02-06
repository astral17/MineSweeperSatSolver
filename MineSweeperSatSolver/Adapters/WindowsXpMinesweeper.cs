using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using WindowsInput;

namespace MineSweeperSatSolver.Adapters
{
    internal class WindowsXpMinesweeper : IMinesweeperAdapter
    {
        private readonly IntPtr windowHandle;

        private readonly InputSimulator inputSimulator = new InputSimulator();

        private const int CellSize = 16;
        private const int OffsetX = 12;
        private const int OffsetY = 55;
        private const int ExtraWidth = OffsetX + 8;
        private const int ExtraHeight = OffsetY + 8;
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
            windowScreenShot = WinApi.CaptureClientRect(windowHandle);
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

                    cells[x, y] = ParseCell(cellHash);
                }

            return cells;
        }

        public bool IsDead()
        {
            if (windowScreenShot.GetPixel(windowScreenShot.Width / 2, 24).R == 0)
                Console.WriteLine("Dead!");
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
            WinApi.SetCursorPos(point.X + OffsetX + x * CellSize + CellSize / 2,
                point.Y + OffsetY + y * CellSize + CellSize / 2);
            inputSimulator.Mouse.LeftButtonClick();
            System.Threading.Thread.Sleep(5);
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
            System.Threading.Thread.Sleep(5);
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
            System.Threading.Thread.Sleep(100);
        }
    }
}
