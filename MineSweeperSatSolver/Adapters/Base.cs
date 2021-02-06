using System;

namespace MineSweeperSatSolver.Adapters
{
    internal interface IMinesweeperAdapter
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
}

namespace MineSweeperSatSolver
{
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