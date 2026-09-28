using System.Text;

namespace GameOfLife.Engine;

public record Configuration(int Rows, int Cols);

public enum State
{
    Dead = 0,
    Alive = 1
}

public enum NeighbourLocation
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Right,
    BotLeft,
    Bot,
    BotRight
}

public interface INeighbourLocationCalculator
{
    (int row, int col) Calculate(NeighbourLocation location, int row, int col);
}

public class NeighbourLocationCalculator(Configuration configuration) : INeighbourLocationCalculator
{
    private readonly Configuration _configuration = configuration;
    private readonly Dictionary<NeighbourLocation, (int rowAdd, int colAdd)> _locationsAdds = new() {
        { NeighbourLocation.TopLeft, (-1, -1) },
        { NeighbourLocation.Top, (-1, 0) },
        { NeighbourLocation.TopRight, (-1, 1) },
        { NeighbourLocation.Left, (0, -1) },
        { NeighbourLocation.Right, (0, 1) },
        { NeighbourLocation.BotLeft, (1, -1) },
        { NeighbourLocation.Bot, (1, 0) },
        { NeighbourLocation.BotRight, (1, 1) }
    };

    public (int row, int col) Calculate(NeighbourLocation location, int row, int col)
    {
        var (addRow, addCol) = _locationsAdds[location];
        return (RowEdgeCase(row + addRow), ColEdgeCase(col + addCol));
    }

    private int RowEdgeCase(int row)
    {
        // 1. row > Height
        if (row >= _configuration.Rows)
        {
            return 0;
        }
        // 2. row < Height
        if (row < 0)
        {
            return _configuration.Rows - 1;
        }

        return row;
    }

    private int ColEdgeCase(int col)
    {
        // 1. col > Width
        if (col >= _configuration.Cols)
        {
            return 0;
        }
        // 2. col < Width
        if (col < 0)
        {
            return _configuration.Cols - 1;
        }

        return col;
    }
}

public class Cell(int row, int col, State initialState)
{
    public int Row { get; private set; } = row;
    public int Col { get; private set; } = col;
    public State CellState { get; private set; } = initialState;

    public void BeBorn()
    {
        CellState = State.Alive;
    }

    public void Die()
    {
        CellState = State.Dead;
    }

    public char AsChar()
    {
        if (CellState == State.Alive)
        {
            return 'O';
        }

        return '.';
    }
}

public interface IBoard
{
    Cell[,] Cells { get; }
    void InitializeEmptyBoard();
    void InitializeFromFile(string fileName);
    Cell GetCellAtFromBuffer(int row, int col);
    void Bufferize();
}

public class Board : IBoard
{
    private readonly Cell[,] _cells;
    private readonly Cell[,] _buffer;
    private readonly Configuration _configuration;

    public Cell[,] Cells { get => _cells; }

    public Board(Configuration configuration)
    {
        _configuration = configuration;
        _cells = new Cell[_configuration.Rows, _configuration.Cols];
        _buffer = new Cell[_configuration.Rows, _configuration.Cols];
    }

    public void InitializeEmptyBoard()
    {
        int rows = _cells.GetLength(0);
        int cols = _cells.GetLength(1);
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                _cells[i, j] = new Cell(i, j, State.Dead);
            }
        }

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                _buffer[i, j] = new Cell(i, j, State.Dead);
            }
        }
    }

    public void InitializeFromFile(string fileName)
    {
        InitializeEmptyBoard();

        if (!File.Exists(fileName))
        {
            throw new FileNotFoundException("File not found", fileName);
        }

        int row = 0;

        foreach (var line in File.ReadAllLines(fileName))
        {
            if (line.StartsWith('!'))
            {
                continue;
            }

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == 'O')
                {
                    _cells[row, i].BeBorn();
                }
                else
                {
                    _cells[row, i].Die();
                }
            }

            row++;
        }

        Bufferize();
    }

    public Cell GetCellAtFromBuffer(int row, int col)
    {
        return _buffer[row, col];
    }

    public void Bufferize()
    {
        int rows = _cells.GetLength(0);
        int cols = _cells.GetLength(1);

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                if (_cells[i, j].CellState == State.Alive)
                {
                    _buffer[i, j].BeBorn();
                }
                else
                {
                    _buffer[i, j].Die();
                }
            }
        }
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        int rows = _cells.GetLength(0);
        int cols = _cells.GetLength(1);

        for (int i = 0; i < rows; i++)
        {
            char[] line = new char[cols];
            for (int j = 0; j < cols; j++)
            {
                line[j] = _cells[i, j].AsChar();
            }

            sb.AppendLine(new string(line));
        }

        return sb.ToString();
    }
}

public interface IGame
{
    bool IsGameInitialized { get; }
    void InitializeGame();
    void InitializeGame(string fileName);
    void Iterate();
}

public class Game(IBoard board, INeighbourLocationCalculator neighbourLocationCalculator): IGame
{
    private readonly IBoard _board = board;
    private readonly INeighbourLocationCalculator _neighbourLocationCalculator = neighbourLocationCalculator;

    public bool IsGameInitialized { get; private set; } = false;

    public void InitializeGame()
    {
        _board.InitializeEmptyBoard();
        IsGameInitialized = true;
    }

    public void InitializeGame(string fileName)
    {
        _board.InitializeFromFile(fileName);
        IsGameInitialized = true;
    }

    public void Iterate()
    {
        if (!IsGameInitialized)
        {
            throw new InvalidOperationException("Game is not initialized");
        }

        var cells = _board.Cells;
        int rows = cells.GetLength(0);
        int cols = cells.GetLength(1);

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                IterateCell(cells[i, j]);
            }
        }

        _board.Bufferize();
    }

    private void IterateCell(Cell cell)
    {
        var aliveNeighboursCount = GetNeighboursOfCell(cell).Count(n => n.CellState == State.Alive);

        if (cell.CellState == State.Dead)
        {
            if (aliveNeighboursCount == 3)
            {
                cell.BeBorn();
            }
        }
        else if (cell.CellState == State.Alive)
        {
            if (aliveNeighboursCount < 2 || aliveNeighboursCount > 3)
            {
                cell.Die();
            }
        }
    }

    private List<Cell> GetNeighboursOfCell(Cell cell)
    {
        var neighbours = new List<Cell>();

        // Get TOP LEFT
        AddNeighbour(NeighbourLocation.TopLeft, cell, neighbours);
        // Get TOP
        AddNeighbour(NeighbourLocation.Top, cell, neighbours);
        // Get TOP RIGHT
        AddNeighbour(NeighbourLocation.TopRight, cell, neighbours);
        // GET LEFT
        AddNeighbour(NeighbourLocation.Left, cell, neighbours);
        // GET RIGHT
        AddNeighbour(NeighbourLocation.Right, cell, neighbours);
        // GET BOT LEFT
        AddNeighbour(NeighbourLocation.BotLeft, cell, neighbours);
        // GET BOT
        AddNeighbour(NeighbourLocation.Bot, cell, neighbours);
        // GET BOT RIGHT
        AddNeighbour(NeighbourLocation.BotRight, cell, neighbours);

        return neighbours;
    }

    private void AddNeighbour(NeighbourLocation location, Cell cell, List<Cell> neighbours)
    {
        var (nRow, nCol) = _neighbourLocationCalculator.Calculate(location, cell.Row, cell.Col);
        neighbours.Add(_board.GetCellAtFromBuffer(nRow, nCol));
    }
}
