using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using ToSParser;

namespace ToSTextClient
{
    class ConsoleUI : TextUI
    {
        protected TextClient game;
        protected readonly object drawLock;

        protected AbstractView mainView;
        protected Stack<AbstractView> sideViews;
        protected Dictionary<AbstractView, Stack<AbstractView>> hiddenSideViews;
        protected int mainWidth;
        protected int sideWidth;
        protected int sideStart;
        protected int textHeight;
        protected int cursorTop;

        protected StringBuilder inputBuffer;
        protected int bufferIndex;
        protected bool commandMode;
        protected string _StatusLine;

        public ITextView HomeView { get; protected set; }
        public IListView<GameModeID> GameModeView { get; protected set; }
        public ITextView GameView { get; protected set; }
        public IListView<PlayerState> PlayerListView { get; protected set; }
        public IListView<RoleID> RoleListView { get; protected set; }
        public IListView<PlayerState> GraveyardView { get; protected set; }
        public IListView<PlayerState> TeamView { get; protected set; }
        public IWillView LastWillView { get; protected set; }
        public string StatusLine
        {
            get { return _StatusLine; }
            set
            {
                _StatusLine = value;
                RedrawCursor();
            }
        }

        public ConsoleUI(TextClient game)
        {
            this.game = game;
            drawLock = new object();

            HomeView = new TextView(this, AppendLine, 60, 2);
            GameModeView = new ListView<GameModeID>(" # Game Modes", () => game.ActiveGameModes, gm => gm.ToString().ToDisplayName(), 25);
            GameView = new TextView(this, AppendLine, 60, 20);
            PlayerListView = new ListView<PlayerState>(" # Players", () => game.GameState.Players, p => p.Dead ? "" : game.GameState.ToName(p.Self, true), 25);
            RoleListView = new ListView<RoleID>(" # Role List", () => game.GameState.Roles, r => r.ToString().ToDisplayName(), 25);
            GraveyardView = new ListView<PlayerState>(" # Graveyard", () => game.GameState.Graveyard, ps => game.GameState.ToName(ps, true), 40);
            TeamView = new ListView<PlayerState>(" # Team", () => game.GameState.Team, ps => !ps.Dead || ps.Role == RoleID.DISGUISER ? game.GameState.ToName(ps, true) : "", 40);
            LastWillView = new WillView();

            mainView = (AbstractView)HomeView;
            sideViews = new Stack<AbstractView>();
            sideViews.Push((AbstractView)GameModeView);
            Stack<AbstractView> gameSideViews = new Stack<AbstractView>();
            gameSideViews.Push((AbstractView)RoleListView);
            gameSideViews.Push((AbstractView)GraveyardView);
            gameSideViews.Push((AbstractView)PlayerListView);
            hiddenSideViews = new Dictionary<AbstractView, Stack<AbstractView>>();
            hiddenSideViews.Add(mainView, sideViews);
            hiddenSideViews.Add((AbstractView)GameView, gameSideViews);
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
                    _StatusLine = null;
                    if (mainView == HomeView)
                    {
                        if (Enum.TryParse(input.Replace(' ', '_').ToUpper(), out GameModeID gameMode) && game.ActiveGameModes.Contains(gameMode)) game.Parser.JoinLobby(gameMode);
                        else StatusLine = string.Format("Cannot join game mode \"{0}\"", input);
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
                                if (Enum.TryParse(string.Join("_", cmd, 1, cmd.Length - 1).ToUpper(), out RoleID role))
                                {
                                    game.Parser.ClickedOnAddButton(role);
                                    game.GameState.AddRole(role);
                                }
                                else StatusLine = string.Format("Invalid role: {0}", string.Join(" ", cmd, 1, cmd.Length - 1));
                            }
                            else if (cmd[0].ToLower() == "remove")
                            {
                                if (byte.TryParse(cmd[1], out byte slot))
                                {
                                    game.Parser.ClickedOnRemoveButton(--slot);
                                    game.GameState.RemoveRole(slot);
                                }
                                else StatusLine = string.Format("Invalid slot number: {0}", cmd[1]);
                            }
                            else if (cmd[0].ToLower() == "start") game.Parser.ClickedOnStartButton();
                            else if (cmd[0].ToLower() == "n") game.Parser.ChooseName(string.Join(" ", cmd, 1, cmd.Length - 1));
                            else if (cmd[0].ToLower() == "t")
                            {
                                int index = 1;
                                if (game.GameState.TryParsePlayer(cmd, ref index, out PlayerID target))
                                {
                                    game.Parser.SetTarget(target);
                                    if (game.GameState.Role.IsMafia()) game.Parser.SetTargetMafiaOrWitch(target, target == PlayerID.JAILOR ? TargetType.CANCEL_TARGET_1 : TargetType.SET_TARGET_1);
                                    GameView.AppendLine(target == PlayerID.JAILOR ? "Unset target" : "Set target to {0}", game.GameState.ToName(target));
                                }
                            }
                            else if (cmd[0].ToLower() == "t2")
                            {
                                int index = 1;
                                if (game.GameState.TryParsePlayer(cmd, ref index, out PlayerID target))
                                {
                                    game.Parser.SetSecondTarget(target);
                                    if (game.GameState.Role.IsMafia()) game.Parser.SetTargetMafiaOrWitch(target, target == PlayerID.JAILOR ? TargetType.CANCEL_TARGET_2 : TargetType.SET_TARGET_2);
                                    GameView.AppendLine(target == PlayerID.JAILOR ? "Unset secondary target" : "Set secondary target to {0}", game.GameState.ToName(target));
                                }
                            }
                            else if (cmd[0].ToLower() == "v")
                            {
                                int index = 1;
                                if (game.GameState.TryParsePlayer(cmd, ref index, out PlayerID target)) game.Parser.SetVote(target);
                            }
                            else if (cmd[0].ToLower() == "guilty") game.Parser.JudgementVoteGuilty();
                            else if (cmd[0].ToLower() == "innocent") game.Parser.JudgementVoteInnocent();
                            else if (cmd[0].ToLower() == "w")
                            {
                                int index = 1;
                                if (game.GameState.TryParsePlayer(cmd, ref index, out PlayerID target, false)) game.Parser.SendPrivateMessage(target, string.Join(" ", cmd, index, cmd.Length - index));
                            }
                            else if (cmd[0].ToLower() == "td")
                            {
                                int index = 1;
                                if (game.GameState.TryParsePlayer(cmd, ref index, out PlayerID target))
                                {
                                    game.Parser.SetDayChoice(target);
                                    GameView.AppendLine(target == PlayerID.JAILOR ? "Unset day target" : "Set day target to {0}", game.GameState.ToName(target));
                                }
                            }
                            else if (cmd[0].ToLower() == "lw")
                            {
                                int index = 1;
                                if (game.GameState.TryParsePlayer(cmd, ref index, out PlayerID target))
                                {
                                    PlayerState ps = game.GameState.Players[(int)target];
                                    if (!ps.Dead)
                                    {
                                        StatusLine = string.Format("{0} isn't dead, so you can't see their last will", game.GameState.ToName(target));
                                        continue;
                                    }
                                    LastWillView.Title = string.Format(" # (LW) {0}", game.GameState.ToName(target));
                                    LastWillView.Value = ps.LastWill;
                                    OpenSideView(LastWillView);
                                }
                            }
                            else if (cmd[0].ToLower() == "dn")
                            {
                                int index = 1;
                                if (game.GameState.TryParsePlayer(cmd, ref index, out PlayerID target))
                                {
                                    PlayerState ps = game.GameState.Players[(int)target];
                                    if (!ps.Dead)
                                    {
                                        StatusLine = string.Format("{0} isn't dead, so you can't see their killer's death note", game.GameState.ToName(target));
                                        continue;
                                    }
                                    LastWillView.Title = string.Format(" # (DN) {0}", game.GameState.ToName(target));
                                    LastWillView.Value = ps.DeathNote;
                                    OpenSideView(LastWillView);
                                }
                            }
                            else if (cmd[0].ToLower() == "report")
                            {
                                PlayerID player = (PlayerID)(byte.Parse(cmd[1]) - 1u);
                                ReportReasonID reason = (ReportReasonID)Enum.Parse(typeof(ReportReasonID), cmd[2].ToUpper());
                                game.Parser.ReportPlayer(player, reason, string.Join(" ", cmd, 3, cmd.Length - 3));
                                GameView.AppendLine("Reported {0} for {1}", game.GameState.ToName(player), reason.ToString().ToLower().Replace('_', ' '));
                            }
                            else if (cmd[0].ToLower() == "redraw") RedrawAll();
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine("Failed to parse GameView command: {0}", input);
                            Debug.WriteLine(e);
                            GameView.AppendLine("Failed to parse command: {0}", e.Message);
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

        public void SetMainView(IView iview)
        {
            if (!(iview is AbstractView view)) throw new ArgumentException("attempt to set incompatible view as main view");
            lock (drawLock)
            {
                if (mainView == view) return;
                if (view == GameView) commandMode = false;
                mainView = view;
                sideViews = hiddenSideViews.SafeIndex(view, () => new Stack<AbstractView>());
                RedrawAll();
            }
        }

        public void OpenSideView(IView iview)
        {
            if (!(iview is AbstractView view)) throw new ArgumentException("attempt to set incompatible view as main view");
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

        public void RedrawView(params IView[] views)
        {
            lock (drawLock)
            {
                if (views.Where(v => v == mainView).Any()) RedrawMainView();
                RedrawSideView(views.Where(sideViews.Contains).ToArray());
            }
        }

        public void RedrawMainView() => RedrawMainView(null);
        public void RedrawSideViews() => RedrawSideViews(null);

        protected void RedrawCursor()
        {
            Console.CursorTop = cursorTop;
            Console.CursorLeft = 0;
            Console.Write(commandMode ? "/ " : "> ");
            Console.Write(inputBuffer.Length == 0 ? _StatusLine : inputBuffer.ToString());
            Console.CursorLeft = bufferIndex + 2;
        }

        protected void RedrawMainView(int? consoleWidth = null)
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

        protected void RedrawSideView(params IView[] views)
        {
            lock (drawLock)
            {
                foreach (IView iview in views)
                {
                    AbstractView view = iview as AbstractView;
                    if (view == null) throw new ArgumentException("attempt to set incompatible view as main view");
                    switch (view.Redraw(sideWidth, cursorTop))
                    {
                        case RedrawResult.WIDTH_CHANGED:
                            RedrawAll();
                            return;
                        case RedrawResult.HEIGHT_CHANGED:
                            RedrawSideViews();
                            return;
                    }
                }
                Console.CursorTop = cursorTop;
                Console.CursorLeft = bufferIndex + 2;
            }
        }

        protected void RedrawSideViews(int? consoleWidth = null)
        {
            lock (drawLock)
            {
                int sideHeight = 0, lastSideHeight = 0;
                int cw = consoleWidth ?? Console.BufferWidth - 1;
                if (sideViews.Count > 0)
                {
                    foreach (AbstractView view in sideViews)
                    {
                        Console.CursorLeft = cw - sideWidth;
                        if (lastSideHeight != 0)
                        {
                            Console.CursorTop = cursorTop - ++sideHeight;
                            Console.Write("".PadRight(sideWidth, '-'));
                            Console.CursorLeft = cw - sideWidth;
                        }
                        lock (view)
                        {
                            lastSideHeight = view.GetFullHeight();
                            if (lastSideHeight == 0) continue;
                            Console.CursorTop = Math.Max(0, cursorTop - sideHeight - lastSideHeight);
                            int lineIndex = Math.Max(0, lastSideHeight + sideHeight - cursorTop);
                            sideHeight += lastSideHeight = view.Draw(sideWidth, cursorTop, lineIndex);
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

        protected void RedrawAll()
        {
            lock (drawLock)
            {
                Console.Clear();
                sideWidth = 0;
                foreach (AbstractView view in sideViews) sideWidth = Math.Max(sideWidth, view.GetMinimumWidth());
                int minimumWidth = sideWidth + mainView.GetMinimumWidth() + 1;
                int consoleWidth = Console.BufferWidth - 1;
                if (consoleWidth < minimumWidth) Console.BufferWidth = (consoleWidth = minimumWidth) + 1;
                textHeight = mainView.Draw(mainWidth = consoleWidth - sideWidth - 1);
                cursorTop = textHeight < Console.WindowHeight ? Console.WindowHeight - 1 : textHeight;
                RedrawCursor();
                RedrawSideViews(consoleWidth);
            }
        }

        protected void AppendLine(TextView view, string text)
        {
            lock (drawLock)
            {
                int lineIndex = view.Lines.Count;
                view.Lines.Add(text);
                if (view != mainView) return;
                Console.CursorTop = textHeight++;
                Console.CursorLeft = 0;
                view.Draw(mainWidth, cursorTop, lineIndex);
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
                cursorTop = textHeight < Console.WindowHeight ? Console.WindowHeight - 1 : textHeight;
                inputBuffer.Clear();
                bufferIndex = 0;
                RedrawCursor();
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
                                    Console.Write(inputBuffer.Length <= 1 ? "".PadRight(_StatusLine?.Length ?? 0) : inputBuffer.ToString(bufferIndex, inputBuffer.Length - bufferIndex));
                                    Console.CursorLeft = bufferIndex + 2;
                                }
                                break;
                            case ConsoleKey.Backspace:
                                if (bufferIndex > 0)
                                {
                                    inputBuffer.Remove(--bufferIndex, 1);
                                    --Console.CursorLeft;
                                    Console.Write(inputBuffer.ToString(bufferIndex, inputBuffer.Length - bufferIndex));
                                    Console.Write(" ");
                                    Console.CursorLeft = bufferIndex + 2;
                                }
                                else if (commandMode && mainView == GameView)
                                {
                                    commandMode = false;
                                    Console.CursorLeft = 0;
                                    Console.Write("> ");
                                }
                                if (inputBuffer.Length == 0)
                                {
                                    Console.Write(_StatusLine);
                                    Console.CursorLeft = bufferIndex + 2;
                                }
                                break;
                            case ConsoleKey.Escape:
                                if (mainView == GameView) commandMode = false;
                                Console.CursorLeft = 0;
                                Console.Write(string.Format("{0} {1}", commandMode ? "/" : ">", _StatusLine).PadRight(inputBuffer.Length + 2));
                                Console.CursorLeft = 2;
                                inputBuffer.Clear();
                                bufferIndex = 0;
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

    abstract class AbstractView : IView
    {
        protected int minimumWidth;
        protected int minimumHeight;

        protected int lastDrawnTop;
        protected int lastDrawnLeft;
        protected int lastStartLine;
        protected int lastWrittenLines;

        protected AbstractView(int minimumWidth, int minimumHeight)
        {
            this.minimumWidth = minimumWidth;
            this.minimumHeight = minimumHeight;
        }

        public virtual int GetMinimumWidth() => minimumWidth;
        public virtual int GetMinimumHeight() => minimumHeight;
        public abstract int GetFullHeight();
        public virtual int Draw(int width, int cursorTop = 0, int startLine = 0)
        {
            lastDrawnTop = Console.CursorTop - cursorTop;
            lastDrawnLeft = Console.CursorLeft;
            return lastWrittenLines = DrawUnsafe(width, lastStartLine = startLine);
        }
        public virtual RedrawResult Redraw(int width, int cursorTop = 0)
        {
            if (lastWrittenLines != GetFullHeight()) return RedrawResult.HEIGHT_CHANGED;
            Console.CursorTop = lastDrawnTop + cursorTop;
            Console.CursorLeft = lastDrawnLeft;
            int written = DrawUnsafe(width, lastStartLine);
            Console.CursorTop = lastDrawnTop + cursorTop + written;
            while (written < lastWrittenLines--)
            {
                Console.Write("".PadRight(width));
                Console.CursorTop++;
                Console.CursorLeft = lastDrawnLeft;
            }
            lastWrittenLines = written;
            return RedrawResult.SUCCESS;
        }
        protected abstract int DrawUnsafe(int width, int startLine = 0);
    }

    class TextView : AbstractView, ITextView
    {
        public List<string> Lines { get; protected set; } = new List<string>();

        protected TextUI ui;
        protected Action<TextView, string> append;

        public TextView(TextUI ui, Action<TextView, string> append, int minimumWidth, int minimumHeight) : base(minimumWidth, minimumHeight)
        {
            this.ui = ui;
            this.append = append;
        }

        public override int GetFullHeight() => Math.Max(Lines.Count, minimumHeight);

        public void AppendLine(string text) => append(this, text);

        public void AppendLine(string format, params object[] args) => append(this, string.Format(format, args));

        public void ReplaceLine(int index, string text)
        {
            Lines.SafeReplace(index, text);
            ui.RedrawView(this);
        }

        public void ReplaceLine(int index, string format, params object[] args) => ReplaceLine(index, string.Format(format, args));

        public void Clear()
        {
            Lines.Clear();
            ui.RedrawView(this);
        }

        protected override int DrawUnsafe(int width, int startLine = 0)
        {
            int cursorOffset = Console.CursorLeft;
            for (; startLine < Lines.Count; startLine++)
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
            return Lines.Count - lastStartLine;
        }
    }

    class ListView<T> : AbstractView, IListView<T>
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

        protected override int DrawUnsafe(int width, int startLine = 0)
        {
            int cursorOffset = Console.CursorLeft;
            if (startLine == GetFullHeight()) return 0;
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
            return GetFullHeight() - lastStartLine;
        }
    }

    class WillView : AbstractView, IWillView
    {
        private const int WILL_WIDTH = 40;
        private const int WILL_HEIGHT = 18;

        public string Title { get; set; }
        public string Value { get; set; }

        public WillView() : base(WILL_WIDTH, WILL_HEIGHT + 1) => Title = Value = "";

        public override int GetFullHeight() => minimumHeight;

        protected override int DrawUnsafe(int width, int startLine = 0)
        {
            int cursorOffset = Console.CursorLeft;
            if (startLine == GetFullHeight()) return 0;
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
            return minimumHeight - lastStartLine;
        }
    }
}
