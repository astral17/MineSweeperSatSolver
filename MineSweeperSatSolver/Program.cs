using System;
using System.Threading;
using CommandLine;
using MineSweeperSatSolver.Adapters;
using MineSweeperSatSolver.Solvers;

namespace MineSweeperSatSolver
{
    internal class Program
    {
        //private static readonly Random random = new Random();
        public enum GameName
        {
            Universal = 0,
            XP = 1,
            SG = 2,
        }
        public enum SolverName
        {
            MixSolver = 0,
            GroupSolver = 1,
            SatSolver = 2,
        }
        public class Options
        {
            //--first-wait NUM; --reset-on-fail; --step-delay NUM; --reset-delay NUM
            [Option('g', "game", Required = true, HelpText = "Set work minesweeper.")]
            public GameName Game { get; set; }

            [Option('s', "solver", Default = SolverName.MixSolver, Required = false, HelpText = "Set solver.")]
            public SolverName Solver { get; set; }

            //[Option('r', "reset-on-fail", Default = true, Required = false, HelpText = "Should solver restart on fail.")]
            //public bool ResetOnFail { get; set; }

            [Option('a', "attempts", Default = -1, Required = false, HelpText = "How much attempts solver should restart when fail. -1 is infinity.")]
            public int Attempts { get; set; }

            [Option('d', "step-delay", Default = 0, Required = false, HelpText = "How much solver should wait until next scan.")]
            public int StepDelay { get; set; }

            [Option('e', "reset-delay", Default = 10, Required = false, HelpText = "How much solver should wait after pressed restart button.")]
            public int ResetDelay { get; set; }

            [Option('f', "start-delay", Default = 0, Required = false, HelpText = "How much solver should wait after first move.")]
            public int StartDelay { get; set; }
        }

        [Verb("solve", HelpText = "Run Until Solve")]
        public class SolveOptions : Options
        {
            public int Handle()
            {
                Console.WriteLine("game: {0}, solver: {1}, attempts: {2}, stepDelay: {3}", Game, Solver, Attempts, StepDelay);
                IMinesweeperAdapter game = Game switch
                {
                    GameName.XP => new WindowsXpMinesweeper(),
                    GameName.SG => new SgTathamMinesweeper(),
                    _ => new UniversalMinesweeper("config.txt"),
                };
                IMinesweeperSolver solver = Solver switch
                {
                    SolverName.GroupSolver => new GroupSolver(),
                    SolverName.SatSolver => new SatSolver(),
                    _ => new MixedSolver(),
                };
                //var game = new UniversalMinesweeper("config.txt");
                //UniversalMinesweeper.FindPattern("cells_TTT2/saved/"); return 0;
                //game.FetchState();
                //if (ResetOnFail && (game.IsDead() || game.IsReady()))
                //    game.Reset();

                while (true)
                {
                    while (game.FetchState() && !game.IsDead() && !game.IsReady())
                    {
                        if (!solver.Solve(game))
                        {
                            Thread.Sleep(2500);
                            throw new Exception("no mines was clicked");
                        }
                        Thread.Sleep(StepDelay);
                    }

                    if (Attempts == 0 || game.IsReady())
                        break;
                    Attempts--;
                    game.Reset();
                }
                return 0;
            }
        }
        [Verb("support", HelpText = "Open only 100% cells")]
        public class SupportOptions : Options
        {
            public int Handle()
            {
                return 1;
            }
        }
        [Verb("hint", HelpText = "Say, is solvable. Also could tell cells, which should be opened (and why?)")]
        public class HintOptions : Options
        {
            public int Handle()
            {
                return 1;
            }
        }
        private static int Main(string[] args) // TODO: Handle args
        {
            //WinApi.CaptureClientRect(IntPtr.Zero).Save("tmp.png"); return 0;
            return Parser.Default.ParseArguments<SolveOptions, SupportOptions, HintOptions>(args)
               .MapResult(
                   (SolveOptions opts) => opts.Handle(),
                   (SupportOptions opts) => opts.Handle(),
                   (HintOptions opts) => opts.Handle(),
                   errs => 1);
            // --game "ID"; --solver "ID"; --handler "ID"; --safe-mode; --first-wait NUM; --reset-on-fail; --step-delay NUM; --reset-delay NUM; solve, support
            string gameID = "WinXP";
            string solverID = "Sat";
            string handlerID = "Helper";
            bool firstWait = false;
            bool resetOnFail = true;
            int stepDelay = 5;
            //UniversalMinesweeper.SaveDefaultConfig("configDef2.txt");

            //var game = new WindowsXpMinesweeper();
            //var game = new SgTathamMinesweeper();
            //Thread.Sleep(1000);
            var game = new UniversalMinesweeper("config.txt");
            //UniversalMinesweeper.FindPattern("cells_TTT2/saved/"); return 0;
            var solver = new MixedSolver(true);
            //var solver = new SatSolver(true);
            //System.Threading.Thread.Sleep(3000);
            //game.FetchState(); game.GetField(); return 0;
            //while (game.FetchState() && !game.IsReady() && !game.IsDead())
            //{
            //var field = game.GetField();
            //for (var y = 0; y < field.GetLength(1); y++)
            //{
            //    for (var x = 0; x < field.GetLength(0); x++)
            //        Console.Write((field[x, y].State == CellState.Opened ? field[x, y].MinesAround.ToString() : field[x, y].State.ToString()).PadLeft(7));
            //    Console.WriteLine();
            //}
            //    solver.Solve(game);
            //}
            //return;

            //while (true)
            //{
            //    game.Reset();
            //    game.FetchState();
            //    game.GetField();
            //}

            game.FetchState();
            //if (game.IsDead() || game.IsReady())
            //    game.Reset();

            while (true)
            {
                while (game.FetchState() && !game.IsDead() && !game.IsReady())
                {
                    if (!solver.Solve(game))
                    {
                        Thread.Sleep(2500);
                        //throw new Exception("no mines was clicked");
                    }
                }

                if (game.IsReady())
                    Thread.Sleep(2500);
                //break;

                //game.Reset();
            }
            return 0;
        }
    }
}
