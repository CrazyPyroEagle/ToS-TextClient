using Optional;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ToSParser;

namespace ToSTextClient
{
    class TextClient
    {
        public const uint BUILD_NUMBER = 9706u;     // Live = 9706 / PTR = 9706
        private static readonly byte[] MODULUS = new byte[] { 0xce, 0x22, 0x31, 0xcc, 0xc2, 0x33, 0xed, 0x95, 0xf8, 0x28, 0x6e, 0x77, 0xd7, 0xb4, 0xa6, 0x55, 0xe0, 0xad, 0xf5, 0x26, 0x08, 0x7b, 0xff, 0xaa, 0x2f, 0x78, 0x6a, 0x3f, 0x93, 0x54, 0x5f, 0x48, 0xb5, 0x89, 0x39, 0x83, 0xef, 0x1f, 0x61, 0x15, 0x1f, 0x18, 0xa0, 0xe1, 0xdd, 0x02, 0xa7, 0x42, 0x27, 0x77, 0x71, 0x8b, 0x79, 0xe9, 0x90, 0x8b, 0x0e, 0xe8, 0x4a, 0x33, 0xd2, 0x5d, 0xde, 0x1f, 0xb4, 0x7d, 0xf4, 0x35, 0xf5, 0xea, 0xf6, 0xe7, 0x04, 0x2c, 0xaf, 0x03, 0x71, 0xe4, 0x6f, 0x50, 0x7f, 0xd2, 0x70, 0x70, 0x39, 0xee, 0xa6, 0x0a, 0xae, 0xf7, 0xbc, 0x17, 0x51, 0x81, 0xf1, 0xd4, 0xf1, 0x33, 0x85, 0xf4, 0xab, 0x54, 0x3b, 0x1e, 0x42, 0x56, 0xa4, 0x79, 0xd1, 0x4e, 0xcc, 0xb4, 0xaa, 0xaa, 0x73, 0xa3, 0x35, 0xf4, 0xe6, 0x57, 0x66, 0xe6, 0x52, 0x0e, 0x51, 0x8b, 0x7e, 0x26, 0xe8, 0x63, 0xdf, 0x58, 0x57, 0x6b, 0x87, 0xdd, 0xd5, 0xf2, 0xb0, 0x58, 0x73, 0x7b, 0x10, 0x99, 0x5a, 0x99, 0x80, 0xe3, 0x8d, 0xde, 0x57, 0x98, 0xac, 0x9a, 0xf8, 0xf7, 0x37, 0x2c, 0x6f, 0x46, 0x4f, 0xf8, 0xba, 0xc8, 0x59, 0x57, 0x9d, 0x2f, 0xac, 0x38, 0xd8, 0x88, 0x89, 0xcd, 0x12, 0x3e, 0x08, 0x09, 0xb4, 0xcd, 0x5d, 0x05, 0x0b, 0x16, 0xce, 0x80, 0x6a, 0x19, 0xad, 0xea, 0xa9, 0xa2, 0x6c, 0x40, 0xba, 0x6d, 0x19, 0x74, 0x4b, 0x84, 0xd9, 0x46, 0xdc, 0xee, 0x93, 0x66, 0xb7, 0x4e, 0x98, 0xa7, 0x2c, 0x9a, 0x28, 0x0d, 0x3b, 0x7d, 0xb3, 0x90, 0x6f, 0x45, 0x18, 0x7c, 0x0c, 0xb1, 0x59, 0x5a, 0xb9, 0x16, 0xa2, 0x38, 0x2b, 0xcd, 0x2d, 0x2c, 0x48, 0xd7, 0x0d, 0xcc, 0xf0, 0x17, 0x60, 0x5c, 0x93, 0x39, 0x81, 0x28, 0xbd, 0x65, 0x8a, 0x5b, 0xb4, 0xe0, 0x51, 0x87, 0xc0, 0x77 };
        private static readonly byte[] EXPONENT = new byte[] { 0x01, 0x00, 0x01 };

        public string Username { get; protected set; }
        public uint TownPoints { get; set; }
        public uint MeritPoints { get; set; }
        public IList<GameMode> ActiveGameModes { get; set; } = new List<GameMode>();
        public IList<Achievement> EarnedAchievements { get; set; } = new List<Achievement>();
        public IList<Character> OwnedCharacters { get; set; } = new List<Character>();
        public IList<House> OwnedHouses { get; set; } = new List<House>();
        public IList<Pet> OwnedPets { get; set; } = new List<Pet>();
        public IList<LobbyIcon> OwnedLobbyIcons { get; set; } = new List<LobbyIcon>();
        public IList<DeathAnimation> OwnedDeathAnimations { get; set; } = new List<DeathAnimation>();
        public GameState GameState
        {
            get => _GameState;
            set
            {
                if (value != null)
                {
                    _GameState = value;
                    UI.GameView.Clear();
                    UI.CommandContext = CommandContext.LOBBY;
                    UI.SetMainView(UI.GameView);
                    UI.GameView.AppendLine(("Joined a lobby for {0}", ConsoleColor.Green, ConsoleColor.Black), value.GameMode.ToString().ToDisplayName());
                }
                else
                {
                    UI.CommandContext = CommandContext.HOME;
                    UI.SetMainView(UI.HomeView);
                    _GameState = value;
                }
            }
        }
        public ITextUI UI { get; set; }
        public MessageParser Parser { get; protected set; }
        public Localization Localization { get; protected set; }
        public int Timer
        {
            get => _Timer;
            set { if ((_Timer = value) >= 0) UI.RedrawTimer(); }
        }
        public string TimerText
        {
            get => _TimerText;
            set { _TimerText = value; Task.Run(UpdateTimer); UI.RedrawTimer(); }
        }

        private Socket socket;
        private byte[] buffer;
        private GameState _GameState;
        private int _Timer;
        private string _TimerText;
        private volatile int timerIndex;

        static void Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.Unicode;
                Console.Title = "Town of Salem (Unofficial Client)";
                Console.WriteLine("Build {0}", BUILD_NUMBER);
                Console.Write("Username: ");
                string user = Console.ReadLine();
                Console.Write("Password: ");
                SecureString pwrd = ReadPassword();
                Console.WriteLine("(hidden)", pwrd.Length);
                RSA rsa = RSA.Create();
                rsa.ImportParameters(new RSAParameters
                {
                    Modulus = MODULUS,
                    Exponent = EXPONENT
                });
                byte[] pwrdb = ToByteArray(pwrd);
                byte[] encpw = rsa.Encrypt(pwrdb, RSAEncryptionPadding.Pkcs1);
                Array.Clear(pwrdb, 0, pwrdb.Length);

                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                Console.WriteLine("Connecting to server");
                socket.Connect("live4.tos.blankmediagames.com", 3600);
                ((ConsoleUI)new TextClient(socket, user, Convert.ToBase64String(encpw)).UI).Run();
                return;
            }
            catch (SocketException)
            {
                Console.Clear();
                Console.WriteLine("Failed to connect to the server: check your internet connection");
            }
            catch (Exception e)
            {
                Console.Clear();
                Console.WriteLine("Exception occurred during startup. Please report this issue, including the following text.");
                Console.WriteLine(e.ToString());
            }
            while (true) ;
        }

        TextClient(Socket socket, string user, string encpw)
        {
            this.socket = socket;
            buffer = new byte[4096];
            Parser = new MessageParser(ParseMessage, (buffer, index, length) => socket.Send(buffer, index, length, SocketFlags.None));
            Parser.Authenticate(AuthenticationMode.BMG_FORUMS, true, BUILD_NUMBER, user, encpw);
            UI = new ConsoleUI(this)
            {
                CommandContext = CommandContext.AUTHENTICATING
            };
            Localization = new Localization(UI);
            UI.RegisterCommand(new Command<GameMode>("Join a lobby for {0}", CommandContext.HOME, ArgumentParsers.ForEnum<GameMode>(UI), (cmd, gameMode) =>
            {
                if (ActiveGameModes.Contains(gameMode))
                {
                    if (gameMode.HasRankedQueue()) Parser.JoinRankedQueue(gameMode);
                    else Parser.JoinLobby(gameMode);
                }
                else UI.StatusLine = string.Format("Cannot join game mode: {0}", gameMode.ToString().ToDisplayName());
            }), "join");
            UI.RegisterCommand(new Command("Leave the queue", CommandContext.HOME, cmd => ClientMessageParsers.LeaveRankedQueue(Parser)), "leavequeue");
            UI.RegisterCommand(new Command("Accept the queue popup", CommandContext.HOME, cmd => Parser.AcceptRanked()), "accept");
            UI.RegisterCommand(new Command("Exit the game", CommandContext.AUTHENTICATING | CommandContext.HOME, cmd => UI.RunInput = false), "quit", "exit");
            UI.RegisterCommand(new Command<Language>("Set the lobby language", CommandContext.HOME, ArgumentParsers.ForEnum<Language>(UI), (cmd, lang) => Parser.UpdateSettings(Setting.SELECTED_QUEUE_LANGUAGE, (byte)lang)), "lang", "language");
            UI.RegisterCommand(new Command("Leave the game", CommandContext.LOBBY | CommandContext.GAME, cmd => Parser.LeaveGame()), "leave");
            UI.RegisterCommand(new Command("Leave the post-game lobby", CommandContext.POST_GAME, cmd => Parser.LeavePostGameLobby()), "leavepost");
            UI.RegisterCommand(new Command("Vote to repick the host", CommandContext.LOBBY, cmd => Parser.VoteToRepickHost()), "repick");
            UI.RegisterCommand(new Command<Role>("Add {0} to the role list", CommandContext.HOST, ArgumentParsers.ForEnum<Role>(UI), (cmd, role) =>
            {
                Parser.ClickedOnAddButton(role);
                GameState.AddRole(role);
            }), "add");
            UI.RegisterCommand(new Command<byte>("Remove {0} from the role list", CommandContext.HOST, ArgumentParsers.Position(UI), (cmd, position) =>
            {
                Parser.ClickedOnRemoveButton(position);
                GameState.RemoveRole(position);
            }), "remove");
            UI.RegisterCommand(new Command("Force the game to start", CommandContext.HOST, cmd => Parser.ClickedOnStartButton()), "start");
            UI.RegisterCommand(new Command<string>("Set your name to {0}", CommandContext.PICK_NAMES, ArgumentParsers.Text(UI, "Name"), (cmd, name) => Parser.ChooseName(name)), "n", "name");
            UI.RegisterCommand(new Command<Player>("Set your target to {0}", CommandContext.NIGHT, ArgumentParsers.Player(UI), (cmd, target) =>
            {
                // TODO: Add check for whether <target> is a valid target for the user's role
                Parser.SetTarget(target);
                if (GameState.Role.IsMafia()) Parser.SetTargetMafiaOrWitch(target, target == Player.JAILOR ? TargetType.CANCEL_TARGET_1 : TargetType.SET_TARGET_1);
                UI.GameView.AppendLine(target == Player.JAILOR ? "Unset target" : "Set target to {0}", GameState.ToName(target));
            }), "t", "target");
            UI.RegisterCommand(new Command<Player>("Set your second target to {0}", CommandContext.NIGHT, ArgumentParsers.Player(UI), (cmd, target) =>
            {
                // TODO: Add check for whether <target> is a valid second target for the user's role
                Parser.SetSecondTarget(target);
                if (GameState.Role.IsMafia()) Parser.SetTargetMafiaOrWitch(target, target == Player.JAILOR ? TargetType.CANCEL_TARGET_2 : TargetType.SET_TARGET_2);
                UI.GameView.AppendLine(target == Player.JAILOR ? "Unset secondary target" : "Set secondary target to {0}", GameState.ToName(target));
            }), "t2", "target2");
            UI.RegisterCommand(new Command<Player>("Set your day choice to {0}", CommandContext.DAY, ArgumentParsers.Player(UI), (cmd, target) =>
            {
                // TODO: Add check for whether <target> is a valid day choice for the user's role
                Parser.SetDayChoice(target);
                UI.GameView.AppendLine(target == Player.JAILOR ? "Unset day target" : "Set day target to {0}", GameState.ToName(target));
            }), "td", "targetday");
            UI.RegisterCommand(new Command<DuelAttack>("Attack with your {0}", CommandContext.DUEL_ATTACKING, ArgumentParsers.ForEnum<DuelAttack>(UI, "Attack", true), (cmd, attack) =>
            {
                Parser.SetPirateChoice((byte)attack);
                UI.GameView.AppendLine("You have decided to attack with your {0}", attack.ToString().ToLower().Replace('_', ' '));
            }), "attack");
            UI.RegisterCommand(new Command<DuelDefense>("Defend with your {0}", CommandContext.DUEL_ATTACKING, ArgumentParsers.ForEnum<DuelDefense>(UI, "Defense"), (cmd, defense) =>
            {
                Parser.SetPirateChoice((byte)defense);
                UI.GameView.AppendLine("You have decided to defend with your {0}", defense.ToString().ToLower().Replace('_', ' '));
            }), "defense");
            UI.RegisterCommand(new Command<Potion>("Use the {0} potion", CommandContext.NIGHT, ArgumentParsers.ForEnum<Potion>(UI), (cmd, potion) =>
            {
                Parser.SetPotionMasterChoice(potion);
                UI.GameView.AppendLine("You have decided to use the {0} potion", potion.ToString().ToLower().Replace('_', ' '));
            }), "potion");
            UI.RegisterCommand(new Command<LocalizationTable>("Make your target see {0}", CommandContext.NIGHT, ArgumentParsers.ForEnum<LocalizationTable>(UI), (cmd, message) =>
            {
                Parser.SetHypnotistChoice(message);
                UI.GameView.AppendLine("Your target will see: {0}", Localization.Of(message));
            }), "hm", "hypnotizemessage");
            UI.RegisterCommand(new Command<Player>("Vote {0} up to the stand", CommandContext.VOTING, ArgumentParsers.Player(UI), (cmd, target) => Parser.SetVote(target)), "v", "vote");
            UI.RegisterCommand(new Command("Vote guilty", CommandContext.JUDGEMENT, cmd => Parser.JudgementVoteGuilty()), "g", "guilty");
            UI.RegisterCommand(new Command("Vote innocent", CommandContext.JUDGEMENT, cmd => Parser.JudgementVoteInnocent()), "i", "innocent");
            UI.RegisterCommand(new Command<Player, string>("Whisper {1} to {0}", CommandContext.DAY, ArgumentParsers.Player(UI), ArgumentParsers.Text(UI, "Message"), (cmd, target, message) => Parser.SendPrivateMessage(target, message)), "w", "pm", "whisper");
            UI.RegisterCommand(new Command<ExecuteReason>("Set your execute reason to [Reason]", CommandContext.GAME, ArgumentParsers.ForEnum<ExecuteReason>(UI, "[Reason]", true), (cmd, reason) => Parser.SetJailorDeathNote(reason)), "jn", "jailornote");
            UI.RegisterCommand(new Command<Player, ReportReason, string>("Report {0} for {1}", CommandContext.GAME, ArgumentParsers.Player(UI), ArgumentParsers.ForEnum<ReportReason>(UI, "[Reason]"), ArgumentParsers.Text("Message"), (cmd, player, reason, message) =>
            {
                Parser.ReportPlayer(player, reason, message);
                UI.GameView.AppendLine(("Reported {0} for {1}", ConsoleColor.Yellow, ConsoleColor.Black), GameState.ToName(player), reason.ToString().ToLower().Replace('_', ' '));
            }), "report");
            UI.RegisterCommand(new CommandGroup("Use the {0} system command", UI, "Subcommand", "Subcommands")
                .Register(new Command<string>("Send {0} to all users", ~CommandContext.AUTHENTICATING, ArgumentParsers.Text(UI, "Message"), (cmd, message) => Parser.SendSystemMessage(SystemCommand.MESSAGE, message)), "message")
                .Register(new Command<string>("Send {0} to all players in your game", CommandContext.LOBBY | CommandContext.GAME, ArgumentParsers.Text(UI, "Message"), (cmd, message) => Parser.SendSystemMessage(SystemCommand.GAME_MESSAGE, message)), "gamemessage")
                .Register(new Command<string>("Ban {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.BAN, username)), "ban")
                .Register(new Command<Player>("Get {0}'s role and username", CommandContext.GAME, ArgumentParsers.Player(UI), (cmd, player) => Parser.SendSystemMessage(SystemCommand.IDENTIFY, ((byte)player).ToString())), "identify")
                .Register(new Command("Queue a restart", ~CommandContext.AUTHENTICATING, cmd => Parser.SendSystemMessage(SystemCommand.RESTART)), "restart")
                .Register(new Command("Cancel the queued restart", ~CommandContext.AUTHENTICATING, cmd => Parser.SendSystemMessage(SystemCommand.CANCEL_RESTART)), "cancelrestart")
                .Register(new Command<string>("Grant {0} 1300 Town Points", ~CommandContext.AUTHENTICATING, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.GRANT_POINTS, username)), "grantpoints")
                .Register(new Command<string>("Suspend {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.SUSPEND, username)), "suspend")
                .Register(new Command("Reload the shop data", ~CommandContext.AUTHENTICATING, cmd => Parser.SendSystemMessage(SystemCommand.RELOAD_XML, "\x01")), "reloadxml")
                .Register(new Command<string>("Whisper to {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.WHISPER, username)), "whisper")
                .Register(new Command<string>("Unban {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.UNBAN, username)), "unban")
                .Register(new Command<string>("Get account info for {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.ACCOUNT_INFO, username)), "accountinfo")
                .Register(new Command<string, Achievement>("Grant {1} to {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.Username(UI), ArgumentParsers.ForEnum<Achievement>(UI, an: true), (cmd, username, achievement) => Parser.SendSystemMessage(SystemCommand.GRANT_ACHIEVEMENT, username, ((byte)achievement).ToString())), "grantachievement")
                .Register(new Command("Toggle dev mode", ~CommandContext.AUTHENTICATING, cmd => Parser.SendSystemMessage(SystemCommand.DEV_LOGIN)), "devlogin")
                .Register(new Command<Promotion>("Request {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.ForEnum<Promotion>(UI), (cmd, promotion) => Parser.SendSystemMessage(SystemCommand.REQUEST_PROMOTION, ((byte)promotion).ToString())), "requestpromotion")
                .Register(new Command("Reset your account", ~CommandContext.AUTHENTICATING, cmd => Parser.SendSystemMessage(SystemCommand.RESET_ACCOUNT, "\x01")), "resetaccount")
                .Register(new Command<Character>("Grant yourself {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.ForEnum<Character>(UI), (cmd, character) => Parser.SendSystemMessage(SystemCommand.GRANT_CHARACTER, ((byte)character).ToString())), "grantcharacter")
                .Register(new Command<Background>("Grant yourself {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.ForEnum<Background>(UI), (cmd, background) => Parser.SendSystemMessage(SystemCommand.GRANT_BACKGROUND, ((byte)background).ToString())), "grantbackground")
                .Register(new Command<DeathAnimation>("Grant yourself {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.ForEnum<DeathAnimation>(UI), (cmd, deathAnimation) => Parser.SendSystemMessage(SystemCommand.GRANT_DEATH_ANIMATION, ((byte)deathAnimation).ToString())), "grantdeathanimation")
                .Register(new Command<House>("Grant yourself {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.ForEnum<House>(UI), (cmd, house) => Parser.SendSystemMessage(SystemCommand.GRANT_HOUSE, ((byte)house).ToString())), "granthouse")
                .Register(new Command<LobbyIcon>("Grant yourself {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.ForEnum<LobbyIcon>(UI), (cmd, lobbyIcon) => Parser.SendSystemMessage(SystemCommand.GRANT_LOBBY_ICON, ((byte)lobbyIcon).ToString())), "grantlobbyicon")
                .Register(new Command<Pack>("Grant yourself {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.ForEnum<Pack>(UI), (cmd, pack) => Parser.SendSystemMessage(SystemCommand.GRANT_PACK, ((byte)pack).ToString())), "grantpack")
                .Register(new Command<Pet>("Grant yourself {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.ForEnum<Pet>(UI), (cmd, pet) => Parser.SendSystemMessage(SystemCommand.GRANT_PET, ((byte)pet).ToString())), "grantpet")
                .Register(new Command<Scroll>("Grant yourself {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.ForEnum<Scroll>(UI), (cmd, scroll) => Parser.SendSystemMessage(SystemCommand.GRANT_SCROLL, ((byte)scroll).ToString())), "grantscroll")
                .Register(new Command("Reset your tutorial progress", ~CommandContext.AUTHENTICATING, cmd => Parser.SendSystemMessage(SystemCommand.RESET_TUTORIAL_PROGRESS, "\x01")), "resettutorialprogress")
                .Register(new Command("Reload the promotion data", ~CommandContext.AUTHENTICATING, cmd => Parser.SendSystemMessage(SystemCommand.RELOAD_PROMOTION_XML, "\x01")), "reloadpromotionxml")
                .Register(new Command<string, Promotion>("Grant {1} to {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.Username(UI), ArgumentParsers.ForEnum<Promotion>(UI), (cmd, username, promotion) => Parser.SendSystemMessage(SystemCommand.GRANT_PROMOTION, username, ((byte)promotion).ToString())), "grantpromotion")
                .Register(new Command<Role>("Set your role to {0}", CommandContext.LOBBY | CommandContext.PICK_NAMES, ArgumentParsers.ForEnum<Role>(UI), (cmd, role) => Parser.SendSystemMessage(SystemCommand.SET_ROLE, ((byte)role).ToString())), "setrole")
                .Register(new Command<AccountItem>("Grant yourself {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.ForEnum<AccountItem>(UI), (cmd, accountItem) => Parser.SendSystemMessage(SystemCommand.GRANT_ACCOUNT_ITEM, ((byte)accountItem).ToString())), "grantaccountitem")
                .Register(new Command<string>("Force {0} to change username", ~CommandContext.AUTHENTICATING, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.FORCE_NAME_CHANGE, username)), "forcenamechange")
                .Register(new Command<string>("Grant {0} 5200 Merit Points", ~CommandContext.AUTHENTICATING, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.GRANT_MERIT, username)), "grantmerit")
                .Register(new Command("Enable global double MP", ~CommandContext.AUTHENTICATING, cmd => Parser.SendSystemMessage(SystemCommand.SET_FREE_CURRENCY_MULTIPLIER, "2")), "doubleglobalfreecurrencymultiplier")
                .Register(new Command("Disable global double MP", ~CommandContext.AUTHENTICATING, cmd => Parser.SendSystemMessage(SystemCommand.SET_FREE_CURRENCY_MULTIPLIER, "1")), "resetglobalfreecurrencymultiplier")
                .Register(new Command<string, string>("Set {0}'s referrer to {1}", ~CommandContext.AUTHENTICATING, ArgumentParsers.Username(UI), ArgumentParsers.Username(UI), (cmd, referee, referrer) => Parser.SendSystemMessage(SystemCommand.GRANT_REFER_A_FRIEND, referee, referrer)), "grantreferafriend")
                .Register(new Command("Reload shop, cauldron & Ranked", ~CommandContext.AUTHENTICATING, cmd => Parser.SendSystemMessage(SystemCommand.RELOAD_CACHES)), "reloadcaches")
                .Register(new Command("Reset your cauldron cooldown", ~CommandContext.AUTHENTICATING, cmd => Parser.SendSystemMessage(SystemCommand.RESET_CAULDRON_COOLDOWN, "\x01")), "resetcauldroncooldown")
                .Register(new Command("Toggle test purchases", ~CommandContext.AUTHENTICATING, cmd => Parser.SendSystemMessage(SystemCommand.TOGGLE_TEST_PURCHASES)), "toggletestpurchases")
                .Register(new Command("Toggle free Coven", ~CommandContext.AUTHENTICATING, cmd => Parser.SendSystemMessage(SystemCommand.TOGGLE_FREE_COVEN, "\x01")), "togglefreecoven")
                .Register(new Command("Toggle your Coven ownership", ~CommandContext.AUTHENTICATING, cmd => Parser.SendSystemMessage(SystemCommand.TOGGLE_ACCOUNT_FEATURE, "2")), "toggleusercoven")
                .Register(new Command("Toggle your Web Premium ownership", ~CommandContext.AUTHENTICATING, cmd => Parser.SendSystemMessage(SystemCommand.TOGGLE_ACCOUNT_FEATURE, "4")), "toggleuserwebpremium")
                .Register(new Command<string>("Unlink Steam from {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.UNLINK_STEAM, username)), "unlinksteam")
                .Register(new Command<string>("Unlink Coven from {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.UNLINK_COVEN, username)), "unlinkcoven")
                .Register(new Command<string>("Grant Coven to {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.GRANT_COVEN, username)), "grantcoven")
                .Register(new Command<string>("Grant Web Premium to {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.GRANT_WEB_PREMIUM, username)), "grantwebpremium")
                .Register(new Command<string>("Kick {0}", ~CommandContext.AUTHENTICATING, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.KICK_USER, username)), "kickuser"), "system");
            UI.HomeView.ReplaceLine(0, "Authenticating...");
            UI.RedrawView(UI.HomeView);
            QueueReceive();
        }

        private void ParseMessage(byte[] buffer, int index, int length)
        {
            switch ((ServerMessageType)buffer[index++])
            {
                case ServerMessageType.AUTHENTICATED:
                    ServerMessageParsers.AUTHENTICATED.Build(buffer, index, length).Parse(out bool registered);
                    if (registered)
                    {
                        UI.CommandContext = (UI.CommandContext & ~CommandContext.AUTHENTICATING) | CommandContext.HOME;
                        UI.HomeView.ReplaceLine(0, ("Authenticated. Loading user information...", ConsoleColor.DarkGreen));
                    }
                    else UI.HomeView.ReplaceLine(0, ("Authentication failed: registration required", ConsoleColor.DarkRed));
                    break;
                case ServerMessageType.CREATE_LOBBY:
                    ServerMessageParsers.CREATE_LOBBY.Build(buffer, index, length).Parse(out bool host).Parse(out GameMode gameMode);
                    GameState = new GameState(this, gameMode)
                    {
                        Host = host
                    };
                    break;
                case ServerMessageType.SET_HOST:
                    GameState.Host = true;
                    break;
                case ServerMessageType.USER_JOINED_GAME:
                    ServerMessageParsers.USER_JOINED_GAME.Build(buffer, index, length).Parse(out host).Parse(out bool display).Parse(out string username).Parse(out Player playerID).Parse(out LobbyIcon lobbyIconID);
                    GameState.AddPlayer(playerID, host, display, username, lobbyIconID);
                    break;
                case ServerMessageType.USER_LEFT_GAME:
                    ServerMessageParsers.USER_LEFT_GAME.Build(buffer, index, length).Parse(out bool update).Parse(out display).Parse(out playerID);
                    GameState.RemovePlayer(playerID, update, display);
                    break;
                case ServerMessageType.CHAT_BOX_MESSAGE:
                    playerID = Player.JAILOR;
                    string message = null;
                    Func<Parser<Player, Parser<string, RootParser>>, RootParser> map = parser => parser.Parse(out playerID).Parse(out message);
                    ServerMessageParsers.CHAT_BOX_MESSAGE.Build(buffer, index, length).Parse(GameState.Started, map, map);
                    UI.GameView.AppendLine(("{0}: {1}", playerID <= Player.PLAYER_15 && GameState.Players[(int)playerID].Dead ? ConsoleColor.Gray : ConsoleColor.White, ConsoleColor.Black), GameState.ToName(playerID), message.Replace("\n", "").Replace("\r", ""));
                    break;
                // Add missing cases here
                case ServerMessageType.HOST_CLICKED_ON_ADD_BUTTON:
                    ServerMessageParsers.HOST_CLICKED_ON_ADD_BUTTON.Build(buffer, index, length).Parse(out Role role);
                    GameState.AddRole(role);
                    break;
                case ServerMessageType.HOST_CLICKED_ON_REMOVE_BUTTON:
                    ServerMessageParsers.HOST_CLICKED_ON_REMOVE_BUTTON.Build(buffer, index, length).Parse(out byte slotID);
                    GameState.RemoveRole(slotID);
                    break;
                case ServerMessageType.HOST_CLICKED_ON_START_BUTTON:
                    Timer = 10;
                    TimerText = "Start";
                    UI.GameView.AppendLine(("The game will start in 10 seconds", ConsoleColor.DarkGreen));
                    break;
                case ServerMessageType.CANCEL_START_COOLDOWN:
                    UI.GameView.AppendLine(("The start cooldown was cancelled", ConsoleColor.DarkRed));
                    break;
                case ServerMessageType.ASSIGN_NEW_HOST:
                    ServerMessageParsers.ASSIGN_NEW_HOST.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine("The host has been repicked");
                    GameState.HostID = playerID;
                    break;
                case ServerMessageType.VOTED_TO_REPICK_HOST:
                    ServerMessageParsers.VOTED_TO_REPICK_HOST.Build(buffer, index, length).Parse(out byte votesNeeded);
                    UI.GameView.AppendLine(("{0} votes are needed to repick the host", ConsoleColor.Green, ConsoleColor.Black), votesNeeded);
                    break;
                case ServerMessageType.NO_LONGER_HOST:
                    GameState.Host = false;
                    break;
                case ServerMessageType.DO_NOT_SPAM:
                    UI.GameView.AppendLine(("Please do not spam the chat", ConsoleColor.Green, ConsoleColor.Black));
                    break;
                case ServerMessageType.HOW_MANY_PLAYERS_AND_GAMES:
                    ServerMessageParsers.HOW_MANY_PLAYERS_AND_GAMES.Build(buffer, index, length).Parse(out uint onlinePlayers).Parse(out uint activeGames);
                    UI.GameView.AppendLine(("There are currently {0} players online and {1} games being played", ConsoleColor.Green, ConsoleColor.Black), onlinePlayers, activeGames);
                    break;
                case ServerMessageType.SYSTEM_MESSAGE:
                    ServerMessageParsers.SYSTEM_MESSAGE.Build(buffer, index, length).Parse(out message);
                    UI.GameView.AppendLine(("(System) {0}", ConsoleColor.Yellow, ConsoleColor.Black), message);
                    break;
                case ServerMessageType.STRING_TABLE_MESSAGE:
                    ServerMessageParsers.STRING_TABLE_MESSAGE.Build(buffer, index, length).Parse(out LocalizationTable tableMessage);
                    UI.GameView.AppendLine(Localization.Of(tableMessage));
                    break;
                // Add missing cases here
                case ServerMessageType.USER_INFORMATION:
                    ServerMessageParsers.USER_INFORMATION.Build(buffer, index, length).Parse(out username).Parse(out uint townPoints).Parse(out uint meritPoints);
                    UI.HomeView.ReplaceLine(0, ("{0} ({1} TP, {2} MP)", ConsoleColor.Yellow, ConsoleColor.Black), Username = username, TownPoints = townPoints, MeritPoints = meritPoints);
                    break;
                // Add missing cases here
                case ServerMessageType.FORCED_LOGOUT:
                    UI.CommandContext = CommandContext.AUTHENTICATING;
                    UI.StatusLine = "Forcefully disconnected";
                    break;
                case ServerMessageType.RETURN_TO_HOME_PAGE:
                    GameState = null;
                    break;
                // Add missing cases here
                case ServerMessageType.PURCHASED_CHARACTERS:
                    OwnedCharacters.Clear();
                    ServerMessageParsers.PURCHASED_CHARACTERS.Build(buffer, index, length).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out Character id);
                        OwnedCharacters.Add(id);
                        return root;
                    }, out _);
                    break;
                case ServerMessageType.PURCHASED_HOUSES:
                    OwnedHouses.Clear();
                    ServerMessageParsers.PURCHASED_HOUSES.Build(buffer, index, length).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out House id);
                        OwnedHouses.Add(id);
                        return root;
                    }, out _);
                    break;
                // Add missing cases here
                case ServerMessageType.PURCHASED_PETS:
                    OwnedPets.Clear();
                    ServerMessageParsers.PURCHASED_PETS.Build(buffer, index, length).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out Pet id);
                        OwnedPets.Add(id);
                        return root;
                    }, out _);
                    break;
                case ServerMessageType.SET_LAST_BONUS_WIN_TIME:
                    ServerMessageParsers.SET_LAST_BONUS_WIN_TIME.Build(buffer, index, length).Parse(out uint seconds);
                    UI.HomeView.ReplaceLine(1, "Next FWotD bonus is available in {0} seconds", seconds);
                    break;
                case ServerMessageType.EARNED_ACHIEVEMENTS_52:
                    EarnedAchievements.Clear();
                    ServerMessageParsers.EARNED_ACHIEVEMENTS_52.Build(buffer, index, length).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out Achievement id);
                        EarnedAchievements.Add(id);
                        return root;
                    }, out int achievementCount);
                    UI.HomeView.ReplaceLine(2, "Number of achievements earned: {0}", achievementCount);
                    break;
                case ServerMessageType.PURCHASED_LOBBY_ICONS:
                    OwnedLobbyIcons.Clear();
                    ServerMessageParsers.PURCHASED_LOBBY_ICONS.Build(buffer, index, length).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out LobbyIcon id);
                        OwnedLobbyIcons.Add(id);
                        return root;
                    }, out _);
                    break;
                case ServerMessageType.PURCHASED_DEATH_ANIMATIONS:
                    OwnedDeathAnimations.Clear();
                    ServerMessageParsers.PURCHASED_DEATH_ANIMATIONS.Build(buffer, index, length).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out DeathAnimation id);
                        OwnedDeathAnimations.Add(id);
                        return root;
                    }, out _);
                    break;
                // Add missing cases here
                case ServerMessageType.START_RANKED_QUEUE:
                    ServerMessageParsers.START_RANKED_QUEUE.Build(buffer, index, length).Parse(out bool requeue).Parse(out seconds);
                    if (requeue) UI.StatusLine = "Requeued due to a lack of players";
                    Timer = (int)seconds;
                    TimerText = "Queue";
                    break;
                case ServerMessageType.LEAVE_RANKED_QUEUE:
                    Timer = 0;
                    TimerText = null;
                    UI.StatusLine = "You have left the ranked queue";
                    break;
                case ServerMessageType.ACCEPT_RANKED_POPUP:
                    UI.StatusLine = "Are you ready for the Ranked game?";
                    Timer = 10;
                    TimerText = "Queue Popup";
                    UI.AudioAlert();
                    break;
                // Add missing cases here
                case ServerMessageType.RANKED_TIMEOUT_DURATION:
                    ServerMessageParsers.RANKED_TIMEOUT_DURATION.Build(buffer, index, length).Parse(out seconds);
                    UI.StatusLine = string.Format("Timed out for {0} seconds", seconds);
                    break;
                // Add missing cases here
                case ServerMessageType.MODERATOR_MESSAGE:
                    ServerMessageParsers.MODERATOR_MESSAGE.Build(buffer, index, length).Parse(out ModeratorMessage modMessageID);
                    UI.GameView.AppendLine(Localization.Of(modMessageID));
                    break;
                // Add missing cases here
                case ServerMessageType.USER_JOINING_LOBBY_TOO_QUICKLY_MESSAGE:
                    UI.StatusLine = "Wait 15 seconds before rejoining";
                    break;
                // Add missing cases here
                case ServerMessageType.PICK_NAMES:
                    ServerMessageParsers.PICK_NAMES.Build(buffer, index, length).Parse(out byte playerCount);
                    if (GameState == null) GameState = new GameState(this, GameMode.RANKED);
                    GameState.OnStart(playerCount);
                    break;
                case ServerMessageType.NAMES_AND_POSITIONS_OF_USERS:
                    ServerMessageParsers.NAMES_AND_POSITIONS_OF_USERS.Build(buffer, index, length).Parse(out playerID).Parse(out string name);
                    GameState.Players[(int)playerID].Name = name;
                    break;
                case ServerMessageType.ROLE_AND_POSITION:
                    ServerMessageParsers.ROLE_AND_POSITION.Build(buffer, index, length).Parse(out Role roleID).Parse(out playerID).Parse(out Option<Player> targetID);
                    GameState.Role = roleID;
                    GameState.Self = playerID;
                    targetID.MatchSome(id => GameState.Target = id);
                    break;
                case ServerMessageType.START_NIGHT:
                    GameState.Night++;
                    break;
                case ServerMessageType.START_DAY:
                    GameState.Day++;
                    break;
                case ServerMessageType.WHO_DIED_AND_HOW:
                    List<DeathCause> causes = new List<DeathCause>();
                    ServerMessageParsers.WHO_DIED_AND_HOW.Build(buffer, index, length).Parse(out playerID).Parse(out roleID).Parse(out display).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out DeathCause id);
                        causes.Add(id);
                        return root;
                    }, out int count);
                    PlayerState playerState = GameState.Players[(int)playerID];
                    playerState.Role = roleID;
                    playerState.Dead = true;
                    UI.GameView.AppendLine(("{0} died", ConsoleColor.DarkRed), GameState.ToName(playerID));
                    GameState.Graveyard.Add(playerState);
                    UI.RedrawView(UI.GraveyardView);
                    if (causes.Count > 0) UI.GameView.AppendLine("Death causes: {0}", string.Join(", ", causes.Select(dc => dc.ToString().ToDisplayName())));
                    UI.GameView.AppendLine("Their role was {0}", roleID.ToString().ToDisplayName());
                    break;
                // Add missing cases here
                case ServerMessageType.START_DISCUSSION:
                    UI.GameView.AppendLine("Discussion may now begin");
                    Timer = GameState.GameMode == GameMode.RAPID_MODE ? 15 : 45;
                    TimerText = "Discussion";
                    break;
                case ServerMessageType.START_VOTING:
                    //ServerMessageParsers.START_VOTING.Build(buffer, index, length).Parse(out byte votesNeeded);     // TODO: How does this message parse?
                    UI.CommandContext |= CommandContext.VOTING;
                    UI.GameView.AppendLine(("{0} votes are needed to lynch someone", ConsoleColor.Green, ConsoleColor.Black), (GameState.Players.Where(ps => !ps.Dead && !ps.Left).Count() + 2) / 2);
                    Timer = 30;
                    TimerText = "Voting";
                    break;
                case ServerMessageType.START_DEFENSE_TRANSITION:
                    ServerMessageParsers.START_DEFENSE_TRANSITION.Build(buffer, index, length).Parse(out playerID);
                    UI.CommandContext &= ~CommandContext.VOTING;
                    UI.GameView.AppendLine(("{0} has been voted up to the stand", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID));
                    break;
                case ServerMessageType.START_JUDGEMENT:
                    UI.CommandContext |= CommandContext.JUDGEMENT;
                    UI.GameView.AppendLine(("You may now vote guilty or innocent", ConsoleColor.Green, ConsoleColor.Black));
                    Timer = 20;
                    TimerText = "Judgement";
                    break;
                case ServerMessageType.TRIAL_FOUND_GUILTY:
                    ServerMessageParsers.TRIAL_FOUND_GUILTY.Build(buffer, index, length).Parse(out byte guiltyVotes).Parse(out byte innocentVotes);
                    UI.CommandContext &= ~CommandContext.JUDGEMENT;
                    UI.GameView.AppendLine(("Judgement results: {0} guilty - {1} innocent", ConsoleColor.Green, ConsoleColor.Black), guiltyVotes, innocentVotes);
                    Timer = 5;
                    TimerText = "Last Words";
                    break;
                case ServerMessageType.TRIAL_FOUND_NOT_GUILTY:
                    ServerMessageParsers.TRIAL_FOUND_NOT_GUILTY.Build(buffer, index, length).Parse(out guiltyVotes).Parse(out innocentVotes);
                    UI.CommandContext = (UI.CommandContext & ~CommandContext.JUDGEMENT) | CommandContext.VOTING;
                    UI.GameView.AppendLine(("Judgement results: {0} guilty - {1} innocent", ConsoleColor.Green, ConsoleColor.Black), guiltyVotes, innocentVotes);
                    break;
                case ServerMessageType.LOOKOUT_NIGHT_ABILITY_MESSAGE:
                    ServerMessageParsers.LOOKOUT_NIGHT_ABILITY_MESSAGE.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine(("{0} visited your target", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID));
                    break;
                case ServerMessageType.USER_VOTED:
                    ServerMessageParsers.USER_VOTED.Build(buffer, index, length).Parse(out playerID).Parse(out Player votedID).Parse(out byte voteCount);
                    UI.GameView.AppendLine(("{0} has placed {2} votes against {1}", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID), GameState.ToName(votedID), voteCount);
                    break;
                case ServerMessageType.USER_CANCELED_VOTE:
                    ServerMessageParsers.USER_CANCELED_VOTE.Build(buffer, index, length).Parse(out playerID).Parse(out votedID).Parse(out voteCount);
                    UI.GameView.AppendLine(("{0} has cancelled their {2} votes against {1}", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID), GameState.ToName(votedID), voteCount);
                    break;
                case ServerMessageType.USER_CHANGED_VOTE:
                    ServerMessageParsers.USER_CHANGED_VOTE.Build(buffer, index, length).Parse(out playerID).Parse(out votedID).Parse(out _).Parse(out voteCount);
                    UI.GameView.AppendLine(("{0} has changed their {2} votes to be against {1}", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID), GameState.ToName(votedID), voteCount);
                    break;
                case ServerMessageType.USER_DIED:
                    UI.GameView.AppendLine(("You have died", ConsoleColor.DarkRed));
                    break;
                case ServerMessageType.RESURRECTION:
                    ServerMessageParsers.RESURRECTION.Build(buffer, index, length).Parse(out playerID).Parse(out roleID);
                    UI.GameView.AppendLine(("{0} ({1}) has been resurrected", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID), roleID.ToString().ToDisplayName());
                    break;
                case ServerMessageType.TELL_ROLE_LIST:
                    int listIndex = 0;
                    ServerMessageParsers.TELL_ROLE_LIST.Build(buffer, index, length).Parse(parser => parser.Parse(out GameState.Roles[listIndex++]), out _);
                    UI.RedrawView(UI.RoleListView);
                    break;
                case ServerMessageType.USER_CHOSEN_NAME:
                    ServerMessageParsers.USER_CHOSEN_NAME.Build(buffer, index, length).Parse(out tableMessage).Parse(out playerID).Parse(out name);
                    GameState.Players[(int)playerID].Name = name;
                    UI.GameView.AppendLine(GameState.ToName(playerID) + " " + Localization.Of(tableMessage));
                    break;
                case ServerMessageType.OTHER_MAFIA:
                    GameState.Team.Clear();
                    ServerMessageParsers.OTHER_MAFIA.Build(buffer, index, length).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out playerID).Parse(out roleID);
                        PlayerState teammate = GameState.Players[(int)playerID];
                        teammate.Role = roleID;
                        GameState.Team.Add(teammate);
                        return root;
                    }, out _);
                    UI.OpenSideView(UI.TeamView);
                    break;
                case ServerMessageType.TELL_TOWN_AMNESIAC_CHANGED_ROLE:
                    ServerMessageParsers.TELL_TOWN_AMNESIAC_CHANGED_ROLE.Build(buffer, index, length).Parse(out roleID);
                    UI.GameView.AppendLine(("An Amnesiac has remembered {0}", ConsoleColor.Green, ConsoleColor.Black), roleID.ToString().ToDisplayName());
                    break;
                case ServerMessageType.AMNESIAC_CHANGED_ROLE:
                    ServerMessageParsers.AMNESIAC_CHANGED_ROLE.Build(buffer, index, length).Parse(out roleID).Parse(out targetID);
                    UI.GameView.AppendLine(("You have attempted to remember who you were!", ConsoleColor.DarkRed));
                    GameState.Role = roleID;
                    targetID.MatchSome(id => GameState.Target = id);
                    break;
                case ServerMessageType.BROUGHT_BACK_TO_LIFE:
                    UI.GameView.AppendLine(("You have been resurrected by a Retributionist!", ConsoleColor.Green, ConsoleColor.Black));
                    break;
                case ServerMessageType.START_FIRST_DAY:
                    GameState.Day = 1;
                    break;
                case ServerMessageType.BEING_JAILED:
                    UI.GameView.AppendLine(("You were hauled off to jail", ConsoleColor.Gray));
                    break;
                case ServerMessageType.JAILED_TARGET:
                    ServerMessageParsers.JAILED_TARGET.Build(buffer, index, length).Parse(out playerID).Parse(out bool canExecute).Parse(out bool executedTown);
                    UI.GameView.AppendLine("You have dragged {0} off to jail", GameState.ToName(playerID));
                    UI.GameView.AppendLine(canExecute ? "You may execute tonight" : executedTown ? "You cannot execute any more as you have executed a Town member" : "You cannot execute tonight");
                    break;
                case ServerMessageType.USER_JUDGEMENT_VOTED:
                    ServerMessageParsers.USER_JUDGEMENT_VOTED.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine(("{0} has voted", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID));
                    break;
                case ServerMessageType.USER_CHANGED_JUDGEMENT_VOTE:
                    ServerMessageParsers.USER_CHANGED_JUDGEMENT_VOTE.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine(("{0} has changed their voted", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID));
                    break;
                case ServerMessageType.USER_CANCELED_JUDGEMENT_VOTE:
                    ServerMessageParsers.USER_CANCELED_JUDGEMENT_VOTE.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine(("{0} has cancelled their vote", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID));
                    break;
                case ServerMessageType.TELL_JUDGEMENT_VOTES:
                    ServerMessageParsers.TELL_JUDGEMENT_VOTES.Build(buffer, index, length).Parse(out playerID).Parse(out JudgementVote judgementVote);
                    UI.GameView.AppendLine(("{0} voted {1}", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID), judgementVote.ToString().ToLower().Replace('_', ' '));
                    break;
                case ServerMessageType.EXECUTIONER_COMPLETED_GOAL:
                    UI.GameView.AppendLine(("You have successfully gotten your target lynched", ConsoleColor.DarkGreen));
                    break;
                case ServerMessageType.JESTER_COMPLETED_GOAL:
                    UI.GameView.AppendLine(("You have successfully gotten yourself lynched", ConsoleColor.DarkGreen));
                    break;
                case ServerMessageType.MAYOR_REVEALED:
                    ServerMessageParsers.MAYOR_REVEALED.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine(("{0} has revealed themselves as the mayor", ConsoleColor.Magenta, ConsoleColor.Black), GameState.ToName(playerID));
                    break;
                /*case ServerMessageType.MAYOR_REVEALED_AND_ALREADY_VOTED:
                    break;*/
                case ServerMessageType.DISGUISER_STOLE_YOUR_IDENTITY:
                    ServerMessageParsers.DISGUISER_STOLE_YOUR_IDENTITY.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine(("A disguiser stole your identity. You are now {0}", ConsoleColor.DarkRed), GameState.ToName(playerID));
                    break;
                case ServerMessageType.DISGUISER_CHANGED_IDENTITY:
                    ServerMessageParsers.DISGUISER_CHANGED_IDENTITY.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine(("You have successfully disguised yourself as {0}", ConsoleColor.DarkRed), GameState.ToName(playerID));
                    break;
                case ServerMessageType.DISGUISER_CHANGED_UPDATE_MAFIA:
                    ServerMessageParsers.DISGUISER_CHANGED_UPDATE_MAFIA.Build(buffer, index, length).Parse(out playerID).Parse(out Player disguiserID);
                    UI.GameView.AppendLine(("{1} has successfully disguised themselves as {0}", ConsoleColor.DarkRed), GameState.ToName(playerID), GameState.ToName(disguiserID));
                    break;
                case ServerMessageType.MEDIUM_IS_TALKING_TO_US:
                    UI.GameView.AppendLine(("A medium is talking to you", ConsoleColor.Green, ConsoleColor.Black));
                    break;
                case ServerMessageType.MEDIUM_COMMUNICATING:
                    UI.GameView.AppendLine(("You have opened a communication with the living", ConsoleColor.Green, ConsoleColor.Black));
                    break;
                case ServerMessageType.TELL_LAST_WILL:
                    ServerMessageParsers.TELL_LAST_WILL.Build(buffer, index, length).Parse(out playerID).Parse(out bool hasLastWill).Parse(hasLastWill, parser =>
                    {
                        RootParser root = parser.Parse(out string will);
                        GameState.Players[(int)playerID].LastWill = will.Replace("\n", "");
                        return root;
                    }, parser =>
                    {
                        UI.GameView.AppendLine("We could not find a last will");
                        return parser;
                    });
                    break;
                case ServerMessageType.HOW_MANY_ABILITIES_LEFT:
                    ServerMessageParsers.HOW_MANY_ABILITIES_LEFT.Build(buffer, index, length).Parse(out byte abilitiesLeft);
                    UI.GameView.AppendLine("Abilities left: {0}", abilitiesLeft);
                    break;
                case ServerMessageType.MAFIA_TARGETING:
                    ServerMessageParsers.MAFIA_TARGETING.Build(buffer, index, length).Parse(out playerID).Parse(out roleID).Parse(out Player teamTargetID);
                    UI.GameView.AppendLine(teamTargetID == Player.JAILOR ? "{0} ({1}) has unset their target" : "{0} ({1}) has set their target to {2}", GameState.ToName(playerID), roleID.ToString().ToDisplayName(), GameState.ToName(teamTargetID));
                    break;
                case ServerMessageType.TELL_JANITOR_TARGETS_ROLE:
                    ServerMessageParsers.TELL_JANITOR_TARGETS_ROLE.Build(buffer, index, length).Parse(out roleID);
                    UI.GameView.AppendLine(("You secretly know that your target's role was {0}", ConsoleColor.Green, ConsoleColor.Black), roleID.ToString().ToDisplayName());
                    break;
                case ServerMessageType.TELL_JANITOR_TARGETS_WILL:
                    ServerMessageParsers.TELL_JANITOR_TARGETS_WILL.Build(buffer, index, length).Parse(out playerID).Parse(out string lastWill);
                    GameState.Players[(int)playerID].LastWill = lastWill;
                    break;
                case ServerMessageType.SOMEONE_HAS_WON:
                    List<Player> winners = new List<Player>();
                    RepeatParser<Parser<Player, RootParser>, RootParser> winnerParser = ServerMessageParsers.SOMEONE_HAS_WON.Build(buffer, index, length).Parse(out Faction factionID);
                    winnerParser.Parse(p =>
                    {
                        RootParser root = p.Parse(out playerID);
                        winners.Add(playerID);
                        return root;
                    }, out _);
                    UI.GameView.AppendLine((winners.Count > 0 ? "Winning faction: {0} ({1})" : "Winning faction: {0}", ConsoleColor.Green, ConsoleColor.Black), factionID.ToString().ToDisplayName(), string.Join(", ", winners.Select(p => GameState.ToName(p))));
                    if (winners.Contains(GameState.Self)) UI.GameView.AppendLine(("You have won", ConsoleColor.Green, ConsoleColor.Black));
                    break;
                case ServerMessageType.MAFIOSO_PROMOTED_TO_GODFATHER:
                    UI.GameView.AppendLine("You have been promoted to Godfather");
                    GameState.Role = Role.GODFATHER;
                    break;
                case ServerMessageType.MAFIOSO_PROMOTED_TO_GODFATHER_UPDATE_MAFIA:
                    ServerMessageParsers.MAFIOSO_PROMOTED_TO_GODFATHER_UPDATE_MAFIA.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine("{0} has been promoted to Godfather", GameState.ToName(playerID));
                    GameState.Players[(int)playerID].Role = Role.GODFATHER;
                    break;
                case ServerMessageType.MAFIA_PROMOTED_TO_MAFIOSO:
                    UI.GameView.AppendLine("You have been promoted to Mafioso");
                    break;
                case ServerMessageType.TELL_MAFIA_ABOUT_MAFIOSO_PROMOTION:
                    ServerMessageParsers.TELL_MAFIA_ABOUT_MAFIOSO_PROMOTION.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine("{0} has been promoted to Mafioso", GameState.ToName(playerID));
                    GameState.Players[(int)playerID].Role = Role.MAFIOSO;
                    break;
                case ServerMessageType.EXECUTIONER_CONVERTED_TO_JESTER:
                    UI.GameView.AppendLine(("Your target has died", ConsoleColor.DarkRed));
                    break;
                case ServerMessageType.AMNESIAC_BECAME_MAFIA_OR_WITCH:
                    ServerMessageParsers.AMNESIAC_BECAME_MAFIA_OR_WITCH.Build(buffer, index, length).Parse(out playerID).Parse(out roleID);
                    UI.GameView.AppendLine(("{0} has remembered {1}", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID), roleID.ToString().ToDisplayName());
                    break;
                case ServerMessageType.USER_DISCONNECTED:
                    ServerMessageParsers.USER_DISCONNECTED.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine(("{0} has left the game", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID));
                    break;
                case ServerMessageType.MAFIA_WAS_JAILED:
                    ServerMessageParsers.MAFIA_WAS_JAILED.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine(("{0} has been dragged off to jail", ConsoleColor.DarkRed), GameState.ToName(GameState.Players[(int)playerID]));
                    break;
                case ServerMessageType.INVALID_NAME_MESSAGE:
                    ServerMessageParsers.INVALID_NAME_MESSAGE.Build(buffer, index, length).Parse(out InvalidNameStatus invalidNameStatus);
                    UI.StatusLine = string.Format("Invalid name: {0}", invalidNameStatus);
                    break;
                case ServerMessageType.START_NIGHT_TRANSITION:
                    UI.CommandContext &= ~CommandContext.VOTING;
                    break;
                case ServerMessageType.START_DAY_TRANSITION:
                    ServerMessageParsers.START_DAY_TRANSITION.Build(buffer, index, length).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out Player deadPlayer);
                        GameState.Players[(int)deadPlayer].Dead = true;
                        return root;
                    }, out _);
                    break;
                // Add missing cases here
                case ServerMessageType.DEATH_NOTE:
                    ServerMessageParsers.DEATH_NOTE.Build(buffer, index, length).Parse(out playerID).Parse(out _).Parse(out string deathNote);
                    GameState.Players[(int)playerID].DeathNote = deathNote.Replace("\n", "");
                    break;
                // Add missing cases here
                case ServerMessageType.RESURRECTION_SET_ALIVE:
                    ServerMessageParsers.RESURRECTION_SET_ALIVE.Build(buffer, index, length).Parse(out playerID);
                    GameState.Players[(int)playerID].Dead = false;
                    break;
                case ServerMessageType.START_DEFENSE:
                    Timer = 20;
                    TimerText = "Defense";
                    UI.GameView.AppendLine(("What is your defense?", ConsoleColor.Green, ConsoleColor.Black));
                    break;
                case ServerMessageType.USER_LEFT_DURING_SELECTION:
                    ServerMessageParsers.USER_LEFT_DURING_SELECTION.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine(("{0} left during selection", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID));
                    GameState.Players[(int)playerID].Left = true;
                    break;
                case ServerMessageType.VIGILANTE_KILLED_TOWN:
                    UI.GameView.AppendLine(("You put your gun away out of fear of shooting another town member", ConsoleColor.Green, ConsoleColor.Black));
                    break;
                case ServerMessageType.NOTIFY_USERS_OF_PRIVATE_MESSAGE:
                    ServerMessageParsers.NOTIFY_USERS_OF_PRIVATE_MESSAGE.Build(buffer, index, length).Parse(out playerID).Parse(out Player receiverID);
                    UI.GameView.AppendLine(("{0} is whispering to {1}", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID), GameState.ToName(receiverID));
                    break;
                case ServerMessageType.PRIVATE_MESSAGE:
                    receiverID = default(Player);
                    message = default(string);       // These values are ignored at runtime, but the compiler will complain without these assignments.
                    ServerMessageParsers.PRIVATE_MESSAGE.Build(buffer, index, length).Parse(out PrivateMessageType pmType).Parse(out playerID).Parse(pmType != PrivateMessageType.FROM_TO, parser => parser.Parse(out message), parser => parser.Parse(out receiverID).Parse(out message));
                    switch (pmType)
                    {
                        case PrivateMessageType.TO:
                            UI.GameView.AppendLine(("To {0}: {1}", ConsoleColor.Magenta, ConsoleColor.Black), GameState.ToName(playerID), message);
                            break;
                        case PrivateMessageType.FROM:
                            UI.GameView.AppendLine(("From {0}: {1}", ConsoleColor.Magenta, ConsoleColor.Black), GameState.ToName(playerID), message);
                            break;
                        case PrivateMessageType.FROM_TO:
                            UI.GameView.AppendLine(("From {0} to {1}: {2}", ConsoleColor.Magenta, ConsoleColor.Black), GameState.ToName(playerID), GameState.ToName(receiverID), message);
                            break;
                    }
                    break;
                case ServerMessageType.EARNED_ACHIEVEMENTS_161:
                    ServerMessageParsers.EARNED_ACHIEVEMENTS_161.Build(buffer, index, length).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out Achievement id);
                        UI.GameView.AppendLine(("You have earned the achievement {0}", ConsoleColor.DarkGreen), id.ToString().ToDisplayName());
                        return root;
                    }, out _);
                    break;
                case ServerMessageType.AUTHENTICATION_FAILED:
                    ServerMessageParsers.AUTHENTICATION_FAILED.Build(buffer, index, length).Parse(out AuthenticationResult authResult);
                    UI.HomeView.ReplaceLine(0, ("Authentication failed: {0}", ConsoleColor.DarkRed), authResult);
                    break;
                case ServerMessageType.SPY_NIGHT_ABILITY_MESSAGE:
                    ServerMessageParsers.SPY_NIGHT_ABILITY_MESSAGE.Build(buffer, index, length).Parse(out bool isCoven).Parse(out playerID);
                    UI.GameView.AppendLine(("A member of the {0} visited {1}", ConsoleColor.Green, ConsoleColor.Black), isCoven ? "Coven" : "Mafia", GameState.ToName(playerID));
                    break;
                case ServerMessageType.ONE_DAY_BEFORE_STALEMATE:
                    UI.GameView.AppendLine(("If noone dies by tomorrow, the game will end in a draw", ConsoleColor.Green, ConsoleColor.Black));
                    break;
                // Add missing cases here
                case ServerMessageType.FULL_MOON_NIGHT:
                    UI.GameView.AppendLine(Localization.Of(LocalizationTable.FULL_MOON));
                    break;
                case ServerMessageType.IDENTIFY:
                    ServerMessageParsers.IDENTIFY.Build(buffer, index, length).Parse(out string value);
                    UI.GameView.AppendLine((value, ConsoleColor.Yellow, ConsoleColor.Black));
                    break;
                case ServerMessageType.END_GAME_INFO:
                    UI.CommandContext = CommandContext.POST_GAME;
                    UI.GameView.AppendLine(("Entered the post-game lobby", ConsoleColor.Green, ConsoleColor.Black));
                    break;
                // Add missing cases here
                case ServerMessageType.VAMPIRE_PROMOTION:
                    UI.GameView.AppendLine(("You have been bitten by a Vampire", ConsoleColor.DarkRed));
                    GameState.Role = Role.VAMPIRE;
                    break;
                case ServerMessageType.OTHER_VAMPIRES:
                    GameState.Team.Clear();
                    ServerMessageParsers.OTHER_VAMPIRES.Build(buffer, index, length).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out playerID).Parse(out bool youngestVampire);
                        //Console.WriteLine(youngestVampire ? "\t{0} (Youngest)" : "\t{0}", GameState.ToName(playerID));
                        // TODO: Find an easy way of displaying youngest
                        GameState.Team.Add(GameState.Players[(int)playerID]);
                        return root;
                    }, out _);
                    UI.OpenSideView(UI.TeamView);
                    break;
                case ServerMessageType.ADD_VAMPIRE:
                    ServerMessageParsers.ADD_VAMPIRE.Build(buffer, index, length).Parse(out playerID).Parse(out bool youngest);
                    UI.GameView.AppendLine(youngest ? "{0} is now the youngest vampire" : "{0} is now a vampire", GameState.ToName(playerID));
                    break;
                case ServerMessageType.CAN_VAMPIRES_CONVERT:
                    ServerMessageParsers.CAN_VAMPIRES_CONVERT.Build(buffer, index, length).Parse(out bool canConvert);
                    UI.GameView.AppendLine(canConvert ? "You may bite someone tonight" : "You cannot bite anyone tonight");
                    break;
                case ServerMessageType.VAMPIRE_DIED:
                    ServerMessageParsers.VAMPIRE_DIED.Build(buffer, index, length).Parse(out playerID).Parse(out targetID);
                    UI.GameView.AppendLine(("{0}, your teammate, died", ConsoleColor.DarkRed));
                    targetID.MatchSome(id => UI.GameView.AppendLine("{0} is now the youngest vampire", GameState.ToName(id)));
                    break;
                case ServerMessageType.VAMPIRE_HUNTER_PROMOTED:
                    UI.GameView.AppendLine(("All vampires have died, so you have become a vigilante with 1 bullet", ConsoleColor.DarkGreen));
                    GameState.Role = Role.VIGILANTE;
                    break;
                case ServerMessageType.VAMPIRE_VISITED_MESSAGE:
                    ServerMessageParsers.VAMPIRE_VISITED_MESSAGE.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine(("Vampires visited {0}", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID));
                    break;
                // Add missing cases here
                case ServerMessageType.TRANSPORTER_NOTIFICATION:
                    ServerMessageParsers.TRANSPORTER_NOTIFICATION.Build(buffer, index, length).Parse(out playerID).Parse(out receiverID);
                    UI.GameView.AppendLine(("A transporter transported {0} and {1}", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID), GameState.ToName(receiverID));
                    break;
                // Add missing cases here
                case ServerMessageType.TRACKER_NIGHT_ABILITY:
                    ServerMessageParsers.TRACKER_NIGHT_ABILITY.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine(("{0} was visited by your target", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID));
                    break;
                case ServerMessageType.AMBUSHER_NIGHT_ABILITY:
                    ServerMessageParsers.AMBUSHER_NIGHT_ABILITY.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine(("{0} was seen preparing an ambush while visiting your target", ConsoleColor.DarkRed), GameState.ToName(playerID));
                    break;
                case ServerMessageType.GUARDIAN_ANGEL_PROTECTION:
                    ServerMessageParsers.GUARDIAN_ANGEL_PROTECTION.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine(("{0} was protected by a Guardian Angel", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID));
                    break;
                case ServerMessageType.PIRATE_DUEL:
                    UI.CommandContext |= CommandContext.DUEL_DEFENDING;
                    UI.GameView.AppendLine(("You have been challenged to a duel by the Pirate", ConsoleColor.DarkRed));
                    break;
                case ServerMessageType.DUEL_TARGET:
                    UI.CommandContext |= CommandContext.DUEL_ATTACKING;
                    UI.GameView.AppendLine(("You have challenged your target to a duel", ConsoleColor.Green, ConsoleColor.Black));
                    break;
                // Add missing cases here
                case ServerMessageType.HAS_NECRONOMICON:
                    byte nightsUntil = 0;
                    ServerMessageParsers.HAS_NECRONOMICON.Build(buffer, index, length).Parse(out bool noNecronomicon).Parse(!noNecronomicon, p => p, p => p.Parse(out nightsUntil));
                    UI.GameView.AppendLine((noNecronomicon ? "{0} nights until the Coven possess the Necronomicon" : "The Coven now possess the Necronomicon", ConsoleColor.Green, ConsoleColor.Black), nightsUntil);
                    break;
                case ServerMessageType.OTHER_WITCHES:
                    GameState.Team.Clear();
                    ServerMessageParsers.OTHER_WITCHES.Build(buffer, index, length).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out playerID).Parse(out roleID);
                        PlayerState teammate = GameState.Players[(int)playerID];
                        teammate.Role = roleID;
                        GameState.Team.Add(teammate);
                        return root;
                    }, out _);
                    UI.OpenSideView(UI.TeamView);
                    break;
                case ServerMessageType.PSYCHIC_NIGHT_ABILITY:
                    Player playerID3 = Player.JAILOR;
                    ServerMessageParsers.PSYCHIC_NIGHT_ABILITY.Build(buffer, index, length).Parse(out bool isEvil).Parse(out playerID).Parse(out Player playerID2).Parse(!isEvil, p => p, p => p.Parse(out playerID3));
                    UI.GameView.AppendLine((isEvil ? "A vision revealed that {0}, {1}, or {2} is evil" : "A vision revealed that {0}, or {1} is good", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID), GameState.ToName(playerID2), GameState.ToName(playerID3));
                    break;
                case ServerMessageType.TRAPPER_NIGHT_ABILITY:
                    ServerMessageParsers.TRAPPER_NIGHT_ABILITY.Build(buffer, index, length).Parse(out roleID);
                    UI.GameView.AppendLine(("{0} has triggered your trap", ConsoleColor.DarkRed), roleID.ToString().ToDisplayName());
                    break;
                case ServerMessageType.TRAPPER_TRAP_STATUS:
                    ServerMessageParsers.TRAPPER_TRAP_STATUS.Build(buffer, index, length).Parse(out TrapStatus trapStatus);
                    UI.GameView.AppendLine(("Trap status: {0}", ConsoleColor.Green, ConsoleColor.Black), trapStatus.ToString().ToDisplayName());
                    break;
                case ServerMessageType.PESTILENCE_CONVERSION:
                    GameState.Role = Role.PESTILENCE;
                    break;
                case ServerMessageType.JUGGERNAUT_KILL_COUNT:
                    ServerMessageParsers.JUGGERNAUT_KILL_COUNT.Build(buffer, index, length).Parse(out byte killCount);
                    UI.GameView.AppendLine(("You have obtained {0} kills", ConsoleColor.Green, ConsoleColor.Black), killCount);
                    break;
                case ServerMessageType.COVEN_GOT_NECRONOMICON:
                    ServerMessageParsers.COVEN_GOT_NECRONOMICON.Build(buffer, index, length).Parse(out _).Parse(out playerID);
                    UI.GameView.AppendLine(("{0} now has the Necronomicon", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID));
                    break;
                case ServerMessageType.GUARDIAN_ANGEL_PROMOTED:
                    UI.GameView.AppendLine(("You failed to protect your target", ConsoleColor.DarkRed));
                    GameState.Role = Role.SURVIVOR;
                    break;
                case ServerMessageType.VIP_TARGET:
                    ServerMessageParsers.VIP_TARGET.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine(("{0} is the VIP", ConsoleColor.Green, ConsoleColor.Black), GameState.ToName(playerID));
                    break;
                case ServerMessageType.PIRATE_DUEL_OUTCOME:
                    ServerMessageParsers.PIRATE_DUEL_OUTCOME.Build(buffer, index, length).Parse(out DuelAttack duelAttack).Parse(out DuelDefense duelDefense);
                    UI.GameView.AppendLine(("Your {1} against the Pirate's {0}: you have {2} the duel", ConsoleColor.DarkRed), duelAttack, duelDefense, (byte)duelAttack == ((byte)duelDefense + 1) % 3 ? "lost" : "won");
                    break;
                // Add missing cases here
                case ServerMessageType.ACTIVE_GAME_MODES:
                    ActiveGameModes.Clear();
                    ServerMessageParsers.ACTIVE_GAME_MODES.Build(buffer, index, length).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out GameMode id);
                        ActiveGameModes.Add(id);
                        return root;
                    }, out _);
                    UI.RedrawView(UI.GameModeView);
                    break;
                // Add missing cases here
                case ServerMessageType.ZOMBIE_ROTTED:
                    ServerMessageParsers.ZOMBIE_ROTTED.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine(("You may no longer use {0}'s night ability", ConsoleColor.Green, ConsoleColor.Black), playerID);
                    break;
                case ServerMessageType.LOVER_TARGET:
                    ServerMessageParsers.LOVER_TARGET.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine(("{0} is your lover", ConsoleColor.Green, ConsoleColor.Black), playerID);
                    break;
                case ServerMessageType.PLAGUE_SPREAD:
                    ServerMessageParsers.PLAGUE_SPREAD.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine(("{0} has been infected with the plague", ConsoleColor.Green, ConsoleColor.Black), playerID);
                    break;
                case ServerMessageType.RIVAL_TARGET:
                    ServerMessageParsers.RIVAL_TARGET.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine(("{0} is your rival", ConsoleColor.Green, ConsoleColor.Black), playerID);
                    break;
                // Add missing cases here
                case ServerMessageType.JAILOR_DEATH_NOTE:
                    ServerMessageParsers.JAILOR_DEATH_NOTE.Build(buffer, index, length).Parse(out playerID).Parse(out _).Parse(out ExecuteReason executeReasonID);
                    UI.GameView.AppendLine("Reason for {0}'s execution: {1}", GameState.ToName(playerID), executeReasonID.ToString().ToDisplayName());
                    break;
                case ServerMessageType.DISCONNECTED:
                    ServerMessageParsers.DISCONNECTED.Build(buffer, index, length).Parse(out DisconnectReason dcReason);
                    UI.HomeView.ReplaceLine(0, ("Disconnected: {0}", ConsoleColor.DarkRed), dcReason);
                    break;
                case ServerMessageType.SPY_NIGHT_INFO:
                    ServerMessageParsers.SPY_NIGHT_INFO.Build(buffer, index, length).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out tableMessage);
                        UI.GameView.AppendLine(Localization.OfSpyResult(tableMessage));
                        return root;
                    }, out _);
                    break;
                // Add missing cases here
#if DEBUG
                default:
                    UI.GameView.AppendLine(("Uncaught message: {0}", ConsoleColor.DarkRed), ((ServerMessageType)buffer[index - 1]).ToString().ToDisplayName());
                    break;
#endif
            }
        }

        private void QueueReceive() => socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, Receive, null);

        private void Receive(IAsyncResult result)
        {
            try
            {
                Parser.Parse(buffer, 0, socket.EndReceive(result));
            }
            catch (SocketException ex)
            {
                Debug.WriteLine("Connection Error: {0}", ex.Message);
                Debug.WriteLine(ex.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error parsing message");
                Debug.WriteLine(ex.ToString());
            }
            try
            {
                QueueReceive();
            }
            catch (SocketException ex)
            {
                Debug.WriteLine("Connection Error: {0}", ex.Message);
                Debug.WriteLine(ex.ToString());
                UI.StatusLine = "Connection lost";
                UI.CommandContext = CommandContext.AUTHENTICATING;
            }
        }

        private async Task UpdateTimer()
        {
            for (int thisInc = ++timerIndex; timerIndex == thisInc && Timer > 0; Timer--) await Task.Delay(1000);
        }

        // Modified from https://stackoverflow.com/a/40869537
        private static SecureString ReadPassword()
        {
            SecureString pass = new SecureString();
            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey(true);
                // Backspace Should Not Work
                if (!char.IsControl(key.KeyChar)) pass.AppendChar(key.KeyChar);
                else if (key.Key == ConsoleKey.Backspace && pass.Length > 0) pass.RemoveAt(pass.Length - 1);
            }
            // Stops Receving Keys Once Enter is Pressed
            while (key.Key != ConsoleKey.Enter);
            return pass;
        }

        // Modified from https://codereview.stackexchange.com/a/107864
        private static byte[] ToByteArray(SecureString secureString, Encoding encoding = null)
        {
            if (secureString == null)
            {
                throw new ArgumentNullException(nameof(secureString));
            }
            encoding = encoding ?? Encoding.UTF8;
            IntPtr unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(secureString);
                return encoding.GetBytes(Marshal.PtrToStringUni(unmanagedString));
            }
            finally
            {
                if (unmanagedString != IntPtr.Zero)
                {
                    Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
                }
            }
        }

        // Modified from https://stackoverflow.com/a/311179
        private static string ByteArrayToHex(byte[] ba, int index, int length)
        {
            StringBuilder hex = new StringBuilder(length * 2);
            for (; index < length; index++) hex.AppendFormat("{0:x2}", ba[index]);
            //foreach (byte b in ba) hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }
    }

    public static class Extensions
    {
        public static void SafeReplace<T>(this List<T> list, int index, T value)
        {
            while (list.Count < index) list.Add(default(T));
            if (list.Count == index) list.Add(value);
            else list[index] = value;
        }

        public static V SafeIndex<K, V>(this Dictionary<K, V> dict, K key, Func<V> ifAbsent)
        {
            if (dict.ContainsKey(key)) return dict[key];
            V result = ifAbsent();
            dict.Add(key, result);
            return result;
        }

        public static string ToDisplayName(this string value) => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.Replace('_', ' ').ToLower());

        public static bool IsMafia(this Role role)
        {
            switch (role)
            {
                default:
                    return false;
                case Role.BLACKMAILER:
                case Role.CONSIGLIERE:
                case Role.CONSORT:
                case Role.DISGUISER:
                case Role.FORGER:
                case Role.FRAMER:
                case Role.GODFATHER:
                case Role.JANITOR:
                case Role.MAFIOSO:
                case Role.RANDOM_MAFIA:
                case Role.MAFIA_SUPPORT:
                case Role.MAFIA_DECEPTION:
                case Role.HYPNOTIST:
                case Role.COVEN_RANDOM_MAFIA:
                case Role.COVEN_MAFIA_SUPPORT:
                case Role.COVEN_MAFIA_DECEPTION:
                    return true;
            }
        }

        public static bool HasRankedQueue(this GameMode mode)
        {
            switch (mode)
            {
                default:
                    return false;
                case GameMode.RANKED:
                case GameMode.COVEN_RANKED:
                    return true;
            }
        }

        public static IEnumerable<string> Wrap(this string value, int lineWidth)
        {
            while (value.Length > lineWidth)
            {
                yield return value.Substring(0, lineWidth);
                value = value.Substring(lineWidth);
            }
            yield return value;
        }

        public static string[] SplitCommand(this string input) => input.Split(' ');

        public static string PadRightHard(this string value, int length) => value.Length > length ? value.Substring(0, length) : value.PadRight(length);

        public static IEnumerable<T> ListHeader<T>(this IEnumerable<T> enumerable, T value)
        {
            bool start = true;
            foreach (T t in enumerable)
            {
                if (start) yield return value;
                start = false;
                yield return t;
            }
        }

        public static string AddSpacing(this string value)
        {
            StringBuilder sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (char.IsUpper(c) && sb.Length > 0) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }

        internal static FormattedString Format(this FormattedString value, params object[] args) => new FormattedString(string.Format(value.Value, args), value.Foreground, value.Background);
    }
}
