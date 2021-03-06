﻿using Optional;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
    public class TextClient
    {
        public const uint BUILD_NUMBER = 10404u;
        private static readonly byte[] MODULUS = new byte[] { 0xce, 0x22, 0x31, 0xcc, 0xc2, 0x33, 0xed, 0x95, 0xf8, 0x28, 0x6e, 0x77, 0xd7, 0xb4, 0xa6, 0x55, 0xe0, 0xad, 0xf5, 0x26, 0x08, 0x7b, 0xff, 0xaa, 0x2f, 0x78, 0x6a, 0x3f, 0x93, 0x54, 0x5f, 0x48, 0xb5, 0x89, 0x39, 0x83, 0xef, 0x1f, 0x61, 0x15, 0x1f, 0x18, 0xa0, 0xe1, 0xdd, 0x02, 0xa7, 0x42, 0x27, 0x77, 0x71, 0x8b, 0x79, 0xe9, 0x90, 0x8b, 0x0e, 0xe8, 0x4a, 0x33, 0xd2, 0x5d, 0xde, 0x1f, 0xb4, 0x7d, 0xf4, 0x35, 0xf5, 0xea, 0xf6, 0xe7, 0x04, 0x2c, 0xaf, 0x03, 0x71, 0xe4, 0x6f, 0x50, 0x7f, 0xd2, 0x70, 0x70, 0x39, 0xee, 0xa6, 0x0a, 0xae, 0xf7, 0xbc, 0x17, 0x51, 0x81, 0xf1, 0xd4, 0xf1, 0x33, 0x85, 0xf4, 0xab, 0x54, 0x3b, 0x1e, 0x42, 0x56, 0xa4, 0x79, 0xd1, 0x4e, 0xcc, 0xb4, 0xaa, 0xaa, 0x73, 0xa3, 0x35, 0xf4, 0xe6, 0x57, 0x66, 0xe6, 0x52, 0x0e, 0x51, 0x8b, 0x7e, 0x26, 0xe8, 0x63, 0xdf, 0x58, 0x57, 0x6b, 0x87, 0xdd, 0xd5, 0xf2, 0xb0, 0x58, 0x73, 0x7b, 0x10, 0x99, 0x5a, 0x99, 0x80, 0xe3, 0x8d, 0xde, 0x57, 0x98, 0xac, 0x9a, 0xf8, 0xf7, 0x37, 0x2c, 0x6f, 0x46, 0x4f, 0xf8, 0xba, 0xc8, 0x59, 0x57, 0x9d, 0x2f, 0xac, 0x38, 0xd8, 0x88, 0x89, 0xcd, 0x12, 0x3e, 0x08, 0x09, 0xb4, 0xcd, 0x5d, 0x05, 0x0b, 0x16, 0xce, 0x80, 0x6a, 0x19, 0xad, 0xea, 0xa9, 0xa2, 0x6c, 0x40, 0xba, 0x6d, 0x19, 0x74, 0x4b, 0x84, 0xd9, 0x46, 0xdc, 0xee, 0x93, 0x66, 0xb7, 0x4e, 0x98, 0xa7, 0x2c, 0x9a, 0x28, 0x0d, 0x3b, 0x7d, 0xb3, 0x90, 0x6f, 0x45, 0x18, 0x7c, 0x0c, 0xb1, 0x59, 0x5a, 0xb9, 0x16, 0xa2, 0x38, 0x2b, 0xcd, 0x2d, 0x2c, 0x48, 0xd7, 0x0d, 0xcc, 0xf0, 0x17, 0x60, 0x5c, 0x93, 0x39, 0x81, 0x28, 0xbd, 0x65, 0x8a, 0x5b, 0xb4, 0xe0, 0x51, 0x87, 0xc0, 0x77 };
        private static readonly byte[] EXPONENT = new byte[] { 0x01, 0x00, 0x01 };

        // We have to use 0xFF0000, 0x00FF00, 0x00CCFF, and 0x505050 to stay within the 16 colour limit
        public static readonly Color BLACK = Color.Black;
        public static readonly Color WHITE = Color.White;
        public static readonly Color GRAY = Color.LightGray;
        public static readonly Color YELLOW = Color.Yellow;
        public static readonly Color MAGENTA = Color.Magenta;
        public static readonly Color GREEN = Color.FromArgb(0, 0xFF, 0);
        public static readonly Color RED = Color.Red;
        public static readonly Color BLUE = Color.FromArgb(0, 0xCC, 0xFF);
        public static readonly Color PINK = Color.Pink;
        public static readonly Color ORANGE = Color.Orange;
        public static readonly Color BROWN = Color.SaddleBrown;
        public static readonly Color PURPLE = Color.DarkMagenta;
        public static readonly Color LIME = Color.Lime;
        public static readonly Color DARK_RED = Color.DarkRed;
        public static readonly Color DARK_YELLOW = Color.FromArgb(255, 200, 0);
        public static readonly Color DARK_GRAY = Color.FromArgb(0x50, 0x50, 0x50);

        public string Username { get; protected set; }
        public uint TownPoints { get => _TownPoints; set { _TownPoints = value; UI.Views.Home.PinnedView.Redraw(); } }
        public uint MeritPoints { get => _MeritPoints; set { _MeritPoints = value; UI.Views.Home.PinnedView.Redraw(); } }
        public byte PermissionLevel { get => _PermissionLevel; protected set { _PermissionLevel = value; UI.Views.GameModes.Redraw(); } }        // TODO: Figure out the best way of getting this value
        public IList<GameMode> ActiveGameModes { get; set; } = new List<GameMode>();
        public IList<Achievement> EarnedAchievements { get; set; } = new List<Achievement>();
        public IList<Character> OwnedCharacters { get; set; } = new List<Character>();
        public IList<House> OwnedHouses { get; set; } = new List<House>();
        public IList<Pack> OwnedPacks { get; set; } = new List<Pack>();
        public IList<Pet> OwnedPets { get; set; } = new List<Pet>();
        public IList<LobbyIcon> OwnedLobbyIcons { get; set; } = new List<LobbyIcon>();
        public IList<DeathAnimation> OwnedDeathAnimations { get; set; } = new List<DeathAnimation>();
        public bool ShareSkin { get => _ShareSkin; set { Parser.UpdateSettings(Setting.DISPLAY_SKINS, (_ShareSkin = value) ? (byte)1 : (byte)0); UI.Views.Settings.Redraw(); } }
        public Language QueueLanguage { get => _QueueLanguage; set { Parser.UpdateSettings(Setting.SELECTED_QUEUE_LANGUAGE, (byte)(_QueueLanguage = value)); UI.Views.Settings.Redraw(); } }
        public FriendList Friends { get => _Friends; set { _Friends = value; UI.Views.Friends.Redraw(); } }
        public IDictionary<string, uint> PendingFriendRequests { get => _PendingFriendRequests; set { _PendingFriendRequests = value; UI.Views.Notifications.Redraw(); } }
        public IDictionary<string, uint> PendingPartyInvitations { get => _PendingPartyInvitations; set { _PendingPartyInvitations = value; UI.Views.Notifications.Redraw(); } }
        public PartyState Party { get => _PartyState; set { _PartyState = value;  UI.OpenSideView(UI.Views.Party); UI.RedrawCursor(); } }
        public GameState GameState
        {
            get => _GameState;
            set
            {
                if (value != null)
                {
                    _GameState = value;
                    UI.Views.Game.Clear();
                    UI.CommandContext = CommandContext.LOBBY;
                    UI.SetMainView(UI.Views.Game);
                    UI.Views.Game.AppendLine(("Joined a lobby for {0}", GREEN, null), value.GameMode.ToString().ToDisplayName());
                    Party = null;
                }
                else
                {
                    UI.CommandContext = CommandContext.HOME;
                    UI.SetMainView(UI.Views.Home);
                    UI.Views.LastWill.Title = "";
                    UI.Views.LastWill.Value = "";
                    _GameState = value;
                }
            }
        }
        public ITextUI UI { get; }
        public MessageParser Parser { get; protected set; }
        public ServerMessageParser MessageParser { get; protected set; }
        public ResourceLoader Resources { get; protected set; }

        private Socket socket;
        private byte[] buffer;

        private uint _TownPoints;
        private uint _MeritPoints;
        private byte _PermissionLevel = 1;
        private bool _ShareSkin;
        private Language _QueueLanguage;
        private FriendList _Friends;
        private IDictionary<string, uint> _PendingFriendRequests = new Dictionary<string, uint>();
        private IDictionary<string, uint> _PendingPartyInvitations = new Dictionary<string, uint>();
        private PartyState _PartyState;
        private GameState _GameState;

        static void Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.Unicode;
                Console.Title = "Town of Salem (Unofficial Client)";
                new ConsoleUI()
                {
                    CommandContext = CommandContext.AUTHENTICATING
                }.Run();
                return;
            }
            catch (Exception e)
            {
                Console.Clear();
                Console.WriteLine("Exception occurred during startup. Please report this issue, including the following text.");
                Console.WriteLine(e.ToString());
            }
            while (true) ;
        }

        public TextClient(ConsoleUI ui, Socket socket, string authUsername, SecureString authPassword)
        {
            UI = ui;
            Resources = new ResourceLoader(UI);
            Friends = new FriendList(UI);
            UI.RegisterCommand(new Command("Disconnect from the server", CommandExtensions.IsAuthenticated, cmd =>
            {
                socket.Close();
                UI.CommandContext = CommandContext.AUTHENTICATING;
                UI.SetMainView(UI.Views.Auth);
                UI.SetInputContext(UI.Views.Auth);
            }), "dc", "logout", "logoff", "disconnect");
            UI.RegisterCommand(new Command<GameMode>("Join a lobby for {0}", CommandContext.HOME.Set(), ArgumentParsers.ForEnum<GameMode>(UI), (cmd, gameMode) =>
            {
                if (ActiveGameModes.Contains(gameMode))
                {
                    if (Party != null)
                    {
                        if (!gameMode.HasRankedQueue()) Parser.PartyStart(gameMode);
                        else UI.StatusLine = string.Format("Cannot join {0} when in a party", gameMode.ToString().ToDisplayName());
                    }
                    else
                    {
                        if (gameMode.HasRankedQueue()) Parser.JoinRankedQueue(gameMode);
                        else Parser.JoinLobby(gameMode);
                    }
                }
                else UI.StatusLine = string.Format("Cannot join game mode: {0}", gameMode.ToString().ToDisplayName());
            }), "join");
            UI.RegisterCommand(new CommandGroup("Friend-related actions", UI, "Subcommand", "Subcommands")
                .Register(new Command<string>("Send {0} a friend request", CommandContext.HOME.Set(), ArgumentParsers.Username(UI), (cmd, username) => Parser.RequestFriend(username)), "add")
                .Register(new Command<string>("Unfriend {0}", CommandContext.HOME.Set(), ArgumentParsers.Username(UI), (cmd, username) =>
                {
                    if (Friends.TryGetValue(username, out FriendState state)) Parser.RemoveFriend(username, state.UserID);
                    else UI.StatusLine = string.Format("{0} is not in your friend list", username);
                }), "remove")
                .Register(new Command<string, string>("Send {1} to {0}", CommandContext.HOME.Set(), ArgumentParsers.Username(UI), ArgumentParsers.Text(UI, "Message"), (cmd, username, message) => Parser.SendFriendMessage(username, message)), "m", "message")
                .Register(new Command<string>("Decline {0}'s friend request", CommandContext.HOME.Set(), ArgumentParsers.Username(UI), (cmd, username) =>
                {
                    if (PendingFriendRequests.TryGetValue(username, out uint userID))
                    {
                        Parser.DeclineFriendRequest(userID);
                        PendingFriendRequests.Remove(username);
                    }
                    else UI.StatusLine = string.Format("No pending friend request from {0}", username);
                }), "decline", "refuse")
                .Register(new Command<string>("Accept a friend request", CommandContext.HOME.Set(), ArgumentParsers.Username(UI), (cmd, username) =>
                {
                    if (PendingFriendRequests.TryGetValue(username, out uint userID)) Parser.AcceptFriend(username, userID);
                    else UI.StatusLine = string.Format("No pending friend request from {0}", username);
                }), "accept"), "f", "friend");
            UI.RegisterCommand(new CommandGroup("Party-related actions", UI, "Subcommand", "Subcommands")
                .Register(new Command<Option<Brand>>("Create a party for {0}", CommandContext.HOME.Set().And(c => Party == null), ArgumentParsers.Optional(ArgumentParsers.ForEnum<Brand>(UI)), (cmd, brand) => Parser.CreateParty(brand.ValueOr(Brand.CLASSIC))), "c", "create")
                .Register(new Command("Leave this party", CommandContext.HOME.Set().And(c => Party != null), cmd => Parser.RequestLeaveParty()), "l", "leave")
                .Register(new Command<Brand>("Set this party's brand to {0}", CommandContext.HOME.Set().And(c => (Party?.Members?[Username]?.PermissionLevel ?? PartyPermissionLevel.None) >= PartyPermissionLevel.Host), ArgumentParsers.ForEnum<Brand>(UI), (cmd, brand) => Parser.SetPartyConfig(brand, Party.SelectedMode)), "b", "brand")
                .Register(new Command<string>("Invite {0} to your party", CommandContext.HOME.Set().And(c => (Party?.Members?[Username]?.PermissionLevel ?? PartyPermissionLevel.None) >= PartyPermissionLevel.CanInvite), ArgumentParsers.Username(UI), (cmd, username) => Parser.InviteToParty(username)), "i", "invite")
                .Register(new Command<string>("Allow {0} to invite", CommandContext.HOME.Set().And(c => (Party?.Members?[Username]?.PermissionLevel ?? PartyPermissionLevel.None) >= PartyPermissionLevel.Host), ArgumentParsers.Username(UI), (cmd, username) => Parser.GivePartyInvitePower(username)), "gi", "giveinvite", "give invite")
                .Register(new Command<string>("Pass party host on to {0}", CommandContext.HOME.Set().And(c => (Party?.Members?[Username]?.PermissionLevel ?? PartyPermissionLevel.None) >= PartyPermissionLevel.Host), ArgumentParsers.Username(UI), (cmd, username) => Parser.SetPartyHost(username)), "sh", "sethost", "set host")
                .Register(new Command<string>("Kick {0}", CommandContext.HOME.Set().And(c => (Party?.Members?[Username]?.PermissionLevel ?? PartyPermissionLevel.None) >= PartyPermissionLevel.Host), ArgumentParsers.Username(UI), (cmd, username) => Parser.KickFromParty(username)), "k", "kick")
                .Register(new Command<string>("Decline {0}'s invitation", CommandContext.HOME.Set(), ArgumentParsers.Username(UI), (cmd, username) =>
                {
                    if (PendingPartyInvitations.TryGetValue(username, out uint userID)) Parser.RespondPartyInvite(PartyInviteResponse.REFUSED, userID);
                    else UI.StatusLine = string.Format("No pending party invitation from {0}", username);
                }), "d", "decline", "refuse")
                .Register(new Command<string>("Accept {0}'s invitation", CommandContext.HOME.Set().And(c => Party == null), ArgumentParsers.Username(UI), (cmd, username) =>
                {
                    if (PendingPartyInvitations.TryGetValue(username, out uint userID))
                    {
                        Parser.RespondPartyInvite(PartyInviteResponse.ACCEPTING, userID);
                        Parser.RespondPartyInvite(PartyInviteResponse.ACCEPTED, userID);
                    }
                    else UI.StatusLine = string.Format("No pending party invitation from {0}", username);
                }), "a", "accept"), "p", "party");
            UI.RegisterCommand(new Command("Leave the queue", CommandContext.HOME.Set(), cmd => ClientMessageParsers.LeaveRankedQueue(Parser)), "leavequeue");
            UI.RegisterCommand(new Command("Accept the queue popup", CommandContext.HOME.Set(), cmd => Parser.AcceptRanked()), "accept");
            UI.RegisterCommand(new Command("Exit the game", context => true, cmd => UI.RunInput = false), "quit", "exit");
            UI.RegisterCommand(new Command("Leave the game", CommandExtensions.IsInLobbyOrGame, cmd => Parser.LeaveGame()), "leave");
            UI.RegisterCommand(new Command("Leave the post-game lobby", CommandContext.POST_GAME.Set(), cmd => Parser.LeavePostGameLobby()), "leavepost");
            UI.RegisterCommand(new Command("Vote to repick the host", CommandContext.LOBBY.Set(), cmd => Parser.VoteToRepickHost()), "repick");
            UI.RegisterCommand(new Command<Role>("Add {0} to the role list", context => context == CommandContext.LOBBY && Resources.GetMetadata(GameState.GameMode).LobbyType == "Custom" && GameState.Host, ArgumentParsers.ForEnum<Role>(UI, map: Resources.Of), (cmd, role) =>
            {
                IEnumerable<(Role role, byte? limit)> meta = Resources.GetMetadata(GameState.GameMode).Catalog.Where(r => r.role == role);
                if (!meta.Any())
                {
                    UI.StatusLine = string.Format("Cannot add {0} to this lobby", Resources.Of(role));
                    return;
                }
                (_, byte? limit) = meta.First();
                if (limit == null || limit > GameState.Roles.Count(r => r == role))
                {
                    Parser.ClickedOnAddButton(role);
                    GameState.AddRole(role);
                }
                else UI.StatusLine = string.Format("Cannot add {0} to this lobby: limit of {1} has been reached", Resources.Of(role), meta.First().limit);
            }), "add");
            UI.RegisterCommand(new Command<byte>("Remove {0} from the role list", context => context == CommandContext.LOBBY && Resources.GetMetadata(GameState.GameMode).LobbyType == "Custom" && GameState.Host, ArgumentParsers.Position(UI), (cmd, position) =>
            {
                if (position < GameState.Roles.Length)
                {
                    Parser.ClickedOnRemoveButton(position);
                    GameState.RemoveRole(position);
                }
                else UI.StatusLine = string.Format("Cannot remove role at index {0} from this lobby: no role found at that index", position + 1);
            }), "remove");
            UI.RegisterCommand(new Command("Force the game to start", context => context == CommandContext.LOBBY && GameState.Host, cmd => Parser.ClickedOnStartButton()), "start");
            UI.RegisterCommand(new Command<string>("Set your name to {0}", CommandContext.PICK_NAMES.Set(), ArgumentParsers.Text(UI, "Name"), (cmd, name) => Parser.ChooseName(name)), "n", "name");
            UI.RegisterCommand(new Command<Player>("Set your target to {0}", CommandContext.NIGHT.Set(), ArgumentParsers.Player(UI), (cmd, target) =>
            {
                // TODO: Add check for whether <target> is a valid target for the user's role
                Parser.SetTarget(target);
                if (GameState.Role.IsMafia() || GameState.Role.IsCoven()) Parser.SetTargetMafiaOrWitch(target, target == Player.JAILOR ? TargetType.CANCEL_TARGET_1 : TargetType.SET_TARGET_1);
                UI.Views.Game.AppendLine(target == Player.JAILOR ? "Unset target" : "Set target to {0}", GameState.ToName(target));
            }), "t", "target");
            UI.RegisterCommand(new Command<Player>("Set your second target to {0}", CommandContext.NIGHT.Set(), ArgumentParsers.Player(UI), (cmd, target) =>
            {
                // TODO: Add check for whether <target> is a valid second target for the user's role
                Parser.SetSecondTarget(target);
                if (GameState.Role.IsMafia() || GameState.Role.IsCoven()) Parser.SetTargetMafiaOrWitch(target, target == Player.JAILOR ? TargetType.CANCEL_TARGET_2 : TargetType.SET_TARGET_2);
                UI.Views.Game.AppendLine(target == Player.JAILOR ? "Unset secondary target" : "Set secondary target to {0}", GameState.ToName(target));
            }), "t2", "target2");
            UI.RegisterCommand(new Command<Player>("Set your day choice to {0}", CommandContext.DAY.Set(), ArgumentParsers.Player(UI), (cmd, target) =>
            {
                // TODO: Add check for whether <target> is a valid day choice for the user's role
                Parser.SetDayChoice(target);
                UI.Views.Game.AppendLine(target == Player.JAILOR ? "Unset day target" : "Set day target to {0}", GameState.ToName(target));
            }), "td", "targetday");
            UI.RegisterCommand(new Command<DuelAttack>("Attack with your {0}", context => context == CommandContext.NIGHT && GameState.NightState.HasFlag(NightState.DUEL_ATTACKING), ArgumentParsers.ForEnum<DuelAttack>(UI, "Attack", true), (cmd, attack) =>
            {
                Parser.SetPirateChoice((byte)attack);
                UI.Views.Game.AppendLine("You have decided to attack with your {0}", attack.ToString().ToLower().Replace('_', ' '));
            }), "attack");
            UI.RegisterCommand(new Command<DuelDefense>("Defend with your {0}", context => context == CommandContext.NIGHT && GameState.NightState.HasFlag(NightState.DUEL_DEFENDING), ArgumentParsers.ForEnum<DuelDefense>(UI, "Defense"), (cmd, defense) =>
            {
                Parser.SetPirateChoice((byte)defense);
                UI.Views.Game.AppendLine("You have decided to defend with your {0}", defense.ToString().ToLower().Replace('_', ' '));
            }), "defense");
            UI.RegisterCommand(new Command<Potion>("Use the {0} potion", CommandContext.NIGHT.Set().And(ac => GameState.Role == Role.POTION_MASTER), ArgumentParsers.ForEnum<Potion>(UI), (cmd, potion) =>
            {
                Parser.SetPotionMasterChoice(potion);
                UI.Views.Game.AppendLine("You have decided to use the {0} potion", potion.ToString().ToLower().Replace('_', ' '));
            }), "potion");
            UI.RegisterCommand(new Command<LocalizationTable>("Make your target see {0}", CommandContext.NIGHT.Set().And(ac => GameState.Role == Role.HYPNOTIST), ArgumentParsers.ForEnum<LocalizationTable>(UI), (cmd, message) =>
            {
                Parser.SetHypnotistChoice(message);
                UI.Views.Game.AppendLine("Your target will see: {0}", Resources.Of(message));
            }), "hm", "hypnotizemessage");
            UI.RegisterCommand(new Command("Reveal yourself as the Mayor", CommandContext.DAY.Set().And(ac => GameState.Role == Role.MAYOR), cmd => Parser.SetDayChoice(GameState.Self.ID)), "reveal");
            UI.RegisterCommand(new Command<Player>("Vote {0} up to the stand", CommandContext.VOTING.Set(), ArgumentParsers.Player(UI), (cmd, target) => Parser.SetVote(target)), "v", "vote");
            UI.RegisterCommand(new Command("Vote guilty", CommandContext.JUDGEMENT.Set(), cmd => Parser.JudgementVoteGuilty()), "g", "guilty");
            UI.RegisterCommand(new Command("Vote innocent", CommandContext.JUDGEMENT.Set(), cmd => Parser.JudgementVoteInnocent()), "i", "innocent");
            UI.RegisterCommand(new Command<Player, string>("Whisper {1} to {0}", CommandContext.DAY.Set(), ArgumentParsers.Player(UI), ArgumentParsers.Text(UI, "Message"), (cmd, target, message) => Parser.SendPrivateMessage(target, message)), "w", "pm", "whisper");
            UI.RegisterCommand(new Command<ExecuteReason>("Set your execute reason to [Reason]", context => CommandExtensions.IsInGame(context) && GameState.Role == Role.JAILOR, ArgumentParsers.ForEnum<ExecuteReason>(UI, "[Reason]", true), (cmd, reason) => Parser.SetJailorDeathNote(reason)), "jn", "jailornote");
            UI.RegisterCommand(new Command<Player, ReportReason, string>("Report {0} for {1}", CommandExtensions.IsInGame, ArgumentParsers.Player(UI), ArgumentParsers.ForEnum<ReportReason>(UI, "[Reason]"), ArgumentParsers.Text("Message"), (cmd, player, reason, message) =>
            {
                Parser.ReportPlayer(player, reason, message);
                UI.Views.Game.AppendLine(("Reported {0} for {1}", YELLOW, null), GameState.ToName(player), reason.ToString().ToLower().Replace('_', ' '));
            }), "report");
            UI.RegisterCommand(new CommandGroup("Use the {0} system command", UI, "Subcommand", "Subcommands")
                .Register(new Command<string>("Send {0} to all users", CommandExtensions.IsAuthenticated, ArgumentParsers.Text(UI, "Message"), (cmd, message) => Parser.SendSystemMessage(SystemCommand.MESSAGE, message)), "message")
                .Register(new Command<string>("Send {0} to all players in your game", CommandExtensions.IsInLobbyOrGame, ArgumentParsers.Text(UI, "Message"), (cmd, message) => Parser.SendSystemMessage(SystemCommand.GAME_MESSAGE, message)), "gamemessage")
                .Register(new Command<string>("Ban {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.BAN, username)), "ban")
                .Register(new Command<Player>("Get {0}'s role and username", CommandExtensions.IsInGame, ArgumentParsers.Player(UI), (cmd, player) => Parser.SendSystemMessage(SystemCommand.IDENTIFY, ((byte)player).ToString())), "identify")
                .Register(new Command("Queue a restart", CommandExtensions.IsAuthenticated, cmd => Parser.SendSystemMessage(SystemCommand.RESTART)), "restart")
                .Register(new Command("Cancel the queued restart", CommandExtensions.IsAuthenticated, cmd => Parser.SendSystemMessage(SystemCommand.CANCEL_RESTART)), "cancelrestart")
                .Register(new Command<string>("Grant {0} 1300 Town Points", CommandExtensions.IsAuthenticated, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.GRANT_POINTS, username)), "grantpoints")
                .Register(new Command<string>("Suspend {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.SUSPEND, username)), "suspend")
                .Register(new Command("Reload the shop data", CommandExtensions.IsAuthenticated, cmd => Parser.SendSystemMessage(SystemCommand.RELOAD_XML, "\x01")), "reloadxml")
                .Register(new Command<string>("Whisper to {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.WHISPER, username)), "whisper")
                .Register(new Command<string>("Unban {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.UNBAN, username)), "unban")
                .Register(new Command<string>("Get account info for {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.ACCOUNT_INFO, username)), "accountinfo")
                .Register(new Command<string, Achievement>("Grant {1} to {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.Username(UI), ArgumentParsers.ForEnum<Achievement>(UI, an: true), (cmd, username, achievement) => Parser.SendSystemMessage(SystemCommand.GRANT_ACHIEVEMENT, username, ((byte)achievement).ToString())), "grantachievement")
                .Register(new Command("Toggle dev mode", CommandExtensions.IsAuthenticated, cmd => Parser.SendSystemMessage(SystemCommand.DEV_LOGIN)), "devlogin")
                .Register(new Command<Promotion>("Request {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.ForEnum<Promotion>(UI), (cmd, promotion) => Parser.SendSystemMessage(SystemCommand.REQUEST_PROMOTION, ((byte)promotion).ToString())), "requestpromotion")
                .Register(new Command("Reset your account", CommandExtensions.IsAuthenticated, cmd => Parser.SendSystemMessage(SystemCommand.RESET_ACCOUNT, "\x01")), "resetaccount")
                .Register(new Command<Character>("Grant yourself {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.ForEnum<Character>(UI), (cmd, character) => Parser.SendSystemMessage(SystemCommand.GRANT_CHARACTER, ((byte)character).ToString())), "grantcharacter")
                .Register(new Command<Background>("Grant yourself {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.ForEnum<Background>(UI), (cmd, background) => Parser.SendSystemMessage(SystemCommand.GRANT_BACKGROUND, ((byte)background).ToString())), "grantbackground")
                .Register(new Command<DeathAnimation>("Grant yourself {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.ForEnum<DeathAnimation>(UI), (cmd, deathAnimation) => Parser.SendSystemMessage(SystemCommand.GRANT_DEATH_ANIMATION, ((byte)deathAnimation).ToString())), "grantdeathanimation")
                .Register(new Command<House>("Grant yourself {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.ForEnum<House>(UI), (cmd, house) => Parser.SendSystemMessage(SystemCommand.GRANT_HOUSE, ((byte)house).ToString())), "granthouse")
                .Register(new Command<LobbyIcon>("Grant yourself {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.ForEnum<LobbyIcon>(UI), (cmd, lobbyIcon) => Parser.SendSystemMessage(SystemCommand.GRANT_LOBBY_ICON, ((byte)lobbyIcon).ToString())), "grantlobbyicon")
                .Register(new Command<Pack>("Grant yourself {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.ForEnum<Pack>(UI), (cmd, pack) => Parser.SendSystemMessage(SystemCommand.GRANT_PACK, ((byte)pack).ToString())), "grantpack")
                .Register(new Command<Pet>("Grant yourself {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.ForEnum<Pet>(UI), (cmd, pet) => Parser.SendSystemMessage(SystemCommand.GRANT_PET, ((byte)pet).ToString())), "grantpet")
                .Register(new Command<Scroll>("Grant yourself {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.ForEnum<Scroll>(UI), (cmd, scroll) => Parser.SendSystemMessage(SystemCommand.GRANT_SCROLL, ((byte)scroll).ToString())), "grantscroll")
                .Register(new Command("Reset your tutorial progress", CommandExtensions.IsAuthenticated, cmd => Parser.SendSystemMessage(SystemCommand.RESET_TUTORIAL_PROGRESS, "\x01")), "resettutorialprogress")
                .Register(new Command("Reload the promotion data", CommandExtensions.IsAuthenticated, cmd => Parser.SendSystemMessage(SystemCommand.RELOAD_PROMOTION_XML, "\x01")), "reloadpromotionxml")
                .Register(new Command<string, Promotion>("Grant {1} to {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.Username(UI), ArgumentParsers.ForEnum<Promotion>(UI), (cmd, username, promotion) => Parser.SendSystemMessage(SystemCommand.GRANT_PROMOTION, username, ((byte)promotion).ToString())), "grantpromotion")
                .Register(new Command<Role>("Set your role to {0}", CommandContext.LOBBY.Set().Or(CommandContext.PICK_NAMES.Set()), ArgumentParsers.ForEnum<Role>(UI, map: Resources.Of), (cmd, role) => Parser.SendSystemMessage(SystemCommand.SET_ROLE, ((byte)role).ToString())), "setrole")
                .Register(new Command<AccountItem>("Grant yourself {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.ForEnum<AccountItem>(UI), (cmd, accountItem) => Parser.SendSystemMessage(SystemCommand.GRANT_ACCOUNT_ITEM, ((byte)accountItem).ToString())), "grantaccountitem")
                .Register(new Command<string>("Force {0} to change username", CommandExtensions.IsAuthenticated, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.FORCE_NAME_CHANGE, username)), "forcenamechange")
                .Register(new Command<string>("Grant {0} 5200 Merit Points", CommandExtensions.IsAuthenticated, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.GRANT_MERIT, username)), "grantmerit")
                .Register(new Command("Enable global double MP", CommandExtensions.IsAuthenticated, cmd => Parser.SendSystemMessage(SystemCommand.SET_FREE_CURRENCY_MULTIPLIER, "2")), "doubleglobalfreecurrencymultiplier")
                .Register(new Command("Disable global double MP", CommandExtensions.IsAuthenticated, cmd => Parser.SendSystemMessage(SystemCommand.SET_FREE_CURRENCY_MULTIPLIER, "1")), "resetglobalfreecurrencymultiplier")
                .Register(new Command<string, string>("Set {0}'s referrer to {1}", CommandExtensions.IsAuthenticated, ArgumentParsers.Username(UI), ArgumentParsers.Username(UI), (cmd, referee, referrer) => Parser.SendSystemMessage(SystemCommand.GRANT_REFER_A_FRIEND, referee, referrer)), "grantreferafriend")
                .Register(new Command("Reload shop, cauldron & Ranked", CommandExtensions.IsAuthenticated, cmd => Parser.SendSystemMessage(SystemCommand.RELOAD_CACHES)), "reloadcaches")
                .Register(new Command("Reset your cauldron cooldown", CommandExtensions.IsAuthenticated, cmd => Parser.SendSystemMessage(SystemCommand.RESET_CAULDRON_COOLDOWN, "\x01")), "resetcauldroncooldown")
                .Register(new Command("Toggle test purchases", CommandExtensions.IsAuthenticated, cmd => Parser.SendSystemMessage(SystemCommand.TOGGLE_TEST_PURCHASES)), "toggletestpurchases")
                .Register(new Command("Toggle free Coven", CommandExtensions.IsAuthenticated, cmd => Parser.SendSystemMessage(SystemCommand.TOGGLE_FREE_COVEN, "\x01")), "togglefreecoven")
                .Register(new Command("Toggle your Coven ownership", CommandExtensions.IsAuthenticated, cmd => Parser.SendSystemMessage(SystemCommand.TOGGLE_ACCOUNT_FEATURE, "2")), "toggleusercoven")
                .Register(new Command("Toggle your Web Premium ownership", CommandExtensions.IsAuthenticated, cmd => Parser.SendSystemMessage(SystemCommand.TOGGLE_ACCOUNT_FEATURE, "4")), "toggleuserwebpremium")
                .Register(new Command<string>("Unlink Steam from {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.UNLINK_STEAM, username)), "unlinksteam")
                .Register(new Command<string>("Unlink Coven from {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.UNLINK_COVEN, username)), "unlinkcoven")
                .Register(new Command<string>("Grant Coven to {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.GRANT_COVEN, username)), "grantcoven")
                .Register(new Command<string>("Grant Web Premium to {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.GRANT_WEB_PREMIUM, username)), "grantwebpremium")
                .Register(new Command<string>("Kick {0}", CommandExtensions.IsAuthenticated, ArgumentParsers.Username(UI), (cmd, username) => Parser.SendSystemMessage(SystemCommand.KICK_USER, username)), "kickuser"), "system");

            RSA rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = MODULUS,
                Exponent = EXPONENT
            });
            byte[] bytes = ToByteArray(authPassword);
            byte[] encpw = rsa.Encrypt(bytes, RSAEncryptionPadding.Pkcs1);
            Array.Clear(bytes, 0, bytes.Length);

            this.socket = socket;
            buffer = new byte[4096];
            Parser = new MessageParser();
            MessageParser = new ServerMessageParser(Parser);
            Parser.MessageWrite += (buffer, index, length) => socket.Send(buffer, index, length, SocketFlags.None);
            Initialize();
            //string authJson = "\u0055{\"username\":\"" + authUsername + "\",\"password\":\"" + Convert.ToBase64String(encpw) + "\",\"type\":" + (byte)AuthenticationPlatform.WEB + ",\"platform\":" + (byte)AuthenticationMode.BMG_FORUMS + ",\"buildId\":" + BUILD_NUMBER + "}\0";
            //socket.Send(Encoding.UTF8.GetBytes(authJson));
            Parser.Authenticate(AuthenticationMode.MOBILE, AuthenticationPlatform.WEB, true, BUILD_NUMBER, authUsername, Convert.ToBase64String(encpw));
            UI.Views.Auth.Status = ("Authenticating...", GREEN, null);
            QueueReceive();
        }

        private void Initialize()
        {
            MessageParser.Authenticated += registered =>
            {
                if (registered)
                {
                    UI.Views.Auth.Status = null;
                    UI.SetMainView(UI.Views.Home);
                    UI.CommandContext = CommandContext.HOME;
                    Parser.RequestCauldronStatus();
                }
                else UI.Views.Auth.Status = ("Authentication failed: registration required", RED);
            };
            MessageParser.CreateLobby += (host, gameMode) => GameState = new GameState(this, gameMode, host);
            MessageParser.SetHost += () => GameState.Host = true;
            MessageParser.UserJoinedGame += (host, display, username, player, lobbyIcon) => GameState.AddPlayer(player, host, display, username, lobbyIcon);
            MessageParser.UserLeftGame += (update, display, player) => GameState.RemovePlayer(player, update, display);
            MessageParser.ChatBoxMessage += (_, player, message) => UI.Views.Game.AppendLine(("{0}: {1}", player <= Player.PLAYER_15 && GameState.Players[(int)player].Dead ? GRAY : WHITE, null), GameState.ToName(player), message.Replace("\n", "").Replace("\r", ""));
            // Add missing cases here
            MessageParser.HostClickedOnAddButton += role => GameState.AddRole(role);
            MessageParser.HostClickedOnRemoveButton += slot => GameState.RemoveRole(slot);
            MessageParser.HostClickedOnStartButton += () =>
            {
                UI.Views.Game.PhaseTimer.Set("Start", 10);
                UI.Views.Game.AppendLine(("The game will start in 10 seconds", GREEN));
            };
            MessageParser.CancelStartCooldown += () =>
            {
                UI.Views.Game.PhaseTimer.Set(0);
                UI.Views.Game.AppendLine(("The start cooldown was cancelled", RED));
            };
            MessageParser.AssignNewHost += player =>
            {
                UI.Views.Game.AppendLine("The host has been repicked");
                GameState.HostID = player;
            };
            MessageParser.VotedToRepickHost += votesNeeded => UI.Views.Game.AppendLine(("{0} votes are needed to repick the host", GREEN, null), votesNeeded);
            MessageParser.NoLongerHost += () => GameState.Host = false;
            MessageParser.DoNotSpam += () => UI.Views.Game.AppendLine(("Please do not spam the chat", GREEN, null));
            MessageParser.HowManyPlayersAndGames += (players, games) => UI.Views.Game.AppendLine(("There are currently {0} players online and {1} games being played", GREEN, null), players, games);
            MessageParser.SystemMessage += message => UI.Views.Game.AppendLine(("(System) {0}", YELLOW, null), message);
            MessageParser.StringTableMessage += tableMessage => UI.Views.Game.AppendLine(Resources.Of(tableMessage));
            MessageParser.FriendList += friends => Friends = friends.Select(fd => new FriendState(this, fd.userID, fd.username, fd.status, fd.ownsCoven)).ToFriendList(UI);
            MessageParser.FriendRequestNotifications += notifications => PendingFriendRequests = notifications.ToDictionary(rd => rd.username, rd => rd.userID);
            // Add missing cases here
            MessageParser.ConfirmFriendRequest += (userID, status, ownsCoven) =>
            {
                string username = PendingFriendRequests.First(fr => fr.Value == userID).Key;
                PendingFriendRequests.Remove(username);
                UI.Views.Notifications.Redraw();
                Friends.Add(new FriendState(this, userID, username, status, ownsCoven));
                UI.Views.Friends.Redraw();
            };
            MessageParser.SuccessfullyRemovedFriend += userID => Friends.RemoveKey<FriendList, uint, FriendState>(userID);
            // Add missing cases here
            MessageParser.FriendUpdate += (userID, status, ownsCoven) =>
            {
                FriendState friend = Friends[userID];
                friend.OnlineStatus = status;
                friend.OwnsCoven = ownsCoven;
            };
            MessageParser.FriendMessage += (userID, sent, message) => UI.Views.Home.AppendLine(sent ? "To {0}: {1}" : "From {0}: {1}", Friends[userID].Username, message);
            MessageParser.UserInformation += (username, townPoints, meritPoints) =>
            {
                TownPoints = townPoints;
                MeritPoints = meritPoints;
                Username = username;
            };
            MessageParser.CreatePartyLobby += brand => Party = new PartyState(this, brand, true);
            MessageParser.PartyInviteFailed += (username, status) => UI.StatusLine = string.Format("Failed to invite {0} to the party: {1}", username, status.ToString().ToDisplayName());
            MessageParser.PartyInviteNotification += (userID, username) =>
            {
                PendingPartyInvitations.Add(username, userID);
                UI.OpenSideView(UI.Views.Notifications);
                UI.AudioAlert();
            };
            MessageParser.AcceptedPartyInvite += result =>
            {
                switch (result)
                {
                    case AcceptInvitationResult.SUCCESS:
                        Party = new PartyState(this, Brand.CLASSIC, false);
                        break;
                    default:
                        UI.StatusLine = string.Format("Failed to accept party invitation: {0}", result.ToString().ToDisplayName());
                        break;
                }
            };
            MessageParser.PendingPartyInviteStatus += (username, status) => Party.GetOrCreateInvitation(username).Status = status;
            MessageParser.SuccessfullyLeftParty += () => Party = null;
            MessageParser.PartyChat += (username, message) => UI.Views.Home.AppendLine("(Party) {0}: {1}", username, message);
            MessageParser.PartyMemberLeft += username => Party.RemoveMember(username);
            MessageParser.SettingsInformation += (filterChat, muteMusic, muteEffects, shareSkin, classicSkinsOnly, displayPets, effectsVolume, musicVolume, language, unknown, tipBehaviour) =>
            {
                _ShareSkin = shareSkin;
                _QueueLanguage = language;
            };
            MessageParser.AddFriend += (username, userID, status, ownsCoven) => Friends.Add(new FriendState(this, userID, username, status, ownsCoven));
            MessageParser.ForcedLogout += () =>
            {
                UI.CommandContext = CommandContext.AUTHENTICATING;
                UI.StatusLine = "Forcefully disconnected";
            };
            MessageParser.ReturnToHomePage += () => GameState = null;
            // Add missing cases here
            MessageParser.PurchasedCharacters += characters => OwnedCharacters = characters.ToList();
            MessageParser.PurchasedHouses += houses => OwnedHouses = houses.ToList();
            // Add missing cases here
            MessageParser.UpdatePaidCurrency += tp => TownPoints = tp;
            MessageParser.PurchasedPacks += packs => OwnedPacks = packs.ToList();
            MessageParser.PurchasedPets += pets => OwnedPets = pets.ToList();
            MessageParser.SetLastBonusWinTime += seconds => UI.Views.Home.FWotDTimer.Set((int)seconds);
            MessageParser.EarnedAchievements52 += achievements => EarnedAchievements = achievements.ToList();
            MessageParser.PurchasedLobbyIcons += lobbyIcons => OwnedLobbyIcons = lobbyIcons.ToList();
            MessageParser.PurchasedDeathAnimations += deathAnimations => OwnedDeathAnimations = deathAnimations.ToList();
            // Add missing cases here
            MessageParser.HostGivenToPlayer += username => Party.Members[username].PermissionLevel = PartyPermissionLevel.Host;
            MessageParser.HostGivenToMe += () => Party.Members[Username].PermissionLevel = PartyPermissionLevel.Host;
            MessageParser.KickedPlayer += username => Party.RemoveMember(username, true);
            MessageParser.KickedMe += () => Party.RemoveMember(Username, true);
            MessageParser.InvitePowersGivenToPlayer += username => Party.Members[username].PermissionLevel = PartyPermissionLevel.CanInvite;
            MessageParser.InvitePowersGivenToMe += () => Party.Members[Username].PermissionLevel = PartyPermissionLevel.CanInvite;
            // Add missing cases here
            MessageParser.UpdateFriendUsername += (username, newUsername) => Friends[username].Username = newUsername;
            // Add missing cases here
            MessageParser.SteamPopup += () => Debug.WriteLine("Received S#68 (SteamPopup)");
            // Add missing cases here
            MessageParser.StartRankedQueue += (requeue, seconds) =>
            {
                if (requeue) UI.StatusLine = "Requeued due to a lack of players";
                UI.Views.Home.QueueTimer.Set((int)seconds);
            };
            MessageParser.LeaveRankedQueue += () =>
            {
                UI.Views.Home.QueueTimer.Set(0);
                UI.StatusLine = "You have left the ranked queue";
            };
            MessageParser.AcceptRankedPopup += () =>
            {
                UI.StatusLine = "Are you ready for the Ranked game?";
                UI.Views.Home.QueueTimer.Set(10);
                UI.AudioAlert();
            };
            // Add missing cases here
            MessageParser.RankedTimeoutDuration += seconds => UI.StatusLine = string.Format("Timed out for {0} seconds", seconds);
            // Add missing cases here
            MessageParser.ModeratorMessage += (modMessage, args) => UI.Views.Game.AppendLine(Resources.Of(modMessage));
            MessageParser.ReferAFriendUpdate += (reward, tp) => TownPoints += tp ?? 0u;
            // Add missing cases here
            MessageParser.UserJoiningLobbyTooQuickly += () => UI.StatusLine = "Wait 15 seconds before rejoining";
            // Add missing cases here
            MessageParser.PickNames += (players, gameMode) =>
            {
                if (GameState == null) GameState = new GameState(this, GameMode.RANKED, false);
                GameState.OnStart(players);
            };
            MessageParser.NamesAndPositionsOfUsers += (player, name) => GameState.Players[(int)player].Name = name;
            MessageParser.RoleAndPosition += (role, player, target) =>
            {
                GameState.Role = role;
                GameState.Self = GameState.Players[(int)player];
                target.MatchSome(id => GameState.Target = id);
            };
            MessageParser.StartNight += () => GameState.Night++;
            MessageParser.StartDay += () => GameState.Day++;
            MessageParser.WhoDiedAndHow += (player, role, display, deathCauses) =>
            {
                PlayerState ps = GameState.Players[(int)player];
                ps.Role = role;
                ps.Dead = true;
                UI.Views.Game.AppendLine(("{0} died", RED), GameState.ToName(player));
                GameState.Graveyard.Add(ps);
                UI.Views.Graveyard.Redraw();
                DeathCause[] causes = deathCauses.ToArray();
                if (causes.Length > 0) UI.Views.Game.AppendLine("Death causes: {0}", string.Join(", ", causes.Select(dc => dc.ToString().ToDisplayName())));
                UI.Views.Game.AppendLine("Their role was {0}", role.ToString().ToDisplayName());
            };
            // Add missing cases here
            MessageParser.StartDiscussion += () =>
            {
                UI.Views.Game.AppendLine("Discussion may now begin");
                UI.Views.Game.PhaseTimer.Set("Discussion", Resources.GetMetadata(GameState.GameMode).RapidMode ? 15 : 45);
            };
            MessageParser.StartVoting += _ =>
            {
                UI.CommandContext = CommandContext.VOTING;
                UI.Views.Game.AppendLine(("{0} votes are needed to lynch someone", GREEN, null), (GameState.Players.Where(ps => !ps.Dead && !ps.Left).Count() + 2) / 2);
                UI.Views.Game.PhaseTimer.Set("Voting", 30);
            };
            MessageParser.StartDefenseTransition += player =>
            {
                UI.CommandContext = CommandContext.DISCUSSION;
                UI.Views.Game.AppendLine(("{0} has been voted up to the stand", GREEN, null), GameState.ToName(player));
            };
            MessageParser.StartJudgement += () =>
            {
                UI.CommandContext = CommandContext.JUDGEMENT;
                UI.Views.Game.AppendLine(("You may now vote guilty or innocent", GREEN, null));
                UI.Views.Game.PhaseTimer.Set("Judgement", 20);
            };
            MessageParser.TrialFoundGuilty += (guiltyVotes, innocentVotes) =>
            {
                UI.CommandContext = CommandContext.DISCUSSION;
                UI.Views.Game.AppendLine(("Judgement results: {0} guilty - {1} innocent", GREEN, null), guiltyVotes, innocentVotes);
                UI.Views.Game.PhaseTimer.Set("Last Words", 5);
            };
            MessageParser.TrialFoundNotGuilty += (guiltyVotes, innocentVotes) =>
            {
                UI.CommandContext = CommandContext.DISCUSSION;
                UI.Views.Game.AppendLine(("Judgement results: {0} guilty - {1} innocent", GREEN, null), guiltyVotes, innocentVotes);
            };
            MessageParser.LookoutNightAbilityMessage += player => UI.Views.Game.AppendLine(("{0} visited your target", GREEN, null), GameState.ToName(player));
            MessageParser.UserVoted += (player, voted, votes) => UI.Views.Game.AppendLine(("{0} has placed {2} votes against {1}", GREEN, null), GameState.ToName(player), GameState.ToName(voted), votes);
            MessageParser.UserCanceledVote += (player, voted, votes) => UI.Views.Game.AppendLine(("{0} has cancelled their {2} votes against {1}", GREEN, null), GameState.ToName(player), GameState.ToName(voted), votes);
            MessageParser.UserChangedVote += (player, voted, _, votes) => UI.Views.Game.AppendLine(("{0} has changed their {2} votes to be against {1}", GREEN, null), GameState.ToName(player), GameState.ToName(voted), votes);
            MessageParser.UserDied += _ => UI.Views.Game.AppendLine(("You have died", RED));
            MessageParser.Resurrection += (player, role) => UI.Views.Game.AppendLine(("{0} ({1}) has been resurrected", GREEN, null), GameState.ToName(player), role.ToString().ToDisplayName());
            MessageParser.TellRoleList += roles => GameState.Roles = roles.ToArray();
            MessageParser.UserChosenName += (tableMessage, player, name) =>
            {
                GameState.Players[(int)player].Name = name;
                UI.Views.Game.AppendLine(GameState.ToName(player) + " " + Resources.Of(tableMessage));
            };
            MessageParser.OtherMafia += mafia =>
            {
                GameState.Team.Clear();
                foreach ((Player player, Role role) in mafia)
                {
                    PlayerState ps = GameState.Players[(int)player];
                    ps.Role = role;
                    GameState.Team.Add(ps);
                }
                UI.OpenSideView(UI.Views.Team);
            };
            MessageParser.TellTownAmnesiacChangedRole += role => UI.Views.Game.AppendLine(("An Amnesiac has remembered {0}", GREEN, null), role.ToString().ToDisplayName());
            MessageParser.AmnesiacChangedRole += (role, target) =>
            {
                UI.Views.Game.AppendLine(("You have attempted to remember who you were!", RED));
                GameState.Role = role;
                target.MatchSome(id => GameState.Target = id);
            };
            MessageParser.BroughtBackToLife += () => UI.Views.Game.AppendLine(("You have been resurrected by a Retributionist!", GREEN, null));
            MessageParser.StartFirstDay += () => GameState.Day = 1;
            MessageParser.BeingJailed += () =>
            {
                GameState.NightState |= NightState.JAILED;
                UI.Views.Game.AppendLine(("You were hauled off to jail", GRAY));
            };
            MessageParser.JailedTarget += (player, canExecute, executedTown) =>
            {
                UI.Views.Game.AppendLine("You have dragged {0} off to jail", GameState.ToName(player));
                UI.Views.Game.AppendLine(canExecute ? "You may execute tonight" : executedTown ? "You cannot execute any more as you have executed a Town member" : "You cannot execute tonight");
            };
            MessageParser.UserJudgementVoted += player => UI.Views.Game.AppendLine(("{0} has voted", GREEN, null), GameState.ToName(player));
            MessageParser.UserChangedJudgementVote += player => UI.Views.Game.AppendLine(("{0} has changed their voted", GREEN, null), GameState.ToName(player));
            MessageParser.UserCanceledJudgementVote += player => UI.Views.Game.AppendLine(("{0} has cancelled their vote", GREEN, null), GameState.ToName(player));
            MessageParser.TellJudgementVotes += (player, vote) => UI.Views.Game.AppendLine(("{0} voted {1}", GREEN, null), GameState.ToName(player), vote.ToString().ToLower().Replace('_', ' '));
            MessageParser.ExecutionerCompletedGoal += () => UI.Views.Game.AppendLine(("You have successfully gotten your target lynched", GREEN));
            MessageParser.JesterCompletedGoal += () => UI.Views.Game.AppendLine(("You have successfully gotten yourself lynched", GREEN));
            MessageParser.MayorRevealed += player => UI.Views.Game.AppendLine(("{0} has revealed themselves as the mayor", MAGENTA, null), GameState.ToName(player));
            // Add missing cases here
            MessageParser.DisguiserStoleYourIdentity += player => UI.Views.Game.AppendLine(("A disguiser stole your identity: you are now {0}", RED), GameState.ToName(player));
            MessageParser.DisguiserChangedIdentity += player => UI.Views.Game.AppendLine(("You have successfully disguised yourself as {0}", RED), GameState.ToName(player));
            MessageParser.DisguiserChangedUpdateMafia += (player, disguiser) => UI.Views.Game.AppendLine(("{1} has successfully disguised themselves as {0}", RED), GameState.ToName(player), GameState.ToName(disguiser));
            MessageParser.MediumIsTalkingToUs += () => UI.Views.Game.AppendLine(("A medium is talking to you", GREEN, null));
            MessageParser.MediumCommunicating += () => UI.Views.Game.AppendLine(("You have opened a communication with the living", GREEN, null));
            MessageParser.TellLastWill += (Player player, string lastWill) => lastWill.SomeNotNull().Match(lw => GameState.Players[(int)player].LastWill = lw, () => UI.Views.Game.AppendLine("We could not find a last will"));
            MessageParser.HowManyAbilitiesLeft += abilitiesLeft => UI.Views.Game.AppendLine("Abilities left: {0}", abilitiesLeft);
            MessageParser.MafiaTargeting += (player, role, target, _1, _2, _3) => UI.Views.Game.AppendLine(target == Player.JAILOR ? "{0} ({1}) has unset their target" : "{0} ({1}) has set their target to {2}", GameState.ToName(player), role.ToString().ToDisplayName(), GameState.ToName(target));
            MessageParser.TellJanitorTargetsRole += role => UI.Views.Game.AppendLine(("You secretly know that your target's role was {0}", GREEN, null), role.ToString().ToDisplayName());
            MessageParser.TellJanitorTargetsWill += (player, lastWill) => GameState.Players[(int)player].LastWill = lastWill;
            MessageParser.SomeoneHasWon += (faction, winners) =>
            {
                GameState.WinningFaction = faction;
                GameState.Winners = winners.ToArray();
            };
            MessageParser.MafiosoPromotedToGodfather += () =>
            {
                UI.Views.Game.AppendLine("You have been promoted to Godfather");
                GameState.Role = Role.GODFATHER;
            };
            MessageParser.MafiosoPromotedToGodfatherUpdateMafia += player =>
            {
                UI.Views.Game.AppendLine("{0} has been promoted to Godfather", GameState.ToName(player));
                GameState.Players[(int)player].Role = Role.GODFATHER;
            };
            MessageParser.MafiaPromotedToMafioso += () =>
            {
                UI.Views.Game.AppendLine("You have been promoted to Mafioso");
                GameState.Role = Role.MAFIOSO;
            };
            MessageParser.TellMafiaAboutMafiosoPromotion += player =>
            {
                UI.Views.Game.AppendLine("{0} has been promoted to Mafioso", GameState.ToName(player));
                GameState.Players[(int)player].Role = Role.MAFIOSO;
            };
            MessageParser.ExecutionerConvertedToJester += () =>
            {
                UI.Views.Game.AppendLine(("Your target has died", RED));
                GameState.Role = Role.JESTER;
            };
            MessageParser.AmnesiacBecameMafiaOrWitch += (player, role) =>
            {
                UI.Views.Game.AppendLine(("{0} has remembered {1}", GREEN, null), GameState.ToName(player), role.ToString().ToDisplayName());
                GameState.Players[(int)player].Role = role;
            };
            MessageParser.UserDisconnected += player => UI.Views.Game.AppendLine(("{0} has left the game", GREEN, null), GameState.ToName(player));
            MessageParser.MafiaWasJailed += player => UI.Views.Game.AppendLine(("{0} has been dragged off to jail", RED), GameState.ToName(GameState.Players[(int)player]));
            MessageParser.InvalidNameMessage += status => UI.StatusLine = string.Format("Invalid name: {0}", status);
            MessageParser.StartNightTransition += () => UI.CommandContext = CommandContext.DISCUSSION;
            MessageParser.StartDayTransition += deaths => deaths.ForEach(player => GameState.Players[(int)player].Dead = true);
            // Add missing cases here
            MessageParser.DeathNote += (player, _, deathNote) => GameState.Players[(int)player].DeathNote = deathNote.Replace("\n", "");
            // Add missing cases here
            MessageParser.ResurrectionSetAlive += player => GameState.Players[(int)player].Dead = false;
            MessageParser.StartDefense += () =>
            {
                UI.Views.Game.PhaseTimer.Set("Defense", 20);
                UI.Views.Game.AppendLine(("What is your defense?", GREEN, null));
            };
            MessageParser.UserLeftDuringSelection += player =>
            {
                UI.Views.Game.AppendLine(("{0} left during selection", GREEN, null), GameState.ToName(player));
                GameState.Players[(int)player].Left = true;
            };
            MessageParser.VigilanteKilledTown += () => UI.Views.Game.AppendLine(("You put your gun away out of fear of shooting another town member", GREEN, null));
            MessageParser.NotifyUsersOfPrivateMessage += (sender, receiver) => UI.Views.Game.AppendLine(FormattedString.From((GameState.ToName(sender), WHITE, null), (" is whispering to ", BLUE, null), (GameState.ToName(receiver), WHITE, null)));
            MessageParser.PrivateMessage += (type, player, message, receiver) =>
            {
                switch (type)
                {
                    case PrivateMessageType.TO:
                        UI.Views.Game.AppendLine(("To {0}: {1}", MAGENTA, null), (FormattedString)(GameState.ToName(player), WHITE, null), message);
                        break;
                    case PrivateMessageType.FROM:
                        UI.Views.Game.AppendLine(("From {0}: {1}", MAGENTA, null), (FormattedString)(GameState.ToName(player), WHITE, null), message);
                        break;
                    case PrivateMessageType.FROM_TO:
                        UI.Views.Game.AppendLine(("From {0} to {1}: {2}", MAGENTA, null), (FormattedString)(GameState.ToName(player), WHITE, null), GameState.ToName(receiver.Value), message);
                        break;
                }
            };
            MessageParser.EarnedAchievements161 += achievements => achievements.ForEach(id =>
            {
                EarnedAchievements.Add(id);
                UI.Views.Game.AppendLine(("You have earned the achievement {0}", GREEN), id.ToString().ToDisplayName());
            });
            MessageParser.AuthenticationFailed += (result, timeout) => UI.Views.Auth.Status = (string.Format(timeout != null ? "Authentication failed: {0} for {1} seconds" : "Authentication failed: {0}", result.ToString().ToDisplayName(), timeout), RED);
            MessageParser.SpyNightAbilityMessage += (isCoven, player) => UI.Views.Game.AppendLine(("A member of the {0} visited {1}", GREEN, null), isCoven ? "Coven" : "Mafia", GameState.ToName(player));
            MessageParser.OneDayBeforeStalemate += () => UI.Views.Game.AppendLine(("If noone dies by tomorrow, the game will end in a draw", GREEN, null));
            // Add missing cases here
            MessageParser.FullMoonNight += () => UI.Views.Game.AppendLine(Resources.Of(LocalizationTable.FULL_MOON));
            MessageParser.Identify += message => UI.Views.Game.AppendLine((message, YELLOW, null));
            MessageParser.EndGameInfo += (timeout, gameMode, winner, won, eloChange, mpGain, players) =>
            {
                UI.CommandContext = CommandContext.POST_GAME;
                UI.Views.Game.AppendLine(("Entered the post-game lobby", GREEN, null));
                // TODO: Display the end game info
            };
            // Add missing cases here
            MessageParser.VampirePromotion += () =>
            {
                UI.Views.Game.AppendLine(("You have been bitten by a Vampire", RED));
                GameState.Role = Role.VAMPIRE;
            };
            MessageParser.OtherVampires += vampires =>
            {
                GameState.Team.Clear();
                foreach ((Player teammate, bool youngest) in vampires)
                {
                    PlayerState ps = GameState.Players[(int)teammate];
                    ps.Youngest = youngest;
                    GameState.Team.Add(ps);
                }
                UI.OpenSideView(UI.Views.Team);
            };
            MessageParser.AddVampire += (player, youngest) =>
            {
                UI.Views.Game.AppendLine(youngest ? "{0} is now the youngest vampire" : "{0} is now a vampire", GameState.ToName(player));
                if (youngest) GameState.Players[(int)player].Youngest = true;
            };
            MessageParser.CanVampiresConvert += canConvert => UI.Views.Game.AppendLine(canConvert ? "You may bite someone tonight" : "You cannot bite anyone tonight");
            MessageParser.VampireDied += (player, newYoungest) =>
            {
                UI.Views.Game.AppendLine(("{0}, a fellow Vampire, died", RED), GameState.ToName(player));
                newYoungest.MatchSome(id => GameState.Players[(int)id].Youngest = true);
            };
            MessageParser.VampireHunterPromoted += () =>
            {
                UI.Views.Game.AppendLine(("All vampires have died, so you have become a vigilante with 1 bullet", GREEN));
                GameState.Role = Role.VIGILANTE;
            };
            MessageParser.VampireVisitedMessage += player => UI.Views.Game.AppendLine(("Vampires visited {0}", GREEN, null), GameState.ToName(player));
            // Add missing cases here
            MessageParser.TransporterNotification += (player1, player2) => UI.Views.Game.AppendLine(("A transporter transported {0} and {1}", GREEN, null), GameState.ToName(player1), GameState.ToName(player2));
            // Add missing cases here
            MessageParser.UpdateFreeCurrency += mp => MeritPoints = mp;
            // Add missing cases here
            MessageParser.TrackerNightAbility += player => UI.Views.Game.AppendLine(("{0} was visited by your target", GREEN, null), GameState.ToName(player));
            MessageParser.AmbusherNightAbility += player => UI.Views.Game.AppendLine(("{0} was seen preparing an ambush while visiting your target", RED), GameState.ToName(player));
            MessageParser.GuardianAngelProtection += player => UI.Views.Game.AppendLine(("{0} was protected by a Guardian Angel", GREEN, null), GameState.ToName(player));
            MessageParser.PirateDuel += () =>
            {
                GameState.NightState |= NightState.DUEL_DEFENDING;
                UI.Views.Game.AppendLine(("You have been challenged to a duel by the Pirate", RED));
            };
            MessageParser.DuelTarget += player =>
            {
                GameState.NightState |= NightState.DUEL_ATTACKING;
                UI.Views.Game.AppendLine(("You have challenged {0} to a duel", GREEN, null), GameState.ToName(player));
            };
            // Add missing cases here
            MessageParser.HasNecronomicon += nightsUntil => UI.Views.Game.AppendLine((nightsUntil != null ? "{0} nights until the Coven possess the Necronomicon" : "The Coven now possess the Necronomicon", GREEN, null), nightsUntil);
            MessageParser.OtherWitches += coven =>
            {
                GameState.Team.Clear();
                foreach ((Player player, Role role) in coven)
                {
                    PlayerState ps = GameState.Players[(int)player];
                    ps.Role = role;
                    GameState.Team.Add(ps);
                }
                UI.OpenSideView(UI.Views.Team);
            };
            MessageParser.PsychicNightAbility += (player1, player2, player3) => UI.Views.Game.AppendLine((player3 != null ? "A vision revealed that {0}, {1}, or {2} is evil" : "A vision revealed that {0}, or {1} is good", GREEN, null), GameState.ToName(player1), GameState.ToName(player2), GameState.ToName(player3 ?? default));
            MessageParser.TrapperNightAbility += role => UI.Views.Game.AppendLine(("{0} has triggered your trap", RED), role.ToString().ToDisplayName());
            MessageParser.TrapperTrapStatus += status => UI.Views.Game.AppendLine(("Trap status: {0}", GREEN, null), status.ToString().ToDisplayName());
            MessageParser.PestilenceConversion += () => GameState.Role = Role.PESTILENCE;
            MessageParser.JuggernautKillCount += kills => UI.Views.Game.AppendLine(("You have obtained {0} kills", GREEN, null), kills);
            MessageParser.CovenGotNecronomicon += (player, newPlayer) => UI.Views.Game.AppendLine(("{0} now has the Necronomicon", GREEN, null), GameState.ToName(newPlayer ?? player));
            MessageParser.GuardianAngelPromoted += () =>
            {
                UI.Views.Game.AppendLine(("You failed to protect your target", RED));
                GameState.Role = Role.SURVIVOR;
            };
            MessageParser.VIPTarget += player => UI.Views.Game.AppendLine(("{0} is the VIP", GREEN, null), GameState.ToName(player));
            MessageParser.PirateDuelOutcome += (attack, defense) => UI.Views.Game.AppendLine(("Your {1} against the Pirate's {0}: you have {2} the duel", RED), attack, defense, (byte)attack == ((byte)defense + 1) % 3 ? "lost" : "won");
            MessageParser.HostSetPartyConfigResult += (brand, mode, result) =>
            {
                switch (result)
                {
                    case SetConfigResult.SUCCESS:
                        Party.Brand = brand;
                        Party.SelectedMode = mode;
                        break;
                    default:
                        UI.StatusLine = string.Format("Failed to set party config: {0}", result.ToString().ToDisplayName());
                        break;
                }
            };
            MessageParser.ActiveGameModes += gameModes =>
            {
                ActiveGameModes.Clear();
                foreach (GameMode mode in gameModes) ActiveGameModes.Add(mode);
                UI.OpenSideView(UI.Views.GameModes);
            };
            MessageParser.AccountFlags += flags =>
            {
                PermissionLevel = flags.HasFlag(AccountFlags.OWNS_COVEN) ? (byte)1 : (byte)0;
                UI.Views.GameModes.Redraw();
            };
            MessageParser.ZombieRotted += player => UI.Views.Game.AppendLine(("You may no longer use {0}'s night ability", GREEN, null), player);
            MessageParser.LoverTarget += player => UI.Views.Game.AppendLine(("{0} is your lover", GREEN, null), player);
            MessageParser.PlagueSpread += player => UI.Views.Game.AppendLine(("{0} has been infected with the plague", GREEN, null), player);
            MessageParser.RivalTarget += player => UI.Views.Game.AppendLine(("{0} is your rival", GREEN, null), player);
            // Add missing cases here
            MessageParser.JailorDeathNote += (player, _, reason) => GameState.Players[(int)player].DeathNote = reason.ToString().ToDisplayName();
            MessageParser.Disconnected += reason => UI.Views.Auth.Status = FormattedString.Format(("Disconnected: {0}", RED), reason.ToString().ToDisplayName());
            MessageParser.SpyNightInfo += tableMessages => tableMessages.ForEach(tableMessage => UI.Views.Game.AppendLine(Resources.OfSpyResult(tableMessage)));
            // Add missing cases here
        }

        private void QueueReceive() => socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, Receive, null);

        private void Receive(IAsyncResult result)
        {
            try
            {
                int read = socket.EndReceive(result);
                if (read == 0)
                {
                    Debug.WriteLine("Connection closed");
                    return;
                }
                Parser.Parse(buffer, 0, read);
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
            catch (ObjectDisposedException)
            {
                Debug.WriteLine("Socket disposed");
                UI.CommandContext = CommandContext.AUTHENTICATING;
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
        public static void SafeReplace<T>(this IList<T> list, int index, T value)
        {
            while (list.Count < index) list.Add(default);
            if (list.Count == index) list.Add(value);
            else list[index] = value;
        }

        public static V SafeIndex<K, V>(this IDictionary<K, V> dict, K key, Func<V> ifAbsent)
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

        public static bool IsCoven(this Role role)
        {
            switch (role)
            {
                default:
                    return false;
                case Role.COVEN_LEADER:
                case Role.POTION_MASTER:
                case Role.HEX_MASTER:
                case Role.NECROMANCER:
                case Role.POISONER:
                case Role.MEDUSA:
                case Role.COVEN_RANDOM_COVEN:
                    return true;
            }
        }

        public static bool HasDeathNote(this Role role)
        {
            switch (role)
            {
                default:
                    return false;
                case Role.GODFATHER:
                case Role.MAFIOSO:
                case Role.ARSONIST:
                case Role.SERIAL_KILLER:
                case Role.WEREWOLF:
                case Role.COVEN_LEADER:
                case Role.POTION_MASTER:
                case Role.HEX_MASTER:
                case Role.NECROMANCER:
                case Role.POISONER:
                case Role.MEDUSA:
                case Role.JUGGERNAUT:
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

        public static Func<T, bool> Or<T>(this Func<T, bool> a, Func<T, bool> b) => input => a(input) || b(input);
        public static Func<T, bool> And<T>(this Func<T, bool> a, Func<T, bool> b) => input => a(input) && b(input);

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

        public static string Limit(this string value, int length) => value.Length > length ? value.Substring(0, length) : value;

        public static string PadRightHard(this string value, int length) => value.Length > length ? value.Substring(0, length) : value.PadRight(length);

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

        public static IEnumerable<T> Prepend<T>(this IEnumerable<T> enumerable, params T[] value)
        {
            foreach (T t in value) yield return t;
            foreach (T t in enumerable) yield return t;
        }

        public static IEnumerable<T> Append<T>(this IEnumerable<T> enumerable, T value)
        {
            foreach (T t in enumerable) yield return t;
            yield return value;
        }

        public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            foreach (T t in enumerable) action(t);
        }

        public static FriendList ToFriendList(this IEnumerable<FriendState> friends, ITextUI ui)
        {
            FriendList friendList = new FriendList(ui);
            foreach (FriendState friend in friends) friendList.Add(friend);
            return friendList;
        }

        public static bool RemoveKey<T, K, V>(this T src, K key) where T : IReadOnlyDictionary<K, V>, ICollection<V> => src.TryGetValue(key, out V value) && src.Remove(value);
        public static V GetOrCreate<K, V>(this IDictionary<K, V> dict, K key, Func<V> gen)
        {
            if (!dict.TryGetValue(key, out V value)) dict.Add(key, value = gen());
            return value;
        }
        public static PartyInvitation GetOrCreateInvitation(this PartyState party, string username)
        {
            if (!party.Invitations.TryGetValue(username, out PartyInvitation invitation)) invitation = party.AddInvitation(username);
            return invitation;
        }
    }
}
