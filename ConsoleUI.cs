using Optional;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Security;
using System.Text;
using ToSParser;

using Console = Colorful.Console;

namespace ToSTextClient
{
    class ConsoleUI : ITextUI
    {
        protected const string DEFAULT_STATUS = "Type /? for help";
        protected const string DEFAULT_COMMAND_STATUS = "Type ? for help";
        protected const string EDITING_STATUS = "Editing, press ESC to close";

        public bool TimerVisible
        {
            get => _TimerVisible;
            set { lock (drawLock) { _TimerVisible = value; RedrawPinned(); } }
        }

        protected readonly object drawLock;

        protected AbstractView mainView;
        protected List<AbstractView> sideViews;
        protected Dictionary<AbstractView, List<AbstractView>> hiddenSideViews;
        protected int mainWidth;
        protected int sideWidth;
        protected int fullWidth;
        protected int sideHeight;
        protected int fullHeight;
        protected int sideEnd;
        protected int pinnedHeight;
        protected bool _TimerVisible;

        protected StringBuilder inputBuffer;
        protected int bufferIndex;
        protected bool commandMode;
        protected string _StatusLine;
        protected IInputView inputContext;
        protected CommandContext _CommandContext;
        protected Dictionary<string, Command> commands;
        protected volatile bool _RunInput = true;
        protected List<(bool cmdMode, string input)> inputHistory;
        protected int historyIndex;

        public TextClient Game { get; protected set; }
        public GameState GameState { get => Game.GameState; }
        public IExceptionView ExceptionView { get; protected set; }
        public IAuthView AuthView { get; protected set; }
        public ITextView HomeView { get; protected set; }
        public IListView<GameMode> GameModeView { get; protected set; }
        public ITextView GameView { get; protected set; }
        public IListView<PlayerState> PlayerListView { get; protected set; }
        public IListView<Role> RoleListView { get; protected set; }
        public IListView<PlayerState> GraveyardView { get; protected set; }
        public IListView<PlayerState> TeamView { get; protected set; }
        public IWillView LastWillView { get; protected set; }
        public IListView<Player> WinnerView { get; protected set; }
        public string StatusLine
        {
            get => _StatusLine;
            set { _StatusLine = value; RedrawCursor(); }
        }
        public CommandContext CommandContext { get => _CommandContext; set { CommandContext old = _CommandContext; _CommandContext = value; UpdateCommandMode(); if (sideViews.Where(view => view.IsAllowed(old) != view.IsAllowed(value)).Count() > 0) RedrawSideViews(); else RedrawView(helpView); } }
        public bool RunInput { get => _RunInput; set => _RunInput = value; }

        public event Action<IView, IView> OnSetMainView;
        
        protected EditableWillView myLastWillView;
        protected EditableWillView myDeathNoteView;
        protected EditableWillView myForgedWillView;
        protected HelpView helpView;
        protected CommandGroup helpCommand;
        protected CommandGroup openCommand;
        protected CommandGroup closeCommand;

        public ConsoleUI()
        {
            drawLock = new object();
            inputBuffer = new StringBuilder();
            inputHistory = new List<(bool cmdMode, string input)>();
            commandMode = true;
            commands = new Dictionary<string, Command>();
            helpCommand = new CommandGroup("View a list of available commands", this, "Topic", "Topics", cmd => helpView.Topic = null, activeContext => true);
            openCommand = new CommandGroup("Open the {0} view", this, "View", "Views");
            closeCommand = new CommandGroup("Close the {0} view", this, "View", "Views");

            ExceptionView = new ExceptionView(60, 10);
            AuthView = new AuthView(this, game => Game = game);
            HomeView = new TextView(this, UpdateView, CommandContext.HOME.Set(), 60, 2, new UserInfoView(this, 20, 2));
            GameView = new TextView(this, UpdateView, CommandExtensions.IsInLobbyOrGame, 60, 20, new NameView(this, 23, 1));
            RegisterView(GameModeView = new ListView<GameMode>(" # Game Modes", () => Game.ActiveGameModes.Where(gm => Game.OwnsCoven || !gm.RequiresCoven()).ToList(), gm => gm.ToString().ToDisplayName(), CommandContext.HOME.Set(), 25), "game modes", "modes");
            RegisterView(PlayerListView = new ListView<PlayerState>(" # Players", () => Game.GameState.Players, p => p.Dead ? "" : Game.GameState.ToName(p.ID, true), CommandExtensions.IsInLobbyOrGame, 25), "player list", "players", "playerlist");
            RegisterView(RoleListView = new ListView<Role>(" # Role List", () => Game.GameState.Roles, r => Game.Localization.Of(r), CommandExtensions.IsInLobbyOrGame, 25), "role list", "roles", "rolelist");
            RegisterView(GraveyardView = new ListView<PlayerState>(" # Graveyard", () => Game.GameState.Graveyard, ps => Game.GameState.ToName(ps, true), CommandExtensions.IsInGame, 40), "graveyard", "graveyard");
            RegisterView(TeamView = new ListView<PlayerState>(" # Team", () => Game.GameState.Team, ps => !ps.Dead || ps.Role == Role.DISGUISER ? Game.GameState.ToName(ps, true) : "", CommandExtensions.IsInGame, 40), "team");
            RegisterView(LastWillView = new WillView(this), "LW/DN", "lw", "dn", "lastwill", "deathnote");
            RegisterView(WinnerView = new ListView<Player>(" # Winners", () => Game.GameState.Winners, p => Game.GameState.ToName(p, true), CommandContext.GAME_END.Set(), 25), "winners");
            myLastWillView = new EditableWillView(this, " # My Last Will", lw => Game.GameState.LastWill = lw, ScrollViews);
            myDeathNoteView = new EditableWillView(this, " # My Death Note", dn => Game.GameState.DeathNote = dn, ScrollViews);
            myForgedWillView = new EditableWillView(this, " # My Forged Will", fw => Game.GameState.ForgedWill = fw, ScrollViews);
            RegisterView(helpView = new HelpView(commands, () => _CommandContext, () => OpenSideView(helpView), 40, 1), "help", "?", "help");

            mainView = (AbstractView)AuthView;
            sideViews = new List<AbstractView>();
            List<AbstractView> homeSideViews = new List<AbstractView>();
            List<AbstractView> gameSideViews = new List<AbstractView>();
            gameSideViews.Insert(0, (AbstractView)RoleListView);
            gameSideViews.Insert(0, (AbstractView)GraveyardView);
            gameSideViews.Insert(0, (AbstractView)PlayerListView);
            hiddenSideViews = new Dictionary<AbstractView, List<AbstractView>>
            {
                { mainView, sideViews },
                { (AbstractView)HomeView, homeSideViews },
                { (AbstractView)GameView, gameSideViews }
            };
            inputContext = AuthView;

            RegisterCommand(helpCommand, "help", "?");
            RegisterCommand(new Command("Open the login view", CommandContext.AUTHENTICATING.Set(), cmd =>
            {
                SetMainView(AuthView);
                SetInputContext(AuthView);
            }), "login", "auth", "authenticate");
            RegisterCommand(openCommand, "open");
            RegisterCommand(closeCommand, "close");
            RegisterCommand(new Command("Redraw the whole screen", activeContext => true, cmd => RedrawAll()), "redraw");
            RegisterCommand(new Command<Option<Player>>("Edit your LW or view {0}'s", CommandExtensions.IsInGame, ArgumentParsers.Optional(ArgumentParsers.Player(this)), (cmd, opTarget) =>
            {
                opTarget.Match(target =>
                {
                    PlayerState ps = Game.GameState.Players[(int)target];
                    if (ps.Dead)
                    {
                        LastWillView.Title = string.Format(" # (LW) {0}", Game.GameState.ToName(target));
                        LastWillView.Value = ps.LastWill;
                        OpenSideView(LastWillView);
                    }
                    else StatusLine = string.Format("{0} isn't dead, so you can't see their last will", Game.GameState.ToName(target));
                }, () =>
                {
                    OpenSideView(myLastWillView);
                    inputContext = myLastWillView;
                    RedrawCursor();
                });
            }), "lw", "lastwill");
            RegisterCommand(new Command<Option<Player>>("Edit your DN or view {0}'s", CommandExtensions.IsInGame, ArgumentParsers.Optional(ArgumentParsers.Player(this)), (cmd, opTarget) =>
            {
                opTarget.Match(target =>
                {
                    PlayerState ps = Game.GameState.Players[(int)target];
                    if (ps.Dead)
                    {
                        LastWillView.Title = string.Format(" # (DN) {0}", Game.GameState.ToName(target));
                        LastWillView.Value = ps.LastWill;
                        OpenSideView(LastWillView);
                    }
                    else StatusLine = string.Format("{0} isn't dead, so you can't see their killer's death note", Game.GameState.ToName(target));
                }, () =>
                {
                    OpenSideView(myLastWillView);
                    inputContext = myLastWillView;
                    RedrawCursor();
                });
            }), "dn", "deathnote");
            RegisterCommand(new Command("Edit your forged will", context => CommandExtensions.IsInGame(context) && GameState.Role == Role.FORGER, cmd =>
            {
                OpenSideView(myForgedWillView);
                inputContext = myForgedWillView;
                RedrawCursor();
            }), "fw", "forgedwill");
            RegisterCommand(new Command<Option<Player>>("Say your will or whisper it to {0}", CommandExtensions.IsInGame, ArgumentParsers.Optional(ArgumentParsers.Player(this)), (cmd, opTarget) => opTarget.Match(target => Game.Parser.SendPrivateMessage(target, Game.GameState.LastWill), () => Game.Parser.SendChatBoxMessage(Game.GameState.LastWill))), "slw", "saylw", "saylastwill");

            Console.ForegroundColor = TextClient.WHITE;
            Console.BackgroundColor = TextClient.BLACK;
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
                                if (command.IsAllowed(_CommandContext)) command.Run(cmdn, cmd, 1);
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
                    else Game.Parser.SendChatBoxMessage(input);
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
            foreach (string name in names) commands[name] = command;
            helpCommand.Register(new Command(command.Description, command.IsAllowed, cmd => helpView.Topic = (command, names)), names);
            foreach (ArgumentParser parser in command.Parsers) helpCommand.Register(new Command(parser.Description, activeContext => true, cmd => helpView.Topic = (parser, parser.HelpNames)), parser.HelpNames);
            RedrawView(helpView);
        }

        public void SetInputContext(IInputView inputView)
        {
            lock (drawLock)
            {
                inputContext?.Close();
                inputContext = inputView;
            }
        }

        public void AudioAlert() => System.Console.Beep();

        public void SetMainView(IView iview)
        {
            if (!(iview is AbstractView view)) throw new ArgumentException("attempt to set incompatible view as main view");
            lock (drawLock)
            {
                if (mainView == view)
                {
                    RedrawMainView();
                    return;
                }
                OnSetMainView?.Invoke(mainView, view);
                SetInputContext(null);
                mainView = view;
                sideViews = hiddenSideViews.SafeIndex(view, () => new List<AbstractView>());
                inputHistory.Clear();
                _TimerVisible = false;
                Game.TimerText = null;
                Game.Timer = 0;
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
            lock (drawLock)
            {
                _TimerVisible = true;
                RedrawPinned();
            }
        }

        public void RedrawView(params IView[] views)
        {
            lock (drawLock)
            {
                Console.CursorVisible = false;
                if (views.Where(v => v == mainView).Any()) RedrawMainView();
                if (views.Where(v => v == mainView.PinnedView).Any()) RedrawPinned();
                views = views.Where(sideViews.Contains).ToArray();
                if (views.Length > 0) RedrawSideView(views);
            }
        }

        public void RedrawMainView()
        {
            lock (drawLock)
            {
                Console.CursorVisible = false;
                if (mainWidth < mainView.GetMinimumWidth())
                {
                    RedrawAll();
                    return;
                }
                int mainHeight = mainView.GetFullHeight();
                Console.CursorTop = 0;
                Console.CursorLeft = 0;
                mainView.Draw(mainWidth, fullHeight - 1, Math.Max(0, mainHeight - fullHeight + 1));
                ResetCursor();
            }
        }

        public void RedrawSideViews()
        {
            lock (drawLock)
            {
                Console.CursorVisible = false;
                if (sideViews.Count > 0)
                {
                    int maxMinWidth = 0;
                    foreach (AbstractView view in sideViews.Where(view => view.IsAllowed(_CommandContext))) maxMinWidth = Math.Max(view.GetMinimumWidth(), maxMinWidth);
                    if (maxMinWidth != sideWidth) RedrawAll();
                    if (sideViews.Where(view => view.IsAllowed(_CommandContext)).Where(v => v.GetMinimumWidth() > sideWidth).Any())
                    {
                        RedrawAll();
                        return;
                    }
                    if (sideWidth <= 0) return;
                    pinnedHeight = ((AbstractView)mainView.PinnedView).GetFullHeight() + (_TimerVisible ? 1 : 0);
                    RedrawPinned();
                    int lastSideHeight = 1;
                    sideHeight = 0;
                    sideEnd = fullHeight - pinnedHeight - 1;
                    foreach (AbstractView view in sideViews.Where(view => view.IsAllowed(_CommandContext)))
                    {
                        Console.CursorLeft = fullWidth - sideWidth - 1;
                        if (lastSideHeight != 0 && sideHeight++ < fullHeight - pinnedHeight)
                        {
                            Console.CursorTop = fullHeight - sideHeight - pinnedHeight;
                            Console.Write("".PadRight(sideWidth, '-'));
                            Console.CursorLeft = fullWidth - sideWidth - 1;
                        }
                        lock (view)
                        {
                            sideHeight += lastSideHeight = view.GetFullHeight();
                            view.DrawOffscreen(sideWidth, fullHeight - pinnedHeight - 1, 0, fullWidth - sideWidth - 1, sideHeight + pinnedHeight - fullHeight);
                        }
                    }
                    for (int currentLine = 0; currentLine < fullHeight - sideHeight - pinnedHeight; currentLine++)
                    {
                        Console.CursorTop = currentLine;
                        Console.CursorLeft = mainWidth + 1;
                        Console.Write("".PadRight(sideWidth));
                    }
                    for (int currentLine = 0; currentLine < fullHeight; currentLine++)
                    {
                        Console.CursorTop = currentLine;
                        Console.CursorLeft = mainWidth;
                        Console.Write('|');
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

        protected void RedrawCursor()
        {
            lock (drawLock)
            {
                Console.CursorVisible = false;
                Console.CursorTop = fullHeight - 1;
                Console.CursorLeft = 0;
                Console.ForegroundColor = TextClient.WHITE;
                Console.Write(commandMode ? "/ " : "> ");
                if (inputContext != null || inputBuffer.Length == 0) Console.ForegroundColor = TextClient.GRAY;
                Console.Write((inputContext != null ? EDITING_STATUS : inputBuffer.Length == 0 ? _StatusLine ?? (commandMode ? DEFAULT_COMMAND_STATUS : DEFAULT_STATUS) : inputBuffer.ToString()).PadRightHard(mainWidth - 2));
                Console.ForegroundColor = TextClient.WHITE;
                ResetCursor();
            }
        }

        protected void RedrawPinned()
        {
            lock (drawLock)
            {
                Console.CursorVisible = false;
                int pinnedFullHeight = ((AbstractView)mainView.PinnedView)?.GetFullHeight() ?? 0;
                if (pinnedFullHeight + (_TimerVisible ? 1 : 0) != pinnedHeight) RedrawSideViews();
                else
                {
                    ((AbstractView)mainView.PinnedView)?.DrawOffscreen(sideWidth, pinnedFullHeight, fullHeight - pinnedHeight, mainWidth + 1);
                    if (_TimerVisible)
                    {
                        Console.CursorTop = fullHeight - 1;
                        Console.CursorLeft = mainWidth + 1;
                        Console.Write((Game.TimerText != null ? string.Format("{0}: {1}", Game.TimerText, Game.Timer) : "").PadRightHard(sideWidth));
                    }
                    ResetCursor();
                }
            }
        }

        protected void RedrawSideView(params IView[] views)
        {
            lock (drawLock)
            {
                Console.CursorVisible = false;
                foreach (IView iview in views)
                {
                    if (!(iview is AbstractView view)) throw new ArgumentException("attempt to set incompatible view as main view");
                    switch (view.Redraw())
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

        protected void RedrawAll()
        {
            lock (drawLock)
            {
                Console.CursorVisible = false;
                Console.Clear();
                Console.WindowHeight = Console.BufferHeight = fullHeight = Math.Max(mainView.GetMinimumHeight() + 1, Console.WindowHeight);
                sideWidth = 0;
                foreach (AbstractView view in sideViews.Where(view => view.IsAllowed(_CommandContext))) sideWidth = Math.Max(sideWidth, view.GetMinimumWidth());
                int minimumWidth = sideWidth + mainView.GetMinimumWidth() + 1;
                int consoleWidth = Console.BufferWidth - 1;
                if (consoleWidth < minimumWidth) Console.BufferWidth = Console.WindowWidth = (consoleWidth = minimumWidth) + 1;
                Console.BufferWidth = Console.WindowWidth = fullWidth = Math.Max(minimumWidth + 1, Console.WindowWidth);
                mainWidth = fullWidth - sideWidth - 2;
                RedrawMainView();
                RedrawCursor();
                RedrawSideViews();
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
                Console.MoveBufferArea(0, 1, mainWidth, fullHeight - 2, 0, 0);
                Console.CursorTop = fullHeight - 1;
                Console.CursorLeft = 0;
                view.Draw(mainWidth, 1, lineIndex);
                ResetCursor();
            }
        }

        protected void ResetCursor()
        {
            if (inputContext != null) inputContext.MoveCursor();
            else
            {
                Console.CursorTop = fullHeight - 1;
                Console.CursorLeft = bufferIndex + 2;
            }
            Console.CursorVisible = true;
        }

        protected void RegisterView(IView view, string displayName, params string[] names)
        {
            openCommand.Register(new Command(string.Format("Open the {0} view", displayName), view.IsAllowed, cmd => OpenSideView(view)), names);
            closeCommand.Register(new Command(string.Format("Close the {0} view", displayName), view.IsAllowed, cmd => CloseSideView(view)), names);
        }

        protected void UpdateCommandMode()
        {
            bool oldCommandMode = commandMode;
            if (CommandContext == CommandContext.AUTHENTICATING || CommandContext == CommandContext.HOME) commandMode = true;
            else if (bufferIndex == 0) commandMode = false;
            if (commandMode != oldCommandMode) RedrawCursor();
        }

        protected string ReadUserInput()
        {
            lock (drawLock)
            {
                commandMode = CommandContext == CommandContext.AUTHENTICATING || CommandContext == CommandContext.HOME;
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
                if (fullWidth != Console.WindowWidth || fullHeight != Console.WindowHeight) RedrawAll();
                lock (drawLock)
                {
                    ResetCursor();
                    Console.CursorVisible = false;
                    switch (key.Key)
                    {
                        default:
                            if (!char.IsControl(key.KeyChar))
                            {
                                if (inputContext != null)
                                {
                                    inputContext.Insert(key.KeyChar);
                                    RedrawView(inputContext);
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
                                }
                                inputBuffer.Insert(bufferIndex++, key.KeyChar);
                                RedrawCursor();
                            }
                            break;
                        case ConsoleKey.Backspace:
                            if (inputContext != null)
                            {
                                inputContext.Backspace();
                                RedrawView(inputContext);
                                break;
                            }
                            if (bufferIndex > 0)
                            {
                                inputBuffer.Remove(--bufferIndex, 1);
                                RedrawCursor();
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
                            if (inputContext != null)
                            {
                                inputContext.Enter();
                                RedrawView(inputContext);
                                break;
                            }
                            break;
                        case ConsoleKey.Escape:
                            if (inputContext != null)
                            {
                                inputContext.Close();
                                CloseSideView(inputContext);
                                inputContext = null;
                                RedrawCursor();
                                break;
                            }
                            if (mainView == GameView) commandMode = false;
                            inputBuffer.Clear();
                            bufferIndex = 0;
                            RedrawCursor();
                            break;
                        case ConsoleKey.PageUp:
                            ScrollViews(-1);
                            break;
                        case ConsoleKey.PageDown:
                            ScrollViews(1);
                            break;
                        case ConsoleKey.End:
                            if (inputContext != null)
                            {
                                inputContext.End();
                                ResetCursor();
                                break;
                            }
                            bufferIndex = inputBuffer.Length;
                            Console.CursorLeft = bufferIndex + 2;
                            break;
                        case ConsoleKey.Home:
                            if (inputContext != null)
                            {
                                inputContext.Home();
                                ResetCursor();
                                break;
                            }
                            bufferIndex = 0;
                            Console.CursorLeft = 2;
                            break;
                        case ConsoleKey.LeftArrow:
                            if (inputContext != null)
                            {
                                inputContext.LeftArrow();
                                RedrawView(inputContext);
                                break;
                            }
                            if (bufferIndex > 0)
                            {
                                bufferIndex--;
                                Console.Write("\b");
                            }
                            break;
                        case ConsoleKey.UpArrow:
                            if (inputContext != null)
                            {
                                inputContext.UpArrow();
                                ResetCursor();
                                break;
                            }
                            if (historyIndex > 0)
                            {
                                string input;
                                (commandMode, input) = inputHistory[--historyIndex];
                                inputBuffer.Clear();
                                inputBuffer.Append(input);
                                bufferIndex = input.Length;
                                RedrawCursor();
                            }
                            break;
                        case ConsoleKey.RightArrow:
                            if (inputContext != null)
                            {
                                inputContext.RightArrow();
                                RedrawView(inputContext);
                                break;
                            }
                            if (bufferIndex < inputBuffer.Length)
                            {
                                bufferIndex++;
                                Console.CursorLeft++;
                            }
                            break;
                        case ConsoleKey.DownArrow:
                            if (inputContext != null)
                            {
                                inputContext.DownArrow();
                                ResetCursor();
                                break;
                            }
                            if (historyIndex < inputHistory.Count - 1)
                            {
                                string input;
                                (commandMode, input) = inputHistory[++historyIndex];
                                inputBuffer.Clear();
                                inputBuffer.Append(input);
                                bufferIndex = input.Length;
                                RedrawCursor();
                            }
                            else if (historyIndex == inputHistory.Count - 1)
                            {
                                commandMode = CommandContext.HasFlag(CommandContext.AUTHENTICATING) || CommandContext.HasFlag(CommandContext.HOME);
                                inputBuffer.Clear();
                                bufferIndex = 0;
                                RedrawCursor();
                            }
                            break;
                        case ConsoleKey.Delete:
                            if (inputContext != null)
                            {
                                inputContext.Delete();
                                RedrawView(inputContext);
                                break;
                            }
                            if (bufferIndex < inputBuffer.Length)
                            {
                                inputBuffer.Remove(bufferIndex, 1);
                                RedrawCursor();
                            }
                            break;
                    }
                }
            }
            while (key.Key != ConsoleKey.Enter || inputContext != null);
            string result = inputBuffer.ToString();
            inputHistory.Add((commandMode, result));
            historyIndex = inputHistory.Count;
            return result;
        }

        protected void UpdateView(AbstractView view)
        {
            lock (drawLock)
            {
                if (view != mainView) return;
                if (view.GetFullHeight() > fullHeight - 1) view.Scroll(1);
                else RedrawMainView();
                ResetCursor();
            }
        }

        protected void ScrollViews(int lines)
        {
            lock (drawLock)
            {
                Console.CursorVisible = false;
                mainView.Scroll(lines);
                sideEnd -= lines = sideEnd - Math.Max(fullHeight - pinnedHeight - 1, Math.Min(sideHeight - 1, sideEnd - lines));
                if (lines > 0)
                {
                    Console.MoveBufferArea(mainWidth + 1, lines, sideWidth, fullHeight - lines - pinnedHeight - 1, mainWidth + 1, 0);
                    int line = sideEnd, lastHeight = 0;
                    foreach (AbstractView sideView in sideViews)
                    {
                        if (lastHeight != 0 && line-- >= fullHeight - lines - pinnedHeight && line < fullHeight - pinnedHeight - 1)
                        {
                            Console.CursorTop = line;
                            Console.CursorLeft = mainWidth + 1;
                            Console.Write("".PadRight(sideWidth, '-'));
                        }
                        line -= lastHeight = sideView.Move(lines);
                    }
                }
                else if (lines < 0)
                {
                    Console.MoveBufferArea(mainWidth + 1, 0, sideWidth, fullHeight + lines - pinnedHeight - 1, mainWidth + 1, -lines);
                    int line = sideEnd, lastHeight = 0;
                    foreach (AbstractView sideView in sideViews)
                    {
                        if (lastHeight != 0 && line-- > 0 && line < -lines)
                        {
                            Console.CursorTop = line;
                            Console.CursorLeft = mainWidth + 1;
                            Console.Write("".PadRight(sideWidth, '-'));
                        }
                        line -= lastHeight = sideView.Move(lines);
                    }
                }
                ResetCursor();
            }
        }
    }

    abstract class AbstractView : IView
    {
        protected Func<CommandContext, bool> isAllowed;
        protected int minimumWidth;
        protected int minimumHeight;

        protected int lastDrawnTop;
        protected int lastDrawnLeft;
        protected int lastStartLine;
        protected int lastWidth;
        protected int lastHeight;
        protected int lastFullHeight;

        public IView PinnedView { get; protected set; }

        protected AbstractView(Func<CommandContext, bool> isAllowed, int minimumWidth, int minimumHeight, IView pinnedView = null)
        {
            this.isAllowed = isAllowed;
            this.minimumWidth = minimumWidth;
            this.minimumHeight = minimumHeight;
            PinnedView = pinnedView;
        }

        public virtual int GetMinimumWidth() => minimumWidth;
        public virtual int GetMinimumHeight() => minimumHeight;
        public abstract int GetFullHeight();
        public virtual void Draw(int width, int height, int startLine = 0)
        {
            lastFullHeight = GetFullHeight();
            lastDrawnTop = Console.CursorTop;
            lastDrawnLeft = Console.CursorLeft;
            for (int drawn = DrawUnsafe(lastWidth = width, lastHeight = height, lastStartLine = startLine); drawn < height; drawn++)
            {
                Console.CursorTop = lastDrawnTop + drawn;
                Console.CursorLeft = lastDrawnLeft;
                Console.Write("".PadRight(width));
            }
        }
        public virtual void DrawOffscreen(int width, int height, int cursorTop, int cursorLeft, int startLine = 0)
        {
            lastWidth = width;
            lastHeight = height;
            lastDrawnTop = cursorTop;
            lastDrawnLeft = cursorLeft;
            lastStartLine = startLine;
            lastFullHeight = GetFullHeight();
            Redraw();
        }
        public virtual RedrawResult Redraw()
        {
            if (lastWidth < GetMinimumWidth()) return RedrawResult.WIDTH_CHANGED;
            else if (lastHeight < GetMinimumHeight() || GetFullHeight() != lastFullHeight) return RedrawResult.HEIGHT_CHANGED;
            int drawHeight = Math.Min(lastFullHeight - lastStartLine, lastHeight);
            for (int drawn = DrawSafe(lastDrawnTop, drawHeight, lastStartLine); drawn < drawHeight; drawn++)
            {
                Console.CursorTop = lastDrawnTop + drawn;
                Console.CursorLeft = lastDrawnLeft;
                Console.Write("".PadRight(lastWidth));
            }
            return RedrawResult.SUCCESS;
        }
        public virtual int Scroll(int lines)
        {
            if (lastWidth == 0 || lastHeight == 0) return 0;
            lastFullHeight = GetFullHeight();
            lastStartLine += lines = Math.Min(Math.Max(lastFullHeight - lastHeight, 0) - lastStartLine, Math.Max(-lastStartLine, lines));
            if (lines > 0)
            {
                Console.MoveBufferArea(lastDrawnLeft, lastDrawnTop + lines, lastWidth, lastHeight - lines, lastDrawnLeft, lastDrawnTop);
                int drawLines = Math.Min(lines, Math.Max(0, Math.Min(lastHeight, lastFullHeight - lastStartLine)));
                if (drawLines > 0)
                {
                    Console.CursorTop = lastDrawnTop + lastHeight - lines;
                    Console.CursorLeft = lastDrawnLeft;
                    DrawUnsafe(lastWidth, drawLines, lastStartLine + lastHeight - lines);
                }
            }
            else if (lines < 0)
            {
                lines = -lines;
                Console.MoveBufferArea(lastDrawnLeft, lastDrawnTop, lastWidth, lastHeight - lines, lastDrawnLeft, lastDrawnTop + lines);
                int drawLines = Math.Min(lines, Math.Min(lastHeight, lastFullHeight - lastStartLine));
                if (drawLines > 0)
                {
                    Console.CursorTop = lastDrawnTop;
                    Console.CursorLeft = lastDrawnLeft;
                    DrawUnsafe(lastWidth, drawLines, lastStartLine);
                }
                lines = -lines;
            }
            return lines;
        }
        public virtual int Move(int lines)
        {
            lastStartLine += lines;
            if (lines > 0)
            {
                int drawLower = Math.Max(-lastStartLine, lastDrawnTop + lastHeight - lines);
                int drawUpper = Math.Min(lastFullHeight - lastStartLine, lastDrawnTop + lastHeight);
                if (drawUpper - drawLower > 0) DrawSafe(drawLower, drawUpper - drawLower, lastStartLine + drawLower - lastDrawnTop);
            }
            else if (lines < 0)
            {
                int drawLower = Math.Max(-lastStartLine, lastDrawnTop);
                int drawUpper = Math.Min(lastFullHeight - lastStartLine, lastDrawnTop - lines);
                if (drawUpper - drawLower > 0) DrawSafe(drawLower, drawUpper - drawLower, lastStartLine + drawLower - lastDrawnTop);
            }
            return lastFullHeight;
        }
        protected int DrawSafe(int cursorTop, int height, int startLine)
        {
            int offset = Math.Min(0, startLine);
            Console.CursorTop = cursorTop - offset;
            Console.CursorLeft = lastDrawnLeft;
            return DrawUnsafe(lastWidth, height + offset, startLine - offset) - offset;
        }
        protected abstract int DrawUnsafe(int width, int height, int startLine = 0);

        public bool IsAllowed(CommandContext activeContext) => isAllowed(activeContext);
    }

    class TextView : AbstractView, ITextView
    {
        public List<FormattedString> Lines { get; protected set; } = new List<FormattedString>();

        protected ITextUI ui;
        protected Action<AbstractView> append;

        public TextView(ITextUI ui, Action<AbstractView> append, Func<CommandContext, bool> isAllowed, int minimumWidth, int minimumHeight, IView pinnedView = null) : base(isAllowed, minimumWidth, minimumHeight, pinnedView)
        {
            this.ui = ui;
            this.append = append;
        }

        public override int GetFullHeight() => Math.Max(Lines.Count, minimumHeight);

        public void AppendLine(FormattedString text)
        {
            Lines.Add(text);
            append(this);
        }

        public void AppendLine(FormattedString format, params object[] args) => AppendLine(FormattedString.Format(format, args));

        public void ReplaceLine(int index, FormattedString text)
        {
            Lines.SafeReplace(index, text);
            ui.RedrawView(this);
        }

        public void ReplaceLine(int index, FormattedString format, params object[] args) => ReplaceLine(index, FormattedString.Format(format, args));

        public void Clear()
        {
            Lines.Clear();
            ui.RedrawView(this);
        }

        protected override int DrawUnsafe(int width, int height, int startLine = 0)
        {
            lock (Lines)
            {
                int cursorOffset = Console.CursorLeft;
                int lineIndex = 0;
                for (; startLine < Lines.Count && lineIndex < height; startLine++, lineIndex++)
                {
                    if (Lines[startLine] != null) Lines[startLine].Render(width);
                    else Console.Write("".PadRight(width));
                    Console.CursorTop++;
                    Console.CursorLeft = cursorOffset;
                }
                return lineIndex;
            }
        }
    }

    class ListView<T> : AbstractView, IListView<T>
    {
        public string Title { get; set; }

        protected Func<IList<T>> list;
        protected Func<T, FormattedString> map;

        public ListView(string title, Func<IList<T>> list, Func<T, FormattedString> map, Func<CommandContext, bool> isAllowed, int minimumWidth) : base(isAllowed, minimumWidth, 1)
        {
            Title = title;
            this.list = list;
            this.map = map;
        }

        public override int GetFullHeight() => Math.Max(list().Count + 1, minimumHeight);

        protected override int DrawUnsafe(int width, int height, int startLine = 0)
        {
            if (height <= 0) return 0;
            int cursorOffset = Console.CursorLeft;
            IList<T> list = this.list();
            lock (list)
            {
                int lineIndex = 0;
                if (startLine == 0)
                {
                    lineIndex++;
                    Console.Write(Title.PadRightHard(width));
                    Console.CursorTop++;
                    Console.CursorLeft = cursorOffset;
                }
                else startLine--;
                if (startLine >= list.Count) return lineIndex;
                for (; startLine < list.Count && lineIndex < height; startLine++, lineIndex++)
                {
                    map(list[startLine]).Render(width);
                    Console.CursorTop++;
                    Console.CursorLeft = cursorOffset;
                }
                return lineIndex;
            }
        }
    }

    class WillView : AbstractView, IWillView
    {
        internal const int WILL_WIDTH = 40;
        internal const int WILL_HEIGHT = 18;

        public string Title { get; set; }
        public string Value { get; set; }

        public WillView(ConsoleUI ui) : base(CommandExtensions.IsInGame, WILL_WIDTH, WILL_HEIGHT + 1)
        {
            Title = Value = "";
            ui.OnSetMainView += (_1, _2) => Title = Value = "";
        }

        public override int GetFullHeight() => minimumHeight;

        protected override int DrawUnsafe(int width, int height, int startLine = 0)
        {
            if (height == 0) return 0;
            int cursorOffset = Console.CursorLeft;
            int lineIndex = 0;
            if (startLine == 0)
            {
                lineIndex++;
                Console.Write(Title.PadRightHard(width));
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            else startLine--;
            lock (Value)
            {
                int currentLine = 0;
                if (Value != null)
                {
                    foreach (string line in Value.Split('\r').SelectMany(s => s.Wrap(width)))
                    {
                        if (currentLine++ < startLine) continue;
                        if (lineIndex++ >= height) return lineIndex;
                        Console.Write(line.PadRight(width));
                        Console.CursorTop++;
                        Console.CursorLeft = cursorOffset;
                    }
                }
                return lineIndex;
            }
        }
    }
    class EditableWillView : AbstractView, IInputView
    {
        public string Title { get; protected set; }
        protected StringBuilder Value { get; set; } = new StringBuilder();
        protected int CursorIndex { get; set; }

        protected Action<string> save;
        protected Action<int> scroll;
        protected int cursorX;
        protected int cursorY;

        public EditableWillView(ConsoleUI ui, string title, Action<string> save, Action<int> scroll) : base(CommandExtensions.IsInGame, WillView.WILL_WIDTH, WillView.WILL_HEIGHT + 1)
        {
            Title = title;
            this.save = save;
            this.scroll = scroll;
            ui.OnSetMainView += (_1, _2) => Value.Length = CursorIndex = 0;
        }

        public void MoveCursor()
        {
            Console.CursorLeft = lastDrawnLeft;
            lock (Value)
            {
                int willIndex = CursorIndex;
                cursorY = 0;
                foreach (string line in Value.ToString().Split('\r'))
                {
                    foreach (string segment in line.Wrap(lastWidth))
                    {
                        if (willIndex <= segment.Length)
                        {
                            SafeCursorTop(lastDrawnTop + cursorY + 1);
                            Console.CursorLeft = lastDrawnLeft + willIndex;
                            cursorX = willIndex;
                            return;
                        }
                        willIndex -= segment.Length;
                        cursorY++;
                    }
                    willIndex--;
                }
            }
            SafeCursorTop(lastDrawnTop + 1);
        }

        public void Insert(char value) => Value.Insert(CursorIndex++, value);

        public void Enter() => Insert('\r');

        public void Backspace()
        {
            if (CursorIndex <= 0) return;
            Value.Remove(--CursorIndex, 1);
        }

        public void Delete() => Value.Remove(CursorIndex, 1);

        public void LeftArrow()
        {
            if (CursorIndex <= 0) return;
            CursorIndex--;
        }
        
        public void RightArrow()
        {
            if (CursorIndex >= Value.Length) return;
            CursorIndex++;
        }

        public void DownArrow()
        {
            cursorY++;
            int willIndex = 0;
            int lineIndex = 0;
            lock (Value)
            {
                foreach (string line in Value.ToString().Split('\r'))
                {
                    foreach (string segment in line.Wrap(lastWidth))
                    {
                        if (++lineIndex > cursorY)
                        {
                            lineIndex = Math.Min(line.Length, cursorX);
                            CursorIndex = willIndex + lineIndex;
                            return;
                        }
                        willIndex += segment.Length;
                    }
                    willIndex++;
                }
            }
            cursorY--;
        }

        public void UpArrow()
        {
            if (cursorY == 0) return;
            cursorY--;
            int willIndex = 0;
            int lineIndex = 0;
            lock (Value)
            {
                foreach (string line in Value.ToString().Split('\r'))
                {
                    foreach (string segment in line.Wrap(lastWidth))
                    {
                        if (++lineIndex > cursorY)
                        {
                            lineIndex = Math.Min(line.Length, cursorX);
                            CursorIndex = willIndex + lineIndex;
                            return;
                        }
                        willIndex += segment.Length;
                    }
                    willIndex++;
                }
            }
        }

        public void Home()
        {
            cursorX = 0;
            int willIndex = 0;
            int lineIndex = 0;
            lock (Value)
            {
                foreach (string line in Value.ToString().Split('\r'))
                {
                    int startLine = lineIndex;
                    foreach (string segment in line.Wrap(lastWidth))
                    {
                        if (++lineIndex > cursorY)
                        {
                            CursorIndex = willIndex;
                            return;
                        }
                    }
                    willIndex += line.Length;
                    willIndex++;
                }
            }
        }

        public void End()
        {
            int willIndex = 0;
            int lineIndex = 0;
            lock (Value)
            {
                foreach (string line in Value.ToString().Split('\r'))
                {
                    bool found = false;
                    foreach (string segment in line.Wrap(lastWidth))
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
                        cursorY = lineIndex - 1;
                        return;
                    }
                    willIndex++;
                }
            }
        }

        public void Close() => save(Value.ToString());

        public void Clear()
        {
            Value.Clear();
            CursorIndex = 0;
        }

        public override int GetFullHeight() => minimumHeight;

        protected void SafeCursorTop(int y)
        {
            if (y < 0)
            {
                scroll(y);
                y = 0;
            }
            else if (y >= lastHeight)
            {
                scroll(y - lastHeight + 1);
                y = lastHeight - 1;
            }
            Console.CursorTop = y;
        }

        protected override int DrawUnsafe(int width, int height, int startLine = 0)
        {
            if (height == 0) return 0;
            int cursorOffset = Console.CursorLeft;
            int lineIndex = 0;
            if (startLine == 0)
            {
                lineIndex++;
                Console.Write(Title.Length > width ? Title.Substring(0, width) : Title.PadRight(width));
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            else startLine--;
            int currentLine = 0;
            lock (Value)
            {
                foreach (string line in Value.ToString().Split('\r').SelectMany(s => s.Wrap(width)))
                {
                    if (currentLine++ < startLine) continue;
                    if (lineIndex++ >= height) return lineIndex;
                    Console.Write(line.PadRight(width));
                    Console.CursorTop++;
                    Console.CursorLeft = cursorOffset;
                }
            }
            return lineIndex;
        }
    }

    class HelpView : AbstractView
    {
        public (IDocumented cmd, string[] names)? Topic
        {
            get => _Topic;
            set { _Topic = value; showHelp(); }
        }

        protected readonly Dictionary<string, Command> commands;
        protected Func<CommandContext> getContext;
        protected Action showHelp;
        protected (IDocumented cmd, string[] names)? _Topic;

        public HelpView(Dictionary<string, Command> commands, Func<CommandContext> getContext, Action showHelp, int minimumWidth, int minimumHeight) : base(activeContext => true, minimumWidth, minimumHeight)
        {
            this.commands = commands;
            this.getContext = getContext;
            this.showHelp = showHelp;
        }

        public override int GetFullHeight()
        {
            CommandContext context = getContext();
            return _Topic == null ? commands.Values.Where(c => c.IsAllowed(context)).Distinct().Count() * 2 + 1 : _Topic.Value.cmd.Documentation.Count() + 2;
        }

        protected override int DrawUnsafe(int width, int height, int startLine = 0)
        {
            if (height == 0) return 0;
            CommandContext context = getContext();
            int cursorOffset = Console.CursorLeft;
            int lineIndex = 0;
            if (startLine == 0)
            {
                lineIndex++;
                Console.Write((_Topic == null ? " # Help" : string.Format(" # Help ({0})", string.Join(", ", _Topic.Value.names))).PadRightHard(width));
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            else startLine--;
            int currentLine = 0;
            if (_Topic == null)
            {
                foreach (KeyValuePair<string, Command> command in commands.GroupBy(c => c.Value).Select(c => c.First()).Where(c => c.Value.IsAllowed(context)))
                {
                    if (++currentLine > startLine)
                    {
                        if (lineIndex++ >= height) return lineIndex;
                        Console.Write(string.Format("  /{0} {1}", command.Key, command.Value.UsageLine).PadRightHard(width));
                        Console.CursorTop++;
                        Console.CursorLeft = cursorOffset;
                    }
                    if (++currentLine > startLine)
                    {
                        if (lineIndex++ >= height) return lineIndex;
                        Console.Write(command.Value.Description.PadRightHard(width));
                        Console.CursorTop++;
                        Console.CursorLeft = cursorOffset;
                    }
                }
            }
            else
            {
                if (++currentLine > startLine)
                {
                    if (lineIndex++ >= height) return lineIndex;
                    Console.Write(_Topic.Value.cmd.Description.PadRightHard(width));
                    Console.CursorTop++;
                    Console.CursorLeft = cursorOffset;
                }
                foreach (FormattedString line in _Topic.Value.cmd.Documentation)
                {
                    if (++currentLine > startLine)
                    {
                        if (lineIndex++ >= height) return lineIndex;
                        line.Render(width);
                        Console.CursorTop++;
                        Console.CursorLeft = cursorOffset;
                    }
                }
            }
            return lineIndex;
        }
    }

    class AuthView : AbstractView, IAuthView
    {
        public FormattedString Status
        {
            get => _Status;
            set { _Status = value; ui.RedrawView(this); }
        }

        private static readonly FormattedString HOST_LIVE = FormattedString.From(("Live", TextClient.GREEN, null), (" / PTR / Local", null, null));
        private static readonly FormattedString HOST_PTR = FormattedString.From(("Live / ", null, null), ("PTR", TextClient.GREEN, null), (" / Local", null, null));
        private static readonly FormattedString HOST_LOCAL = FormattedString.From(("Live / PTR / ", null, null), ("Local", TextClient.GREEN, null));

        protected ConsoleUI ui;
        protected Action<TextClient> setGame;
        protected Host selectedHost;
        protected StringBuilder username;
        protected int usernameCursor;
        protected SecureString password;
        protected int passwordCursor;
        protected int lineIndex;
        protected FormattedString _Status;

        public AuthView(ConsoleUI ui, Action<TextClient> setGame) : base(CommandContext.AUTHENTICATING.Set(), 30, 3)
        {
            this.ui = ui;
            this.setGame = setGame;
            username = new StringBuilder(20);
            password = new SecureString();
            lineIndex = 1;
        }

        public void Insert(char c)
        {
            if (lineIndex == 1 && username.Length < 20 && c != ' ') username.Insert(usernameCursor++, c);
            else if (lineIndex == 2) password.InsertAt(passwordCursor++, c);
        }

        public void Enter()
        {
            if (lineIndex < 2) DownArrow();
            else
            {
                try
                {
                    Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.Connect(GetHost(selectedHost), 3600);
                    setGame(new TextClient(ui, socket, username.ToString(), password));
                    Close();
                }
                catch (SocketException)
                {
                    Status = ("Failed to connect to the server: check your internet connection", TextClient.RED);
                    Close();
                }
                catch (Exception e)
                {
                    ui.ExceptionView.Exception = e;
                    ui.SetMainView(ui.ExceptionView);
                    ui.StatusLine = "Exception occurred during authentication";
                }
            }
        }

        public void Backspace()
        {
            if (lineIndex == 1 && usernameCursor > 0) username.Remove(--usernameCursor, 1);
            else if (lineIndex == 2 && passwordCursor > 0) password.RemoveAt(--passwordCursor);
        }

        public void Delete()
        {
            if (lineIndex == 1 && usernameCursor < username.Length) username.Remove(usernameCursor, 1);
            else if (lineIndex == 2 && passwordCursor < password.Length) password.RemoveAt(passwordCursor);
        }

        public void Home()
        {
            if (lineIndex == 1) usernameCursor = 0;
            else if (lineIndex == 2) passwordCursor = 0;
        }

        public void End()
        {
            if (lineIndex == 1) usernameCursor = username.Length;
            else if (lineIndex == 2) passwordCursor = password.Length;
        }

        public void LeftArrow()
        {
            if (lineIndex == 0 && selectedHost > 0) selectedHost--;
            else if (lineIndex == 1 && usernameCursor > 0) usernameCursor--;
            else if (lineIndex == 2 && passwordCursor > 0) passwordCursor--;
        }

        public void RightArrow()
        {
            if (lineIndex == 0 && selectedHost < Host.Local) selectedHost++;
            else if (lineIndex == 1 && usernameCursor < username.Length) usernameCursor++;
            else if (lineIndex == 2 && passwordCursor < password.Length) passwordCursor++;
        }

        public void UpArrow()
        {
            if (lineIndex > 0) lineIndex--;
        }

        public void DownArrow()
        {
            if (lineIndex < 2) lineIndex++;
        }

        public void MoveCursor()
        {
            Console.CursorTop = lastDrawnTop + lineIndex;
            Console.CursorLeft = lineIndex == 1 ? usernameCursor + 10 : lineIndex == 2 && password.Length == 0 ? 10 : 18;
        }

        public void Close()
        {
            lineIndex = 1;
            username.Clear();
            usernameCursor = 0;
            password.Clear();
            passwordCursor = 0;
        }

        public override int GetFullHeight()
        {
            return 3;
        }

        protected override int DrawUnsafe(int width, int height, int startLine = 0)
        {
            int cursorOffset = Console.CursorLeft;
            int lineIndex = 0, currentLine = 0;
            if (++currentLine > startLine)
            {
                if (lineIndex++ >= height) return lineIndex;
                switch (selectedHost)
                {
                    case Host.Live:
                        HOST_LIVE.Render(width);
                        break;
                    case Host.PTR:
                        HOST_PTR.Render(width);
                        break;
                    case Host.Local:
                        HOST_LOCAL.Render(width);
                        break;
                }
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            if (++currentLine > startLine)
            {
                if (lineIndex++ >= height) return lineIndex;
                Console.Write("Username: ");
                Console.Write(username.ToString().PadRightHard(width - 10));
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            if (++currentLine > startLine)
            {
                if (lineIndex++ >= height) return lineIndex;
                Console.Write("Password: ");
                Console.Write((password.Length > 0 ? "(hidden)" : "").PadRightHard(width - 10));
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            if (++currentLine > startLine)
            {
                if (lineIndex++ >= height) return lineIndex;
                if (_Status != null) _Status.Render(width);
                else Console.Write("".PadRight(width));
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            return lineIndex;
        }

        protected string GetHost(Host host)
        {
            switch (host)
            {
                case Host.Live:
                    return "live4.tos.blankmediagames.com";
                case Host.PTR:
                    return "ptr.tos.blankmediagames.com";
                case Host.Local:
                    return "localhost";
            }
            throw new ArgumentOutOfRangeException("unknown host value " + host);
        }

        protected enum Host
        {
            Live, PTR, Local
        }
    }

    class UserInfoView : AbstractView
    {
        protected ConsoleUI ui;

        public UserInfoView(ConsoleUI ui, int minimumWidth, int minimumHeight) : base(context => true, minimumWidth, minimumHeight) => this.ui = ui;

        public override int GetFullHeight() => ui.Game.Username != null ? 2 : 0;

        protected override int DrawUnsafe(int width, int height, int startLine = 0)
        {
            int cursorOffset = Console.CursorLeft;
            int currentLine = 0, lineIndex = 0;
            if (++currentLine > startLine)
            {
                if (lineIndex++ >= height) return lineIndex;
                Console.Write(ui.Game.Username.PadRightHard(width));
                Console.CursorTop++;
                Console.CursorLeft = cursorOffset;
            }
            if (++currentLine > startLine)
            {
                if (lineIndex++ >= height) return lineIndex;
                Console.Write(string.Format("{0} TP / {1} MP", ui.Game.TownPoints, ui.Game.MeritPoints).PadRightHard(width));
            }
            return lineIndex;
        }
    }

    class NameView : AbstractView
    {
        protected ConsoleUI ui;

        public NameView(ConsoleUI ui, int minimumWidth, int minimumHeight) : base(context => true, minimumWidth, minimumHeight) => this.ui = ui;

        public override int GetFullHeight() => ui.Game.GameState.Self != null ? 1 : 0;

        protected override int DrawUnsafe(int width, int height, int startLine = 0)
        {
            if (startLine > 0 || height < 1) return 0;
            ui.Game.GameState.ToName(ui.Game.GameState.Self, true).Render(width);
            return 1;
        }
    }

    class ExceptionView : AbstractView, IExceptionView
    {
        public Exception Exception { get; set; }

        public ExceptionView(int minimumWidth, int minimumHeight) : base(activeContext => true, minimumWidth, minimumHeight) { }

        public override int GetFullHeight() => (Exception?.ToString()?.Where(c => c == '\r')?.Count() ?? -1) + 1;

        protected override int DrawUnsafe(int width, int height, int startLine = 0)
        {
            int cursorOffset = Console.CursorLeft;
            int lineIndex = 0, currentLine = 0;
            foreach (string line in Exception?.ToString()?.Split('\r') ?? Enumerable.Empty<string>())
            {
                if (++currentLine > startLine)
                {
                    if (lineIndex++ >= height) return lineIndex;
                    Console.Write(line);
                    Console.CursorTop++;
                    Console.CursorLeft = cursorOffset;
                }
            }
            return lineIndex;
        }
    }
}
