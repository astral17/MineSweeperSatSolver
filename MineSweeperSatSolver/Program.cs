using System;
using MineSweeperSatSolver.Adapters;
using MineSweeperSatSolver.Solvers;

namespace MineSweeperSatSolver
{
    internal class Program
    {
        //private static readonly Random random = new Random();

        private static void Main(string[] args) // TODO: Handle args
        {
            //UniversalMinesweeper.FindPattern("cells/saved/");
            //UniversalMinesweeper.SaveDefaultConfig("configDef2.txt");
            //return;

            //var game = new WindowsXpMinesweeper();
            var game = new SgTathamMinesweeper();
            //var game = new UniversalMinesweeper("config.txt");
            var solver = new MixedSolver();
            //System.Threading.Thread.Sleep(3000);
            //game.FetchState(); game.GetField(); return;
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

            game.FetchState();
            if (game.IsDead() || game.IsReady())
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

    internal enum CellState // Mb remove unuseful states, save only (Closed, Opened, Marked, Mine, Unknown, ?Question or replace to Closed)
    {
        Closed,
        Opened,
        Marked,
        QuestionMarked,
        Mine,
        BlownMine,
        NoMine,
        Question,
        Unknown,
    }

    internal struct MinesweeperCell
    {        
        public CellState State { get; set; }

        public int MinesAround { get; set; }

        public override string ToString() => $"{nameof(State)}: {State}, {nameof(MinesAround)}: {MinesAround}";
        
        public MinesweeperCell(string s)
        {
            State = CellState.Opened;
            if (!int.TryParse(s, out int mines))
                State = (CellState)Enum.Parse(typeof(CellState), s);
            MinesAround = mines;
        }
    }
}
