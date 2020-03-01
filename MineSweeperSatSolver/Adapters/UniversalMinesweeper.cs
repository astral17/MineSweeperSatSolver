using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Drawing;
using System.Runtime.InteropServices;
using WindowsInput;

namespace MineSweeperSatSolver.Adapters
{
    internal class UniversalMinesweeper : IMinesweeperAdapter // TODO: rename this class
    {
        private readonly InputSimulator inputSimulator = new InputSimulator();

        public Bitmap windowScreenShot = null;
        private int CellSize => configFile.CellSize;
        private int Width => configFile.Width;
        private int Height => configFile.Height;
        private int OffsetX => configFile.OffsetX;
        private int OffsetY => configFile.OffsetY;
        private WinApi.Point Point => configFile.Point;
        private WinApi.Rect Rect => configFile.Rect; // TODO: Extract data
        private WinApi.Rect HashRect => configFile.HashRect;

        /*
        private static int cellSize = 16; // TODO: remove static and load from config file
        private static int width = 30;
        private static int height = 16;

        private static int offsetX = 8; // from left top rect to first pixel of field
        private static int offsetY = 50;

        private WinApi.Point point = new WinApi.Point { X = 660, Y = 259 };
        //private WinApi.Point point = new WinApi.Point { X = 713, Y = 97 };
        //private WinApi.Point point = new WinApi.Point { X = 703, Y = 97 };
        private WinApi.Rect rect = new WinApi.Rect { Left = 0, Top = 0, Right = width * cellSize + offsetX, Bottom = height * cellSize + offsetY };*/
        private struct ConfigFile
        {
            public int CellSize { get; set; }
            public WinApi.Rect HashRect { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int OffsetX { get; set; }
            public int OffsetY { get; set; }
            public WinApi.Point Point { get; set; }
            [JsonIgnore]
            public WinApi.Rect Rect { get; set; }
            public Dictionary<string, List<ulong>> Hashes { get; set; }
        }
        private ConfigFile configFile;
        private readonly string configPath;
        private readonly Dictionary<ulong, MinesweeperCell> hashConverter;
        private static readonly Dictionary<Color, MinesweeperCell> uniqueColor = new Dictionary<Color, MinesweeperCell>();
        public UniversalMinesweeper(string path) // TODO: constructor from file
        {
            configPath = path;
            if (!File.Exists(path))
                SaveDefaultConfig(path);
            configFile = JsonSerializer.Deserialize<ConfigFile>(File.ReadAllText(path));
            configFile.Rect = new WinApi.Rect
            {
                Left = 0,
                Top = 0,
                Right = Width * CellSize + OffsetX,
                Bottom = Height * CellSize + OffsetY,
            };
            hashConverter = new Dictionary<ulong, MinesweeperCell>();
            foreach (var kv in configFile.Hashes)
            {
                MinesweeperCell cell = new MinesweeperCell(kv.Key);
                foreach (var element in kv.Value)
                    hashConverter.Add(element, cell);
            }


            if (!Directory.Exists("cells"))
                Directory.CreateDirectory("cells");
            if (!Directory.Exists("cells/saved"))
                Directory.CreateDirectory("cells/saved");
        }
        public static void SaveDefaultConfig(string path)
        {
            const int cellSize = 16, width = 30, height = 16, offsetX = 8, offsetY = 50;
            List<ulong> emptyList = new List<ulong>();
            File.WriteAllText(path, JsonSerializer.Serialize(new ConfigFile
            {
                CellSize = cellSize,
                HashRect = new WinApi.Rect { Left = 0, Top = 0, Right = cellSize, Bottom = cellSize },
                Width = width,
                Height = height,
                OffsetX = offsetX,
                OffsetY = offsetY,
                Point = new WinApi.Point { X = 660, Y = 259 },
                Rect = new WinApi.Rect { Left = 0, Top = 0, Right = width * cellSize + offsetX, Bottom = height * cellSize + offsetY },
                Hashes = new Dictionary<string, List<ulong>>
                {
                    { "Closed", emptyList },
                    { "Marked", emptyList },
                    { "BlownMine", emptyList },
                    { "NoMine", emptyList },
                    { "Mine", emptyList },
                    { "0", emptyList },
                    { "1", emptyList },
                    { "2", emptyList },
                    { "3", emptyList },
                    { "4", emptyList },
                    { "5", emptyList },
                    { "6", emptyList },
                    { "7", emptyList },
                    { "8", emptyList },
                }
            }, new JsonSerializerOptions { WriteIndented = true }));
        }

        public bool FetchState()
        {
            System.Threading.Thread.Sleep(50);
            windowScreenShot = new Bitmap(Rect.Right - Rect.Left, Rect.Bottom - Rect.Top);
            using var screenGraphics = Graphics.FromImage(windowScreenShot);

            screenGraphics.CopyFromScreen(Point.X, Point.Y,
                0, 0, new Size(windowScreenShot.Width, windowScreenShot.Height),
                CopyPixelOperation.SourceCopy);
            File.Move("temp.png", "prev.png", true);
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
            List<string> names = new List<string>();
            foreach (string dir in Directory.GetDirectories(path))
            {
                Console.WriteLine($"Directory: {Path.GetFileName(dir)}");
                names.Add(Path.GetFileName(dir));
                HashSet<Color> rColors = null;
                foreach (string filename in Directory.GetFiles(dir))
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
                    //rColors.UnionWith(colors);
                }
                list.Add(rColors);
            }
            List<HashSet<Color>> result = new List<HashSet<Color>>();
            for (int i = 0; i < list.Count; i++)
            {
                HashSet<Color> cur = new HashSet<Color>(list[i]);
                Console.WriteLine($"name: {names[i]}, BSize = {cur.Count}");
                //if (cur.Count > 0)
                //    foreach (Color color in cur)
                //        Console.WriteLine($"BEFcell: {names[i]} color: {color}");
                for (int j = i + 1; j < list.Count; j++)
                //if (i != j)
                {
                    cur.ExceptWith(list[j]);
                    list[j].ExceptWith(list[i]);
                }
                result.Add(cur);
                Console.WriteLine($"name: {names[i]}, ASize = {cur.Count}");
                if (cur.Count > 0)
                {
                    foreach (Color color in cur)
                    {
                        uniqueColor.Add(color, new MinesweeperCell(names[i]));
                        //Console.WriteLine($"cell: {names[i]} color: {color}");
                    }
                }

            }
        }
        private MinesweeperCell ParseCell(ulong cellHash)
        {
            return hashConverter[cellHash];
        }

        public MinesweeperCell[,] GetField()
        {
            var cells = new MinesweeperCell[Width, Height];

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
            for (var x = 0; x < Width; x++)
                for (var y = 0; y < Height; y++)
                {
                    ulong cellHash = 0;
                    //int[] diffs = new int[texCount];
                    //for (var cellX = 0; cellX < CellSize; cellX++)
                    //    for (var cellY = 0; cellY < CellSize; cellY++)
                    bool saved = false;
                    MinesweeperCell found = new MinesweeperCell();
                    for (var cellX = HashRect.Left; cellX < HashRect.Right; cellX++)
                        for (var cellY = HashRect.Top; cellY < HashRect.Bottom; cellY++)
                        {
                            cellHash = 257 * cellHash + imageData[(OffsetX + x * CellSize + cellX + (OffsetY + y * CellSize + cellY)
                                                                  * bitmapData.Width) * 3];
                            cellHash = 257 * cellHash + imageData[(OffsetX + x * CellSize + cellX + (OffsetY + y * CellSize + cellY)
                                                                  * bitmapData.Width) * 3 + 1];
                            cellHash = 257 * cellHash + imageData[(OffsetX + x * CellSize + cellX + (OffsetY + y * CellSize + cellY)
                                                                  * bitmapData.Width) * 3 + 2];

                            //Color color = Color.FromArgb(255, imageData[(OffsetX + x * CellSize + cellX + (OffsetY + y * CellSize + cellY) * bitmapData.Width) * 3 + 2],
                            //                      imageData[(OffsetX + x * CellSize + cellX + (OffsetY + y * CellSize + cellY) * bitmapData.Width) * 3 + 1],
                            //                      imageData[(OffsetX + x * CellSize + cellX + (OffsetY + y * CellSize + cellY) * bitmapData.Width) * 3]);
                            //Color color = windowScreenShot.GetPixel(OffsetX + x * CellSize + cellX, OffsetY + y * CellSize + cellY);
                            //if (!saved && uniqueColor.ContainsKey(color))
                            //{
                            //    saved = true;
                            //    found = uniqueColor[color];
                            //}

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
                    if (saved)
                    {
                        string name = found.State == CellState.Opened ? found.MinesAround.ToString() : found.State.ToString();
                        if (!Directory.Exists($"cells/predict/"))
                            Directory.CreateDirectory($"cells/predict/");
                        if (!Directory.Exists($"cells/predict/{name}"))
                            Directory.CreateDirectory($"cells/predict/{name}");
                        windowScreenShot.Clone(new Rectangle(x * CellSize + OffsetX + HashRect.Left, y * CellSize + OffsetY + HashRect.Top,
                            HashRect.Right - HashRect.Left, HashRect.Bottom - HashRect.Top),
                            System.Drawing.Imaging.PixelFormat.DontCare).Save($"cells/predict/{name}/{cellHash}.png");
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
                    if (!File.Exists($"cells2/{cellHash}_{id}.png"))
                        windowScreenShot.Clone(new Rectangle(x * cellSize + offsetX, y * cellSize + offsetY, cellSize, cellSize), System.Drawing.Imaging.PixelFormat.DontCare).Save($"cells2/{cellHash}_{id}.png");*/
                    //if (x == 1 && y == 2)
                    //    windowScreenShot.Clone(new Rectangle(x * CellSize + OffsetX + HashRect.Left, y * CellSize + OffsetY + HashRect.Top,
                    //        HashRect.Right - HashRect.Left, HashRect.Bottom - HashRect.Top),
                    //        System.Drawing.Imaging.PixelFormat.DontCare).Save($"cells/{cellHash}_WTF.png");
                    if (!hashConverter.ContainsKey(cellHash))// && !File.Exists($"cells2/{cellHash}.png"))
                    {
                        //windowScreenShot.Clone(new Rectangle(x * CellSize + OffsetX, y * CellSize + OffsetY, CellSize, CellSize), System.Drawing.Imaging.PixelFormat.DontCare).Save($"cells/{cellHash}.png");
                        windowScreenShot.Clone(new Rectangle(x * CellSize + OffsetX + HashRect.Left, y * CellSize + OffsetY + HashRect.Top,
                            HashRect.Right - HashRect.Left, HashRect.Bottom - HashRect.Top),
                            System.Drawing.Imaging.PixelFormat.DontCare).Save($"cells/{cellHash}.png");
                        Console.Write($"Unknown hash: {cellHash}, Enter his value: "); // TODO: Normal handling this case
                        //string cell = "Closed";
                        string cell = Console.ReadLine();
                        hashConverter.Add(cellHash, new MinesweeperCell(cell));
                        if (!configFile.Hashes.ContainsKey(cell))
                            configFile.Hashes.Add(cell, new List<ulong>());
                        configFile.Hashes[cell].Add(cellHash);
                        File.WriteAllText(configPath, JsonSerializer.Serialize(configFile, new JsonSerializerOptions { WriteIndented = true }));
                        if (!Directory.Exists($"cells/saved/{cell}"))
                            Directory.CreateDirectory($"cells/saved/{cell}");
                        File.Move($"cells/{cellHash}.png", $"cells/saved/{cell}/{cellHash}.png", true);
                    }
                    cells[x, y] = ParseCell(cellHash);
                    if (cells[x, y].State == CellState.Mine)
                        return cells;
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
            var field = GetField();
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (field[x, y].State == CellState.BlownMine || field[x, y].State == CellState.NoMine || field[x, y].State == CellState.Mine)
                        return true;
            return false;

            //throw new NotImplementedException();
            //return windowScreenShot.GetPixel(windowScreenShot.Width / 2, 24).R == 0;
        }

        public bool IsReady()
        {
            var field = GetField();
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (field[x, y].State == CellState.Closed)
                        return false;
            return true;

            //throw new NotImplementedException();
            //return windowScreenShot.GetPixel(windowScreenShot.Width / 2 - 5, 28).R == 0;
        }

        public void Click(int x, int y)
        {
            // TODO: wait while mouse not in rect
            Console.WriteLine($"Click at {x} {y}");
            WinApi.SetCursorPos(Point.X + OffsetX + x * CellSize + CellSize / 2, Point.Y + OffsetY + y * CellSize + CellSize / 2);
            inputSimulator.Mouse.LeftButtonClick();
            System.Threading.Thread.Sleep(5);
        }

        public void Mark(int x, int y)
        {
            // TODO: wait while mouse not in rect
            Console.WriteLine($"Mark at {x} {y}");
            WinApi.SetCursorPos(Point.X + OffsetX + x * CellSize + CellSize / 2, Point.Y + OffsetY + y * CellSize + CellSize / 2);
            inputSimulator.Mouse.RightButtonClick();
            System.Threading.Thread.Sleep(5);
        }

        public void Reset()
        {
            throw new NotImplementedException();
            //WinApi.SetCursorPos(point.X + (rect.Right - rect.Left) / 2, point.Y + 28);
            //inputSimulator.Mouse.LeftButtonClick();
        }
    }
}
