using System;
using System.Collections.Generic;
using System.Text;
using ToSParser;

namespace ToSTextClient
{
    class TextUI
    {
        protected TextClient game;
        protected readonly object drawLock;

        protected View mainView;
        protected Stack<View> sideViews;
        protected Dictionary<View, Stack<View>> hiddenSideViews;
        protected int mainWidth;
        protected int sideWidth;
        protected int textHeight;
        protected int cursorTop;

        protected StringBuilder inputBuffer;
        protected int bufferIndex;

        public TextView HomeView { get; protected set; }
        public ListView<GameModeID> GameModeView { get; protected set; }
        public TextView GameView { get; protected set; }
        public ListView<PlayerState> PlayerListView { get; protected set; }
        public ListView<RoleID> RoleListView { get; protected set; }
        public ListView<PlayerState> GraveyardView { get; protected set; }
        public ListView<PlayerState> TeamView { get; protected set; }
        public TextView LastWillView { get; protected set; }
        public TextView InfoView { get; protected set; }

        public TextUI(TextClient game)
        {
            this.game = game;
            drawLock = new object();

            HomeView = new TextView(60, 2);
            GameModeView = new ListView<GameModeID>(" # Game Modes", () => game.ActiveGameModes, gm => gm.ToString().ToDisplayName(), 25, 1);
            GameView = new TextView(60, 20);
            PlayerListView = new ListView<PlayerState>(" # Players", () => game.GameState.Players, p => game.GameState.ToName(p.Self), 25, 1);
            RoleListView = new ListView<RoleID>(" # Role List", () => game.GameState.Roles, r => r.ToString().ToDisplayName(), 25, 1);
            GraveyardView = new ListView<PlayerState>(" # Graveyard", () => game.GameState.Graveyard, ps => game.GameState.ToName(ps), 40, 1);
            TeamView = new ListView<PlayerState>(" # Team", () => game.GameState.Team, ps => game.GameState.ToName(ps), 40, 1);
            LastWillView = new TextView(40, 20);
            InfoView = new TextView(25, 1);

            mainView = HomeView;
            sideViews = new Stack<View>();
            sideViews.Push(GameModeView);
            Stack<View> gameSideViews = new Stack<View>();
            gameSideViews.Push(RoleListView);
            gameSideViews.Push(PlayerListView);
            hiddenSideViews = new Dictionary<View, Stack<View>>();
            hiddenSideViews.Add(mainView, sideViews);
            hiddenSideViews.Add(GameView, gameSideViews);
            inputBuffer = new StringBuilder();
        }

        public void Run()
        {
            int width = 0;
            int height = 0;
            while (true)
            {
                if (width != Console.BufferWidth || height != Console.WindowHeight)
                {
                    RedrawAll();
                    width = Console.BufferWidth;
                    height = Console.WindowHeight;
                }
                string input = ReadUserInput();
            }
        }

        public void SelectMainView(View view)
        {
            lock (drawLock)
            {
                if (mainView == view) return;
                mainView = view;
                sideViews = hiddenSideViews.SafeIndex(view, () => new Stack<View>());
                RedrawAll();
            }
        }

        public void OpenSideView(View view)
        {
            lock (drawLock)
            {
                if (sideViews.Contains(view))
                {
                    RedrawSideView(view);
                    return;
                }
                sideViews.Push(view);
                RedrawSideViews();
            }
        }

        public void AppendLine(string text)
        {
            lock (drawLock)
            {
                int lineIndex = GameView.Lines.Count;
                GameView.Lines.Add(text);
                if (GameView != mainView) return;
                Console.CursorTop = lineIndex;
                Console.CursorLeft = 0;
                GameView.Draw(mainWidth, lineIndex, 0);
                textHeight++;
                int newCursorTop = textHeight < Console.WindowHeight ? Console.WindowHeight - 1 : textHeight;
                if (newCursorTop != cursorTop)
                {
                    RedrawSideViews();
                    Console.CursorTop = cursorTop = newCursorTop;
                    Console.CursorLeft = 0;
                    Console.Write("> ");
                    Console.Write(inputBuffer.ToString());
                }
                else
                {
                    Console.CursorTop = cursorTop;
                    Console.CursorLeft = bufferIndex + 2;
                }
            }
        }

        public void AppendLine(string text, params object[] args) => AppendLine(string.Format(text, args));

        public void RedrawView(View view, int? consoleWidth = null)
        {
            lock (drawLock)
            {
                if (view == mainView) RedrawMainView(consoleWidth);
                else if (sideViews.Contains(view)) RedrawSideView(view);
            }
        }

        public void RedrawMainView(int? consoleWidth = null)
        {
            lock (drawLock)
            {
                if (mainWidth < mainView.GetMinimumWidth())
                {
                    RedrawAll();
                    return;
                }
                Console.CursorTop = 0;
                Console.CursorLeft = 0;
                int newTextHeight = mainView.Draw(mainWidth);
                if (textHeight != newTextHeight)
                {
                    textHeight = newTextHeight;
                    int newCursorTop = textHeight < Console.WindowHeight ? Console.WindowHeight - 1 : textHeight;
                    if (newCursorTop != cursorTop)
                    {
                        RedrawSideViews(consoleWidth);
                        Console.CursorTop = cursorTop = newCursorTop;
                        Console.CursorLeft = 0;
                        Console.Write("> ");
                        Console.Write(inputBuffer.ToString());
                    }
                }
                Console.CursorTop = cursorTop;
                Console.CursorLeft = bufferIndex + 2;
            }
        }

        public void RedrawSideView(View view)
        {
            lock (drawLock)
            {
                switch (view.Redraw(sideWidth))
                {
                    case RedrawResult.WIDTH_CHANGED:
                        RedrawAll();
                        return;
                    case RedrawResult.HEIGHT_CHANGED:
                        RedrawSideViews();
                        return;
                }
                Console.CursorTop = cursorTop;
                Console.CursorLeft = bufferIndex + 2;
            }
        }

        public void RedrawSideViews(int? consoleWidth = null)
        {
            lock (drawLock)
            {
                int sideHeight = 0, lastSideHeight = 0;
                int cw = consoleWidth ?? Console.BufferWidth - 1;
                if (sideViews.Count > 0)
                {
                    foreach (View view in sideViews)
                    {
                        Console.CursorLeft = cw - sideWidth;
                        if (lastSideHeight != 0)
                        {
                            Console.CursorTop = cursorTop - sideHeight - 1;
                            Console.Write("".PadRight(sideWidth, '-'));
                            Console.CursorLeft = cw - sideWidth;
                            sideHeight++;
                        }
                        lock (view)
                        {
                            lastSideHeight = view.GetFullHeight();
                            if (lastSideHeight == 0) continue;
                            int lineIndex = Math.Max(0, lastSideHeight - (Console.CursorTop = Math.Max(0, cursorTop - sideHeight - lastSideHeight)));
                            sideHeight += lastSideHeight = view.Draw(sideWidth, lineIndex);
                        }
                        if (sideHeight >= cursorTop)
                        {
                            sideHeight = cursorTop;
                            break;
                        }
                    }
                    for (int currentLine = cursorTop - sideHeight; currentLine < cursorTop; currentLine++)
                    {
                        Console.CursorTop = currentLine;
                        Console.CursorLeft = cw - 1;
                        Console.WriteLine();
                        if (currentLine < textHeight)
                        {
                            Console.CursorLeft = mainWidth;
                            Console.Write('|');
                        }
                    }
                }
                Console.CursorTop = cursorTop;
                Console.CursorLeft = bufferIndex + 2;
            }
        }

        public void RedrawAll()
        {
            lock (drawLock)
            {
                Console.Clear();
                sideWidth = 0;
                foreach (View view in sideViews) sideWidth = Math.Max(sideWidth, view.GetMinimumWidth());
                int minimumWidth = sideWidth + mainView.GetMinimumWidth() + 1;
                int consoleWidth = Console.BufferWidth - 1;
                if (consoleWidth < minimumWidth) Console.BufferWidth = (consoleWidth = minimumWidth) + 1;
                textHeight = mainView.Draw(mainWidth = consoleWidth - sideWidth - 1);
                Console.CursorTop = cursorTop = textHeight < Console.WindowHeight ? Console.WindowHeight - 1 : textHeight;
                Console.CursorLeft = 0;
                Console.Write("> ");
                Console.Write(inputBuffer.ToString());
                RedrawSideViews(consoleWidth);
            }
        }

        protected void DrawHome()
        {
            WriteLineFullWidth(string.Format("{0} ({1} TP, {2} MP)", game.Username, game.TownPoints, game.MeritPoints));
            if (game.ActiveGameModes != null)
            {
                WriteLineFullWidth("Active game modes:");
                foreach (GameModeID gameMode in game.ActiveGameModes) WriteLineFullWidth(string.Format("    {0}", gameMode));
            }
        }

        protected void WriteLineFullWidth(string line)
        {
            if (Console.BufferWidth <= line.Length) Console.BufferWidth = line.Length + 1;
            Console.WriteLine(line.PadRight(Console.BufferWidth - 1));
        }

        protected string ReadUserInput()
        {
            lock (drawLock)
            {
                Console.CursorTop = cursorTop = textHeight < Console.WindowHeight ? Console.WindowHeight - 1 : textHeight;
                Console.CursorLeft = 0;
                Console.Write("> ".PadRight(inputBuffer.Length + 2));
                Console.CursorLeft = 2;
                inputBuffer.Clear();
            }
            ConsoleKeyInfo key;
            bufferIndex = 0;
            do
            {
                key = Console.ReadKey(true);
                lock (drawLock)
                {
                    Console.CursorTop = cursorTop;
                    Console.CursorLeft = bufferIndex + 2;
                    if (!char.IsControl(key.KeyChar) && Console.CursorLeft + 1 < Console.BufferWidth)
                    {
                        inputBuffer.Insert(bufferIndex++, key.KeyChar);
                        Console.Write(key.KeyChar);
                        int cursor = Console.CursorLeft;
                        Console.Write(inputBuffer.ToString(bufferIndex, inputBuffer.Length - bufferIndex));
                        Console.CursorLeft = cursor;
                    }
                    else
                    {
                        switch (key.Key)
                        {
                            case ConsoleKey.Backspace:
                                if (bufferIndex > 0)
                                {
                                    inputBuffer.Remove(--bufferIndex, 1);
                                    int cursor = --Console.CursorLeft;
                                    Console.Write(inputBuffer.ToString(bufferIndex, inputBuffer.Length - bufferIndex));
                                    Console.Write(" ");
                                    Console.CursorLeft = cursor;
                                }
                                break;
                            case ConsoleKey.End:
                                bufferIndex = inputBuffer.Length;
                                Console.CursorLeft = bufferIndex + 2;
                                break;
                            case ConsoleKey.Home:
                                bufferIndex = 0;
                                Console.CursorLeft = 2;
                                break;
                            case ConsoleKey.LeftArrow:
                                if (bufferIndex > 0)
                                {
                                    bufferIndex--;
                                    Console.Write("\b");
                                }
                                break;
                            case ConsoleKey.RightArrow:
                                if (bufferIndex < inputBuffer.Length)
                                {
                                    bufferIndex++;
                                    Console.CursorLeft++;
                                }
                                break;
                            case ConsoleKey.Delete:
                                if (bufferIndex < inputBuffer.Length)
                                {
                                    inputBuffer.Remove(bufferIndex, 1);
                                    int cursor = Console.CursorLeft;
                                    Console.Write(inputBuffer.ToString(bufferIndex, inputBuffer.Length - bufferIndex));
                                    Console.Write(" ");
                                    Console.CursorLeft = cursor;
                                }
                                break;
                        }
                    }
                }
            }
            while (key.Key != ConsoleKey.Enter);
            return inputBuffer.ToString();
        }
    }

    interface View
    {
        int GetMinimumWidth();
        int GetMinimumHeight();
        int GetFullHeight();
        int Draw(int width, int startLine = 0, int lineCount = 0);
        RedrawResult Redraw(int width);
    } 

    class TextView : View
    {
        public List<string> Lines { get; protected set; }
        protected int minimumWidth;
        protected int minimumHeight;

        protected int lastDrawnTop;
        protected int lastDrawnLeft;
        protected int lastStartLine;
        protected int lastWrittenLines;

        public TextView(int minimumWidth, int minimumHeight)
        {
            Lines = new List<string>();
            this.minimumWidth = minimumWidth;
            this.minimumHeight = minimumHeight;
        }

        public int GetMinimumWidth() => minimumWidth;

        public int GetMinimumHeight() => minimumHeight;

        public int GetFullHeight() => Math.Max(Lines.Count, minimumHeight);

        public int Draw(int width, int startLine = 0, int lineCount = 0)
        {
            lastDrawnTop = Console.CursorTop;
            int cursorOffset = lastDrawnLeft = Console.CursorLeft;
            for (lastStartLine = startLine; startLine < Lines.Count; startLine++)
            {
                Console.Write(Lines[startLine].Length > width ? Lines[startLine].Substring(0, width) : Lines[startLine].PadRight(width));
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            while (startLine <= lineCount-- || startLine++ <= minimumHeight)
            {
                Console.Write("".PadRight(width));
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            return lastWrittenLines = startLine;
        }

        public RedrawResult Redraw(int width)
        {
            if (lastWrittenLines != GetFullHeight()) return RedrawResult.HEIGHT_CHANGED;
            Console.CursorTop = lastDrawnTop;
            Console.CursorLeft = lastDrawnLeft;
            Draw(width, lastStartLine, lastWrittenLines);
            return RedrawResult.SUCCESS;
        }
    }

    class ListView<T> : View
    {
        public string Title { get; set; }

        protected Func<IList<T>> list;
        protected Func<T, string> map;
        protected int minimumWidth;
        protected int minimumHeight;

        protected int lastDrawnTop;
        protected int lastDrawnLeft;
        protected int lastStartLine;
        protected int lastWrittenLines;

        public ListView(string title, Func<IList<T>> list, Func<T, string> map, int minimumWidth, int minimumHeight)
        {
            Title = title;
            this.list = list;
            this.map = map;
            this.minimumWidth = minimumWidth;
            this.minimumHeight = minimumHeight;
        }

        public int GetMinimumWidth() => minimumWidth;

        public int GetMinimumHeight() => minimumHeight;

        public int GetFullHeight() => Math.Max(list().Count + 1, minimumHeight);

        public int Draw(int width, int startLine = 0, int lineCount = 0)
        {
            lastDrawnTop = Console.CursorTop;
            int cursorOffset = lastDrawnLeft = Console.CursorLeft;
            lastStartLine = startLine;
            if (startLine == GetFullHeight()) return lastWrittenLines = 0;
            Console.Write(Title.Length > width ? Title.Substring(0, width) : Title.PadRight(width));
            Console.CursorTop++;
            Console.CursorLeft = cursorOffset;
            IList<T> list = this.list();
            for (; startLine < list.Count; startLine++)
            {
                string line = map(list[startLine]);
                Console.Write(line.Length > width ? line.Substring(0, width) : line.PadRight(width));
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            while (startLine <= lineCount-- || startLine++ <= minimumHeight)
            {
                Console.Write("".PadRight(width));
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            return lastWrittenLines = GetFullHeight();
        }

        public RedrawResult Redraw(int width)
        {
            if (lastWrittenLines != GetFullHeight()) return RedrawResult.HEIGHT_CHANGED;
            Console.CursorTop = lastDrawnTop;
            Console.CursorLeft = lastDrawnLeft;
            Draw(width, lastStartLine, lastWrittenLines);
            return RedrawResult.SUCCESS;
        }
    }

    enum RedrawResult
    {
        SUCCESS,
        WIDTH_CHANGED,
        HEIGHT_CHANGED
    }
}
