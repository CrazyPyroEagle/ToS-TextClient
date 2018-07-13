using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ToSParser;

namespace ToSTextClient
{
    class GameState
    {
        public ITextUI UI => game.UI;
        public GameModeID GameMode { get; protected set; }
        public bool Started { get; protected set; }
        public RoleID Role
        {
            get => _Role;
            set { game.UI.CommandContext &= ~CommandContext.PICK_NAMES; game.UI.GameView.AppendLine("Your role is {0}", (_Role = value).ToString().ToDisplayName()); }
        }
        public PlayerID Self { get; set; }
        public PlayerID Target { get => _Target; set => game.UI.GameView.AppendLine("Your target is {0}", ToName(_Target = value)); }
        public PlayerState[] Players { get; protected set; } = new PlayerState[15];
        public RoleID[] Roles { get; protected set; } = new RoleID[0];
        public List<PlayerState> Team { get; set; } = new List<PlayerState>();
        public List<PlayerState> Graveyard { get; set; } = new List<PlayerState>();
        public int Day
        {
            get => _Day;
            set
            {
                game.UI.CommandContext = (game.UI.CommandContext & ~CommandContext.NIGHT) | CommandContext.DAY;
                game.UI.GameView.AppendLine(("Day {0}", ConsoleColor.Black, ConsoleColor.White), _Day = value);
            }
        }
        public int Night
        {
            get => _Night;
            set
            {
                game.UI.CommandContext = (game.UI.CommandContext & ~CommandContext.DAY) | CommandContext.NIGHT;
                game.UI.GameView.AppendLine(("Night {0}", ConsoleColor.Black, ConsoleColor.White), _Night = value);
            }
        }
        public int AbilitiesLeft { get; set; }
        public bool Host
        {
            get => _Host;
            set { if (_Host != value) { game.UI.SetCommandContext(CommandContext.HOST, _Host = value); game.UI.GameView.AppendLine(((_Host = value) ? "You are now host" : "You are no longer host", ConsoleColor.Green, ConsoleColor.Black)); } }
        }
        public PlayerID? HostID
        {
            get => _HostID;
            set { _HostID = value; game.UI.RedrawView(game.UI.PlayerListView); }
        }
        public string LastWill
        {
            get => _LastWill;
            set { game.Parser.SaveLastWill(_LastWill = value); }
        }
        public string DeathNote
        {
            get => _DeathNote;
            set { game.Parser.SaveDeathNote(_DeathNote = value); }
        }
        public string ForgedWill
        {
            get => _ForgedWill;
            set { game.Parser.SaveForgedWill(_ForgedWill = value); }
        }
        public int Timer
        {
            get => _Timer;
            set { if ((_Timer = value) >= 0) game.UI.RedrawTimer(); }
        }
        public string TimerText
        {
            get => _TimerText;
            set { _TimerText = value; Task.Run(UpdateTimer); game.UI.RedrawTimer(); }
        }

        protected TextClient game;
        protected RoleID _Role;
        protected PlayerID _Target;
        protected int _Day;
        protected int _Night;
        protected bool _Host;
        protected PlayerID? _HostID;
        protected string _LastWill = "";
        protected string _DeathNote = "";
        protected string _ForgedWill = "";
        protected int _Timer;
        protected string _TimerText;
        protected volatile int timerIndex;

        public GameState(TextClient game, GameModeID gameMode)
        {
            this.game = game;
            GameMode = gameMode;
            PopulatePlayers();
        }

        public void OnStart(int playerCount)
        {
            Started = true;
            Players = new PlayerState[playerCount];
            Roles = new RoleID[playerCount];
            PopulatePlayers();
            HostID = null;
            game.UI.CommandContext = CommandContext.GAME | CommandContext.PICK_NAMES;
            game.UI.RedrawSideViews();
            game.UI.GameView.AppendLine(("Please choose a name (or wait to get a random name)", ConsoleColor.Green, ConsoleColor.Black));
        }

        public void AddPlayer(PlayerID player, bool host, bool display, string username, LobbyIconID lobbyIcon)
        {
            if (display) game.UI.GameView.AppendLine(("{0} has joined the game", ConsoleColor.Green, ConsoleColor.Black), username);
            if (host) HostID = player;
            PlayerState playerState = Players[(int)player];
            playerState.Name = username;
            playerState.SelectedLobbyIcon = lobbyIcon;
        }

        public void RemovePlayer(PlayerID player, bool update, bool display)
        {
            if (display) game.UI.GameView.AppendLine(("{0} has left the game", ConsoleColor.Green, ConsoleColor.Black), ToName(player));
            if (update)
            {
                for (int index = (int)player + 1; index < Players.Length; index++)
                {
                    Players[index].Self--;
                    Players[index - 1] = Players[index];
                }
                Players[14] = new PlayerState(this, PlayerID.PLAYER_15);
                game.UI.RedrawView(game.UI.PlayerListView);
            }
            else Players[(int)player].Left = true;
        }

        public void AddRole(RoleID role)
        {
            RoleID[] newRoles = new RoleID[Roles.Length + 1];
            Roles.CopyTo(newRoles, 0);
            newRoles[Roles.Length] = role;
            Roles = newRoles;
            game.UI.RedrawView(game.UI.RoleListView);
        }

        public void RemoveRole(byte index)
        {
            RoleID[] newRoles = new RoleID[Roles.Length - 1];
            for (int cpi = 0; cpi < Roles.Length; cpi++)
            {
                if (cpi == index) continue;
                newRoles[cpi < index ? cpi : cpi - 1] = Roles[cpi];
            }
            Roles = newRoles;
            game.UI.RedrawView(game.UI.RoleListView);
        }

        public string ToName(PlayerID playerID, bool inList = false)
        {
            int rawID = (int)playerID;
            if (rawID < Players.Length) return string.Format(playerID == HostID ? inList ? "{0,2} {1} (Host)" : "(Host) {1}" : inList ? Players[rawID]?.Left ?? false ? "{0,2} [{1}]" : "{0,2} {1}" : "({0}) {1}", rawID + 1, Players[rawID]?.Name ?? string.Format("#{0}", rawID + 1));
            switch (playerID)
            {
                case PlayerID.JAILOR:
                    return "Jailor";
                case PlayerID.MEDIUM:
                    return "Medium";
                case PlayerID.MAFIA:
                    return "Mafia";
                case PlayerID.VAMPIRE:
                    return "Vampire";
            }
            return string.Format("#{0}", rawID + 1);
        }

        public string ToName(PlayerState playerState, bool inList = false)
        {
            if (playerState.Role == null) return ToName(playerState.Self, inList);
            return string.Format("{0} ({1})", ToName(playerState.Self, inList), playerState.Role?.ToString()?.ToDisplayName());
        }

        public bool TryParsePlayer(string[] args, ref int index, out PlayerID player, bool allowNone = true)
        {
            if (byte.TryParse(args[index], out byte rawID))
            {
                index++;
                player = (PlayerID)(rawID - 1);
                return true;
            }
            for (int length = args.Length - index; length > 0; length++)
            {
                string value = string.Join(" ", args, index, length).ToLower();
                if (allowNone && value == "none")
                {
                    index += length;
                    player = PlayerID.JAILOR;
                    return true;
                }
                foreach (PlayerState ps in Players.Where(ps => ps.Name.ToLower() == value))
                {
                    index += length;
                    player = ps.Self;
                    return true;
                }
            }
            player = PlayerID.JAILOR;
            return false;
        }

        public bool TryParsePlayer(string value, out PlayerID player)
        {
            if (byte.TryParse(value, out byte rawID))
            {
                player = (PlayerID)(rawID - 1);
                return true;
            }
            if (value == "none")
            {
                player = PlayerID.JAILOR;
                return true;
            }
            foreach (PlayerState ps in Players.Where(ps => ps.Name.ToLower() == value))
            {
                player = ps.Self;
                return true;
            }
            player = PlayerID.JAILOR;
            return false;
        }

        private void PopulatePlayers()
        {
            for (int index = 0; index < Players.Length; index++) Players[index] = new PlayerState(this, (PlayerID)index);
        }

        private async Task UpdateTimer()
        {
            for (int thisInc = ++timerIndex; timerIndex == thisInc && Timer > 0; Timer--) await Task.Delay(1000);
        }
    }

    class PlayerState
    {
        public PlayerID Self { get; set; }
        public string Name
        {
            get { return _Name; }
            set { _Name = value; game.UI.RedrawView(game.UI.PlayerListView); }
        }
        public RoleID? Role
        {
            get { return _Role; }
            set { _Role = value; game.UI.RedrawView(game.UI.TeamView); }
        }
        public CharacterID SelectedCharacter { get; set; }
        public HouseID SelectedHouse { get; set; }
        public PetID SelectedPet { get; set; }
        public LobbyIconID SelectedLobbyIcon { get; set; }
        public DeathAnimationID SelectedDeathAnimation { get; set; }
        public bool Dead
        {
            get { return _Dead; }
            set { if (_Dead = value) game.UI.GameView.AppendLine(("{0} died", ConsoleColor.DarkRed), game.ToName(Self)); game.Graveyard.Add(this); game.UI.RedrawView(game.UI.GraveyardView, game.UI.TeamView); }
        }
        public bool Left
        {
            get { return _Left; }
            set { _Left = value; game.UI.RedrawView(game.UI.PlayerListView, game.UI.TeamView, game.UI.GraveyardView); }
        }
        public string LastWill
        {
            get { return _LastWill; }
            set
            {
                game.UI.LastWillView.Title = string.Format(" # (LW) {0}", game.ToName(Self));
                game.UI.LastWillView.Value = _LastWill = value;
                game.UI.OpenSideView(game.UI.LastWillView);
            }
        }
        public string DeathNote
        {
            get { return _DeathNote; }
            set
            {
                game.UI.LastWillView.Title = string.Format(" # (DN) {0}", game.ToName(Self));
                game.UI.LastWillView.Value = _DeathNote = value;
                game.UI.OpenSideView(game.UI.LastWillView);
            }
        }

        private GameState game;
        private string _Name;
        private RoleID? _Role;
        private bool _Dead;
        private bool _Left;
        private string _LastWill;
        private string _DeathNote;

        public PlayerState(GameState parent, PlayerID self)
        {
            game = parent;
            Self = self;
        }
    }
}
