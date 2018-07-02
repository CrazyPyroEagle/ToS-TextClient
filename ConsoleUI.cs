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
        
        protected EditableWillView myLastWillView;
        protected EditableWillView myDeathNoteView;
        protected EditableWillView myForgedWillView;
        protected TextView helpView;

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
            myLastWillView = new EditableWillView(" # My Last Will", lw => game.GameState.LastWill = lw);
            myDeathNoteView = new EditableWillView(" # My Death Note", dn => game.GameState.DeathNote = dn);
            myForgedWillView = new EditableWillView(" # My Forged Will", fw => game.GameState.ForgedWill = fw);
            helpView = new TextView(this, AppendLine, 40, 1);

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
                    if (input.Length == 0) continue;
                    _StatusLine = null;
                    if (mainView == HomeView)
                    {
                        string[] cmd = input.Split(' ');
                        try
                        {
                            string cmdn = cmd[0].ToLower();
                            if (cmdn == "?" || cmdn == "help")
                            {
                                helpView.Clear();
                                helpView.AppendLine(" # Help");
                                helpView.AppendLine("  /join [Game Mode]");
                                helpView.AppendLine("Join a lobby for [Game Mode]");
                                helpView.AppendLine("  /quit");
                                helpView.AppendLine("Exit the game");
                                helpView.AppendLine("  /redraw");
                                helpView.AppendLine("Redraw the whole screen");
                                OpenSideView(helpView);
                            }
                            else if (cmdn == "join")
                            {
                                if (Enum.TryParse(string.Join(" ", cmd, 1, cmd.Length - 1).ToUpper(), out GameModeID gameMode) && game.ActiveGameModes.Contains(gameMode)) game.Parser.JoinLobby(gameMode);
                                else StatusLine = string.Format("Cannot join game mode: {0}", string.Join(" ", cmd, 1, cmd.Length - 1));
                            }
                            else if (cmdn == "quit")
                            {
                                return;
                            }
                            else if (cmdn == "redraw") RedrawAll();
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine("Failed to parse HomeView command: {0}", (object)input);
                            Debug.WriteLine(e);
                            StatusLine = string.Format("Failed to parse command: {0}", e.Message);
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
                            string cmdn = cmd[0].ToLower();
                            if (cmdn == "?" || cmdn == "help")
                            {
                                helpView.Clear();
                                helpView.AppendLine(" # Help");
                                helpView.AppendLine("  /leave");
                                helpView.AppendLine("Leave the game");
                                helpView.AppendLine("  /leavepost");
                                helpView.AppendLine("Leave the post-game lobby");
                                helpView.AppendLine("  /repick");
                                helpView.AppendLine("Vote to repick the host");
                                helpView.AppendLine("  /add [Role]");
                                helpView.AppendLine("Add [Role] to the role list");
                                helpView.AppendLine("  /remove [Position]");
                                helpView.AppendLine("Remove [Position] from the role list");
                                helpView.AppendLine("  /start");
                                helpView.AppendLine("Force the game to start");
                                helpView.AppendLine("  /n [Name]");
                                helpView.AppendLine("Set your name to [Name]");
                                helpView.AppendLine("  /t [Player]");
                                helpView.AppendLine("Set your target to [Player]");
                                helpView.AppendLine("  /t2 [Player]");
                                helpView.AppendLine("Set your second target to [Player]");
                                helpView.AppendLine("  /v [Player]");
                                helpView.AppendLine("Vote [Player] up to the stand");
                                helpView.AppendLine("  /g");
                                helpView.AppendLine("Vote guilty");
                                helpView.AppendLine("  /i");
                                helpView.AppendLine("Vote innocent");
                                helpView.AppendLine("  /td [Player]");
                                helpView.AppendLine("Set your day choice to [Player]");
                                helpView.AppendLine("  /lw <Player>");
                                helpView.AppendLine("Edit your LW or view <Player>'s");
                                helpView.AppendLine("  /dn <Player>");
                                helpView.AppendLine("Edit your DN or view <Player>'s");
                                helpView.AppendLine("  /fw");
                                helpView.AppendLine("Edit your forged will");
                                helpView.AppendLine("  /jn [Reason]");
                                helpView.AppendLine("Set your execute reason to [Reason]");
                                helpView.AppendLine("  /report [ID] [Reason] <Message>");
                                helpView.AppendLine("Report [ID] for [Reason]");
                                helpView.AppendLine("  /redraw");
                                helpView.AppendLine("Redraw the whole screen");
                                OpenSideView(helpView);
                            }
                            else if (cmdn == "leave") game.Parser.LeaveGame();
                            else if (cmdn == "leavepost") game.Parser.LeavePostGameLobby();
                            else if (cmdn == "repick") game.Parser.VoteToRepickHost();
                            else if (cmdn == "add")
                            {
                                if (Enum.TryParse(string.Join("_", cmd, 1, cmd.Length - 1).ToUpper(), out RoleID role))
                                {
                                    game.Parser.ClickedOnAddButton(role);
                                    game.GameState.AddRole(role);
                                }
                                else StatusLine = string.Format("Invalid role: {0}", string.Join(" ", cmd, 1, cmd.Length - 1));
                            }
                            else if (cmdn == "remove")
                            {
                                if (byte.TryParse(cmd[1], out byte slot))
                                {
                                    game.Parser.ClickedOnRemoveButton(--slot);
                                    game.GameState.RemoveRole(slot);
                                }
                                else StatusLine = string.Format("Invalid slot number: {0}", cmd[1]);
                            }
                            else if (cmdn == "start") game.Parser.ClickedOnStartButton();
                            else if (cmdn == "n") game.Parser.ChooseName(string.Join(" ", cmd, 1, cmd.Length - 1));
                            else if (cmdn == "t")
                            {
                                int index = 1;
                                if (game.GameState.TryParsePlayer(cmd, ref index, out PlayerID target))
                                {
                                    game.Parser.SetTarget(target);
                                    if (game.GameState.Role.IsMafia()) game.Parser.SetTargetMafiaOrWitch(target, target == PlayerID.JAILOR ? TargetType.CANCEL_TARGET_1 : TargetType.SET_TARGET_1);
                                    GameView.AppendLine(target == PlayerID.JAILOR ? "Unset target" : "Set target to {0}", game.GameState.ToName(target));
                                }
                                else StatusLine = string.Format("Player not found: {0}", string.Join(" ", cmd, 1, cmd.Length - 1));
                            }
                            else if (cmdn == "t2")
                            {
                                int index = 1;
                                if (game.GameState.TryParsePlayer(cmd, ref index, out PlayerID target))
                                {
                                    game.Parser.SetSecondTarget(target);
                                    if (game.GameState.Role.IsMafia()) game.Parser.SetTargetMafiaOrWitch(target, target == PlayerID.JAILOR ? TargetType.CANCEL_TARGET_2 : TargetType.SET_TARGET_2);
                                    GameView.AppendLine(target == PlayerID.JAILOR ? "Unset secondary target" : "Set secondary target to {0}", game.GameState.ToName(target));
                                }
                                else StatusLine = string.Format("Player not found: {0}", string.Join(" ", cmd, 1, cmd.Length - 1));
                            }
                            else if (cmdn == "v")
                            {
                                int index = 1;
                                if (game.GameState.TryParsePlayer(cmd, ref index, out PlayerID target)) game.Parser.SetVote(target);
                                else StatusLine = string.Format("Player not found: {0}", string.Join(" ", cmd, 1, cmd.Length - 1));
                            }
                            else if (cmdn == "g") game.Parser.JudgementVoteGuilty();
                            else if (cmdn == "i") game.Parser.JudgementVoteInnocent();
                            else if (cmdn == "w")
                            {
                                int index = 1;
                                if (game.GameState.TryParsePlayer(cmd, ref index, out PlayerID target, false)) game.Parser.SendPrivateMessage(target, string.Join(" ", cmd, index, cmd.Length - index));
                                else StatusLine = "Player not found";
                            }
                            else if (cmdn == "td")
                            {
                                int index = 1;
                                if (game.GameState.TryParsePlayer(cmd, ref index, out PlayerID target))
                                {
                                    game.Parser.SetDayChoice(target);
                                    GameView.AppendLine(target == PlayerID.JAILOR ? "Unset day target" : "Set day target to {0}", game.GameState.ToName(target));
                                }
                                else StatusLine = string.Format("Player not found: {0}", string.Join(" ", cmd, 1, cmd.Length - 1));
                            }
                            else if (cmdn == "lw")
                            {
                                int index = 1;
                                if (cmd.Length == 1)
                                {
                                    OpenSideView(myLastWillView);
                                    willContext = myLastWillView;
                                    RedrawCursor();
                                }
                                else if (game.GameState.TryParsePlayer(cmd, ref index, out PlayerID target))
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
                                else StatusLine = string.Format("Player not found: {0}", string.Join(" ", cmd, 1, cmd.Length - 1));
                            }
                            else if (cmdn == "dn")
                            {
                                int index = 1;
                                if (cmd.Length == 1)
                                {
                                    OpenSideView(myDeathNoteView);
                                    willContext = myDeathNoteView;
                                    RedrawCursor();
                                }
                                else if (game.GameState.TryParsePlayer(cmd, ref index, out PlayerID target))
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
                                else StatusLine = string.Format("Player not found: {0}", string.Join(" ", cmd, 1, cmd.Length - 1));
                            }
                            else if (cmdn == "fw")
                            {
                                OpenSideView(myForgedWillView);
                                willContext = myForgedWillView;
                                RedrawCursor();
                            }
                            else if (cmdn == "jn")
                            {
                                if (Enum.TryParse(string.Join("_", cmd, 1, cmd.Length).ToUpper(), out ExecuteReasonID reason))
                                {
                                    game.Parser.SetJailorDeathNote(reason);
                                }
                                else StatusLine = string.Format("Reason not found: {0}", string.Join(" ", cmd, 1, cmd.Length - 1));
                            }
                            else if (cmdn == "report")
                            {
                                PlayerID player = (PlayerID)(byte.Parse(cmd[1]) - 1u);
                                ReportReasonID reason = (ReportReasonID)Enum.Parse(typeof(ReportReasonID), cmd[2].ToUpper());
                                game.Parser.ReportPlayer(player, reason, string.Join(" ", cmd, 3, cmd.Length - 3));
                                GameView.AppendLine("Reported {0} for {1}", game.GameState.ToName(player), reason.ToString().ToLower().Replace('_', ' '));
                            }
                            else if (cmdn == "redraw") RedrawAll();
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine("Failed to parse GameView command: {0}", (object)input);
                            Debug.WriteLine(e);
                            StatusLine = string.Format("Failed to parse command: {0}", e.Message);
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
            string line = willContext != null ? EDITING_STATUS : inputBuffer.Length == 0 ? _StatusLine ?? (commandMode ? DEFAULT_COMMAND_STATUS : DEFAULT_STATUS) : inputBuffer.ToString();
            Console.Write(line.PadRight(inputLength));
            inputLength = line.Length;
            ResetCursor();
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
                ResetCursor();
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
                ResetCursor();
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
                    if (sideViews.Where(v => v.GetMinimumWidth() > sideWidth).Any())
                    {
                        RedrawAll();
                        return;
                    }
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
                ResetCursor();
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
                ResetCursor();
            }
        }

        protected void ResetCursor()
        {
            if (willContext != null)
            {
                willContext.MoveCursor(sideWidth, cursorTop);
                return;
            }
            Console.CursorTop = cursorTop;
            Console.CursorLeft = bufferIndex + 2;
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
                    ResetCursor();
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
                                if (Console.CursorLeft + 1 >= Console.BufferWidth) break;
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
                                ResetCursor();
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
            Console.Write(Title.Length > width ? Title.Substring(0, width) : Title.PadRight(width));
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
}
