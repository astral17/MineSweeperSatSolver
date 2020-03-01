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
