using Optional;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using ToSParser;

namespace ToSTextClient
{
    class ConsoleUI : ITextUI
    {
        protected const string DEFAULT_STATUS = "Type /? for help";
        protected const string DEFAULT_COMMAND_STATUS = "Type ? for help";
        protected const string EDITING_STATUS = "Editing, press ESC to save & close";

        protected TextClient game;
        protected readonly object drawLock;

        protected AbstractView mainView;
        protected List<AbstractView> sideViews;
        protected Dictionary<AbstractView, List<AbstractView>> hiddenSideViews;
        protected int mainWidth;
        protected int sideWidth;
        protected int sideStart;
        protected int textHeight;
        protected int cursorTop;

        protected StringBuilder inputBuffer;
        protected int bufferIndex;
        protected bool commandMode;
        protected string _StatusLine;
        protected EditableWillView willContext;
        protected int inputLength;
        protected CommandContext _CommandContext;
        protected Dictionary<string, Command> commands;
        protected volatile bool _RunInput = true;

        public GameState GameState { get => game.GameState; }
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
            get => _StatusLine;
            set { _StatusLine = value; RedrawCursor(); }
        }
        public CommandContext CommandContext { get => _CommandContext; set { _CommandContext = value; UpdateCommandMode(); RedrawView(helpView); } }
        public bool RunInput { get => _RunInput; set => _RunInput = value; }

        protected EditableWillView myLastWillView;
        protected EditableWillView myDeathNoteView;
        protected EditableWillView myForgedWillView;
        protected HelpView helpView;

        protected CommandGroup helpCommand;

        public ConsoleUI(TextClient game)
        {
            this.game = game;
            drawLock = new object();
            inputBuffer = new StringBuilder();
            commandMode = true;
            commands = new Dictionary<string, Command>();

            HomeView = new TextView(this, AppendLine, 60, 2);
            GameModeView = new ListView<GameModeID>(" # Game Modes", () => game.ActiveGameModes, gm => gm.ToString().ToDisplayName(), 25);
            GameView = new TextView(this, AppendLine, 60, 20);
            PlayerListView = new ListView<PlayerState>(" # Players", () => game.GameState.Players, p => p.Dead ? "" : game.GameState.ToName(p.Self, true), 25);
            RoleListView = new ListView<RoleID>(" # Role List", () => game.GameState.Roles, r => r.ToString().ToDisplayName(), 25);
            GraveyardView = new ListView<PlayerState>(" # Graveyard", () => game.GameState.Graveyard, ps => game.GameState.ToName(ps, true), 40);
            TeamView = new ListView<PlayerState>(" # Team", () => game.GameState.Team, ps => !ps.Dead || ps.Role == RoleID.DISGUISER ? game.GameState.ToName(ps, true) : "", 40);
            LastWillView = new WillView();
            myLastWillView = new EditableWillView(" # My Last Will", lw => game.GameState.LastWill = lw);
            myDeathNoteView = new EditableWillView(" # My Death Note", dn => game.GameState.DeathNote = dn);
            myForgedWillView = new EditableWillView(" # My Forged Will", fw => game.GameState.ForgedWill = fw);
            helpView = new HelpView(commands, () => _CommandContext, () => OpenSideView(helpView), 40, 1);

            mainView = (AbstractView)HomeView;
            sideViews = new List<AbstractView>();
            sideViews.Insert(0, (AbstractView)GameModeView);
            List<AbstractView> gameSideViews = new List<AbstractView>();
            gameSideViews.Insert(0, (AbstractView)RoleListView);
            gameSideViews.Insert(0, (AbstractView)GraveyardView);
            gameSideViews.Insert(0, (AbstractView)PlayerListView);
            hiddenSideViews = new Dictionary<AbstractView, List<AbstractView>>();
            hiddenSideViews.Add(mainView, sideViews);
            hiddenSideViews.Add((AbstractView)GameView, gameSideViews);

            RegisterCommand(helpCommand = new CommandGroup("View a list of available commands", ~CommandContext.NONE, this, "Command", "Commands", cmd => helpView.Topic = null), "help", "?");
            RegisterCommand(new CommandGroup("Open the {0} view", ~CommandContext.NONE, this, "View", "Views")
                .Register(new Command("Open the help view", ~CommandContext.NONE, cmd => OpenSideView(helpView)), "help")
                .Register(new Command("Open the game modes view", CommandContext.HOME, cmd => OpenSideView(GameModeView)), "modes")
                .Register(new Command("Open the role list view", CommandContext.LOBBY | CommandContext.GAME, cmd => OpenSideView(RoleListView)), "roles", "rolelist")
                .Register(new Command("Open the player list view", CommandContext.LOBBY | CommandContext.GAME, cmd => OpenSideView(PlayerListView)), "players", "playerlist")
                .Register(new Command("Open the graveyard view", CommandContext.GAME, cmd => OpenSideView(GraveyardView)), "graveyard")
                .Register(new Command("Open the team view", CommandContext.GAME, cmd => OpenSideView(TeamView)), "team")
                .Register(new Command("Open the LW/DN view", CommandContext.GAME, cmd => OpenSideView(LastWillView)), "lw", "dn", "lastwill", "deathnote"), "open");
            RegisterCommand(new CommandGroup("Close the {0} view", ~CommandContext.NONE, this, "View", "Views")
                .Register(new Command("Close the help view", ~CommandContext.NONE, cmd => CloseSideView(helpView)), "help")
                .Register(new Command("Close the game modes view", CommandContext.HOME, cmd => CloseSideView(GameModeView)), "modes")
                .Register(new Command("Close the role list view", CommandContext.LOBBY | CommandContext.GAME, cmd => CloseSideView(RoleListView)), "roles", "rolelist")
                .Register(new Command("Close the player list view", CommandContext.LOBBY | CommandContext.GAME, cmd => CloseSideView(PlayerListView)), "players", "playerlist")
                .Register(new Command("Close the graveyard view", CommandContext.GAME, cmd => CloseSideView(GraveyardView)), "graveyard")
                .Register(new Command("Close the team view", CommandContext.GAME, cmd => CloseSideView(TeamView)), "team")
                .Register(new Command("Close the LW/DN view", CommandContext.GAME, cmd => CloseSideView(LastWillView)), "lw", "dn", "lastwill", "deathnote"), "close");
            RegisterCommand(new Command("Redraw the whole screen", ~CommandContext.NONE, cmd => RedrawAll()), "redraw");
            RegisterCommand(new Command<Option<PlayerID>>("Edit your LW or view {0}'s", CommandContext.GAME, ArgumentParsers.Optional(ArgumentParsers.Player(this)), (cmd, opTarget) =>
            {
                opTarget.Match(target =>
                {
                    PlayerState ps = game.GameState.Players[(int)target];
                    if (ps.Dead)
                    {
                        LastWillView.Title = string.Format(" # (LW) {0}", game.GameState.ToName(target));
                        LastWillView.Value = ps.LastWill;
                        OpenSideView(LastWillView);
                    }
                    else StatusLine = string.Format("{0} isn't dead, so you can't see their last will", game.GameState.ToName(target));
                }, () =>
                {
                    OpenSideView(myLastWillView);
                    willContext = myLastWillView;
                    RedrawCursor();
                });
            }), "lw", "lastwill");
            RegisterCommand(new Command<Option<PlayerID>>("Edit your DN or view {0}'s", CommandContext.GAME, ArgumentParsers.Optional(ArgumentParsers.Player(this)), (cmd, opTarget) =>
            {
                opTarget.Match(target =>
                {
                    PlayerState ps = game.GameState.Players[(int)target];
                    if (ps.Dead)
                    {
                        LastWillView.Title = string.Format(" # (DN) {0}", game.GameState.ToName(target));
                        LastWillView.Value = ps.LastWill;
                        OpenSideView(LastWillView);
                    }
                    else StatusLine = string.Format("{0} isn't dead, so you can't see their killer's death note", game.GameState.ToName(target));
                }, () =>
                {
                    OpenSideView(myLastWillView);
                    willContext = myLastWillView;
                    RedrawCursor();
                });
            }), "dn", "deathnote");
            RegisterCommand(new Command("Edit your forged will", CommandContext.GAME, cmd =>
            {
                OpenSideView(myForgedWillView);
                willContext = myForgedWillView;
                RedrawCursor();
            }), "fw", "forgedwill");
            RegisterCommand(new Command("Say your will in chat", CommandContext.GAME, cmd => game.Parser.SendChatBoxMessage(game.GameState.LastWill)), "slw", "saylw", "saylastwill");
        }

        public void Run()
        {
            int width = 0;
            int height = 0;
            while (_RunInput)
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
                    if (input.Length == 0) continue;
                    _StatusLine = null;
                    if (commandMode)
                    {
                        string[] cmd = input.SplitCommand();
                        try
                        {
                            string cmdn = cmd[0].ToLower();
                            if (commands.TryGetValue(cmdn, out Command command))
                            {
                                if ((command.UsableContexts & _CommandContext) != 0) command.Run(cmdn, cmd, 1);
                                else StatusLine = string.Format("Command not allowed in the current context: {0}", cmdn);
                            }
                            else StatusLine = string.Format("Command not found: {0}", cmdn);
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine("Failed to parse command: {0}", (object)input);
                            Debug.WriteLine(e);
                            StatusLine = string.Format("Failed to parse command: {0}", e.Message);
                        }
                    }
                    else game.Parser.SendChatBoxMessage(input);
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Exception in UI loop");
                    Debug.WriteLine(e);
                }
            }
        }

        public void RegisterCommand(Command command, params string[] names)
        {
            foreach (string name in names) commands.Add(name, command);
            helpCommand.Register(new Command(command.Description, command.UsableContexts, cmd => helpView.Topic = (command, names)), names);
            RedrawView(helpView);
        }

        public void SetCommandContext(CommandContext context, bool value)
        {
            if (value) CommandContext |= context;
            else CommandContext &= ~context;
        }

        public void SetMainView(IView iview)
        {
            if (!(iview is AbstractView view)) throw new ArgumentException("attempt to set incompatible view as main view");
            lock (drawLock)
            {
                if (mainView == view) return;
                willContext = null;
                mainView = view;
                sideViews = hiddenSideViews.SafeIndex(view, () => new List<AbstractView>());
                RedrawAll();
            }
        }

        public void OpenSideView(IView iview)
        {
            if (!(iview is AbstractView view)) throw new ArgumentException("attempt to open incompatible view as side view");
            lock (drawLock)
            {
                if (sideViews.Contains(view))
                {
                    RedrawSideView(view);
                    return;
                }
                sideViews.Insert(0, view);
                RedrawSideViews();
            }
        }

        public void CloseSideView(IView iview)
        {
            if (!(iview is AbstractView view)) throw new ArgumentException("attempt to close incompatible view as side view");
            lock (drawLock) if (sideViews.Remove(view)) RedrawSideViews();
        }

        public void RedrawTimer()
        {
            if (game.GameState?.TimerText != null)
            {
                lock (drawLock)
                {
                    Console.CursorTop = cursorTop;
                    Console.CursorLeft = mainWidth + 1;
                    Console.Write(string.Format("{0}: {1}", game.GameState.TimerText, game.GameState.Timer).PadRightHard(sideWidth));
                    ResetCursor();
                }
            }
        }

        public void RedrawView(params IView[] views)
        {
            lock (drawLock)
            {
                Console.CursorVisible = false;
                if (views.Where(v => v == mainView).Any()) RedrawMainView();
                views = views.Where(sideViews.Contains).ToArray();
                if (views.Length > 0) RedrawSideView(views);
            }
        }

        public void RedrawMainView() => RedrawMainView(null);
        public void RedrawSideViews() => RedrawSideViews(null);

        protected void RedrawCursor()
        {
            lock (drawLock)
            {
                Console.CursorTop = cursorTop;
                Console.CursorLeft = 0;
                Console.Write(commandMode ? "/ " : "> ");
                string line = willContext != null ? EDITING_STATUS : inputBuffer.Length == 0 ? _StatusLine ?? (commandMode ? DEFAULT_COMMAND_STATUS : DEFAULT_STATUS) : inputBuffer.ToString();
                Console.Write(line.PadRight(inputLength));
                inputLength = line.Length;
                ResetCursor();
            }
        }

        protected void RedrawMainView(int? consoleWidth = null)
        {
            lock (drawLock)
            {
                Console.CursorVisible = false;
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
                ResetCursor();
            }
        }

        protected void RedrawSideView(params IView[] views)
        {
            lock (drawLock)
            {
                Console.CursorVisible = false;
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
                ResetCursor();
            }
        }

        protected void RedrawSideViews(int? consoleWidth = null)
        {
            lock (drawLock)
            {
                Console.CursorVisible = false;
                if (sideViews.Count > 0)
                {
                    int maxMinWidth = 0;
                    foreach (AbstractView view in sideViews) maxMinWidth = Math.Max(view.GetMinimumWidth(), maxMinWidth);
                    if (maxMinWidth != sideWidth) RedrawAll();
                    if (sideViews.Where(v => v.GetMinimumWidth() > sideWidth).Any())
                    {
                        RedrawAll();
                        return;
                    }
                    RedrawTimer();
                    int sideHeight = 0, lastSideHeight = 1;
                    int cw = consoleWidth ?? Console.BufferWidth - 1;
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
                        Console.Write("".PadRight(sideWidth + 1));
                    }
                    sideStart = cursorTop - sideHeight;     // only has an effect if sideStart > cursorTop - sideHeight
                    //for (int currentLine = cursorTop - sideHeight; currentLine < cursorTop; currentLine++)
                    for (int currentLine = cursorTop - Console.WindowHeight + 1; currentLine <= cursorTop; currentLine++)
                    {
                        Console.CursorTop = currentLine;
                        Console.CursorLeft = mainWidth;
                        Console.Write('|');
                        if (currentLine < cursorTop)
                        {
                            Console.CursorLeft = cw - 1;
                            Console.WriteLine();
                        }
                    }
                }
                else if (sideWidth > 0)
                {
                    RedrawAll();
                    return;
                }
                ResetCursor();
            }
        }

        protected void RedrawAll()
        {
            lock (drawLock)
            {
                Console.CursorVisible = false;
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

        protected void AppendLine(TextView view, FormattedString text)
        {
            lock (drawLock)
            {
                Console.CursorVisible = false;
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
                    int targetHeight = cursorTop - textHeight + sideStart + 1;
                    Console.MoveBufferArea(mainWidth, sideStart, sideWidth + 1, textHeight - sideStart, mainWidth, targetHeight);
                    for (; sideStart <= targetHeight; sideStart++)
                    {
                        Console.CursorTop = sideStart;
                        Console.CursorLeft = mainWidth;
                        Console.Write("|");
                    }
                    sideStart--;
                }
                ResetCursor();
            }
        }

        protected void ResetCursor()
        {
            Console.CursorTop = cursorTop;      // Ensure that the screen shows the cursor line when in edit mode.
            if (willContext != null)
            {
                willContext.MoveCursor(sideWidth, cursorTop);
                return;
            }
            Console.CursorLeft = bufferIndex + 2;
            Console.CursorVisible = true;
        }

        protected void UpdateCommandMode()
        {
            bool oldCommandMode = commandMode;
            if (CommandContext.HasFlag(CommandContext.AUTHENTICATING) || CommandContext.HasFlag(CommandContext.HOME)) commandMode = true;
            else if (bufferIndex == 0) commandMode = false;
            if (commandMode != oldCommandMode) RedrawCursor();
        }

        protected string ReadUserInput()
        {
            lock (drawLock)
            {
                commandMode = CommandContext.HasFlag(CommandContext.AUTHENTICATING) || CommandContext.HasFlag(CommandContext.HOME);
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
                Console.CursorVisible = true;
                key = Console.ReadKey(true);
                lock (drawLock)
                {
                    ResetCursor();
                    Console.CursorVisible = false;
                    switch (key.Key)
                    {
                        default:
                            if (!char.IsControl(key.KeyChar))
                            {
                                if (willContext != null)
                                {
                                    willContext.Value.Insert(willContext.CursorIndex++, key.KeyChar);
                                    RedrawView(willContext);
                                    break;
                                }
                                if (Console.CursorLeft + 1 >= mainWidth) break;
                                if (!commandMode && inputBuffer.Length == 0 && key.KeyChar == '/')
                                {
                                    commandMode = true;
                                    RedrawCursor();
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
                                Console.Write(inputBuffer.Length <= 1 ? "".PadRight((_StatusLine ?? DEFAULT_STATUS).Length) : inputBuffer.ToString(bufferIndex, inputBuffer.Length - bufferIndex));
                                Console.CursorLeft = bufferIndex + 2;
                            }
                            break;
                        case ConsoleKey.Backspace:
                            if (willContext != null)
                            {
                                if (willContext.CursorIndex == 0) break;
                                willContext.Value.Remove(--willContext.CursorIndex, 1);
                                RedrawView(willContext);
                                break;
                            }
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
                                RedrawCursor();
                            }
                            break;
                        case ConsoleKey.Enter:
                            if (willContext != null)
                            {
                                willContext.Value.Insert(willContext.CursorIndex++, '\r');
                                RedrawView(willContext);
                                break;
                            }
                            break;
                        case ConsoleKey.Escape:
                            if (willContext != null)
                            {
                                willContext.Save();
                                CloseSideView(willContext);
                                willContext = null;
                                Console.CursorTop = cursorTop;
                                Console.CursorLeft = 2;
                                Console.Write("".PadRight(EDITING_STATUS.Length));
                                RedrawCursor();
                                break;
                            }
                            if (mainView == GameView) commandMode = false;
                            Console.CursorLeft = 0;
                            Console.Write("".PadRight(inputBuffer.Length + 2));
                            RedrawCursor();
                            inputBuffer.Clear();
                            bufferIndex = 0;
                            break;
                        case ConsoleKey.End:
                            if (willContext != null)
                            {
                                willContext.MoveCursorEnd(sideWidth, cursorTop);
                                break;
                            }
                            bufferIndex = inputBuffer.Length;
                            Console.CursorLeft = bufferIndex + 2;
                            break;
                        case ConsoleKey.Home:
                            if (willContext != null)
                            {
                                willContext.MoveCursorHome(sideWidth, cursorTop);
                                break;
                            }
                            bufferIndex = 0;
                            Console.CursorLeft = 2;
                            break;
                        case ConsoleKey.LeftArrow:
                            if (willContext != null)
                            {
                                if (willContext.CursorIndex <= 0) break;
                                willContext.CursorIndex--;
                                ResetCursor();
                                break;
                            }
                            if (bufferIndex > 0)
                            {
                                bufferIndex--;
                                Console.Write("\b");
                            }
                            break;
                        case ConsoleKey.UpArrow:
                            if (willContext != null)
                            {
                                willContext.MoveCursorUp(sideWidth, cursorTop);
                                break;
                            }
                            // TODO: add history
                            break;
                        case ConsoleKey.RightArrow:
                            if (willContext != null)
                            {
                                if (willContext.CursorIndex >= willContext.Value.Length) break;
                                willContext.CursorIndex++;
                                ResetCursor();
                                break;
                            }
                            if (bufferIndex < inputBuffer.Length)
                            {
                                bufferIndex++;
                                Console.CursorLeft++;
                            }
                            break;
                        case ConsoleKey.DownArrow:
                            if (willContext != null)
                            {
                                willContext.MoveCursorDown(sideWidth, cursorTop);
                                break;
                            }
                            // TODO: add history
                            break;
                        case ConsoleKey.Delete:
                            if (willContext != null)
                            {
                                if (willContext.CursorIndex == 0) break;
                                willContext.Value.Remove(willContext.CursorIndex, 1);
                                RedrawView(willContext);
                                break;
                            }
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
            while (key.Key != ConsoleKey.Enter || willContext != null);
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
        public List<FormattedString> Lines { get; protected set; } = new List<FormattedString>();

        protected ITextUI ui;
        protected Action<TextView, FormattedString> append;

        public TextView(ITextUI ui, Action<TextView, FormattedString> append, int minimumWidth, int minimumHeight) : base(minimumWidth, minimumHeight)
        {
            this.ui = ui;
            this.append = append;
        }

        public override int GetFullHeight() => Math.Max(Lines.Count, minimumHeight);

        public void AppendLine(FormattedString text) => append(this, text);

        public void AppendLine(FormattedString format, params object[] args) => append(this, format.Format(args));

        public void ReplaceLine(int index, FormattedString text)
        {
            Lines.SafeReplace(index, text);
            ui.RedrawView(this);
        }

        public void ReplaceLine(int index, FormattedString format, params object[] args) => ReplaceLine(index, format.Format(args));

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
                Console.ForegroundColor = Lines[startLine].Foreground;
                Console.BackgroundColor = Lines[startLine].Background;
                Console.Write(Lines[startLine].Value.PadRightHard(width));
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            Console.ResetColor();
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
            Console.Write(Title.PadRightHard(width));
            Console.CursorTop++;
            Console.CursorLeft = cursorOffset;
            IList<T> list = this.list();
            for (; startLine < list.Count; startLine++)
            {
                string line = map(list[startLine]);
                Console.Write(line.PadRightHard(width));
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
        internal const int WILL_WIDTH = 40;
        internal const int WILL_HEIGHT = 18;

        public string Title { get; set; }
        public string Value { get; set; }

        public WillView() : base(WILL_WIDTH, WILL_HEIGHT + 1) => Title = Value = "";

        public override int GetFullHeight() => minimumHeight;

        protected override int DrawUnsafe(int width, int startLine = 0)
        {
            int cursorOffset = Console.CursorLeft;
            if (startLine == GetFullHeight()) return 0;
            Console.Write(Title.PadRightHard(width));
            Console.CursorTop++;
            Console.CursorLeft = cursorOffset;
            int lineIndex = 0;
            if (Value != null)
            {
                foreach (string line in Value.Split('\r').SelectMany(s => s.Wrap(width)))
                {
                    if (lineIndex++ < startLine) continue;
                    Console.Write(line.PadRight(width));
                    Console.CursorTop++;
                    Console.CursorLeft = cursorOffset;
                }
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
    class EditableWillView : AbstractView
    {
        public string Title { get; protected set; }
        public StringBuilder Value { get; set; } = new StringBuilder();
        public int CursorIndex { get; set; }

        protected Action<string> save;
        protected int cursorX;
        protected int cursorY;

        public EditableWillView(string title, Action<string> save) : base(WillView.WILL_WIDTH, WillView.WILL_HEIGHT + 1)
        {
            Title = title;
            this.save = save;
        }

        public void MoveCursor(int width, int cursorTop = 0)
        {
            Console.CursorTop = lastDrawnTop + cursorTop - lastStartLine + 1;
            Console.CursorLeft = lastDrawnLeft;
            int willIndex = CursorIndex;
            cursorY = 0;
            foreach (string line in Value.ToString().Split('\r'))
            {
                foreach (string segment in line.Wrap(width))
                {
                    if (willIndex <= segment.Length)
                    {
                        Console.CursorLeft = lastDrawnLeft + willIndex;
                        cursorX = willIndex;
                        return;
                    }
                    willIndex -= segment.Length;
                    cursorY++;
                    Console.CursorTop++;
                }
                willIndex--;
            }
        }

        public void MoveCursorDown(int width, int cursorTop = 0)
        {
            cursorY++;
            int willIndex = 0;
            int lineIndex = 0;
            foreach (string line in Value.ToString().Split('\r'))
            {
                foreach (string segment in line.Wrap(width))
                {
                    if (++lineIndex > cursorY)
                    {
                        lineIndex = Math.Min(line.Length, cursorX);
                        CursorIndex = willIndex + lineIndex;
                        Console.CursorTop = lastDrawnTop + cursorTop - lastStartLine + cursorY + 1;
                        Console.CursorLeft = lastDrawnLeft + lineIndex;
                        return;
                    }
                    willIndex += segment.Length;
                }
                willIndex++;
            }
            cursorY--;
        }

        public void MoveCursorUp(int width, int cursorTop = 0)
        {
            if (cursorY == 0) return;
            cursorY--;
            int willIndex = 0;
            int lineIndex = 0;
            foreach (string line in Value.ToString().Split('\r'))
            {
                foreach (string segment in line.Wrap(width))
                {
                    if (++lineIndex > cursorY)
                    {
                        lineIndex = Math.Min(line.Length, cursorX);
                        CursorIndex = willIndex + lineIndex;
                        Console.CursorTop = lastDrawnTop + cursorTop - lastStartLine + cursorY + 1;
                        Console.CursorLeft = lastDrawnLeft + lineIndex;
                        return;
                    }
                    willIndex += segment.Length;
                }
                willIndex++;
            }
        }

        public void MoveCursorHome(int width, int cursorTop = 0)
        {
            cursorX = 0;
            int willIndex = 0;
            int lineIndex = 0;
            foreach (string line in Value.ToString().Split('\r'))
            {
                int startLine = lineIndex;
                foreach (string segment in line.Wrap(width))
                {
                    if (++lineIndex > cursorY)
                    {
                        CursorIndex = willIndex;
                        Console.CursorTop = lastDrawnTop + cursorTop - lastStartLine + startLine + 1;
                        Console.CursorLeft = lastDrawnLeft;
                        return;
                    }
                }
                willIndex += line.Length;
                willIndex++;
            }
        }

        public void MoveCursorEnd(int width, int cursorTop = 0)
        {
            int willIndex = 0;
            int lineIndex = 0;
            foreach (string line in Value.ToString().Split('\r'))
            {
                bool found = false;
                foreach (string segment in line.Wrap(width))
                {
                    if (++lineIndex > cursorY)
                    {
                        cursorX = segment.Length;
                        found = true;
                    }
                }
                willIndex += line.Length;
                if (found)
                {
                    CursorIndex = willIndex;
                    Console.CursorTop = lastDrawnTop + cursorTop - lastStartLine + (cursorY = lineIndex - 1) + 1;
                    Console.CursorLeft = lastDrawnLeft + cursorX;
                    return;
                }
                willIndex++;
            }
        }

        public void Save() => save(Value.ToString());

        public void Clear()
        {
            Value.Clear();
            CursorIndex = 0;
        }

        public override int GetFullHeight() => minimumHeight;

        protected override int DrawUnsafe(int width, int startLine = 0)
        {
            int cursorOffset = Console.CursorLeft;
            if (startLine == GetFullHeight()) return 0;
            Console.Write(Title.Length > width ? Title.Substring(0, width) : Title.PadRight(width));
            Console.CursorTop++;
            Console.CursorLeft = cursorOffset;
            int lineIndex = 0;
            foreach (string line in Value.ToString().Split('\r').SelectMany(s => s.Wrap(width)))
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

    class HelpView : AbstractView
    {
        public (Command cmd, string[] names)? Topic
        {
            get => _Topic;
            set { _Topic = value; showHelp(); }
        }

        protected readonly Dictionary<string, Command> commands;
        protected Func<CommandContext> getContext;
        protected Action showHelp;
        protected (Command cmd, string[] names)? _Topic;

        public HelpView(Dictionary<string, Command> commands, Func<CommandContext> getContext, Action showHelp, int minimumWidth, int minimumHeight) : base(minimumWidth, minimumHeight)
        {
            this.commands = commands;
            this.getContext = getContext;
            this.showHelp = showHelp;
        }

        public override int GetFullHeight()
        {
            CommandContext context = getContext();
            int usableCommands = (_Topic == null ? commands : _Topic.Value.cmd.Subcommands).Values.Where(c => (c.UsableContexts & context) > 0).Distinct().Count() * 2;
            return _Topic == null ? usableCommands + 1 : _Topic.Value.cmd.Parsers.Length * 2 + 2 + (usableCommands > 0 ? usableCommands + 1 : 0);
        }

        protected override int DrawUnsafe(int width, int startLine = 0)
        {
            CommandContext context = getContext();
            int cursorOffset = Console.CursorLeft;
            if (startLine == 0)
            {
                Console.Write((_Topic == null ? " # Help" : string.Format(" # Help ({0})", string.Join(", ", _Topic?.names))).PadRightHard(width));
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            else startLine--;
            int lineIndex = 0;
            if (_Topic == null)
            {
                foreach (KeyValuePair<string, Command> command in commands.GroupBy(c => c.Value).Select(c => c.First()).Where(c => (c.Value.UsableContexts & context) > 0))
                {
                    if (++lineIndex > startLine)
                    {
                        Console.Write(string.Format("  /{0} {1}", command.Key, command.Value.UsageLine).PadRightHard(width));
                        Console.CursorTop++;
                        Console.CursorLeft = cursorOffset;
                    }
                    if (++lineIndex > startLine)
                    {
                        Console.Write(command.Value.Description.PadRightHard(width));
                        Console.CursorTop++;
                        Console.CursorLeft = cursorOffset;
                    }
                }
            }
            else
            {
                if (++lineIndex > startLine)
                {
                    Console.Write(_Topic.Value.cmd.Description.PadRightHard(width));
                    Console.CursorTop++;
                    Console.CursorLeft = cursorOffset;
                }
                foreach (ArgumentParser argument in _Topic.Value.cmd.Parsers)
                {
                    if (++lineIndex > startLine)
                    {
                        Console.Write(string.Format("  {0}", argument.DisplayName).PadRightHard(width));
                        Console.CursorTop++;
                        Console.CursorLeft = cursorOffset;
                    }
                    if (++lineIndex > startLine)
                    {
                        Console.Write(argument.Description.PadRightHard(width));
                        Console.CursorTop++;
                        Console.CursorLeft = cursorOffset;
                    }
                }
                bool start = true;
                foreach (KeyValuePair<string, Command> command in _Topic.Value.cmd.Subcommands.GroupBy(c => c.Value).Select(c => c.First()).Where(c => (c.Value.UsableContexts & context) > 0))
                {
                    if (start)
                    {
                        if (++lineIndex > startLine)
                        {
                            Console.Write(string.Format(" ~ {0}", _Topic.Value.cmd.SubcommandHeader).PadRightHard(width));
                            Console.CursorTop++;
                            Console.CursorLeft = cursorOffset;
                        }
                        start = false;
                    }
                    if (++lineIndex > startLine)
                    {
                        Console.Write(string.Format("  {0} {1}", command.Key, command.Value.UsageLine).PadRightHard(width));
                        Console.CursorTop++;
                        Console.CursorLeft = cursorOffset;
                    }
                    if (++lineIndex > startLine)
                    {
                        Console.Write(command.Value.Description.PadRightHard(width));
                        Console.CursorTop++;
                        Console.CursorLeft = cursorOffset;
                    }
                }
            }
            startLine = lineIndex;
            while (lineIndex++ < minimumHeight)
            {
                Console.Write("".PadRight(width));
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            return startLine - lastStartLine + 1;
        }
    }
}
