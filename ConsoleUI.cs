﻿using Optional;
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
        protected int fullWidth;
        protected int sideHeight;
        protected int fullHeight;
        protected int sideEnd;

        protected StringBuilder inputBuffer;
        protected int bufferIndex;
        protected bool commandMode;
        protected string _StatusLine;
        protected EditableWillView willContext;
        protected CommandContext _CommandContext;
        protected Dictionary<string, Command> commands;
        protected volatile bool _RunInput = true;
        protected List<(bool cmdMode, string input)> inputHistory;
        protected int historyIndex;

        public GameState GameState { get => game.GameState; }
        public ITextView HomeView { get; protected set; }
        public IListView<GameMode> GameModeView { get; protected set; }
        public ITextView GameView { get; protected set; }
        public IListView<PlayerState> PlayerListView { get; protected set; }
        public IListView<Role> RoleListView { get; protected set; }
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
            inputHistory = new List<(bool cmdMode, string input)>();
            commandMode = true;
            commands = new Dictionary<string, Command>();

            HomeView = new TextView(this, UpdateView, 60, 2);
            GameModeView = new ListView<GameMode>(" # Game Modes", () => game.ActiveGameModes, gm => gm.ToString().ToDisplayName(), 25);
            GameView = new TextView(this, UpdateView, 60, 20);
            PlayerListView = new ListView<PlayerState>(" # Players", () => game.GameState.Players, p => p.Dead ? "" : game.GameState.ToName(p.Self, true), 25);
            RoleListView = new ListView<Role>(" # Role List", () => game.GameState.Roles, r => r.ToString().ToDisplayName(), 25);
            GraveyardView = new ListView<PlayerState>(" # Graveyard", () => game.GameState.Graveyard, ps => game.GameState.ToName(ps, true), 40);
            TeamView = new ListView<PlayerState>(" # Team", () => game.GameState.Team, ps => !ps.Dead || ps.Role == Role.DISGUISER ? game.GameState.ToName(ps, true) : "", 40);
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
            
            RegisterCommand(helpCommand = new CommandGroup("View a list of available commands", this, "Topic", "Topics", cmd => helpView.Topic = null, ~CommandContext.NONE), "help", "?");
            RegisterCommand(new CommandGroup("Open the {0} view", this, "View", "Views")
                .Register(new Command("Open the help view", ~CommandContext.NONE, cmd => OpenSideView(helpView)), "help")
                .Register(new Command("Open the game modes view", CommandContext.HOME, cmd => OpenSideView(GameModeView)), "modes")
                .Register(new Command("Open the role list view", CommandContext.LOBBY | CommandContext.GAME, cmd => OpenSideView(RoleListView)), "roles", "rolelist")
                .Register(new Command("Open the player list view", CommandContext.LOBBY | CommandContext.GAME, cmd => OpenSideView(PlayerListView)), "players", "playerlist")
                .Register(new Command("Open the graveyard view", CommandContext.GAME, cmd => OpenSideView(GraveyardView)), "graveyard")
                .Register(new Command("Open the team view", CommandContext.GAME, cmd => OpenSideView(TeamView)), "team")
                .Register(new Command("Open the LW/DN view", CommandContext.GAME, cmd => OpenSideView(LastWillView)), "lw", "dn", "lastwill", "deathnote"), "open");
            RegisterCommand(new CommandGroup("Close the {0} view", this, "View", "Views")
                .Register(new Command("Close the help view", ~CommandContext.NONE, cmd => CloseSideView(helpView)), "help")
                .Register(new Command("Close the game modes view", CommandContext.HOME, cmd => CloseSideView(GameModeView)), "modes")
                .Register(new Command("Close the role list view", CommandContext.LOBBY | CommandContext.GAME, cmd => CloseSideView(RoleListView)), "roles", "rolelist")
                .Register(new Command("Close the player list view", CommandContext.LOBBY | CommandContext.GAME, cmd => CloseSideView(PlayerListView)), "players", "playerlist")
                .Register(new Command("Close the graveyard view", CommandContext.GAME, cmd => CloseSideView(GraveyardView)), "graveyard")
                .Register(new Command("Close the team view", CommandContext.GAME, cmd => CloseSideView(TeamView)), "team")
                .Register(new Command("Close the LW/DN view", CommandContext.GAME, cmd => CloseSideView(LastWillView)), "lw", "dn", "lastwill", "deathnote"), "close");
            RegisterCommand(new Command("Redraw the whole screen", ~CommandContext.NONE, cmd => RedrawAll()), "redraw");
            RegisterCommand(new Command<Option<Player>>("Edit your LW or view {0}'s", CommandContext.GAME, ArgumentParsers.Optional(ArgumentParsers.Player(this)), (cmd, opTarget) =>
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
            RegisterCommand(new Command<Option<Player>>("Edit your DN or view {0}'s", CommandContext.GAME, ArgumentParsers.Optional(ArgumentParsers.Player(this)), (cmd, opTarget) =>
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
            foreach (string name in names) commands[name] = command;
            helpCommand.Register(new Command(command.Description, command.UsableContexts, cmd => helpView.Topic = (command, names)), names);
            foreach (ArgumentParser parser in command.Parsers) helpCommand.Register(new Command(parser.Description, ~CommandContext.NONE, cmd => helpView.Topic = (parser, parser.HelpNames)), parser.HelpNames);
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
                inputHistory.Clear();
                game.GameState.Timer = 0;
                game.GameState.TimerText = null;
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
                    Console.CursorTop = fullHeight - 1;
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
                    foreach (AbstractView view in sideViews) maxMinWidth = Math.Max(view.GetMinimumWidth(), maxMinWidth);
                    if (maxMinWidth != sideWidth) RedrawAll();
                    if (sideViews.Where(v => v.GetMinimumWidth() > sideWidth).Any())
                    {
                        RedrawAll();
                        return;
                    }
                    RedrawTimer();
                    int lastSideHeight = 1;
                    sideHeight = 0;
                    sideEnd = fullHeight - 2;
                    foreach (AbstractView view in sideViews)
                    {
                        Console.CursorLeft = fullWidth - sideWidth - 1;
                        if (lastSideHeight != 0)
                        {
                            if (++sideHeight < fullHeight)
                            {
                                Console.CursorTop = fullHeight - sideHeight - 1;
                                Console.Write("".PadRight(sideWidth, '-'));
                                Console.CursorLeft = fullWidth - sideWidth - 1;
                            }
                        }
                        /*lock (view)
                        {
                            lastSideHeight = view.GetFullHeight();
                            if (lastSideHeight == 0) continue;
                            Console.CursorTop = Math.Max(0, fullHeight - sideHeight - lastSideHeight - 1);
                            int lineIndex = Math.Max(0, lastSideHeight + sideHeight - fullHeight + 1);
                            view.Draw(sideWidth, lastSideHeight - lineIndex, lineIndex);
                            sideHeight += lastSideHeight -= lineIndex;
                        }
                        if (sideHeight >= fullHeight - 1)
                        {
                            sideHeight = fullHeight - 1;
                            break;
                        }*/
                        lock (view)
                        {
                            sideHeight += lastSideHeight = view.GetFullHeight();
                            view.DrawOffscreen(sideWidth, lastSideHeight, fullHeight - sideHeight - 1, fullWidth - sideWidth - 1);
                        }
                    }
                    for (int currentLine = 0; currentLine < fullHeight - sideHeight - 1; currentLine++)
                    {
                        Console.CursorTop = currentLine;
                        Console.CursorLeft = mainWidth;
                        Console.Write("".PadRight(sideWidth + 1));
                    }
                    for (int currentLine = 0; currentLine < fullHeight; currentLine++)
                    {
                        Console.CursorTop = currentLine;
                        Console.CursorLeft = mainWidth;
                        Console.Write('|');
                        if (currentLine < fullHeight - 1)
                        {
                            Console.CursorLeft = fullWidth - 2;
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

        protected void RedrawCursor()
        {
            lock (drawLock)
            {
                Console.CursorTop = fullHeight - 1;
                Console.CursorLeft = 0;
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(commandMode ? "/ " : "> ");
                if (willContext != null || inputBuffer.Length == 0) Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write((willContext != null ? EDITING_STATUS : inputBuffer.Length == 0 ? _StatusLine ?? (commandMode ? DEFAULT_COMMAND_STATUS : DEFAULT_STATUS) : inputBuffer.ToString()).PadRightHard(mainWidth - 2));
                Console.ResetColor();
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
                foreach (AbstractView view in sideViews) sideWidth = Math.Max(sideWidth, view.GetMinimumWidth());
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
            if (willContext != null)
            {
                willContext.MoveCursor();
                return;
            }
            Console.CursorTop = fullHeight - 1;
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
                                }
                                inputBuffer.Insert(bufferIndex++, key.KeyChar);
                                RedrawCursor();
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
                                RedrawCursor();
                                break;
                            }
                            if (mainView == GameView) commandMode = false;
                            inputBuffer.Clear();
                            bufferIndex = 0;
                            RedrawCursor();
                            break;
                        case ConsoleKey.PageUp:
                            ScrollMainView(-1);
                            break;
                        case ConsoleKey.PageDown:
                            ScrollMainView(1);
                            break;
                        case ConsoleKey.End:
                            if (willContext != null)
                            {
                                willContext.MoveCursorEnd();
                                ResetCursor();
                                break;
                            }
                            bufferIndex = inputBuffer.Length;
                            Console.CursorLeft = bufferIndex + 2;
                            break;
                        case ConsoleKey.Home:
                            if (willContext != null)
                            {
                                willContext.MoveCursorHome();
                                ResetCursor();
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
                                willContext.MoveCursorUp();
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
                                willContext.MoveCursorDown();
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
                                RedrawCursor();
                            }
                            break;
                    }
                }
            }
            while (key.Key != ConsoleKey.Enter || willContext != null);
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

        protected void ScrollMainView(int lines)
        {
            lock (drawLock)
            {
                // TODO: Change this to just move the entire side, update the views' position tracking, and fill in the missing lines.
                mainView.Scroll(lines);
                sideEnd -= lines = sideEnd - Math.Max(fullHeight - 2, Math.Min(sideHeight - 1, sideEnd - lines));
                /*if (lines > 0)
                {
                    int line = sideEnd - sideHeight, lastHeight = 0;
                    foreach (AbstractView sideView in ((IEnumerable<AbstractView>)sideViews).Reverse())
                    {
                        if (line > 0 && line < fullHeight - 1 && lastHeight != 0)
                        {
                            Console.CursorTop = ++line + 1;
                            Console.CursorLeft = fullWidth - sideWidth - 1;
                            Console.Write("".PadRight(sideWidth, '-'));
                        }
                        line += lastHeight = sideView.Move(lines, fullHeight - 2);
                    }
                }
                else if (lines < 0)
                {
                    int line = sideEnd, lastHeight = 0;
                    foreach (AbstractView sideView in sideViews)
                    {
                        if (line > 0 && line < fullHeight - 1 && lastHeight != 0)
                        {
                            Console.CursorTop = --line;
                            Console.CursorLeft = fullWidth - sideWidth - 1;
                            Console.Write("".PadRight(sideWidth, '-'));
                        }
                        line -= lastHeight = sideView.Move(lines, fullHeight - 2);
                    }
                }*/
                if (lines > 0)
                {
                    Console.MoveBufferArea(mainWidth + 1, lines, sideWidth, fullHeight - lines - 2, mainWidth + 1, 0);
                    int line = sideEnd, lastHeight = 0;
                    foreach (AbstractView sideView in sideViews)
                    {
                        if (lastHeight != 0 && line < fullHeight - 1 && line >= fullHeight - lines - 1)
                        {
                            Console.CursorTop = --line;
                            Console.CursorLeft = mainWidth + 1;
                            Console.Write("".PadRight(sideWidth, '-'));
                        }
                        line -= lastHeight = sideView.Move(lines, line, fullHeight - 2);
                    }
                }
                else if (lines < 0)
                {
                    Console.MoveBufferArea(mainWidth + 1, 0, sideWidth, fullHeight + lines - 2, mainWidth + 1, -lines);
                    int line = sideEnd, lastHeight = 0;
                    foreach (AbstractView sideView in sideViews)
                    {
                        if (lastHeight != 0 && line > 0 && line <= -lines)
                        {
                            Console.CursorTop = --line;
                            Console.CursorLeft = mainWidth + 1;
                            Console.Write("".PadRight(sideWidth, '-'));
                        }
                        line -= lastHeight = sideView.Move(lines, line, 0);
                    }
                }
                ResetCursor();
            }
        }
    }

    abstract class AbstractView : IView
    {
        protected int minimumWidth;
        protected int minimumHeight;

        protected int lastDrawnTop;
        protected int lastDrawnLeft;
        protected int lastStartLine;
        protected int lastWidth;
        protected int lastHeight;
        protected int lastFullHeight;

        protected AbstractView(int minimumWidth, int minimumHeight)
        {
            this.minimumWidth = minimumWidth;
            this.minimumHeight = minimumHeight;
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
            Console.CursorTop = Math.Max(lastDrawnTop, 0);
            Console.CursorLeft = lastDrawnLeft;
            int offset = Math.Min(0, lastDrawnTop);
            for (int drawn = DrawUnsafe(lastWidth, lastHeight + offset, lastStartLine - offset); drawn < lastHeight + offset; drawn++)
            {
                Console.CursorTop = lastDrawnTop - offset + drawn;
                Console.CursorLeft = lastDrawnLeft;
                Console.Write("".PadRight(lastWidth));
            }
            return RedrawResult.SUCCESS;
        }
        public virtual int Scroll(int lines)
        {
            if (lastWidth == 0 || lastHeight == 0) return 0;
            int fullHeight = GetFullHeight();
            lastStartLine += lines = Math.Min(Math.Max(fullHeight - lastHeight, 0) - lastStartLine, Math.Max(-lastStartLine, lines));
            if (lines > 0)
            {
                Console.MoveBufferArea(lastDrawnLeft, lastDrawnTop + lines, lastWidth, lastHeight - lines, lastDrawnLeft, lastDrawnTop);
                int drawLines = Math.Min(lines, Math.Max(0, fullHeight - lastStartLine));
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
                int drawLines = Math.Min(lines, fullHeight - lastStartLine);
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
        public virtual int Move(int lines, int pos, int drawOffset)
        {
            lastDrawnTop -= lines;
            if (lines > 0)
            {
                // pos - lastHeight <= View < pos
                // drawOffset - lines - 1 <= Draw < drawOffset - 1
                int drawLower = Math.Max(pos - lastHeight, drawOffset - lines);
                int drawUpper = Math.Min(pos, drawOffset);
                if (drawUpper - drawLower > 0)
                {
                    Console.CursorTop = drawLower;
                    Console.CursorLeft = lastDrawnLeft;
                    DrawUnsafe(lastWidth, drawUpper - drawLower, lastStartLine + drawLower - pos + lastHeight);
                }
            }
            else if (lines < 0)
            {
                // pos - lastHeight <= View < pos
                // drawOffset <= Draw < drawOffset - lines
                int drawLower = Math.Max(--pos - lastHeight, drawOffset);
                int drawUpper = Math.Min(pos, drawOffset - lines);
                if (drawUpper - drawLower > 0)
                {
                    Console.CursorTop = drawLower;
                    Console.CursorLeft = lastDrawnLeft;
                    DrawUnsafe(lastWidth, drawUpper - drawLower, lastStartLine + drawLower - pos + lastHeight);
                }
            }
            /*if (lastWidth == 0 || lastHeight == 0) return lastHeight;
            if (lines > 0)
            {
                int offset = Math.Min(0, lastDrawnTop -= lines);
                int moveTop = Math.Max(0, lastDrawnTop);
                int moveHeight = Math.Min(lastHeight + offset, screenHeight - moveTop - lines);
                if (moveHeight > 0) Console.MoveBufferArea(lastDrawnLeft, moveTop + lines, lastWidth, moveHeight, lastDrawnLeft, moveTop);
                int drawLines = Math.Min(lines, screenHeight - moveTop);
                if (drawLines > 0)
                {
                    Console.CursorTop = screenHeight - lines;
                    Console.CursorLeft = lastDrawnLeft;
                    DrawUnsafe(lastWidth, drawLines, lastStartLine - offset + moveHeight);
                }
            }
            else if (lines < 0)
            {
                lines = -lines;
                int offset = Math.Min(0, lastDrawnTop);
                int moveTop = Math.Max(0, lastDrawnTop);
                int moveHeight = Math.Min(lastHeight + offset, screenHeight - moveTop - lines);
                if (moveHeight > 0) Console.MoveBufferArea(lastDrawnLeft, moveTop, lastWidth, moveHeight, lastDrawnLeft, moveTop + lines);
                int drawLines = Math.Min(lines, lastStartLine - offset);
                offset = Math.Min(0, lastDrawnTop += lines);
                moveTop = Math.Max(0, lastDrawnTop);
                if (drawLines > 0)
                {
                    Console.CursorTop = moveTop;
                    Console.CursorLeft = lastDrawnLeft;
                    DrawUnsafe(lastWidth, drawLines, lastStartLine - offset);
                }
            }*/
            return lastHeight;
        }
        protected abstract int DrawUnsafe(int width, int height, int startLine = 0);
    }

    class TextView : AbstractView, ITextView
    {
        public List<FormattedString> Lines { get; protected set; } = new List<FormattedString>();

        protected ITextUI ui;
        protected Action<AbstractView> append;

        public TextView(ITextUI ui, Action<AbstractView> append, int minimumWidth, int minimumHeight) : base(minimumWidth, minimumHeight)
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

        public void AppendLine(FormattedString format, params object[] args) => AppendLine(format.Format(args));

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

        protected override int DrawUnsafe(int width, int height, int startLine = 0)
        {
            lock (Lines)
            {
                int cursorOffset = Console.CursorLeft;
                int lineIndex = 0;
                for (; startLine < Lines.Count && lineIndex < height; startLine++, lineIndex++)
                {
                    Console.ForegroundColor = Lines[startLine].Foreground;
                    Console.BackgroundColor = Lines[startLine].Background;
                    Console.Write(Lines[startLine].Value.PadRightHard(width));
                    Console.CursorTop++;
                    Console.CursorLeft = cursorOffset;
                }
                Console.ResetColor();
                return lineIndex;
            }
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
                    Console.Write(map(list[startLine]).PadRightHard(width));
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

        public WillView() : base(WILL_WIDTH, WILL_HEIGHT + 1) => Title = Value = "";

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

        public void MoveCursor()
        {
            Console.CursorTop = lastDrawnTop + 1;
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
        }

        public void MoveCursorDown()
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

        public void MoveCursorUp()
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

        public void MoveCursorHome()
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

        public void MoveCursorEnd()
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

        public void Save() => save(Value.ToString());

        public void Clear()
        {
            Value.Clear();
            CursorIndex = 0;
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

        public HelpView(Dictionary<string, Command> commands, Func<CommandContext> getContext, Action showHelp, int minimumWidth, int minimumHeight) : base(minimumWidth, minimumHeight)
        {
            this.commands = commands;
            this.getContext = getContext;
            this.showHelp = showHelp;
        }

        public override int GetFullHeight()
        {
            CommandContext context = getContext();
            return _Topic == null ? commands.Values.Where(c => (c.UsableContexts & context) > 0).Distinct().Count() * 2 + 1 : _Topic.Value.cmd.Documentation.Count() + 2;
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
                foreach (KeyValuePair<string, Command> command in commands.GroupBy(c => c.Value).Select(c => c.First()).Where(c => (c.Value.UsableContexts & context) > 0))
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
                foreach (string line in _Topic.Value.cmd.Documentation)
                {
                    if (++currentLine > startLine)
                    {
                        if (lineIndex++ >= height) return lineIndex;
                        Console.Write(line.PadRightHard(width));
                        Console.CursorTop++;
                        Console.CursorLeft = cursorOffset;
                    }
                }
            }
            return lineIndex;
        }
    }
}
