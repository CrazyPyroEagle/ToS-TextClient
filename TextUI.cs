using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        protected int sideStart;
        protected int textHeight;
        protected int cursorTop;

        protected StringBuilder inputBuffer;
        protected int bufferIndex;
        protected bool commandMode;

        public TextView HomeView { get; protected set; }
        public ListView<GameModeID> GameModeView { get; protected set; }
        public TextView GameView { get; protected set; }
        public ListView<PlayerState> PlayerListView { get; protected set; }
        public ListView<RoleID> RoleListView { get; protected set; }
        public ListView<PlayerState> GraveyardView { get; protected set; }
        public ListView<PlayerState> TeamView { get; protected set; }
        public WillView LastWillView { get; protected set; }
        public TextView InfoView { get; protected set; }

        public TextUI(TextClient game)
        {
            this.game = game;
            drawLock = new object();

            HomeView = new TextView(60, 2);
            GameModeView = new ListView<GameModeID>(" # Game Modes", () => game.ActiveGameModes, gm => gm.ToString().ToDisplayName(), 25);
            GameView = new TextView(60, 20);
            PlayerListView = new ListView<PlayerState>(" # Players", () => game.GameState.Players, p => p.Dead ? "" : game.GameState.ToName(p.Self, true), 25);
            RoleListView = new ListView<RoleID>(" # Role List", () => game.GameState.Roles, r => r.ToString().ToDisplayName(), 25);
            GraveyardView = new ListView<PlayerState>(" # Graveyard", () => game.GameState.Graveyard, ps => game.GameState.ToName(ps, true), 40);
            TeamView = new ListView<PlayerState>(" # Team", () => game.GameState.Team, ps => !ps.Dead || ps.Role == RoleID.DISGUISER ? game.GameState.ToName(ps, true) : "", 40);
            LastWillView = new WillView();
            InfoView = new TextView(50, 1);

            mainView = HomeView;
            sideViews = new Stack<View>();
            sideViews.Push(GameModeView);
            Stack<View> gameSideViews = new Stack<View>();
            gameSideViews.Push(RoleListView);
            gameSideViews.Push(GraveyardView);
            gameSideViews.Push(PlayerListView);
            hiddenSideViews = new Dictionary<View, Stack<View>>();
            hiddenSideViews.Add(mainView, sideViews);
            hiddenSideViews.Add(GameView, gameSideViews);
            inputBuffer = new StringBuilder();
            commandMode = true;
        }

        public void Run()
        {
            int width = 0;
            int height = 0;
            while (true)
            {
                try
                {
                    if (width != Console.BufferWidth || height != Console.WindowHeight)
                    {
                        RedrawAll();
                        width = Console.BufferWidth;
                        height = Console.WindowHeight;
                    }
                    string input = ReadUserInput();
                    if (mainView == HomeView)
                    {
                        if (Enum.TryParse(input.Replace(' ', '_').ToUpper(), out GameModeID gameMode) && game.ActiveGameModes.Contains(gameMode))
                        {
                            game.Parser.JoinLobby(gameMode);
                        }
                        else
                        {
                            InfoView.Lines.SafeReplace(0, string.Format("Cannot join game mode \"{0}\"", input));
                            OpenSideView(InfoView);
                        }
                    }
                    else if (mainView == GameView)
                    {
                        if (!commandMode)
                        {
                            game.Parser.SendChatBoxMessage(input);
                            continue;
                        }
                        string[] cmd = input.Split(' ');
                        try
                        {
                            if (cmd[0].ToLower() == "leave") game.Parser.LeaveGame();
                            else if (cmd[0].ToLower() == "leavepost") game.Parser.LeavePostGameLobby();
                            else if (cmd[0].ToLower() == "repick") game.Parser.VoteToRepickHost();
                            else if (cmd[0].ToLower() == "add")
                            {
                                RoleID role = (RoleID)Enum.Parse(typeof(RoleID), string.Join("_", cmd, 1, cmd.Length - 1).ToUpper());
                                game.Parser.ClickedOnAddButton(role);
                                game.GameState.AddRole(role);
                            }
                            else if (cmd[0].ToLower() == "remove")
                            {
                                byte slot = (byte)(byte.Parse(cmd[1]) - 1u);
                                game.Parser.ClickedOnRemoveButton(slot);
                                game.GameState.RemoveRole(slot);
                            }
                            else if (cmd[0].ToLower() == "start") game.Parser.ClickedOnStartButton();
                            else if (cmd[0].ToLower() == "n") game.Parser.ChooseName(string.Join(" ", cmd, 1, cmd.Length - 1));
                            else if (cmd[0].ToLower() == "t")
                            {
                                if (cmd[1].ToLower() == "none")
                                {
                                    game.Parser.SetTarget(PlayerID.JAILOR);
                                    if (game.GameState.Role.IsMafia()) game.Parser.SetTargetMafiaOrWitch(PlayerID.JAILOR, TargetType.CANCEL_TARGET_1);
                                    AppendLine("Unset target");
                                }
                                else
                                {
                                    PlayerID target = (PlayerID)(byte.Parse(cmd[1]) - 1);
                                    game.Parser.SetTarget(target);
                                    if (game.GameState.Role.IsMafia()) game.Parser.SetTargetMafiaOrWitch(PlayerID.JAILOR, TargetType.SET_TARGET_1);
                                    AppendLine("Set target to {0}", game.GameState.ToName(target));
                                }
                            }
                            else if (cmd[0].ToLower() == "t2")
                            {
                                if (cmd[1].ToLower() == "none")
                                {
                                    game.Parser.SetSecondTarget(PlayerID.JAILOR);
                                    if (game.GameState.Role.IsMafia()) game.Parser.SetTargetMafiaOrWitch(PlayerID.JAILOR, TargetType.CANCEL_TARGET_2);
                                    AppendLine("Unset secondary target");
                                }
                                else
                                {
                                    PlayerID target = (PlayerID)(byte.Parse(cmd[1]) - 1);
                                    game.Parser.SetSecondTarget(target);
                                    if (game.GameState.Role.IsMafia()) game.Parser.SetTargetMafiaOrWitch(PlayerID.JAILOR, TargetType.SET_TARGET_2);
                                    AppendLine("Set secondary target to {0}", game.GameState.ToName(target));
                                }
                            }
                            else if (cmd[0].ToLower() == "v") game.Parser.SetVote(cmd[1].ToLower() == "none" ? PlayerID.JAILOR : (PlayerID)(byte.Parse(cmd[1]) - 1));
                            else if (cmd[0].ToLower() == "guilty") game.Parser.JudgementVoteGuilty();
                            else if (cmd[0].ToLower() == "innocent") game.Parser.JudgementVoteInnocent();
                            else if (cmd[0].ToLower() == "w") game.Parser.SendPrivateMessage((PlayerID)(byte.Parse(cmd[1]) - 1), string.Join(" ", cmd, 2, cmd.Length - 2));
                            else if (cmd[0].ToLower() == "td")
                            {
                                if (cmd[1].ToLower() == "none")
                                {
                                    game.Parser.SetDayChoice(PlayerID.JAILOR);
                                    AppendLine("Unset day target");
                                }
                                else
                                {
                                    PlayerID target = (PlayerID)(byte.Parse(cmd[1]) - 1);
                                    game.Parser.SetDayChoice(target);
                                    AppendLine("Set day target to {0}", game.GameState.ToName(target));
                                }
                            }
                            else if (cmd[0].ToLower() == "lw")
                            {
                                byte rawPlayer = byte.Parse(cmd[1]);
                                PlayerState ps = game.GameState.Players[--rawPlayer];
                                if (!ps.Dead)
                                {
                                    AppendLine("{0} isn't dead, so you can't see their last will", game.GameState.ToName((PlayerID)rawPlayer));
                                    continue;
                                }
                                LastWillView.Title = string.Format(" # (LW) {0}", game.GameState.ToName((PlayerID)rawPlayer));
                                LastWillView.Value = ps.LastWill;
                                OpenSideView(LastWillView);
                            }
                            else if (cmd[0].ToLower() == "dn")
                            {
                                byte rawPlayer = byte.Parse(cmd[1]);
                                PlayerState ps = game.GameState.Players[--rawPlayer];
                                if (!ps.Dead)
                                {
                                    AppendLine("{0} isn't dead, so you can't see their death note", game.GameState.ToName((PlayerID)rawPlayer));
                                    continue;
                                }
                                LastWillView.Title = string.Format(" # (DN) {0}", game.GameState.ToName((PlayerID)rawPlayer));
                                LastWillView.Value = ps.LastWill;
                                OpenSideView(LastWillView);
                            }
                            else if (cmd[0].ToLower() == "report")
                            {
                                PlayerID player = (PlayerID)(byte.Parse(cmd[1]) - 1u);
                                ReportReasonID reason = (ReportReasonID)Enum.Parse(typeof(ReportReasonID), cmd[2].ToUpper());
                                game.Parser.ReportPlayer(player, reason, string.Join(" ", cmd, 3, cmd.Length - 3));
                                AppendLine("Reported {0} for {1}", game.GameState.ToName(player), reason.ToString().ToLower().Replace('_', ' '));
                            }
                            else if (cmd[0].ToLower() == "redraw") RedrawAll();
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine("Failed to parse GameView command: {0}", input);
                            Debug.WriteLine(e);
                            AppendLine("Failed to parse command: {0}", e.Message);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Exception in UI loop");
                    Debug.WriteLine(e);
                }
            }
        }

        public void SelectMainView(View view)
        {
            lock (drawLock)
            {
                if (mainView == view) return;
                if (view == GameView) commandMode = false;
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
                Console.CursorTop = textHeight++;
                Console.CursorLeft = 0;
                GameView.Draw(mainWidth, lineIndex);
                int windowHeight = Console.WindowHeight;
                int newCursorTop = textHeight < windowHeight ? windowHeight - 1 : textHeight;
                if (newCursorTop != cursorTop)
                {
                    cursorTop = newCursorTop;
                    RedrawCursor();
                    //RedrawSideViews();
                    int targetHeight = cursorTop - textHeight + sideStart + 1;
                    Console.MoveBufferArea(mainWidth, sideStart, sideWidth + 1, textHeight - sideStart - 1, mainWidth, targetHeight);
                    for (; sideStart < targetHeight; sideStart++)
                    {
                        Console.CursorTop = sideStart;
                        Console.CursorLeft = mainWidth;
                        Console.Write("|");
                    }
                }
                Console.CursorTop = cursorTop;
                Console.CursorLeft = bufferIndex + 2;
            }
        }

        public void AppendLine(string text, params object[] args) => AppendLine(string.Format(text, args));

        public void RedrawCursor()
        {
            Console.CursorTop = cursorTop;
            Console.CursorLeft = 0;
            Console.Write(commandMode ? "/ " : "> ");
            Console.Write(inputBuffer.ToString());
            Console.CursorLeft = bufferIndex + 2;
        }

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
                        cursorTop = newCursorTop;
                        RedrawSideViews(consoleWidth);
                        RedrawCursor();
                        return;
                    }
                }
                Console.CursorTop = cursorTop;
                Console.CursorLeft = bufferIndex + 2;
            }
        }

        protected void RedrawSideView(View view)
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
                            Console.CursorTop = Math.Max(0, cursorTop - sideHeight - lastSideHeight);
                            int lineIndex = Math.Max(0, lastSideHeight + sideHeight - cursorTop);
                            sideHeight += lastSideHeight = view.Draw(sideWidth, lineIndex);
                        }
                        if (sideHeight >= cursorTop)
                        {
                            sideHeight = cursorTop;
                            break;
                        }
                    }
                    for (; sideStart < cursorTop - sideHeight; sideStart++)
                    {
                        Console.CursorTop = sideStart;
                        Console.CursorLeft = mainWidth;
                        Console.Write("".PadRight(sideWidth));
                    }
                    sideStart = cursorTop - sideHeight;     // only has an effect if sideStart > cursorTop - sideHeight
                    //for (int currentLine = cursorTop - sideHeight; currentLine < cursorTop; currentLine++)
                    for (int currentLine = cursorTop - Console.WindowHeight + 1; currentLine < cursorTop; currentLine++)
                    {
                        Console.CursorTop = currentLine;
                        Console.CursorLeft = mainWidth;
                        Console.Write('|');
                        Console.CursorLeft = cw - 1;
                        Console.WriteLine();
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
                cursorTop = textHeight < Console.WindowHeight ? Console.WindowHeight - 1 : textHeight;
                RedrawCursor();
                RedrawSideViews(consoleWidth);
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
                if (mainView == GameView) commandMode = false;
                Console.CursorTop = cursorTop;
                Console.CursorLeft = 0;
                Console.Write("".PadRight(inputBuffer.Length + 2));
                Console.CursorTop = cursorTop = textHeight < Console.WindowHeight ? Console.WindowHeight - 1 : textHeight;
                Console.CursorLeft = 0;
                Console.Write(commandMode ? "/ " : "> ");
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
                    {
                        switch (key.Key)
                        {
                            default:
                                if (!char.IsControl(key.KeyChar) && Console.CursorLeft + 1 < Console.BufferWidth)
                                {
                                    if (!commandMode && inputBuffer.Length == 0 && key.KeyChar == '/')
                                    {
                                        commandMode = true;
                                        Console.CursorLeft = 0;
                                        Console.Write("/ ");
                                        break;
                                    }
                                    else if (commandMode && mainView == GameView && bufferIndex == 0 && key.KeyChar == '/')
                                    {
                                        commandMode = false;
                                        Console.CursorLeft = 0;
                                        Console.Write("> ");
                                    }
                                    inputBuffer.Insert(bufferIndex++, key.KeyChar);
                                    Console.Write(key.KeyChar);
                                    int cursor = Console.CursorLeft;
                                    Console.Write(inputBuffer.ToString(bufferIndex, inputBuffer.Length - bufferIndex));
                                    Console.CursorLeft = cursor;
                                }
                                break;
                            case ConsoleKey.Backspace:
                                if (bufferIndex > 0)
                                {
                                    inputBuffer.Remove(--bufferIndex, 1);
                                    int cursor = --Console.CursorLeft;
                                    Console.Write(inputBuffer.ToString(bufferIndex, inputBuffer.Length - bufferIndex));
                                    Console.Write(" ");
                                    Console.CursorLeft = cursor;
                                }
                                else if (commandMode && mainView == GameView)
                                {
                                    commandMode = false;
                                    Console.CursorLeft = 0;
                                    Console.Write("> ");
                                }
                                break;
                            case ConsoleKey.Escape:
                                if (mainView == GameView) commandMode = false;
                                Console.CursorLeft = 0;
                                Console.Write((commandMode ? "/ " : "> ").PadRight(inputBuffer.Length + 2));
                                Console.CursorLeft = 2;
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

    abstract class View
    {
        protected int minimumWidth;
        protected int minimumHeight;

        protected int lastDrawnTop;
        protected int lastDrawnLeft;
        protected int lastStartLine;
        protected int lastWrittenLines;

        protected View(int minimumWidth, int minimumHeight)
        {
            this.minimumWidth = minimumWidth;
            this.minimumHeight = minimumHeight;
        }

        public virtual int GetMinimumWidth() => minimumWidth;
        public virtual int GetMinimumHeight() => minimumHeight;
        public abstract int GetFullHeight();
        public abstract int Draw(int width, int startLine = 0);
        public virtual RedrawResult Redraw(int width)
        {
            if (lastWrittenLines != GetFullHeight()) return RedrawResult.HEIGHT_CHANGED;
            Console.CursorTop = lastDrawnTop;
            Console.CursorLeft = lastDrawnLeft;
            int written = lastWrittenLines;
            Draw(width, lastStartLine);
            int cursorOffset = Console.CursorLeft;
            while (lastWrittenLines < written--)
            {
                Console.Write("".PadRight(width));
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            return RedrawResult.SUCCESS;
        }
    }

    class TextView : View
    {
        public List<string> Lines { get; protected set; }

        public TextView(int minimumWidth, int minimumHeight) : base(minimumWidth, minimumHeight) => Lines = new List<string>();

        public override int GetFullHeight() => Math.Max(Lines.Count, minimumHeight);

        public override int Draw(int width, int startLine = 0)
        {
            lastDrawnTop = Console.CursorTop;
            int cursorOffset = lastDrawnLeft = Console.CursorLeft;
            for (lastStartLine = startLine; startLine < Lines.Count; startLine++)
            {
                Console.Write(Lines[startLine].Length > width ? Lines[startLine].Substring(0, width) : Lines[startLine].PadRight(width));
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            while (startLine++ < minimumHeight)
            {
                Console.Write("".PadRight(width));
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            return lastWrittenLines = Lines.Count - lastStartLine;
        }
    }

    class ListView<T> : View
    {
        public string Title { get; set; }

        protected Func<IList<T>> list;
        protected Func<T, string> map;

        public ListView(string title, Func<IList<T>> list, Func<T, string> map, int minimumWidth) : base(minimumWidth, 1)
        {
            Title = title;
            this.list = list;
            this.map = map;
        }

        public override int GetFullHeight() => Math.Max(list().Count + 1, minimumHeight);

        public override int Draw(int width, int startLine = 0)
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
            while (++startLine < minimumHeight)
            {
                Console.Write("".PadRight(width));
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            return lastWrittenLines = GetFullHeight();
        }
    }

    class WillView : View
    {
        private const int WILL_WIDTH = 40;
        private const int WILL_HEIGHT = 18;

        public string Title { get; set; }
        public string Value { get; set; }

        public WillView() : base(WILL_WIDTH, WILL_HEIGHT + 1) => Title = Value = "";

        public override int GetFullHeight() => minimumHeight;

        public override int Draw(int width, int startLine = 0)
        {
            lastDrawnTop = Console.CursorTop;
            int cursorOffset = lastDrawnLeft = Console.CursorLeft;
            lastStartLine = startLine;
            if (startLine == GetFullHeight()) return lastWrittenLines = 0;
            Console.Write(Title.Length > width ? Title.Substring(0, width) : Title.PadRight(width));
            Console.CursorTop++;
            Console.CursorLeft = cursorOffset;
            int lineIndex = 0;
            foreach (string line in Value.Split('\r').SelectMany(s => s.Wrap(width)))
            {
                if (lineIndex++ < startLine) continue;
                Console.Write(line.PadRight(width));
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            while (++lineIndex < minimumHeight)
            {
                Console.Write("".PadRight(width));
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            return lastWrittenLines = minimumHeight;
        }
    }

    enum RedrawResult
    {
        SUCCESS,
        WIDTH_CHANGED,
        HEIGHT_CHANGED
    }
}
