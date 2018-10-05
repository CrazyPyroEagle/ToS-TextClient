using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using ToSParser;

namespace ToSTextClient
{
    public class ViewRegistry
    {
        public IExceptionView Exception { get; protected set; }
        public IAuthView Auth { get; protected set; }
        public IHomeView Home { get; protected set; }
        public IView GameModes { get; protected set; }
        public IInputView Settings { get; protected set; }
        public IGameView Game { get; protected set; }
        public IView Players { get; protected set; }
        public IView Roles { get; protected set; }
        public IView Graveyard { get; protected set; }
        public IView Team { get; protected set; }
        public IWillView LastWill { get; protected set; }
        public IView Winners { get; protected set; }
        public EditableWillView MyLastWill { get; protected set; }
        public EditableWillView MyDeathNote { get; protected set; }
        public EditableWillView MyForgedWill { get; protected set; }
        public HelpView Help { get; protected set; }

        public ViewRegistry(ITextUI ui)
        {
            TextClient GetGame() => ui.Game;
            ui.RegisterMainView(Exception = new ExceptionView(), "exception");
            ui.RegisterMainView(Auth = new AuthView(), "auth", "authentication", "login");
            ui.RegisterMainView(Home = new HomeView(CommandContext.HOME.Set(), 60, 2, GetGame), "home");
            ui.RegisterSideView(GameModes = new ListView(" # Game Modes", () => ui.Game.ActiveGameModes.Select(gm => ui.Game.Resources.GetMetadata(gm)).Where(gm => gm.PermissionLevel <= ui.Game.PermissionLevel).Select(gm => (FormattedString)gm.Name), CommandContext.HOME.Set(), 25), "modes", "game modes", "gamemodes");
            ui.RegisterSideView(Settings = new SettingsView(GetGame), "settings", "options");
            ui.RegisterMainView(Game = new GameView(CommandExtensions.IsInLobbyOrGame, 60, 20, () => ui.Game.GameState), "game");
            ui.RegisterSideView(Players = new ListView(" # Players", () => ui.Game.GameState.Players.Select(ps => (FormattedString)(ps.Dead ? "" : ui.Game.GameState.ToName(ps.ID, true))), CommandExtensions.IsInLobbyOrGame, 25), "players", "playerlist", "player list");
            ui.RegisterSideView(Roles = new ListView(" # Roles", () => ui.Game.GameState.Roles.Select(r => ui.Game.Resources.Of(r)), CommandExtensions.IsInLobbyOrGame, 25), "roles", "rolelist", "role list");
            ui.RegisterSideView(Graveyard = new ListView(" # Graveyard", () => ui.Game.GameState.Graveyard.Select(ps => ui.Game.GameState.ToName(ps, true)), CommandExtensions.IsInGame, 40), "graveyard", "deaths");
            ui.RegisterSideView(Team = new ListView(" # Team", () => ui.Game.GameState.Team.Select(ps => !ps.Dead || ps.Role == Role.DISGUISER ? ui.Game.GameState.ToName(ps, true) : ""), CommandExtensions.IsInGame, 40), "team", "teammates");
            ui.RegisterSideView(LastWill = new WillView(CommandExtensions.IsInGame), "lw", "dn", "lastwill", "deathnote", "last will", "death note");
            ui.RegisterSideView(Winners = new ListView(" # Winners", () => ui.Game.GameState.Winners.Select(p => (FormattedString)ui.Game.GameState.ToName(p, true)), CommandContext.GAME_END.Set(), 25), "winners", "winnerlist", "winner list");
            ui.RegisterSideView(MyLastWill = new EditableWillView(CommandExtensions.IsInGame), "mlw", "mylastwill", "my lastwill", "my last will");
            ui.RegisterSideView(MyDeathNote = new EditableWillView(context => CommandExtensions.IsInGame(context) && ui.Game.GameState.Role.HasDeathNote()), "mdn", "mydeathnote", "my deathnote", "my death note");
            ui.RegisterSideView(MyForgedWill = new EditableWillView(context => CommandExtensions.IsInGame(context) && ui.Game.GameState.Role == Role.FORGER), "mfw", "myforgedwill", "my forgedwill", "my forged will");
            ui.RegisterSideView(Help = new HelpView(ui.Commands, () => ui.CommandContext, 40, 1), "?", "h", "help");
        }
    }

    public abstract class BaseView : IView
    {
        public virtual int MinimumWidth { get; protected set; }

        public virtual int MinimumHeight { get; protected set; }

        public virtual IPinnedView PinnedView { get; set; }

        public event Action<string, Exception> OnException;
        public event Action OnTextChange;

        protected Func<CommandContext, bool> isAllowed;

        protected BaseView(Func<CommandContext, bool> isAllowed, int minimumWidth, int minimumHeight, IPinnedView pinned)
        {
            this.isAllowed = isAllowed;
            MinimumWidth = minimumWidth;
            MinimumHeight = minimumHeight;
            PinnedView = pinned;
        }

        public abstract IEnumerable<FormattedString> Lines(int width);

        public virtual void Redraw() => OnTextChange?.Invoke();

        public virtual bool IsAllowed(CommandContext activeContext) => isAllowed(activeContext);

        protected void Handle(string msg, Exception ex) => OnException?.Invoke(msg, ex);
    }

    public abstract class BasePinnedView : BaseView, IPinnedView
    {
        protected BasePinnedView(Func<CommandContext, bool> isAllowed, int minimumWidth, int minimumHeight, IPinnedView pinned) : base(isAllowed, minimumWidth, minimumHeight, pinned) { }

        public virtual IEnumerable<INamedTimer> Timers => Enumerable.Empty<INamedTimer>();

        protected class BaseNamedTimer : INamedTimer
        {
            public virtual string Name { get; protected set; }
            public virtual int Value { get => _Value; set { _Value = value; parent.Redraw(); } }

            protected readonly IPinnedView parent;
            protected int _Value;
            private int timerIndex;

            public BaseNamedTimer(IPinnedView parent, string name)
            {
                this.parent = parent;
                Name = name;
            }

            public virtual void Set(int value)
            {
                Value = value;
                Task.Run(UpdateTimer);
            }

            protected virtual async Task UpdateTimer()
            {
                for (int thisInc = ++timerIndex; timerIndex == thisInc && Value > 0; Value--) await Task.Delay(1000);
            }
        }

        protected class BaseEditableNamedTimer : BaseNamedTimer, IEditableNamedTimer
        {
            public BaseEditableNamedTimer(IPinnedView parent, string name) : base(parent, name) { }

            public void Set(string name, int value)
            {
                Name = name;
                Set(value);
            }
        }
    }

    public abstract class BaseInputView : BaseView, IInputView
    {
        public virtual (int x, int y) Cursor => (0, 0);

        public event Action OnCursorChange;
        protected event Action<char> OnChar;
        protected event Action OnBackspace;
        protected event Action OnDelete;
        protected event Action OnUpArrow;
        protected event Action OnDownArrow;
        protected event Action OnLeftArrow;
        protected event Action OnRightArrow;
        protected event Action OnHome;
        protected event Action OnEnd;
        protected event Action OnEnter;

        protected BaseInputView(Func<CommandContext, bool> isAllowed, int minimumWidth, int minimumHeight, IPinnedView pinned) : base(isAllowed, minimumWidth, minimumHeight, pinned) { }

        public virtual void Close() { }

        public virtual void KeyPress(ConsoleKeyInfo key)
        {
            switch (key.Key)
            {
                default:
                    if (OnChar != null && !char.IsControl(key.KeyChar)) OnChar(key.KeyChar);
                    break;
                case ConsoleKey.Backspace:
                    OnBackspace?.Invoke();
                    break;
                case ConsoleKey.Delete:
                    OnDelete?.Invoke();
                    break;
                case ConsoleKey.UpArrow:
                    OnUpArrow?.Invoke();
                    break;
                case ConsoleKey.DownArrow:
                    OnDownArrow?.Invoke();
                    break;
                case ConsoleKey.LeftArrow:
                    OnLeftArrow?.Invoke();
                    break;
                case ConsoleKey.RightArrow:
                    OnRightArrow?.Invoke();
                    break;
                case ConsoleKey.Home:
                    OnHome?.Invoke();
                    break;
                case ConsoleKey.End:
                    OnEnd?.Invoke();
                    break;
                case ConsoleKey.Enter:
                    OnEnter?.Invoke();
                    break;
            }
        }

        protected void CursorChange() => OnCursorChange?.Invoke();
    }

    public class AuthView : BaseInputView, IAuthView
    {
        public FormattedString Status { set { _Status = value; Redraw(); } }

        public override (int x, int y) Cursor => (selectedLine == 1 ? usernameCursor + 10 : selectedLine == 2 && password.Length == 0 ? 10 : 18, selectedLine);

        public event Action<Socket, string, SecureString> OnAuthenticate;

        private int selectedLine;
        private Host selectedHost;
        private StringBuilder username;
        private int usernameCursor;
        private SecureString password;
        private int passwordCursor;
        private FormattedString _Status;

        public AuthView() : base(CommandContext.AUTHENTICATING.Set(), 30, 4, null)
        {
            selectedLine = 1;
            username = new StringBuilder(20);
            password = new SecureString();
            OnChar += Insert;
            OnBackspace += Backspace;
            OnDelete += Delete;
            OnUpArrow += UpArrow;
            OnDownArrow += DownArrow;
            OnLeftArrow += LeftArrow;
            OnRightArrow += RightArrow;
            OnHome += Home;
            OnEnd += End;
            OnEnter += Enter;
        }

        public override IEnumerable<FormattedString> Lines(int width)
        {
            yield return FormattedString.Concat(ToName(Host.Live), " / ", ToName(Host.PTR), " / ", ToName(Host.Local));
            yield return FormattedString.Concat("Username: ", username.ToString());
            yield return password.Length > 0 ? "Password: (hidden)" : "Password:";
            if (_Status != null) yield return _Status;
        }

        public override void Close()
        {
            selectedLine = 1;
            username.Clear();
            usernameCursor = 0;
            password.Clear();
            passwordCursor = 0;
        }

        private void Insert(char c)
        {
            if (selectedLine == 1 && username.Length < 20 && c != ' ') username.Insert(usernameCursor++, c);
            else if (selectedLine == 2) password.InsertAt(passwordCursor++, c);
            else return;
            Redraw();
        }

        private void Backspace()
        {
            if (selectedLine == 1 && usernameCursor > 0) username.Remove(--usernameCursor, 1);
            else if (selectedLine == 2 && passwordCursor > 0) password.RemoveAt(--passwordCursor);
            else return;
            Redraw();
        }

        private void Delete()
        {
            if (selectedLine == 1 && usernameCursor < username.Length) username.Remove(usernameCursor, 1);
            else if (selectedLine == 2 && passwordCursor < password.Length) password.RemoveAt(passwordCursor);
            else return;
            Redraw();
        }

        private void UpArrow()
        {
            if (selectedLine <= 0) return;
            selectedLine--;
            CursorChange();
        }

        private void DownArrow()
        {
            if (selectedLine >= 2) return;
            selectedLine++;
            CursorChange();
        }

        private void LeftArrow()
        {
            if (selectedLine == 0 && selectedHost > 0)
            {
                selectedHost--;
                Redraw();
                return;
            }
            else if (selectedLine == 1 && usernameCursor > 0) usernameCursor--;
            else if (selectedLine == 2 && passwordCursor > 0) passwordCursor--;
            else return;
            CursorChange();
        }

        private void RightArrow()
        {
            if (selectedLine == 0 && selectedHost < Host.Local)
            {
                selectedHost++;
                Redraw();
                return;
            }
            else if (selectedLine == 1 && usernameCursor < username.Length) usernameCursor++;
            else if (selectedLine == 2 && passwordCursor < password.Length) passwordCursor++;
            else return;
            CursorChange();
        }

        private void Home()
        {
            if (selectedLine == 1) usernameCursor = 0;
            else if (selectedLine == 2) passwordCursor = 0;
            else return;
            CursorChange();
        }

        private void End()
        {
            if (selectedLine == 1) usernameCursor = username.Length;
            else if (selectedLine == 2) passwordCursor = password.Length;
            else return;
            CursorChange();
        }

        private void Enter()
        {
            if (selectedLine < 2) DownArrow();
            else
            {
                try
                {
                    Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.Connect(GetHost(selectedHost), 3600);
                    OnAuthenticate?.Invoke(socket, username.ToString(), password);
                    Close();
                }
                catch (SocketException)
                {
                    Status = ("Failed to connect to the server: check your internet connection", TextClient.RED);
                    Close();
                }
                catch (Exception e)
                {
                    Handle("Failed to authenticate", e);
                }
            }
        }

        private FormattedString ToName(Host host) => (host.ToString(), host == selectedHost ? TextClient.GREEN : TextClient.WHITE, TextClient.BLACK);

        private string GetHost(Host host)
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

        private enum Host
        {
            Live, PTR, Local
        }
    }

    public class TextView : BaseView, ITextView
    {
        public event Action<int, FormattedString> OnAppend;
        public event Action<int, FormattedString> OnReplace;

        private readonly IList<FormattedString> _Lines;

        public TextView(Func<CommandContext, bool> isAllowed, int minimumWidth, int minimumHeight, IPinnedView pinned = null) : base(isAllowed, minimumWidth, minimumHeight, pinned) => _Lines = new List<FormattedString>();

        public override IEnumerable<FormattedString> Lines(int width) => _Lines;

        public void AppendLine(FormattedString text)
        {
            _Lines.Add(text);
            OnAppend?.Invoke(_Lines.Count - 1, text);
        }

        public void AppendLine(FormattedString format, params object[] args) => AppendLine(FormattedString.Format(format, args));

        public void ReplaceLine(int index, FormattedString text)
        {
            _Lines.SafeReplace(index, text);
            OnReplace?.Invoke(index, text);
        }

        public void ReplaceLine(int index, FormattedString format, params object[] args) => ReplaceLine(index, FormattedString.Format(format, args));

        public void Clear()
        {
            _Lines.Clear();
            Redraw();
        }
    }

    public class HomeView : TextView, IHomeView
    {
        public INamedTimer FWotDTimer { get; protected set; }
        public INamedTimer QueueTimer { get; protected set; }

        public HomeView(Func<CommandContext, bool> isAllowed, int minimumWidth, int minimumHeight, Func<TextClient> getGame) : base(isAllowed, minimumWidth, minimumHeight) => PinnedView = new UserInfoView(this, getGame);

        protected class UserInfoView : BasePinnedView
        {
            public override IEnumerable<INamedTimer> Timers
            {
                get
                {
                    yield return parent.FWotDTimer;
                    yield return parent.QueueTimer;
                }
            }

            private readonly HomeView parent;
            private readonly Func<TextClient> getGame;

            public UserInfoView(HomeView parent, Func<TextClient> getGame) : base(CommandContext.HOME.Set(), 20, 2, null)
            {
                this.parent = parent;
                this.getGame = getGame;
                parent.FWotDTimer = new BaseNamedTimer(this, "FWotD Bonus");
                parent.QueueTimer = new BaseNamedTimer(this, "Queue");
            }

            public override IEnumerable<FormattedString> Lines(int width)
            {
                TextClient game = getGame();
                if (game.Username == null) yield break;
                yield return game.Username;
                yield return string.Format("{0} TP / {1} MP", game.TownPoints, game.MeritPoints);
            }
        }
    }

    public class GameView : TextView, IGameView
    {
        public GameView(Func<CommandContext, bool> isAllowed, int minimumWidth, int minimumHeight, Func<GameState> getState) : base(isAllowed, minimumWidth, minimumHeight, null) => PinnedView = new NameView(this, getState);

        public IEditableNamedTimer PhaseTimer { get; protected set; }

        protected class NameView : BasePinnedView
        {
            public override IEnumerable<INamedTimer> Timers
            {
                get
                {
                    yield return parent.PhaseTimer;
                }
            }

            private readonly GameView parent;
            private readonly Func<GameState> getState;

            public NameView(GameView parent, Func<GameState> getState) : base(CommandExtensions.IsInLobbyOrGame, 23, 2, null)
            {
                this.parent = parent;
                this.getState = getState;
                parent.PhaseTimer = new BaseEditableNamedTimer(this, null);
            }

            public override IEnumerable<FormattedString> Lines(int width)
            {
                GameState state = getState();
                if (state.Self == null) yield break;
                yield return state.ToName(state.Self, true);
            }
        }
    }

    public class ListView : BaseView
    {
        private readonly FormattedString title;
        private readonly Func<IEnumerable<FormattedString>> _Lines;

        public ListView(FormattedString title, Func<IEnumerable<FormattedString>> lines, Func<CommandContext, bool> isAllowed, int minimumWidth) : base(isAllowed, minimumWidth, 1, null)
        {
            this.title = title;
            _Lines = lines;
        }

        public override IEnumerable<FormattedString> Lines(int width) => _Lines().Prepend(title);
    }

    public class WillView : BaseView, IWillView
    {
        internal const int WILL_WIDTH = 40;
        internal const int WILL_HEIGHT = 18;

        public string Title { get => _Title; set { _Title = value; Redraw(); } }
        public string Value { get => _Value; set { _Value = value; Redraw(); } }

        private string _Title;
        private string _Value;

        public WillView(Func<CommandContext, bool> isAllowed) : base(isAllowed, WILL_WIDTH, WILL_HEIGHT + 1, null) => _Title = _Value = "";

        public override IEnumerable<FormattedString> Lines(int width) => Value.Split('\r').SelectMany(s => s.Wrap(width).Select(s2 => (FormattedString)s2)).Prepend(Title);
    }

    public class EditableWillView : BaseInputView, IEditableWillView
    {
        public override (int x, int y) Cursor => (cursorX, cursorY);

        public string Title { get => _Title; set { _Title = value; Redraw(); } }

        public event Action<string> OnSave;

        private readonly StringBuilder value;
        private int cursorIndex;
        private string _Title;
        private int cursorX;
        private int cursorY;
        private int lastWidth;

        public EditableWillView(Func<CommandContext, bool> isAllowed) : base(isAllowed, WillView.WILL_WIDTH, WillView.WILL_HEIGHT, null)
        {
            value = new StringBuilder();
            OnChar += Insert;
            OnBackspace += Backspace;
            OnDelete += Delete;
            OnUpArrow += UpArrow;
            OnDownArrow += DownArrow;
            OnLeftArrow += LeftArrow;
            OnRightArrow += RightArrow;
            OnHome += Home;
            OnEnd += End;
            OnEnter += Enter;
        }

        public override IEnumerable<FormattedString> Lines(int width)
        {
            lastWidth = width;
            return value.ToString().Split('\r').SelectMany(s => s.Wrap(width).Select(s2 => (FormattedString)s2)).Prepend(Title);
        }

        public override void Close() => OnSave?.Invoke(value.ToString());

        private void Insert(char c)
        {
            value.Insert(cursorIndex++, c);
            Redraw();
        }

        private void Backspace()
        {
            if (cursorIndex <= 0) return;
            value.Remove(--cursorIndex, 1);
            Redraw();
        }

        private void Delete()
        {
            value.Remove(cursorIndex, 1);
            Redraw();
        }

        private void UpArrow()
        {
            if (cursorY == 0) return;
            cursorY--;
            int willIndex = 0;
            int lineIndex = 0;
            foreach (string line in value.ToString().Split('\r'))
            {
                foreach (string segment in line.Wrap(lastWidth))
                {
                    if (++lineIndex > cursorY)
                    {
                        lineIndex = Math.Min(line.Length, cursorX);
                        cursorIndex = willIndex + lineIndex;
                        return;
                    }
                    willIndex += segment.Length;
                }
                willIndex++;
            }
            CursorChange();
        }

        private void DownArrow()
        {
            cursorY++;
            int willIndex = 0;
            int lineIndex = 0;
            foreach (string line in value.ToString().Split('\r'))
            {
                foreach (string segment in line.Wrap(lastWidth))
                {
                    if (++lineIndex > cursorY)
                    {
                        lineIndex = Math.Min(line.Length, cursorX);
                        cursorIndex = willIndex + lineIndex;
                        return;
                    }
                    willIndex += segment.Length;
                }
                willIndex++;
            }
            cursorY--;
            CursorChange();
        }

        private void LeftArrow()
        {
            if (cursorIndex <= 0) return;
            cursorIndex--;
            CursorChange();
        }

        private void RightArrow()
        {
            if (cursorIndex >= value.Length) return;
            cursorIndex++;
            CursorChange();
        }

        private void Home()
        {
            cursorX = 0;
            int willIndex = 0;
            int lineIndex = 0;
            foreach (string line in value.ToString().Split('\r'))
            {
                int startLine = lineIndex;
                foreach (string segment in line.Wrap(lastWidth))
                {
                    if (++lineIndex > cursorY)
                    {
                        cursorIndex = willIndex;
                        return;
                    }
                }
                willIndex += line.Length;
                willIndex++;
            }
            CursorChange();
        }

        private void End()
        {
            int willIndex = 0;
            int lineIndex = 0;
            foreach (string line in value.ToString().Split('\r'))
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
                    cursorIndex = willIndex;
                    cursorY = lineIndex - 1;
                    return;
                }
                willIndex++;
            }
            CursorChange();
        }

        private void Enter() => Insert('\r');
    }

    public class HelpView : BaseView, IHelpView
    {
        public (IDocumented cmd, string[] names)? Topic { get => _Topic; set { _Topic = value; OnShowHelp?.Invoke(); } }

        public event Action OnShowHelp;

        protected readonly IReadOnlyDictionary<string, Command> commands;
        protected Func<CommandContext> getContext;
        protected (IDocumented cmd, string[] names)? _Topic;

        public HelpView(IReadOnlyDictionary<string, Command> commands, Func<CommandContext> getContext, int minimumWidth, int minimumHeight) : base(context => true, minimumWidth, minimumHeight, null)
        {
            this.commands = commands;
            this.getContext = getContext;
        }

        public override IEnumerable<FormattedString> Lines(int width)
        {
            CommandContext context = getContext();
            if (Topic == null) return commands.GroupBy(c => c.Value).Select(c => c.First()).Where(c => c.Value.IsAllowed(context)).SelectMany(FormatCommand).Prepend(" # Help");
            else return Topic.Value.cmd.Documentation.Prepend(string.Format(" # Help ({0})", string.Join(", ", Topic.Value.names)), Topic.Value.cmd.Description);
        }

        private IEnumerable<FormattedString> FormatCommand(KeyValuePair<string, Command> command, int width)
        {
            yield return string.Format("  /{0} {1}", command.Key, command.Value.UsageLine);
            yield return command.Value.Description;
        }
    }

    public class SettingsView : BaseInputView
    {
        public override (int x, int y) Cursor => (GetLength(), 2 * selected + 2);

        private readonly Func<TextClient> getGame;
        private int selected;

        public SettingsView(Func<TextClient> getGame) : base(CommandContext.HOME.Set(), 20, 5, null)
        {
            this.getGame = getGame;
            OnUpArrow += UpArrow;
            OnDownArrow += DownArrow;
            OnLeftArrow += LeftArrow;
            OnRightArrow += RightArrow;
        }

        public override IEnumerable<FormattedString> Lines(int width)
        {
            TextClient game = getGame();
            yield return " # Settings";
            yield return "  Share Skin";
            yield return game.ShareSkin ? "Yes" : "No";
            yield return "  Queue Language";
            yield return game.QueueLanguage.ToString().ToDisplayName();
        }

        private void UpArrow()
        {
            if (selected > 0) selected--;
            CursorChange();
        }

        private void DownArrow()
        {
            if (selected < 1) selected++;
            CursorChange();
        }

        private void LeftArrow()
        {
            TextClient game = getGame();
            switch (selected)
            {
                case 0:
                    game.ShareSkin = !game.ShareSkin;
                    break;
                case 1:
                    if (game.QueueLanguage > Language.UNSELECTED) game.QueueLanguage--;
                    break;
                default:
                    return;
            }
            Redraw();
        }

        private void RightArrow()
        {
            TextClient game = getGame();
            switch (selected)
            {
                case 0:
                    game.ShareSkin = !game.ShareSkin;
                    break;
                case 1:
                    if (game.QueueLanguage < Language.SPANISH) game.QueueLanguage++;
                    break;
                default:
                    return;
            }
            Redraw();
        }

        private int GetLength()
        {
            switch (selected)
            {
                case 0:
                    return getGame().ShareSkin ? 3 : 2;
                case 1:
                    return getGame().QueueLanguage.ToString().Length;
            }
            return 0;
        }
    }

    public class ExceptionView : BaseView, IExceptionView
    {
        public Exception Exception { get => _Exception; set { _Exception = value; Redraw(); } }

        private Exception _Exception;

        public ExceptionView() : base(context => true, 60, 10, null) { }

        public override IEnumerable<FormattedString> Lines(int width) => Exception?.ToString()?.Split('\r').Select(s => (FormattedString)s) ?? Enumerable.Empty<FormattedString>().Append("False");
    }
}
