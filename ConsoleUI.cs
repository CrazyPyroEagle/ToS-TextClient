using Optional;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using ToSParser;

using Console = Colorful.Console;

namespace ToSTextClient
{
    public class ConsoleUI : ITextUI
    {
        protected const string DEFAULT_STATUS = "Type /? for help";
        protected const string DEFAULT_COMMAND_STATUS = "Type ? for help";
        protected const string EDITING_STATUS = "Editing, press ESC to close";


        public TextClient Game { get; protected set; }
        public ViewRegistry Views { get; protected set; }
        public string StatusLine { get => _StatusLine; set { _StatusLine = value; RedrawCursor(); } }
        public IReadOnlyDictionary<string, Command> Commands => _Commands;
        public CommandContext CommandContext { get => _CommandContext; set => UpdateCommandMode(value); }
        public bool RunInput { get => _RunInput; set => _RunInput = value; }
        public bool TimerVisible
        {
            get => _TimerVisible;
            set { lock (drawLock) { _TimerVisible = value; RedrawPinned(); } }
        }

        protected readonly object drawLock;

        protected Dictionary<IView, MainViewRenderer> mainRenderers;
        protected Dictionary<IView, SideViewRenderer> sideRenderers;
        protected MainViewRenderer mainView;
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
        protected Dictionary<string, Command> _Commands;
        protected CommandContext _CommandContext;
        protected volatile bool _RunInput = true;
        protected List<(bool cmdMode, string input)> inputHistory;
        protected int historyIndex;

        public event Action<IView, IView> OnSetMainView;
        
        protected CommandGroup helpCommand;
        protected CommandGroup openCommand;
        protected CommandGroup closeCommand;

        public ConsoleUI()
        {
            drawLock = new object();
            inputBuffer = new StringBuilder();
            inputHistory = new List<(bool cmdMode, string input)>();
            commandMode = true;
            _Commands = new Dictionary<string, Command>();
            helpCommand = new CommandGroup("View a list of available commands", this, "Topic", "Topics", cmd => Views.Help.Topic = null, activeContext => true);
            openCommand = new CommandGroup("Open the {0} view", this, "View", "Views");
            closeCommand = new CommandGroup("Close the {0} view", this, "View", "Views");

            mainRenderers = new Dictionary<IView, MainViewRenderer>();
            sideRenderers = new Dictionary<IView, SideViewRenderer>();
            Views = new ViewRegistry(this);
            mainView = mainRenderers[Views.Auth];
            mainRenderers[Views.Game].SideViews.Add(sideRenderers[Views.Players]);
            mainRenderers[Views.Game].SideViews.Add(sideRenderers[Views.Graveyard]);
            mainRenderers[Views.Game].SideViews.Add(sideRenderers[Views.Roles]);
            inputContext = Views.Auth;

            RegisterCommand(helpCommand, "help", "?");
            RegisterCommand(new Command("Open the login view", CommandContext.AUTHENTICATING.Set(), cmd =>
            {
                SetMainView(Views.Auth);
                SetInputContext(Views.Auth);
            }), "login", "auth", "authenticate");
            RegisterCommand(openCommand, "open");
            RegisterCommand(closeCommand, "close");
            RegisterCommand(new Command("Redraw the whole screen", activeContext => true, cmd => RedrawAll()), "redraw");
            RegisterCommand(new Command("Change your settings", Views.Settings.IsAllowed, cmd =>
            {
                OpenSideView(Views.Settings);
                SetInputContext(Views.Settings);
            }), "settings", "options");
            RegisterCommand(new Command<Option<Player>>("Edit your LW or view {0}'s", CommandExtensions.IsInGame, ArgumentParsers.Optional(ArgumentParsers.Player(this)), (cmd, opTarget) =>
            {
                opTarget.Match(target =>
                {
                    PlayerState ps = Game.GameState.Players[(int)target];
                    if (ps.Dead)
                    {
                        Views.LastWill.Title = string.Format(" # (LW) {0}", Game.GameState.ToName(target));
                        Views.LastWill.Value = ps.LastWill;
                        OpenSideView(Views.LastWill);
                    }
                    else StatusLine = string.Format("{0} isn't dead, so you can't see their last will", Game.GameState.ToName(target));
                }, () =>
                {
                    OpenSideView(Views.MyLastWill);
                    SetInputContext(Views.MyLastWill);
                });
            }), "lw", "lastwill");
            RegisterCommand(new Command<Option<Player>>("Edit your DN or view {0}'s", CommandExtensions.IsInGame, ArgumentParsers.Optional(ArgumentParsers.Player(this)), (cmd, opTarget) =>
            {
                opTarget.Match(target =>
                {
                    PlayerState ps = Game.GameState.Players[(int)target];
                    if (ps.Dead)
                    {
                        Views.LastWill.Title = string.Format(" # (DN) {0}", Game.GameState.ToName(target));
                        Views.LastWill.Value = ps.LastWill;
                        OpenSideView(Views.LastWill);
                    }
                    else StatusLine = string.Format("{0} isn't dead, so you can't see their killer's death note", Game.GameState.ToName(target));
                }, () =>
                {
                    OpenSideView(Views.MyLastWill);
                    SetInputContext(Views.MyLastWill);
                    RedrawCursor();
                });
            }), "dn", "deathnote");
            RegisterCommand(new Command("Edit your forged will", context => CommandExtensions.IsInGame(context) && Game.GameState.Role == Role.FORGER, cmd =>
            {
                OpenSideView(Views.MyForgedWill);
                SetInputContext(Views.MyForgedWill);
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
                            if (_Commands.TryGetValue(cmdn, out Command command))
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
            foreach (string name in names) _Commands[name] = command;
            helpCommand.Register(new Command(command.Description, command.IsAllowed, cmd => Views.Help.Topic = (command, names)), names);
            foreach (ArgumentParser parser in command.Parsers) helpCommand.Register(new Command(parser.Description, activeContext => true, cmd => Views.Help.Topic = (parser, parser.HelpNames)), parser.HelpNames);
            Views.Help.Redraw();
        }

        public void RegisterMainView(IView view, params string[] names)
        {
            lock (drawLock)
            {
                MainViewRenderer renderer = mainRenderers[view] = new MainViewRenderer(this, view);
                view.OnTextChange += () => RedrawMainView(renderer);
                view.OnException += HandleException;
                if (view is IAuthView authView) authView.OnAuthenticate += (socket, username, password) => Game = new TextClient(this, socket, username, password);
                if (view.PinnedView != null) RegisterSideView(view.PinnedView);
            }
        }

        public void RegisterSideView(IView view, params string[] names)
        {
            lock (drawLock)
            {
                if (!sideRenderers.ContainsKey(view))
                {
                    SideViewRenderer renderer = sideRenderers[view] = new SideViewRenderer(this, view);
                    view.OnTextChange += () => RedrawSideView(renderer);
                    view.OnException += HandleException;
                    if (view is ITextView textView)
                    {
                        void Redraw(int line, FormattedString text) => view.Redraw();
                        textView.OnAppend += Redraw;
                        textView.OnReplace += Redraw;
                    }
                    if (view is IHelpView helpView) helpView.OnShowHelp += () => OpenSideView(view);
                }
                if (names.Length == 0) return;
                string displayName = names[0].ToDisplayName();
                openCommand.Register(new Command(string.Format("Open the {0} view", displayName), view.IsAllowed, cmd => OpenSideView(view)), names);
                closeCommand.Register(new Command(string.Format("Close the {0} view", displayName), view.IsAllowed, cmd => CloseSideView(view)), names);
            }
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

        public void SetMainView(IView view)
        {
            if (!mainRenderers.TryGetValue(view, out MainViewRenderer renderer)) throw new ArgumentException("attempt to set unregistered view as main view");
            lock (drawLock)
            {
                if (mainView == renderer)
                {
                    RedrawMainView();
                    return;
                }
                OnSetMainView?.Invoke(mainView.View, renderer.View);
                SetInputContext(null);
                mainView = renderer;
                inputHistory.Clear();
                _TimerVisible = false;
                Game.TimerText = null;
                Game.Timer = 0;
                RedrawAll();
            }
        }

        public void OpenSideView(IView view)
        {
            if (!sideRenderers.TryGetValue(view, out SideViewRenderer renderer)) throw new ArgumentException("attempt to open unregistered side view");
            lock (drawLock)
            {
                if (mainView.SideViews.Contains(renderer))
                {
                    RedrawSideView(renderer);
                    return;
                }
                mainView.SideViews.Insert(0, renderer);
                RedrawSideViews();
            }
        }

        public void CloseSideView(IView view)
        {
            if (!sideRenderers.TryGetValue(view, out SideViewRenderer renderer)) throw new ArgumentException("attempt to close unregistered side view");
            lock (drawLock) if (mainView.SideViews.Remove(renderer)) RedrawSideViews();
        }

        public void RedrawTimer()
        {
            lock (drawLock)
            {
                _TimerVisible = true;
                RedrawPinned();
            }
        }

        protected void RedrawMainView()
        {
            lock (drawLock)
            {
                bool state = StartRendering();
                if (mainWidth < mainView.MinimumWidth)
                {
                    RedrawAll();
                    return;
                }
                int mainHeight = mainView.FullHeight;
                mainView.Render(mainWidth, fullHeight - 1, 0, 0, Math.Max(0, mainHeight - fullHeight + 1));
                ResetCursor(state);
            }
        }

        protected void RedrawSideViews()
        {
            lock (drawLock)
            {
                bool state = StartRendering();
                if (mainView.SideViews.Count > 0)
                {
                    int maxMinWidth = 0;
                    foreach (SideViewRenderer view in mainView.SideViews.Where(view => view.View.IsAllowed(_CommandContext))) maxMinWidth = Math.Max(view.MinimumWidth, maxMinWidth);
                    if (maxMinWidth != sideWidth)
                    {
                        RedrawAll();
                        return;
                    }
                    if (sideWidth <= 0) return;
                    pinnedHeight = sideRenderers[mainView.View.PinnedView].FullHeight + (_TimerVisible ? 1 : 0);
                    RedrawPinned();
                    int lastSideHeight = 1;
                    sideHeight = 0;
                    sideEnd = fullHeight - pinnedHeight - 1;
                    foreach (SideViewRenderer view in mainView.SideViews.Where(view => view.View.IsAllowed(_CommandContext)))
                    {
                        Console.CursorLeft = fullWidth - sideWidth - 1;
                        if (lastSideHeight != 0 && sideHeight++ < fullHeight - pinnedHeight)
                        {
                            Console.CursorTop = fullHeight - sideHeight - pinnedHeight;
                            Console.Write("".PadRight(sideWidth, '-'));
                            Console.CursorLeft = fullWidth - sideWidth - 1;
                        }
                        lock (view.View)
                        {
                            sideHeight += lastSideHeight = view.FullHeight;
                            view.Render(sideWidth, fullHeight - pinnedHeight - 1, 0, fullWidth - sideWidth - 1, sideHeight + pinnedHeight - fullHeight);
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
                ResetCursor(state);
            }
        }

        protected void RedrawCursor()
        {
            lock (drawLock)
            {
                bool state = StartRendering();
                Console.CursorTop = fullHeight - 1;
                Console.CursorLeft = 0;
                Console.ForegroundColor = TextClient.WHITE;
                Console.Write(commandMode ? "/ " : "> ");
                if (inputContext != null || inputBuffer.Length == 0) Console.ForegroundColor = TextClient.GRAY;
                Console.Write((inputContext != null ? EDITING_STATUS : inputBuffer.Length == 0 ? _StatusLine ?? (commandMode ? DEFAULT_COMMAND_STATUS : DEFAULT_STATUS) : inputBuffer.ToString()).PadRightHard(mainWidth - 2));
                Console.ForegroundColor = TextClient.WHITE;
                ResetCursor(state);
            }
        }

        protected void RedrawPinned()
        {
            lock (drawLock)
            {
                bool state = StartRendering();
                int pinnedFullHeight = mainView.View.PinnedView != null && sideRenderers.TryGetValue(mainView.View.PinnedView, out SideViewRenderer renderer) ? renderer.FullHeight : 0;
                if (pinnedFullHeight + (_TimerVisible ? 1 : 0) != pinnedHeight) RedrawSideViews();
                else
                {
                    sideRenderers[mainView.View.PinnedView]?.Render(sideWidth, pinnedFullHeight, fullHeight - pinnedHeight, mainWidth + 1);
                    if (_TimerVisible)
                    {
                        Console.CursorTop = fullHeight - 1;
                        Console.CursorLeft = mainWidth + 1;
                        Console.Write((Game.TimerText != null ? string.Format("{0}: {1}", Game.TimerText, Game.Timer) : "").PadRightHard(sideWidth));
                    }
                }
                ResetCursor(state);
            }
        }

        protected void RedrawMainView(MainViewRenderer view)
        {
            lock (drawLock) if (mainView == view) RedrawMainView();
        }

        protected void RedrawSideView(SideViewRenderer view)
        {
            lock (drawLock)
            {
                if (view.View == mainView.View.PinnedView) RedrawPinned();
                if (!mainView.SideViews.Contains(view)) return;
                bool state = StartRendering();
                switch (view.Redraw())
                {
                    case RedrawResult.WIDTH_CHANGED:
                        RedrawAll();
                        return;
                    case RedrawResult.HEIGHT_CHANGED:
                        RedrawSideViews();
                        return;
                }
                ResetCursor(state);
            }
        }

        protected void RedrawAll()
        {
            lock (drawLock)
            {
                Console.CursorVisible = false;
                Console.Clear();
                Console.WindowHeight = Console.BufferHeight = fullHeight = Math.Max(mainView.MinimumHeight + 1, Console.WindowHeight);
                sideWidth = 0;
                foreach (SideViewRenderer view in mainView.SideViews.Where(view => view.View.IsAllowed(_CommandContext))) sideWidth = Math.Max(sideWidth, view.MinimumWidth);
                int minimumWidth = sideWidth + mainView.MinimumWidth + 1;
                int consoleWidth = Console.BufferWidth - 1;
                if (consoleWidth < minimumWidth) Console.BufferWidth = Console.WindowWidth = (consoleWidth = minimumWidth) + 1;
                fullWidth = Console.WindowWidth;
                mainWidth = fullWidth - sideWidth - 2;
                RedrawMainView();
                RedrawCursor();
                RedrawSideViews();
            }
        }

        protected void HandleException(string msg, Exception ex)
        {
            StatusLine = msg;
            Views.Exception.Exception = ex;
            SetMainView(Views.Exception);
        }

        protected bool StartRendering()
        {
            bool end = Console.CursorVisible;
            Console.CursorVisible = false;
            return end;
        }

        protected void ResetCursor(bool end)
        {
            if (inputContext != null)
            {
                int x, y;
                if (mainRenderers.TryGetValue(inputContext, out MainViewRenderer mainRenderer)) (x, y) = mainRenderer.ToAbsolute(inputContext.Cursor);
                else if (sideRenderers.TryGetValue(inputContext, out SideViewRenderer sideRenderer)) (x, y) = sideRenderer.ToAbsolute(inputContext.Cursor);
                else throw new InvalidOperationException("input context is not a main view or a side view");
                if (y < 0) ScrollSideViews(y);
                else if (y >= sideEnd) ScrollSideViews(y - sideEnd + 1);
                Console.CursorTop = y;
                Console.CursorLeft = x;
            }
            else
            {
                Console.CursorTop = fullHeight - 1;
                Console.CursorLeft = bufferIndex + 2;
            }
            Console.CursorVisible = end;
        }

        protected void UpdateCommandMode(CommandContext value)
        {
            lock (drawLock)
            {
                CommandContext old = _CommandContext;
                _CommandContext = value;
                bool oldCommandMode = commandMode;
                if (CommandContext == CommandContext.AUTHENTICATING || CommandContext == CommandContext.HOME) commandMode = true;
                else if (bufferIndex == 0) commandMode = false;
                if (commandMode != oldCommandMode) RedrawCursor();
                if (mainView.SideViews.Where(view => view.View.IsAllowed(old) != view.View.IsAllowed(value)).Count() > 0) RedrawSideViews();
                else Views.Help.Redraw();
            }
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
                    ResetCursor(false);
                    switch (key.Key)
                    {
                        default:
                            if (inputContext != null) inputContext.KeyPress(key);
                            else if (!char.IsControl(key.KeyChar))
                            {
                                if (Console.CursorLeft + 1 >= mainWidth) break;
                                if (!commandMode && inputBuffer.Length == 0 && key.KeyChar == '/')
                                {
                                    commandMode = true;
                                    RedrawCursor();
                                    break;
                                }
                                else if (commandMode && mainView == Views.Game && bufferIndex == 0 && key.KeyChar == '/')
                                {
                                    commandMode = false;
                                }
                                inputBuffer.Insert(bufferIndex++, key.KeyChar);
                                RedrawCursor();
                            }
                            break;
                        case ConsoleKey.Backspace:
                            if (inputContext != null) goto default;
                            if (bufferIndex > 0)
                            {
                                inputBuffer.Remove(--bufferIndex, 1);
                                RedrawCursor();
                            }
                            else if (commandMode && mainView.View == Views.Game)     // Replace mainView.View == Views.Game with an interface method (getter?)
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
                            if (inputContext != null) goto default;
                            break;
                        case ConsoleKey.Escape:
                            if (inputContext != null)
                            {
                                inputContext.Close();
                                if (inputContext != mainView.View) CloseSideView(inputContext);
                                inputContext = null;
                                RedrawCursor();
                                break;
                            }
                            if (mainView.View == Views.Game) commandMode = false;
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
                            if (inputContext != null) goto default;
                            bufferIndex = inputBuffer.Length;
                            Console.CursorLeft = bufferIndex + 2;
                            break;
                        case ConsoleKey.Home:
                            if (inputContext != null) goto default;
                            bufferIndex = 0;
                            Console.CursorLeft = 2;
                            break;
                        case ConsoleKey.LeftArrow:
                            if (inputContext != null) goto default;
                            if (bufferIndex > 0)
                            {
                                bufferIndex--;
                                Console.Write("\b");
                            }
                            break;
                        case ConsoleKey.UpArrow:
                            if (inputContext != null) goto default;
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
                            if (inputContext != null) goto default;
                            if (bufferIndex < inputBuffer.Length)
                            {
                                bufferIndex++;
                                Console.CursorLeft++;
                            }
                            break;
                        case ConsoleKey.DownArrow:
                            if (inputContext != null) goto default;
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
                            if (inputContext != null) goto default;
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

        protected void ScrollViews(int lines)
        {
            lock (drawLock)
            {
                bool state = StartRendering();
                Console.CursorVisible = false;
                mainView.Scroll(lines);
                ScrollSideViews(lines);
                ResetCursor(state);
            }
        }

        protected void ScrollSideViews(int lines)
        {
            sideEnd -= lines = sideEnd - Math.Max(fullHeight - pinnedHeight - 1, Math.Min(sideHeight - 1, sideEnd - lines));
            if (lines > 0)
            {
                Console.MoveBufferArea(mainWidth + 1, lines, sideWidth, fullHeight - lines - pinnedHeight - 1, mainWidth + 1, 0);
                int line = sideEnd, lastHeight = 0;
                foreach (SideViewRenderer sideView in mainView.SideViews)
                {
                    if (lastHeight != 0 && line-- >= fullHeight - lines - pinnedHeight && line < fullHeight - pinnedHeight - 1)
                    {
                        Console.CursorTop = line;
                        Console.CursorLeft = mainWidth + 1;
                        Console.Write("".PadRight(sideWidth, '-'));
                    }
                    line -= lastHeight = sideView.Scroll(lines);
                }
            }
            else if (lines < 0)
            {
                Console.MoveBufferArea(mainWidth + 1, 0, sideWidth, fullHeight + lines - pinnedHeight - 1, mainWidth + 1, -lines);
                int line = sideEnd, lastHeight = 0;
                foreach (SideViewRenderer sideView in mainView.SideViews)
                {
                    if (lastHeight != 0 && line-- > 0 && line < -lines)
                    {
                        Console.CursorTop = line;
                        Console.CursorLeft = mainWidth + 1;
                        Console.Write("".PadRight(sideWidth, '-'));
                    }
                    line -= lastHeight = sideView.Scroll(lines);
                }
            }
        }

        protected abstract class ViewRenderer
        {
            public IView View { get; protected set; }
            public int MinimumWidth => View.MinimumWidth;
            public int MinimumHeight => View.MinimumHeight;
            public int FullHeight => lastFullHeight = View.Lines(lastWidth).Count();

            protected readonly ConsoleUI ui;

            protected int lastWidth;
            protected int lastHeight;
            protected int lastCursorTop;
            protected int lastCursorLeft;
            protected int lastStartLine;
            protected int lastFullHeight;

            public ViewRenderer(ConsoleUI ui, IView view)
            {
                this.ui = ui;
                View = view;
                if (view is IInputView inputView) inputView.OnCursorChange += CursorChange;
            }

            public int Render(int width, int height, int cursorTop, int cursorLeft, int startLine = 0) => RenderStateless(lastWidth = width, lastHeight = height, lastCursorTop = cursorTop, lastCursorLeft = cursorLeft, lastStartLine = startLine);

            public (int x, int y) ToAbsolute((int x, int y) rel) => (lastCursorLeft + rel.x, lastCursorTop - lastStartLine + rel.y);

            public virtual RedrawResult Redraw()
            {
                if (lastWidth < MinimumWidth) return RedrawResult.WIDTH_CHANGED;
                else if (lastHeight < MinimumHeight || View.Lines(lastWidth).Count() != lastFullHeight) return RedrawResult.HEIGHT_CHANGED;
                RenderStateless(lastWidth, Math.Min(lastFullHeight - lastStartLine, lastHeight), lastCursorTop, lastCursorLeft, lastStartLine);
                return RedrawResult.SUCCESS;
            }

            public abstract int Scroll(int lines);

            protected virtual int RenderStateless(int width, int height, int cursorTop, int cursorLeft, int startLine = 0)
            {
                IEnumerable<FormattedString> lines = View.Lines(width);
                lastFullHeight = lines.Count();
                int lineIndex = -Math.Min(0, startLine);
                foreach (FormattedString line in lines.Skip(startLine + lineIndex).Take(height - lineIndex))
                {
                    Console.CursorTop = cursorTop + lineIndex++;
                    Console.CursorLeft = cursorLeft;
                    line.Render(width);
                }
                return lineIndex;
            }

            protected void CursorChange()
            {
                if (ui.inputContext == View) ui.ResetCursor(true);
            }

            protected int SafeCursorTop(int y)
            {
                if (y < 0)
                {
                    ui.ScrollViews(y);
                    y = 0;
                }
                else if (y >= lastHeight)
                {
                    ui.ScrollViews(y - lastHeight + 1);
                    y = lastHeight - 1;
                }
                return y;
            }
        }

        protected class MainViewRenderer : ViewRenderer
        {
            public IList<SideViewRenderer> SideViews { get; }

            public MainViewRenderer(ConsoleUI ui, IView view) : base(ui, view)
            {
                SideViews = new List<SideViewRenderer>();
                if (view is ITextView textView)
                {
                    textView.OnAppend += AppendLine;
                    textView.OnReplace += ReplaceLine;
                }
            }

            public override int Scroll(int lines)
            {
                if (lastWidth == 0 || lastHeight == 0) return 0;
                lastFullHeight = FullHeight;
                lastStartLine += lines = Math.Min(Math.Max(lastFullHeight - lastHeight, 0) - lastStartLine, Math.Max(-lastStartLine, lines));
                if (lines > 0)
                {
                    Console.MoveBufferArea(lastCursorLeft, lastCursorTop + lines, lastWidth, lastHeight - lines, lastCursorLeft, lastCursorTop);
                    int drawLines = Math.Min(lines, Math.Max(0, Math.Min(lastHeight, lastFullHeight - lastStartLine)));
                    if (drawLines > 0) base.RenderStateless(lastWidth, drawLines, lastCursorTop + lastHeight - lines, lastCursorLeft, lastStartLine + lastHeight - lines);
                }
                else if (lines < 0)
                {
                    lines = -lines;
                    Console.MoveBufferArea(lastCursorLeft, lastCursorTop, lastWidth, lastHeight - lines, lastCursorLeft, lastCursorTop + lines);
                    int drawLines = Math.Min(lines, Math.Min(lastHeight, lastFullHeight - lastStartLine));
                    if (drawLines > 0) base.RenderStateless(lastWidth, drawLines, lastCursorTop, lastCursorLeft, lastStartLine);
                    lines = -lines;
                }
                return lines;
            }

            protected override int RenderStateless(int width, int height, int cursorTop, int cursorLeft, int startLine = 0)
            {
                int drawn;
                for (drawn = base.RenderStateless(width, height, cursorTop, cursorLeft, startLine); drawn < height; drawn++)
                {
                    Console.CursorTop = cursorTop + drawn;
                    Console.CursorLeft = cursorLeft;
                    Console.Write("".PadRight(width));
                }
                return drawn;
            }

            protected void AppendLine(int line, FormattedString text)
            {
                /*lock (drawLock)
                {
                    if (view != mainView.View) return;
                    Console.CursorVisible = false;
                    Console.MoveBufferArea(0, 1, mainWidth, fullHeight - 2, 0, 0);
                    mainRenderers[view].Render(mainWidth, 1, fullHeight - 1, 0, line);
                    ResetCursor();
                }*/
                lock (ui.drawLock)
                {
                    if (this != ui.mainView) return;
                    if (line >= ui.fullHeight - 1) Scroll(1);
                    else ui.RedrawMainView();
                }
            }

            protected void ReplaceLine(int line, FormattedString text)
            {
                lock (ui.drawLock)
                {
                    if (this != ui.mainView) return;
                    bool state = ui.StartRendering();
                    base.RenderStateless(lastWidth, lastHeight, 0, 0, -line);
                    ui.ResetCursor(state);
                }
            }
        }

        protected class SideViewRenderer : ViewRenderer
        {
            public SideViewRenderer(ConsoleUI ui, IView view) : base(ui, view) { }

            public override int Scroll(int lines)
            {
                lastStartLine += lines;
                if (lines > 0)
                {
                    int drawLower = Math.Max(-lastStartLine, lastCursorTop + lastHeight - lines);
                    int drawUpper = Math.Min(lastFullHeight - lastStartLine, lastCursorTop + lastHeight);
                    if (drawUpper - drawLower > 0) base.RenderStateless(lastWidth, drawUpper - drawLower, drawLower, lastCursorLeft, lastStartLine + drawLower - lastCursorTop);
                }
                else if (lines < 0)
                {
                    int drawLower = Math.Max(-lastStartLine, lastCursorTop);
                    int drawUpper = Math.Min(lastFullHeight - lastStartLine, lastCursorTop - lines);
                    if (drawUpper - drawLower > 0) base.RenderStateless(lastWidth, drawUpper - drawLower, drawLower, lastCursorLeft, lastStartLine + drawLower - lastCursorTop);
                }
                return lastFullHeight;
            }

            protected override int RenderStateless(int width, int height, int cursorTop, int cursorLeft, int startLine = 0) => base.RenderStateless(width, Math.Min(lastFullHeight - startLine, lastHeight), cursorTop, cursorLeft, startLine);
        }
    }
}
