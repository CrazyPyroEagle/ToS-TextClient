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
using ToSParser;

namespace ToSTextClient
{
    class TextClient
    {
        public const uint BUILD_NUMBER = 9069u;
        private static readonly byte[] MODULUS = new byte[] { 0xce, 0x22, 0x31, 0xcc, 0xc2, 0x33, 0xed, 0x95, 0xf8, 0x28, 0x6e, 0x77, 0xd7, 0xb4, 0xa6, 0x55, 0xe0, 0xad, 0xf5, 0x26, 0x08, 0x7b, 0xff, 0xaa, 0x2f, 0x78, 0x6a, 0x3f, 0x93, 0x54, 0x5f, 0x48, 0xb5, 0x89, 0x39, 0x83, 0xef, 0x1f, 0x61, 0x15, 0x1f, 0x18, 0xa0, 0xe1, 0xdd, 0x02, 0xa7, 0x42, 0x27, 0x77, 0x71, 0x8b, 0x79, 0xe9, 0x90, 0x8b, 0x0e, 0xe8, 0x4a, 0x33, 0xd2, 0x5d, 0xde, 0x1f, 0xb4, 0x7d, 0xf4, 0x35, 0xf5, 0xea, 0xf6, 0xe7, 0x04, 0x2c, 0xaf, 0x03, 0x71, 0xe4, 0x6f, 0x50, 0x7f, 0xd2, 0x70, 0x70, 0x39, 0xee, 0xa6, 0x0a, 0xae, 0xf7, 0xbc, 0x17, 0x51, 0x81, 0xf1, 0xd4, 0xf1, 0x33, 0x85, 0xf4, 0xab, 0x54, 0x3b, 0x1e, 0x42, 0x56, 0xa4, 0x79, 0xd1, 0x4e, 0xcc, 0xb4, 0xaa, 0xaa, 0x73, 0xa3, 0x35, 0xf4, 0xe6, 0x57, 0x66, 0xe6, 0x52, 0x0e, 0x51, 0x8b, 0x7e, 0x26, 0xe8, 0x63, 0xdf, 0x58, 0x57, 0x6b, 0x87, 0xdd, 0xd5, 0xf2, 0xb0, 0x58, 0x73, 0x7b, 0x10, 0x99, 0x5a, 0x99, 0x80, 0xe3, 0x8d, 0xde, 0x57, 0x98, 0xac, 0x9a, 0xf8, 0xf7, 0x37, 0x2c, 0x6f, 0x46, 0x4f, 0xf8, 0xba, 0xc8, 0x59, 0x57, 0x9d, 0x2f, 0xac, 0x38, 0xd8, 0x88, 0x89, 0xcd, 0x12, 0x3e, 0x08, 0x09, 0xb4, 0xcd, 0x5d, 0x05, 0x0b, 0x16, 0xce, 0x80, 0x6a, 0x19, 0xad, 0xea, 0xa9, 0xa2, 0x6c, 0x40, 0xba, 0x6d, 0x19, 0x74, 0x4b, 0x84, 0xd9, 0x46, 0xdc, 0xee, 0x93, 0x66, 0xb7, 0x4e, 0x98, 0xa7, 0x2c, 0x9a, 0x28, 0x0d, 0x3b, 0x7d, 0xb3, 0x90, 0x6f, 0x45, 0x18, 0x7c, 0x0c, 0xb1, 0x59, 0x5a, 0xb9, 0x16, 0xa2, 0x38, 0x2b, 0xcd, 0x2d, 0x2c, 0x48, 0xd7, 0x0d, 0xcc, 0xf0, 0x17, 0x60, 0x5c, 0x93, 0x39, 0x81, 0x28, 0xbd, 0x65, 0x8a, 0x5b, 0xb4, 0xe0, 0x51, 0x87, 0xc0, 0x77 };
        private static readonly byte[] EXPONENT = new byte[] { 0x01, 0x00, 0x01 };

        public string Username { get; protected set; }
        public uint TownPoints { get; set; }
        public uint MeritPoints { get; set; }
        public IList<GameModeID> ActiveGameModes { get; set; } = new List<GameModeID>();
        public IList<AchievementID> EarnedAchievements { get; set; } = new List<AchievementID>();
        public IList<CharacterID> OwnedCharacters { get; set; } = new List<CharacterID>();
        public IList<HouseID> OwnedHouses { get; set; } = new List<HouseID>();
        public IList<PetID> OwnedPets { get; set; } = new List<PetID>();
        public IList<LobbyIconID> OwnedLobbyIcons { get; set; } = new List<LobbyIconID>();
        public IList<DeathAnimationID> OwnedDeathAnimations { get; set; } = new List<DeathAnimationID>();
        public GameState GameState { get; set; }
        public TextUI UI { get; set; }
        public MessageParser Parser { get; protected set; }

        private Socket socket;
        private byte[] buffer;

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.Unicode;
            Console.Title = "Town of Salem (Unofficial Client)";
            Console.WriteLine("Build {0}", BUILD_NUMBER);
            Console.Write("Username: ");
            string user = Console.ReadLine();
            Console.Write("Password: ");
            SecureString pwrd = ReadPassword();
            Console.WriteLine("(hidden)", pwrd.Length);
            // TODO: Authentication and user input parsing
            RSA rsa = RSA.Create();
            RSAParameters rsaParams = new RSAParameters();
            rsaParams.Modulus = MODULUS;
            rsaParams.Exponent = EXPONENT;
            rsa.ImportParameters(rsaParams);
            byte[] pwrdb = ToByteArray(pwrd);
            byte[] encpw = rsa.Encrypt(pwrdb, RSAEncryptionPadding.Pkcs1);
            Array.Clear(pwrdb, 0, pwrdb.Length);

            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Console.WriteLine("Connecting to server");
            socket.Connect("live4.tos.blankmediagames.com", 3600);
            ((ConsoleUI)new TextClient(socket, user, Convert.ToBase64String(encpw)).UI).Run();
        }

        TextClient(Socket socket, string user, string encpw)
        {
            this.socket = socket;
            buffer = new byte[4096];
            Parser = new MessageParser(ParseMessage, (buffer, index, length) => socket.Send(buffer, index, length, SocketFlags.None));
            Parser.Authenticate(AuthenticationModeID.BMG_FORUMS, true, BUILD_NUMBER, user, encpw);
            UI = new ConsoleUI(this);
            UI.HomeView.ReplaceLine(0, "Authenticating...");
            UI.RedrawView(UI.HomeView);
            QueueReceive();
        }

        private void ParseMessage(byte[] buffer, int index, int length)
        {
            //Console.WriteLine("Received {0}", (ServerMessageType)buffer[index]);
            switch ((ServerMessageType)buffer[index++])
            {
                case ServerMessageType.AUTHENTICATED:
                    ServerMessageParsers.AUTHENTICATED.Build(buffer, index, length).Parse(out bool registered);
                    if (!registered) UI.HomeView.ReplaceLine(0, "Authentication failed: registration required");
                    else UI.HomeView.ReplaceLine(0, "Authenticated. Loading user information...");
                    break;
                case ServerMessageType.CREATE_LOBBY:
                    ServerMessageParsers.CREATE_LOBBY.Build(buffer, index, length).Parse(out bool host).Parse(out GameModeID gameMode);
                    GameState = new GameState(this, gameMode);
                    GameState.Host = host;
                    UI.GameView.Clear();
                    UI.SetMainView(UI.GameView);
                    UI.GameView.AppendLine("Joined a lobby for {0}", gameMode.ToString().ToDisplayName());
                    break;
                case ServerMessageType.SET_HOST:
                    GameState.Host = true;
                    break;
                case ServerMessageType.USER_JOINED_GAME:
                    ServerMessageParsers.USER_JOINED_GAME.Build(buffer, index, length).Parse(out host).Parse(out bool display).Parse(out string username).Parse(out PlayerID playerID).Parse(out LobbyIconID lobbyIconID);
                    if (display) UI.GameView.AppendLine("{0} has joined the game", username);
                    if (host) GameState.HostID = playerID;
                    GameState.PlayerCount++;
                    PlayerState playerState = GameState.Players[(int)playerID];
                    playerState.Name = username;
                    playerState.SelectedLobbyIcon = lobbyIconID;
                    break;
                case ServerMessageType.USER_LEFT_GAME:
                    ServerMessageParsers.USER_LEFT_GAME.Build(buffer, index, length).Parse(out bool update).Parse(out display).Parse(out playerID);
                    if (display) UI.GameView.AppendLine("{0} has left the game", GameState.ToName(playerID));
                    if (update) GameState.RemovePlayer(playerID);
                    else GameState.Players[(int)playerID].Left = true;
                    break;
                case ServerMessageType.CHAT_BOX_MESSAGE:
                    playerID = PlayerID.JAILOR;
                    string message = null;
                    Func<Parser<PlayerID, Parser<string, RootParser>>, RootParser> map = parser => parser.Parse(out playerID).Parse(out message);
                    ServerMessageParsers.CHAT_BOX_MESSAGE.Build(buffer, index, length).Parse(GameState.Started, map, map);
                    UI.GameView.AppendLine("{0}: {1}", GameState.ToName(playerID), message);
                    break;
                // Add missing cases here
                case ServerMessageType.HOST_CLICKED_ON_ADD_BUTTON:
                    ServerMessageParsers.HOST_CLICKED_ON_ADD_BUTTON.Build(buffer, index, length).Parse(out RoleID role);
                    GameState.AddRole(role);
                    break;
                case ServerMessageType.HOST_CLICKED_ON_REMOVE_BUTTON:
                    ServerMessageParsers.HOST_CLICKED_ON_REMOVE_BUTTON.Build(buffer, index, length).Parse(out byte slotID);
                    GameState.RemoveRole(slotID);
                    break;
                case ServerMessageType.HOST_CLICKED_ON_START_BUTTON:
                    UI.GameView.AppendLine("The game will start in 10 seconds");
                    break;
                case ServerMessageType.CANCEL_START_COOLDOWN:
                    UI.GameView.AppendLine("The start cooldown was cancelled");
                    break;
                case ServerMessageType.ASSIGN_NEW_HOST:
                    ServerMessageParsers.ASSIGN_NEW_HOST.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine("The host has been repicked");
                    GameState.HostID = playerID;
                    break;
                case ServerMessageType.VOTED_TO_REPICK_HOST:
                    ServerMessageParsers.VOTED_TO_REPICK_HOST.Build(buffer, index, length).Parse(out byte votesNeeded);
                    UI.GameView.AppendLine("{0} votes are needed to repick the host", votesNeeded);
                    break;
                case ServerMessageType.NO_LONGER_HOST:
                    GameState.Host = false;
                    break;
                case ServerMessageType.DO_NOT_SPAM:
                    UI.GameView.AppendLine("Please do not spam the chat");
                    break;
                case ServerMessageType.HOW_MANY_PLAYERS_AND_GAMES:
                    ServerMessageParsers.HOW_MANY_PLAYERS_AND_GAMES.Build(buffer, index, length).Parse(out uint onlinePlayers).Parse(out uint activeGames);
                    UI.GameView.AppendLine("There are currently {0} players online and {1} games being played", onlinePlayers, activeGames);
                    break;
                case ServerMessageType.SYSTEM_MESSAGE:
                    ServerMessageParsers.SYSTEM_MESSAGE.Build(buffer, index, length).Parse(out message);
                    UI.GameView.AppendLine("(System) {0}", message);
                    break;
                case ServerMessageType.STRING_TABLE_MESSAGE:
                    ServerMessageParsers.STRING_TABLE_MESSAGE.Build(buffer, index, length).Parse(out LocalizationTableID tableMessage);
                    UI.GameView.AppendLine(tableMessage.ToEnglish());     // TODO: Write the localized message instead of the name
                    break;
                // Add missing cases here
                case ServerMessageType.USER_INFORMATION:
                    ServerMessageParsers.USER_INFORMATION.Build(buffer, index, length).Parse(out username).Parse(out uint townPoints).Parse(out uint meritPoints);
                    UI.HomeView.ReplaceLine(0, "{0} ({1} TP, {2} MP)", Username = username, TownPoints = townPoints, MeritPoints = meritPoints);
                    break;
                // Add missing cases here
                case ServerMessageType.FORCED_LOGOUT:
                    UI.StatusLine = "Forcefully disconnected";
                    break;
                case ServerMessageType.RETURN_TO_HOME_PAGE:
                    UI.SetMainView(UI.HomeView);
                    GameState = null;
                    break;
                // Add missing cases here
                case ServerMessageType.PURCHASED_CHARACTERS:
                    OwnedCharacters.Clear();
                    ServerMessageParsers.PURCHASED_CHARACTERS.Build(buffer, index, length).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out CharacterID id);
                        OwnedCharacters.Add(id);
                        return root;
                    }, out _);
                    break;
                case ServerMessageType.PURCHASED_HOUSES:
                    OwnedHouses.Clear();
                    ServerMessageParsers.PURCHASED_HOUSES.Build(buffer, index, length).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out HouseID id);
                        OwnedHouses.Add(id);
                        return root;
                    }, out _);
                    break;
                // Add missing cases here
                case ServerMessageType.PURCHASED_PETS:
                    OwnedPets.Clear();
                    ServerMessageParsers.PURCHASED_PETS.Build(buffer, index, length).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out PetID id);
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
                        RootParser root = parser.Parse(out AchievementID id);
                        EarnedAchievements.Add(id);
                        return root;
                    }, out int achievementCount);
                    UI.HomeView.ReplaceLine(2, "Number of achievements earned: {0}", achievementCount);
                    break;
                case ServerMessageType.PURCHASED_LOBBY_ICONS:
                    OwnedLobbyIcons.Clear();
                    ServerMessageParsers.PURCHASED_LOBBY_ICONS.Build(buffer, index, length).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out LobbyIconID id);
                        OwnedLobbyIcons.Add(id);
                        return root;
                    }, out _);
                    break;
                case ServerMessageType.PURCHASED_DEATH_ANIMATIONS:
                    OwnedDeathAnimations.Clear();
                    ServerMessageParsers.PURCHASED_DEATH_ANIMATIONS.Build(buffer, index, length).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out DeathAnimationID id);
                        OwnedDeathAnimations.Add(id);
                        return root;
                    }, out _);
                    break;
                // Add missing cases here
                case ServerMessageType.START_RANKED_QUEUE:
                    ServerMessageParsers.START_RANKED_QUEUE.Build(buffer, index, length).Parse(out bool requeue).Parse(out seconds);
                    if (requeue) UI.StatusLine = "Requeued due to a lack of players";
                    UI.HomeView.ReplaceLine(3, "The ranked game will start in {0} seconds", seconds);
                    break;
                case ServerMessageType.LEAVE_RANKED_QUEUE:
                    UI.HomeView.ReplaceLine(3, "You have left the ranked queue");
                    break;
                case ServerMessageType.ACCEPT_RANKED_POPUP:
                    UI.HomeView.ReplaceLine(3, "Are you ready for the Ranked game? (10 seconds to reply)");
                    break;
                // Add missing cases here
                case ServerMessageType.RANKED_TIMEOUT_DURATION:
                    ServerMessageParsers.RANKED_TIMEOUT_DURATION.Build(buffer, index, length).Parse(out seconds);
                    UI.StatusLine = string.Format("Timed out for {0} seconds", seconds);
                    break;
                // Add missing cases here
                case ServerMessageType.USER_JOINING_LOBBY_TOO_QUICKLY_MESSAGE:
                    UI.StatusLine = "Wait 15 seconds before rejoining";
                    break;
                // Add missing cases here
                case ServerMessageType.PICK_NAMES:
                    ServerMessageParsers.PICK_NAMES.Build(buffer, index, length).Parse(out byte playerCount);
                    GameState.PlayerCount = playerCount;
                    GameState.OnStart();
                    UI.GameView.AppendLine("Please choose a name (or wait to get a random name)");
                    break;
                case ServerMessageType.NAMES_AND_POSITIONS_OF_USERS:
                    ServerMessageParsers.NAMES_AND_POSITIONS_OF_USERS.Build(buffer, index, length).Parse(out playerID).Parse(out string name);
                    GameState.Players[(int)playerID].Name = name;
                    break;
                case ServerMessageType.ROLE_AND_POSITION:
                    ServerMessageParsers.ROLE_AND_POSITION.Build(buffer, index, length).Parse(out RoleID roleID).Parse(out playerID).Parse(out Option<PlayerID> targetID);
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
                    List<DeathCauseID> causes = new List<DeathCauseID>();
                    ServerMessageParsers.WHO_DIED_AND_HOW.Build(buffer, index, length).Parse(out playerID).Parse(out roleID).Parse(out display).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out DeathCauseID id);
                        causes.Add(id);
                        return root;
                    }, out int count);
                    playerState = GameState.Players[(int)playerID];
                    playerState.Role = roleID;
                    playerState.Dead = true;
                    if (causes.Count > 0) UI.GameView.AppendLine("Death causes: {0}", string.Join(", ", causes.Select(dc => dc.ToString().ToDisplayName())));
                    UI.GameView.AppendLine("Their role was {0}", roleID.ToString().ToDisplayName());
                    break;
                // Add missing cases here
                case ServerMessageType.START_DISCUSSION:
                    UI.GameView.AppendLine("Discussion may now begin");
                    break;
                case ServerMessageType.START_VOTING:
                    ServerMessageParsers.START_VOTING.Build(buffer, index, length).Parse(out bool hideVotesNeeded);
                    if (!hideVotesNeeded) UI.GameView.AppendLine("{0} votes are needed to lynch someone", (GameState.Players.Where(ps => !ps.Dead).Count() + 1) / 2);
                    break;
                case ServerMessageType.START_DEFENSE_TRANSITION:
                    ServerMessageParsers.START_DEFENSE_TRANSITION.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine("{0} has been voted up to the stand", GameState.ToName(playerID));
                    break;
                case ServerMessageType.START_JUDGEMENT:
                    UI.GameView.AppendLine("You may now vote guilty or innocent");
                    break;
                case ServerMessageType.TRIAL_FOUND_GUILTY:
                    ServerMessageParsers.TRIAL_FOUND_GUILTY.Build(buffer, index, length).Parse(out byte guiltyVotes).Parse(out byte innocentVotes);
                    UI.GameView.AppendLine("Judgement results: {0} guilty - {1} innocent", guiltyVotes, innocentVotes);
                    break;
                case ServerMessageType.TRIAL_FOUND_NOT_GUILTY:
                    ServerMessageParsers.TRIAL_FOUND_NOT_GUILTY.Build(buffer, index, length).Parse(out guiltyVotes).Parse(out innocentVotes);
                    UI.GameView.AppendLine("Judgement results: {0} guilty - {1} innocent", guiltyVotes, innocentVotes);
                    break;
                case ServerMessageType.LOOKOUT_NIGHT_ABILITY_MESSAGE:
                    ServerMessageParsers.LOOKOUT_NIGHT_ABILITY_MESSAGE.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine("{0} visited your target", GameState.ToName(playerID));
                    break;
                case ServerMessageType.USER_VOTED:
                    ServerMessageParsers.USER_VOTED.Build(buffer, index, length).Parse(out playerID).Parse(out PlayerID votedID).Parse(out byte voteCount);
                    UI.GameView.AppendLine("{0} has placed {2} votes against {1}", GameState.ToName(playerID), GameState.ToName(votedID), voteCount);
                    break;
                case ServerMessageType.USER_CANCELED_VOTE:
                    ServerMessageParsers.USER_CANCELED_VOTE.Build(buffer, index, length).Parse(out playerID).Parse(out votedID).Parse(out voteCount);
                    UI.GameView.AppendLine("{0} has cancelled their {2} votes against {1}", GameState.ToName(playerID), GameState.ToName(votedID), voteCount);
                    break;
                case ServerMessageType.USER_CHANGED_VOTE:
                    ServerMessageParsers.USER_CHANGED_VOTE.Build(buffer, index, length).Parse(out playerID).Parse(out votedID).Parse(out _).Parse(out voteCount);
                    UI.GameView.AppendLine("{0} has changed their {2} votes to be against {1}", GameState.ToName(playerID), GameState.ToName(votedID), voteCount);
                    break;
                case ServerMessageType.USER_DIED:
                    UI.GameView.AppendLine("You have died");
                    break;
                case ServerMessageType.RESURRECTION:
                    ServerMessageParsers.RESURRECTION.Build(buffer, index, length).Parse(out playerID).Parse(out roleID);
                    UI.GameView.AppendLine("{0} ({1}) has been brought back to life", GameState.ToName(playerID), roleID.ToString().ToDisplayName());
                    break;
                case ServerMessageType.TELL_ROLE_LIST:
                    int listIndex = 0;
                    ServerMessageParsers.TELL_ROLE_LIST.Build(buffer, index, length).Parse(parser => parser.Parse(out GameState.Roles[listIndex++]), out _);
                    UI.RedrawView(UI.RoleListView);
                    break;
                case ServerMessageType.USER_CHOSEN_NAME:
                    ServerMessageParsers.USER_CHOSEN_NAME.Build(buffer, index, length).Parse(out tableMessage).Parse(out playerID).Parse(out name);
                    GameState.Players[(int)playerID].Name = name;
                    UI.GameView.AppendLine(tableMessage.ToEnglish(), GameState.ToName(playerID));
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
                    UI.GameView.AppendLine("An Amnesiac has remembered {0}", roleID.ToString().ToDisplayName());
                    break;
                case ServerMessageType.AMNESIAC_CHANGED_ROLE:
                    ServerMessageParsers.AMNESIAC_CHANGED_ROLE.Build(buffer, index, length).Parse(out roleID).Parse(out targetID);
                    UI.GameView.AppendLine("You successfully remembered your role");
                    GameState.Role = roleID;
                    targetID.MatchSome(id => GameState.Target = id);
                    break;
                case ServerMessageType.BROUGHT_BACK_TO_LIFE:
                    UI.GameView.AppendLine("You have been brought back to life");
                    break;
                case ServerMessageType.START_FIRST_DAY:
                    GameState.Day = 1;
                    break;
                case ServerMessageType.BEING_JAILED:
                    UI.GameView.AppendLine("You have been dragged off to jail");
                    break;
                case ServerMessageType.JAILED_TARGET:
                    ServerMessageParsers.JAILED_TARGET.Build(buffer, index, length).Parse(out playerID).Parse(out bool canExecute).Parse(out bool executedTown);
                    UI.GameView.AppendLine("You have dragged {0} off to jail", GameState.ToName(playerID));
                    UI.GameView.AppendLine(canExecute ? "You may execute tonight" : executedTown ? "You cannot execute any more as you have executed a Town member" : "You cannot execute tonight");
                    break;
                case ServerMessageType.USER_JUDGEMENT_VOTED:
                    ServerMessageParsers.USER_JUDGEMENT_VOTED.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine("{0} has voted", GameState.ToName(playerID));
                    break;
                case ServerMessageType.USER_CHANGED_JUDGEMENT_VOTE:
                    ServerMessageParsers.USER_CHANGED_JUDGEMENT_VOTE.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine("{0} has changed their voted", GameState.ToName(playerID));
                    break;
                case ServerMessageType.USER_CANCELED_JUDGEMENT_VOTE:
                    ServerMessageParsers.USER_CANCELED_JUDGEMENT_VOTE.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine("{0} has cancelled their vote", GameState.ToName(playerID));
                    break;
                case ServerMessageType.TELL_JUDGEMENT_VOTES:
                    ServerMessageParsers.TELL_JUDGEMENT_VOTES.Build(buffer, index, length).Parse(out playerID).Parse(out JudgementVoteID judgementVote);
                    UI.GameView.AppendLine("{0} voted {1}", GameState.ToName(playerID), judgementVote.ToString().ToLower().Replace('_', ' '));
                    break;
                case ServerMessageType.EXECUTIONER_COMPLETED_GOAL:
                    UI.GameView.AppendLine("You have successfully gotten your target lynched");
                    break;
                case ServerMessageType.JESTER_COMPLETED_GOAL:
                    UI.GameView.AppendLine("You have successfully gotten yourself lynched");
                    break;
                case ServerMessageType.MAYOR_REVEALED:
                    ServerMessageParsers.MAYOR_REVEALED.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine("{0} has revealed themselves as the mayor", GameState.ToName(playerID));
                    break;
                /*case ServerMessageType.MAYOR_REVEALED_AND_ALREADY_VOTED:
                    break;*/
                case ServerMessageType.DISGUISER_STOLE_YOUR_IDENTITY:
                    ServerMessageParsers.DISGUISER_STOLE_YOUR_IDENTITY.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine("A disguiser stole your identity. You are now {0}", GameState.ToName(playerID));
                    break;
                case ServerMessageType.DISGUISER_CHANGED_IDENTITY:
                    ServerMessageParsers.DISGUISER_CHANGED_IDENTITY.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine("You have successfully disguised yourself as {0}", GameState.ToName(playerID));
                    break;
                case ServerMessageType.DISGUISER_CHANGED_UPDATE_MAFIA:
                    ServerMessageParsers.DISGUISER_CHANGED_UPDATE_MAFIA.Build(buffer, index, length).Parse(out playerID).Parse(out PlayerID disguiserID);
                    UI.GameView.AppendLine("{1} has successfully disguised themselves as {0}", GameState.ToName(playerID), GameState.ToName(disguiserID));
                    break;
                case ServerMessageType.MEDIUM_IS_TALKING_TO_US:
                    UI.GameView.AppendLine("A medium is talking to you");
                    break;
                case ServerMessageType.MEDIUM_COMMUNICATING:
                    UI.GameView.AppendLine("You have opened a communication with the living");
                    break;
                case ServerMessageType.TELL_LAST_WILL:
                    ServerMessageParsers.TELL_LAST_WILL.Build(buffer, index, length).Parse(out playerID).Parse(out bool hasLastWill).Parse(hasLastWill, parser =>
                    {
                        RootParser root = parser.Parse(out string will);
                        GameState.Players[(int)playerID].LastWill = will;
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
                    ServerMessageParsers.MAFIA_TARGETING.Build(buffer, index, length).Parse(out playerID).Parse(out roleID).Parse(out PlayerID teamTargetID);
                    UI.GameView.AppendLine(teamTargetID == PlayerID.JAILOR ? "{0} ({1}) has unset their target" : "{0} ({1}) has set their target to {2}", GameState.ToName(playerID), roleID.ToString().ToDisplayName(), GameState.ToName(teamTargetID));
                    break;
                case ServerMessageType.TELL_JANITOR_TARGETS_ROLE:
                    ServerMessageParsers.TELL_JANITOR_TARGETS_ROLE.Build(buffer, index, length).Parse(out roleID);
                    UI.GameView.AppendLine("You secretly know that your target's role was {0}", roleID.ToString().ToDisplayName());
                    break;
                case ServerMessageType.TELL_JANITOR_TARGETS_WILL:
                    ServerMessageParsers.TELL_JANITOR_TARGETS_WILL.Build(buffer, index, length).Parse(out playerID).Parse(out string lastWill);
                    GameState.Players[(int)playerID].LastWill = lastWill;
                    break;
                case ServerMessageType.SOMEONE_HAS_WON:
                    List<PlayerID> winners = new List<PlayerID>();
                    RepeatParser<Parser<PlayerID, RootParser>, RootParser> winnerParser = ServerMessageParsers.SOMEONE_HAS_WON.Build(buffer, index, length).Parse(out FactionID factionID);
                    winnerParser.Parse(p =>
                    {
                        RootParser root = p.Parse(out playerID);
                        winners.Add(playerID);
                        return root;
                    }, out _);
                    UI.GameView.AppendLine(winners.Count > 0 ? "Winning faction: {0} ({1})" : "Winning faction: {0}", factionID.ToString().ToDisplayName(), string.Join(", ", winners.Select(p => GameState.ToName(p))));
                    if (winners.Contains(GameState.Self)) UI.GameView.AppendLine("You have won");
                    break;
                case ServerMessageType.MAFIOSO_PROMOTED_TO_GODFATHER:
                    UI.GameView.AppendLine("You have been promoted to Godfather");
                    // TODO: Check if role should be changed (as well as for the other role change messages).
                    break;
                case ServerMessageType.MAFIOSO_PROMOTED_TO_GODFATHER_UPDATE_MAFIA:
                    ServerMessageParsers.MAFIOSO_PROMOTED_TO_GODFATHER_UPDATE_MAFIA.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine("{0} has been promoted to Godfather", GameState.ToName(playerID));
                    GameState.Players[(int)playerID].Role = RoleID.GODFATHER;
                    break;
                case ServerMessageType.MAFIA_PROMOTED_TO_MAFIOSO:
                    UI.GameView.AppendLine("You have been promoted to Mafioso");
                    break;
                case ServerMessageType.TELL_MAFIA_ABOUT_MAFIOSO_PROMOTION:
                    ServerMessageParsers.TELL_MAFIA_ABOUT_MAFIOSO_PROMOTION.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine("{0} has been promoted to Mafioso", GameState.ToName(playerID));
                    GameState.Players[(int)playerID].Role = RoleID.MAFIOSO;
                    break;
                case ServerMessageType.EXECUTIONER_CONVERTED_TO_JESTER:
                    UI.GameView.AppendLine("Your target has died");
                    break;
                case ServerMessageType.AMNESIAC_BECAME_MAFIA_OR_WITCH:
                    ServerMessageParsers.AMNESIAC_BECAME_MAFIA_OR_WITCH.Build(buffer, index, length).Parse(out playerID).Parse(out roleID);
                    UI.GameView.AppendLine("{0} has remembered {1}", GameState.ToName(playerID), roleID.ToString().ToDisplayName());
                    break;
                case ServerMessageType.USER_DISCONNECTED:
                    ServerMessageParsers.USER_DISCONNECTED.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine("{0} has left the game", GameState.ToName(playerID));
                    break;
                case ServerMessageType.MAFIA_WAS_JAILED:
                    ServerMessageParsers.MAFIA_WAS_JAILED.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine("{0} has been dragged off to jail", GameState.ToName(GameState.Players[(int)playerID]));
                    break;
                case ServerMessageType.INVALID_NAME_MESSAGE:
                    ServerMessageParsers.INVALID_NAME_MESSAGE.Build(buffer, index, length).Parse(out InvalidNameStatus invalidNameStatus);
                    UI.StatusLine = string.Format("Invalid name: {0}", invalidNameStatus);
                    break;
                // Add missing cases here
                case ServerMessageType.DEATH_NOTE:
                    ServerMessageParsers.DEATH_NOTE.Build(buffer, index, length).Parse(out playerID).Parse(out _).Parse(out string deathNote);
                    GameState.Players[(int)playerID].DeathNote = deathNote;
                    break;
                // Add missing cases here
                case ServerMessageType.RESURRECTION_SET_ALIVE:
                    ServerMessageParsers.RESURRECTION_SET_ALIVE.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine("{0} has been brought back to life (set alive)", GameState.ToName(playerID));
                    break;
                case ServerMessageType.START_DEFENSE:
                    UI.GameView.AppendLine("What is your defense?");
                    break;
                case ServerMessageType.USER_LEFT_DURING_SELECTION:
                    ServerMessageParsers.USER_LEFT_DURING_SELECTION.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine("{0} left during selection", GameState.ToName(playerID));
                    GameState.Players[(int)playerID].Left = true;
                    break;
                case ServerMessageType.VIGILANTE_KILLED_TOWN:
                    UI.GameView.AppendLine("You put your gun away out of fear of shooting another town member");
                    break;
                case ServerMessageType.NOTIFY_USERS_OF_PRIVATE_MESSAGE:
                    ServerMessageParsers.NOTIFY_USERS_OF_PRIVATE_MESSAGE.Build(buffer, index, length).Parse(out playerID).Parse(out PlayerID receiverID);
                    UI.GameView.AppendLine("{0} is whispering to {1}", GameState.ToName(playerID), GameState.ToName(receiverID));
                    break;
                case ServerMessageType.PRIVATE_MESSAGE:
                    receiverID = default(PlayerID);
                    message = default(string);       // These values are ignored at runtime, but the compiler will complain without these assignments.
                    ServerMessageParsers.PRIVATE_MESSAGE.Build(buffer, index, length).Parse(out PrivateMessageType pmType).Parse(out playerID).Parse(pmType != PrivateMessageType.FROM_TO, parser => parser.Parse(out message), parser => parser.Parse(out receiverID).Parse(out message));
                    switch (pmType)
                    {
                        case PrivateMessageType.TO:
                            UI.GameView.AppendLine("To {0}: {1}", GameState.ToName(playerID), message);
                            break;
                        case PrivateMessageType.FROM:
                            UI.GameView.AppendLine("From {0}: {1}", GameState.ToName(playerID), message);
                            break;
                        case PrivateMessageType.FROM_TO:
                            UI.GameView.AppendLine("From {0} to {1}: {2}", GameState.ToName(playerID), GameState.ToName(receiverID), message);
                            break;
                    }
                    break;
                case ServerMessageType.EARNED_ACHIEVEMENTS_161:
                    ServerMessageParsers.EARNED_ACHIEVEMENTS_161.Build(buffer, index, length).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out AchievementID id);
                        UI.GameView.AppendLine("You have earned the achievement {0}", id.ToString().ToDisplayName());
                        return root;
                    }, out _);
                    break;
                case ServerMessageType.AUTHENTICATION_FAILED:
                    ServerMessageParsers.AUTHENTICATION_FAILED.Build(buffer, index, length).Parse(out AuthenticationResult authResult);
                    UI.HomeView.ReplaceLine(0, "Authentication failed: {0}", authResult);
                    break;
                case ServerMessageType.SPY_NIGHT_ABILITY_MESSAGE:
                    ServerMessageParsers.SPY_NIGHT_ABILITY_MESSAGE.Build(buffer, index, length).Parse(out factionID).Parse(out playerID);
                    UI.GameView.AppendLine("A member of the {0} visited {1}", GameState.ToName(playerID));
                    break;
                case ServerMessageType.ONE_DAY_BEFORE_STALEMATE:
                    UI.GameView.AppendLine("If noone dies by tomorrow, the game will end in a draw");
                    break;
                // Add missing cases here
                case ServerMessageType.FULL_MOON_NIGHT:
                    UI.GameView.AppendLine("There is a full moon out tonight");
                    break;
                // Add missing cases here
                case ServerMessageType.VAMPIRE_PROMOTION:
                    UI.GameView.AppendLine("You have been bitten by a Vampire");        // TODO: Check if I should set role here.
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
                    UI.GameView.AppendLine("{0}, your teammate, died");
                    targetID.MatchSome(id => UI.GameView.AppendLine("{0} is now the youngest vampire", GameState.ToName(id)));
                    break;
                case ServerMessageType.VAMPIRE_HUNTER_PROMOTED:
                    UI.GameView.AppendLine("All vampires have died, so you have become a vigilante with 1 bullet");     // TODO: Check if I should set role here.
                    break;
                case ServerMessageType.VAMPIRE_VISITED_MESSAGE:
                    ServerMessageParsers.VAMPIRE_VISITED_MESSAGE.Build(buffer, index, length).Parse(out playerID);
                    UI.GameView.AppendLine("Vampires visited {0}", GameState.ToName(playerID));
                    break;
                // Add missing cases here
                case ServerMessageType.TRANSPORTER_NOTIFICATION:
                    ServerMessageParsers.TRANSPORTER_NOTIFICATION.Build(buffer, index, length).Parse(out playerID).Parse(out receiverID);
                    UI.GameView.AppendLine("A transporter transported {0} and {1}", GameState.ToName(playerID), GameState.ToName(receiverID));
                    break;
                // Add missing cases here
                case ServerMessageType.ACTIVE_GAME_MODES:
                    ActiveGameModes.Clear();
                    ServerMessageParsers.ACTIVE_GAME_MODES.Build(buffer, index, length).Parse(parser =>
                    {
                        RootParser root = parser.Parse(out GameModeID id);
                        ActiveGameModes.Add(id);
                        return root;
                    }, out _);
                    UI.RedrawView(UI.GameModeView);
                    break;
                // Add missing cases here
                case ServerMessageType.JAILOR_DEATH_NOTE:
                    ServerMessageParsers.JAILOR_DEATH_NOTE.Build(buffer, index, length).Parse(out playerID).Parse(out _).Parse(out ExecuteReasonID executeReasonID);
                    UI.GameView.AppendLine("Reason for {0}'s execution: {1}", GameState.ToName(playerID), executeReasonID.ToString().ToDisplayName());
                    break;
                case ServerMessageType.DISCONNECTED:
                    ServerMessageParsers.DISCONNECTED.Build(buffer, index, length).Parse(out DisconnectReasonID dcReason);
                    UI.HomeView.ReplaceLine(0, "Disconnected: {0}", dcReason);
                    UI.RedrawView(UI.HomeView);
                    break;
                case ServerMessageType.SPY_NIGHT_INFO:
                    ServerMessageParsers.SPY_NIGHT_INFO.Build(buffer, index, length).Parse(out tableMessage);
                    UI.GameView.AppendLine("Target saw message: {0}", tableMessage.ToString().ToDisplayName());
                    break;
                // Add missing cases here
            }
        }

        private void QueueReceive() => socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, Receive, null);

        private void Receive(IAsyncResult result)
        {
            try
            {
                Parser.Parse(buffer, 0, socket.EndReceive(result));
                QueueReceive();
            }
            catch (SocketException ex)
            {
                Debug.WriteLine("Connection Error: {0}", ex.Message);
                socket.Close();
                Environment.Exit(-1);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error parsing message");
                Debug.WriteLine(ex.ToString());
            }
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

        public static bool IsMafia(this RoleID role)
        {
            switch (role)
            {
                default:
                    return false;
                case RoleID.BLACKMAILER:
                case RoleID.CONSIGLIERE:
                case RoleID.CONSORT:
                case RoleID.DISGUISER:
                case RoleID.FORGER:
                case RoleID.FRAMER:
                case RoleID.GODFATHER:
                case RoleID.JANITOR:
                case RoleID.MAFIOSO:
                case RoleID.RANDOM_MAFIA:
                case RoleID.MAFIA_SUPPORT:
                case RoleID.MAFIA_DECEPTION:
                case RoleID.HYPNOTIST:
                case RoleID.COVEN_RANDOM_MAFIA:
                case RoleID.COVEN_MAFIA_SUPPORT:
                case RoleID.COVEN_MAFIA_DECEPTION:
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

        public static string ToEnglish(this LocalizationTableID id)
        {
            switch (id)
            {
                case LocalizationTableID.SHERIFF_TARGET_MEMBER_OF_MAFIA:
                    return "Your target is a member of the mafia!";
                case LocalizationTableID.SHERIFF_TARGET_NOT_SUSPICIOUS:
                    return "Your target is not suspicious";
                case LocalizationTableID.SHERIFF_TARGET_MEMBER_OF_CULT:
                    return "Your target is a member of the cult!";
                case LocalizationTableID.SHERIFF_TARGET_SERIAL_KILLER:
                    return "Your target is a Serial Killer!";
                case LocalizationTableID.EXECUTED_BY_JAILOR:
                    return "You were executed by the Jailor!";
                case LocalizationTableID.TRANSPORTED_BUT_JAILED:
                    return "Someone tried to transport you but you were in jail";
                case LocalizationTableID.TRANSPORTED:
                    return "You were transported to another location";
                case LocalizationTableID.ROLE_BLOCKED_BUT_JAILED:
                    return "Someone tried to role block you but you were in jail";
                case LocalizationTableID.ROLE_BLOCKED:
                    return "Someone occupied your night. You were role blocked!";
                case LocalizationTableID.BLACKMAILED_BUT_JAILED:
                    return "Someone tried to blackmail you but you were in jail";
                case LocalizationTableID.BLACKMAILED:
                    return "Someone threatened to reveal your secrets. You are blackmailed!";
                case LocalizationTableID.DOUSED_BUT_JAILED:
                    return "Someone tried to douse you in gas but you were in jail";
                case LocalizationTableID.DOUSED:
                    return "You were doused in gas!";
                case LocalizationTableID.ATTACKED_BUT_JAILED:
                    return "Someone tried to attack you but you were in jail";
                case LocalizationTableID.ATTACKED_BUT_BODYGUARD_PROTECTED:
                    return "You were attacked but someone fought off your attacker!";
                case LocalizationTableID.ATTACKED_BUT_HEALED:
                    return "You were attacked but someone nursed you back to health!";
                case LocalizationTableID.ATTACKED_BY_MAFIA:
                    return "You were attacked by a member of the Mafia!";
                case LocalizationTableID.ATTACKED_BY_SERIAL_KILLER:
                    return "You were attacked by a Serial Killer!";
                case LocalizationTableID.SHOT_BY_VIGILANTE:
                    return "You were shot by a Vigilante!";
                case LocalizationTableID.IGNITED_BY_ARSONIST:
                    return "You were set on fire by an Arsonist!";
                case LocalizationTableID.SHOT_BY_VETERAN:
                    return "You were shot by the Veteran you visited!";
                case LocalizationTableID.KILLED_PROTECTING:
                    return "You were killed protecting your target!";
                case LocalizationTableID.TARGET_ATTACKED:
                    return "Your target was attacked last night!";
                case LocalizationTableID.DOUSED_BUT_BODYGUARD_PROTECTED:
                    return "An Arsonist tried to douse you in gas but a Bodyguard fought off your attacker!";
                case LocalizationTableID.MURDERED_BY_VISITED_SERIAL_KILLER:
                    return "You were murdered by the Serial Killer you visited!";
                case LocalizationTableID.TARGET_IS_FRAMER:
                    return "Your target has a desire to deceive. They must be a Framer!";
                case LocalizationTableID.INVESTIGATOR_TARGET_IS_ESCORT:
                    return "Your target could be an Escort, Transporter, or Consort";
                case LocalizationTableID.INVESTIGATOR_TARGET_IS_DOCTOR:
                    return "Your target could be a Doctor, Disguiser, or Serial Killer";
                case LocalizationTableID.INVESTIGATOR_TARGET_IS_INVESTIGATOR:
                    return "Your target could be an Investigator, Consigliere, or Mayor";
                case LocalizationTableID.TARGET_IS_MYSTERY_ROLE:
                    return "Your target is the Mystery role!";
                case LocalizationTableID.INVESTIGATOR_TARGET_IS_BODYGUARD:
                    return "Your target could be a Bodyguard, Godfather, or Arsonist";
                case LocalizationTableID.INVESTIGATOR_TARGET_IS_VIGILANTE:
                    return "Your target could be a Vigilante, Veteran, or Mafioso";
                case LocalizationTableID.INVESTIGATOR_TARGET_IS_MEDIUM:
                    return "Your target could be a Medium, Janitor, or Retributionist";
                case LocalizationTableID.INVESTIGATOR_TARGET_IS_SURVIVOR:
                    return "Your target could be a Survivor, Vampire Hunter, or Amnesiac";
                case LocalizationTableID.INVESTIGATOR_TARGET_IS_SPY:
                    return "Your target could be a Spy, Blackmailer, or Jailor";
                case LocalizationTableID.INVESTIGATOR_TARGET_IS_SHERIFF:
                    return "Your target could be a Sheriff, Executioner, or Werewolf";
                case LocalizationTableID.UNUSED:
                    return "UNUSED";
                case LocalizationTableID.INVESTIGATOR_TARGET_IS_FRAMER:
                    return "Your target could be a Framer, Vampire, or Jester";
                case LocalizationTableID.INVESTIGATOR_TARGET_IS_LOOKOUT:
                    return "Your target could be a Lookout, Forger, or Witch";
                case LocalizationTableID.ATTACKED_BY_BODYGUARD_BUT_HEALED:
                    return "A Bodyguard attacked you but someone nursed you back to health!";
                case LocalizationTableID.ATTACKED_BY_BODYGUARD_BUT_BODYGUARD_PROTECTED:
                    return "A Bodyguard attacked you but someone fought off your attacker!";
                case LocalizationTableID.KILLED_BY_BODYGUARD:
                    return "You were killed by a Bodyguard!";
                case LocalizationTableID.ATTACKED_BUT_DEFENSE_TOO_STRONG:
                    return "Someone attacked you but your defense was too strong!";
                case LocalizationTableID.TRANSPORTER_TARGET_JAILED:
                    return "One of your targets was jailed so you could not transport them!";
                case LocalizationTableID.TARGET_IS_BODYGUARD:
                    return "Your target is a trained protector: they must be a Bodyguard";
                case LocalizationTableID.TARGET_IS_TRANSPORTER:
                    return "Your target specializes in transportation: they must be a Transporter";
                case LocalizationTableID.TARGET_IS_DOCTOR:
                    return "Your target is a professional surgeon: they must be a Doctor";
                case LocalizationTableID.TARGET_IS_ESCORT:
                    return "Your target is a beautiful person working for the town: they must be an Escort";
                case LocalizationTableID.TARGET_IS_INVESTIGATOR:
                    return "Your target gathers information about people: they must be an Investigator";
                case LocalizationTableID.TARGET_IS_JAILOR:
                    return "Your target detains people at night: they must be a Jailor";
                case LocalizationTableID.TARGET_IS_LOOKOUT:
                    return "Your target watches who visits people at night: they must be a Lookout";
                case LocalizationTableID.TARGET_IS_MASON:
                    return "Your target is a member of a secret town organization: they must be a Mason";
                case LocalizationTableID.TARGET_IS_MASON_LEADER:
                    return "Your target is the leader of a secret town organization: they must be a Mason Leader";
                case LocalizationTableID.TARGET_IS_MAYOR:
                    return "Your target is the leader of the town: they must be the Mayor";
                case LocalizationTableID.TARGET_IS_SHERIFF:
                    return "Your target is a protector of the town: they must be a Sheriff";
                case LocalizationTableID.TARGET_IS_SPY:
                    return "Your target secretly watches who someone visits: they must be a Spy";
                case LocalizationTableID.TARGET_IS_VETERAN:
                    return "Your target is a paranoid war hero: they must be a Veteran";
                case LocalizationTableID.TARGET_IS_VIGILANTE:
                    return "Your target will bend the law to enact justice: they must be a Vigilante";
                case LocalizationTableID.TARGET_IS_MEDIUM:
                    return "Your target speaks with the dead: they must be a Medium";
                case LocalizationTableID.TARGET_IS_RETRIBUTIONIST:
                    return "Your target wields mystical powers: they must be a Retributionist";
                case LocalizationTableID.TARGET_IS_BLACKMAILER:
                    return "Your target uses information to silence people: they must be a Blackmailer";
                case LocalizationTableID.TARGET_IS_CONSIGLIERE:
                    return "Your target gathers information for the Mafia: they must be a Consigliere";
                case LocalizationTableID.TARGET_IS_CONSORT:
                    return "Your target is a beautiful person working for the Mafia: they must be a Consort";
                case LocalizationTableID.TARGET_IS_DISGUISER:
                    return "Your target pretends to be other people: they must be a Disguiser";
                case LocalizationTableID.TARGET_IS_FORGER:
                    return "Your target is good at forging documents: they must be a Forger";
                case LocalizationTableID.TARGET_IS_GODFATHER:
                    return "Your target is the leader of the Mafia: they must be the Godfather";
                case LocalizationTableID.TARGET_IS_JANITOR:
                    return "Your target cleans up dead bodies: they must be a Janitor";
                case LocalizationTableID.TARGET_IS_MAFIOSO:
                    return "Your target does the Godfather's dirty work: they must be a Mafioso";
                case LocalizationTableID.TARGET_IS_AMNESIAC:
                    return "Your target does not remember their role: they must be an Amnesiac";
                case LocalizationTableID.TARGET_IS_ARSONIST:
                    return "Your target likes to watch things burn: they must be an Arsonist";
                case LocalizationTableID.TARGET_IS_CULTIST:
                    return "Your target is a member of an evil secret organization: they must be a Cultist";
                case LocalizationTableID.TARGET_IS_EXECUTIONER:
                    return "Your target wants someone to be lynched at any cost: they must be an Executioner";
                case LocalizationTableID.TARGET_IS_JESTER:
                    return "Your target wants to be lynched: they must be a Jester";
                case LocalizationTableID.TARGET_IS_SERIAL_KILLER:
                    return "Your target wants to kill everyone: they must be a Serial Killer";
                case LocalizationTableID.TARGET_IS_SURVIVOR:
                    return "Your target simply wants to live: they must be a Survivor";
                case LocalizationTableID.TARGET_IS_WITCH:
                    return "Your target casts spells on people: they must be a Witch";
                case LocalizationTableID.WITCH_TARGET_JAILED:
                    return "Your target was jailed so you could not control them";
                case LocalizationTableID.ROLEBLOCKED_BUT_IMMUNE:
                    return "Someone tried to role block you but you are immune!";
                case LocalizationTableID.CONTROLLED_BY_WITCH:
                    return "You feel a mystical power dominating you. You were controlled by a Witch!";
                case LocalizationTableID.CONTROLLED_BY_WITCH_BUT_IMMUNE:
                    return "A Witch tried to control you but you are immune";
                case LocalizationTableID.CONTROLLED_BUT_JAILED:
                    return "Someone tried to control you but you were jailed";
                case LocalizationTableID.JOINED_THE_TOWN:
                    return "{0} has joined the Town";
                case LocalizationTableID.ATTEMPTED_TO_REMEMBER:
                    return "You have attempted to remember who you were!";
                case LocalizationTableID.HAUNTED_BY_JESTER:
                    return "You were haunted by the Jester. You committed suicide over the guilt!";
                case LocalizationTableID.ATTACKED_BUT_VESTED:
                    return "You were attacked but your bulletproof vest saved you!";
                case LocalizationTableID.SHOT_VISITOR:
                    return "You shot someone who visited you last night!";
                case LocalizationTableID.ATTACKED_BUT_ALERT_DEFENSE_TOO_STRONG:
                    return "Someone tried to attack you but your defense while on alert was too strong!";
                case LocalizationTableID.HAUNTED_BY_JESTER_BUT_HEALED:
                    return "You were haunted by the Jester. You tried to commit suicide but someone healed you!";
                case LocalizationTableID.VIGILANTE_SUICIDE:
                    return "You could not get over the guilt of killing a town member. You shot yourself!";
                case LocalizationTableID.ATTACKED_ROLE_BLOCKER:
                    return "Someone role blocked you, so you attacked them!";
                case LocalizationTableID.ORDERED_BY_GODFATHER:
                    return "The Godfather has ordered you to kill his target";
                case LocalizationTableID.CANNOT_RESURRECT_DISCONNECTED:
                    return "You cannot resurrect a user who has left the game";
                case LocalizationTableID.CLEANED_GASOLINE_OFF:
                    return "You have cleaned the gasoline off of yourself";
                case LocalizationTableID.JAILOR_DECIDED_TO_EXECUTE:
                    return "The Jailor has decided to execute you";
                case LocalizationTableID.JAILOR_CHANGED_MIND:
                    return "The Jailor has changed his mind";
                case LocalizationTableID.KILLED_BY_JAILED_SERIAL_KILLER:
                    return "You were killed by the Serial Killer you jailed";
                case LocalizationTableID.ATTACKED_JAILOR:
                    return "You attacked the Jailor!";
                case LocalizationTableID.TARGET_DEFENSE_TOO_STRONG:
                    return "Your target's defense was too strong to kill";
                case LocalizationTableID.LYNCHED_JESTER:
                    return "The Jester will get his revenge from the grave!";
                case LocalizationTableID.JESTER_RANDOM_TARGET_CHOSEN:
                    return "You did not select a target so one was chosen at random";
                case LocalizationTableID.FULL_MOON:
                    return "There is a full moon out tonight";
                case LocalizationTableID.ATTACKED_SOMEONE:
                    return "You attacked someone";
                case LocalizationTableID.SHERIFF_TARGET_IS_WEREWOLF:
                    return "Your target is a Werewolf!";
                case LocalizationTableID.ATTACKED_BY_WEREWOLF:
                    return "You were attacked by a Werewolf!";
                case LocalizationTableID.TURNED_INTO_WEREWOLF:
                    return "The light of the full moon has transformed you into a rampaging Werewolf!";
                case LocalizationTableID.ROLE_BLOCKED_SO_STAYED_AT_HOME:
                    return "Someone role blocked you so you stayed at home";
                case LocalizationTableID.TARGET_IS_WEREWOLF:
                    return "Your target howls at the moon: they must be a Werewolf";
                case LocalizationTableID.DID_NOT_PERFORM_DAY_ABILITY:
                    return "You did not perform your day ability";
                case LocalizationTableID.DID_NOT_PERFORM_NIGHT_ABILITY:
                    return "You did not perform your night ability";
                case LocalizationTableID.STAKED_BY_VAMPIRE_HUNTER:
                    return "You were staked by a Vampire Hunter!";
                case LocalizationTableID.STAKED_BY_VISITED_VAMPIRE_HUNTER:
                    return "You were staked by the Vampire Hunter you visited!";
                case LocalizationTableID.STAKED_ATTACKING_VAMPIRE:
                    return "You staked the Vampire who attacked you!";
                case LocalizationTableID.TARGET_IS_VAMPIRE_HUNTER:
                    return "Your target tracks Vampires: they must be a Vampire Hunter";
                case LocalizationTableID.TARGET_IS_VAMPIRE:
                    return "Your target drinks blood: they must be a Vampire";
                case LocalizationTableID.ATTACKED_BY_VAMPIRE:
                    return "You were attacked by a Vampire!";
                case LocalizationTableID.CANNOT_WHISPER_REVEALED_MAYOR:
                    return "You can't whisper to a revealed Mayor";
                case LocalizationTableID.MAYOR_CANNOT_WHISPER_WHEN_REVEALED:
                    return "You can't whisper once you have revealed as the Mayor";
                case LocalizationTableID.MAFIA_SOMEONE_IS_LISTENING:
                    return "You feel that someone can hear your conversations";
                case LocalizationTableID.MAFIA_NOBODY_IS_LISTENING:
                    return "You feel like you can speak privately";
                // Add missing cases here
            }
            return id.ToString().ToDisplayName();
        }
    }
}
