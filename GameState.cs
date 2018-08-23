﻿using System;
using System.Collections.Generic;
using System.Linq;
using ToSParser;

namespace ToSTextClient
{
    class GameState
    {
        public TextClient Game { get; protected set; }
        public GameMode GameMode { get; protected set; }
        public bool Started { get; protected set; }
        public Role Role
        {
            get => _Role;
            set { Game.UI.CommandContext = CommandContext.ROLE_SELECTION; Game.UI.GameView.AppendLine("Your role is {0}", Game.Resources.Of(_Role = value)); }
        }
        public PlayerState Self { get => _Self; set { _Self = value; Game.UI.RedrawView(Game.UI.GameView.PinnedView); } }
        public Player Target { get => _Target; set => Game.UI.GameView.AppendLine("Your target is {0}", ToName(_Target = value)); }
        public PlayerState[] Players { get; protected set; } = new PlayerState[15];
        public Role[] Roles { get => _Roles; set { _Roles = value; Game.UI.RedrawView(Game.UI.RoleListView); } }
        public List<PlayerState> Team { get; set; } = new List<PlayerState>();
        public List<PlayerState> Graveyard { get; set; } = new List<PlayerState>();
        public int Day
        {
            get => _Day;
            set
            {
                NightState = NightState.NONE;
                Game.UI.CommandContext = CommandContext.DAY;
                Game.UI.GameView.AppendLine(("Day {0}", TextClient.BLACK, TextClient.WHITE), _Day = value);
                if (value == 1)
                {
                    Game.Timer = 15;
                    Game.TimerText = "Discussion";
                }
            }
        }
        public int Night
        {
            get => _Night;
            set
            {
                DayState = DayState.NONE;
                Game.UI.CommandContext = CommandContext.NIGHT;
                Game.UI.GameView.AppendLine(("Night {0}", TextClient.BLACK, TextClient.WHITE), _Night = value);
                Game.Timer = Game.Resources.GetMetadata(GameMode).RapidMode ? 15 : 30;
                Game.TimerText = "Night";
            }
        }
        public int AbilitiesLeft { get; set; }
        public bool Host
        {
            get => _Host;
            set { if (_Host != value) { Game.UI.GameView.AppendLine(((_Host = value) ? "You are now host" : "You are no longer host", TextClient.GREEN, null)); } }
        }
        public Player? HostID
        {
            get => _HostID;
            set { _HostID = value; Game.UI.RedrawView(Game.UI.PlayerListView); }
        }
        public string LastWill
        {
            get => _LastWill;
            set { Game.Parser.SaveLastWill(_LastWill = value); }
        }
        public string DeathNote
        {
            get => _DeathNote;
            set { Game.Parser.SaveDeathNote(_DeathNote = value); }
        }
        public string ForgedWill
        {
            get => _ForgedWill;
            set { Game.Parser.SaveForgedWill(_ForgedWill = value); }
        }
        public DayState DayState { get; set; }
        public NightState NightState { get; set; }
        public Faction WinningFaction { get => _WinningFaction; set => Game.UI.GameView.AppendLine((string.Format("Winning faction: {0}", (_WinningFaction = value).ToString().ToDisplayName()), TextClient.GREEN, null)); }
        public Player[] Winners { get => _Winners; set { _Winners = value; Game.UI.CommandContext = CommandContext.GAME_END; Game.UI.OpenSideView(Game.UI.WinnerView); if (value.Contains(Self.ID)) Game.UI.GameView.AppendLine(("You have won", TextClient.GREEN, null)); } }
        
        protected Role _Role;
        protected PlayerState _Self;
        protected Player _Target;
        protected Role[] _Roles;
        protected int _Day;
        protected int _Night;
        protected bool _Host;
        protected Player? _HostID;
        protected string _LastWill = "";
        protected string _DeathNote = "";
        protected string _ForgedWill = "";
        protected Faction _WinningFaction = Faction.DRAW;
        protected Player[] _Winners = Array.Empty<Player>();

        public GameState(TextClient game, GameMode gameMode, bool host)
        {
            Game = game;
            GameMode = gameMode;
            PopulatePlayers();
            Host = host;
            _Roles = new Role[0];
        }

        public void OnStart(int playerCount)
        {
            Started = true;
            Players = new PlayerState[playerCount];
            PopulatePlayers();
            HostID = null;
            Game.UI.CommandContext = CommandContext.PICK_NAMES;
            Game.UI.RedrawSideViews();
            Game.UI.GameView.AppendLine(("Please choose a name (or wait to get a random name)", TextClient.GREEN, null));
            Game.Timer = 25;
            Game.TimerText = "Pick Names";
        }

        public void AddPlayer(Player player, bool host, bool display, string username, LobbyIcon lobbyIcon)
        {
            if (host) HostID = player;
            PlayerState playerState = Players[(int)player];
            playerState.Name = username;
            playerState.SelectedLobbyIcon = lobbyIcon;
            if (display) Game.UI.GameView.AppendLine(("{0} has joined the game", TextClient.GREEN, null), ToName(player));
            if (username == Game.Username) Self = playerState;
        }

        public void RemovePlayer(Player player, bool update, bool display)
        {
            if (display) Game.UI.GameView.AppendLine(("{0} has left the game", TextClient.GREEN, null), ToName(player));
            if (update)
            {
                for (int index = (int)player + 1; index < Players.Length; index++)
                {
                    Players[index].ID--;
                    Players[index - 1] = Players[index];
                }
                Players[14] = new PlayerState(this, Player.PLAYER_15);
                Game.UI.RedrawView(Game.UI.PlayerListView);
            }
            else Players[(int)player].Left = true;
        }

        public void AddRole(Role role)
        {
            Role[] newRoles = new Role[Roles.Length + 1];
            Roles.CopyTo(newRoles, 0);
            newRoles[Roles.Length] = role;
            Roles = newRoles;
        }

        public void RemoveRole(byte index)
        {
            Role[] newRoles = new Role[Roles.Length - 1];
            for (int cpi = 0; cpi < Roles.Length; cpi++)
            {
                if (cpi == index) continue;
                newRoles[cpi < index ? cpi : cpi - 1] = Roles[cpi];
            }
            Roles = newRoles;
        }

        public string ToName(Player playerID, bool inList = false)
        {
            int rawID = (int)playerID;
            if (rawID < Players.Length) return string.Format(playerID == HostID ? inList ? "{0,2} {1} (Host)" : "(Host) {1}" : inList ? Players[rawID]?.Left ?? false ? "{0,2} [{1}]" : "{0,2} {1}" : "({0}) {1}", rawID + 1, Players[rawID]?.Name ?? string.Format("#{0}", rawID + 1));
            switch (playerID)
            {
                case Player.JAILOR:
                    return "Jailor";
                case Player.MEDIUM:
                    return "Medium";
                case Player.MAFIA:
                    return "Mafia";
                case Player.VAMPIRE:
                    return "Vampire";
            }
            return string.Format("#{0}", rawID + 1);
        }

        public FormattedString ToName(PlayerState playerState, bool inList = false)
        {
            if (playerState.Role == null) return ToName(playerState.ID, inList);
            return FormattedString.Format("{0} ({1})", ToName(playerState.ID, inList), playerState.Youngest ? "Youngest" : Game.Resources.Of((Role)playerState.Role));
        }

        public bool TryParsePlayer(string[] args, ref int index, out Player player, bool allowNone = true)
        {
            if (byte.TryParse(args[index], out byte rawID))
            {
                index++;
                player = (Player)(rawID - 1);
                return true;
            }
            for (int length = args.Length - index; length > 0; length++)
            {
                string value = string.Join(" ", args, index, length).ToLower();
                if (allowNone && value == "none")
                {
                    index += length;
                    player = Player.JAILOR;
                    return true;
                }
                foreach (PlayerState ps in Players.Where(ps => ps.Name?.ToLower() == value))
                {
                    index += length;
                    player = ps.ID;
                    return true;
                }
            }
            player = Player.JAILOR;
            return false;
        }

        public bool TryParsePlayer(string value, out Player player)
        {
            if (byte.TryParse(value, out byte rawID))
            {
                player = (Player)(rawID - 1);
                return true;
            }
            if (value == "none")
            {
                player = Player.JAILOR;
                return true;
            }
            foreach (PlayerState ps in Players.Where(ps => ps.Name?.ToLower() == value))
            {
                player = ps.ID;
                return true;
            }
            player = Player.JAILOR;
            return false;
        }

        private void PopulatePlayers()
        {
            for (int index = 0; index < Players.Length; index++) Players[index] = new PlayerState(this, (Player)index);
        }
    }

    class PlayerState
    {
        public Player ID { get; set; }
        public string Name
        {
            get { return _Name; }
            set { _Name = value; game.Game.UI.RedrawView(game.Game.UI.PlayerListView, game.Game.UI.GameView.PinnedView); }
        }
        public Role? Role
        {
            get { return _Role; }
            set { _Role = value; game.Game.UI.RedrawView(game.Game.UI.TeamView); }
        }
        public Character SelectedCharacter { get; set; }
        public House SelectedHouse { get; set; }
        public Pet SelectedPet { get; set; }
        public LobbyIcon SelectedLobbyIcon { get; set; }
        public DeathAnimation SelectedDeathAnimation { get; set; }
        public bool Dead
        {
            get { return _Dead; }
            set { _Dead = value; game.Game.UI.RedrawView(game.Game.UI.TeamView, game.Game.UI.PlayerListView); }
        }
        public bool Left
        {
            get { return _Left; }
            set { _Left = value; game.Game.UI.RedrawView(game.Game.UI.PlayerListView, game.Game.UI.TeamView, game.Game.UI.GraveyardView); }
        }
        public string LastWill
        {
            get { return _LastWill; }
            set
            {
                game.Game.UI.LastWillView.Title = string.Format(" # (LW) {0}", game.ToName(ID));
                game.Game.UI.LastWillView.Value = _LastWill = value;
                game.Game.UI.OpenSideView(game.Game.UI.LastWillView);
                game.Game.Timer = 6;
                game.Game.TimerText = "Last Will";
            }
        }
        public string DeathNote
        {
            get { return _DeathNote; }
            set
            {
                game.Game.UI.LastWillView.Title = string.Format(" # (DN) {0}", game.ToName(ID));
                game.Game.UI.LastWillView.Value = _DeathNote = value;
                game.Game.UI.OpenSideView(game.Game.UI.LastWillView);
            }
        }
        public bool Youngest { get => _Youngest && !_Dead; set { if (_Youngest = value) game.Game.UI.GameView.AppendLine("{0} is now the youngest vampire", game.ToName(ID)); game.Game.UI.RedrawView(game.Game.UI.TeamView, game.Game.UI.GraveyardView); } }

        private GameState game;
        private string _Name;
        private Role? _Role;
        private bool _Dead;
        private bool _Left;
        private string _LastWill;
        private string _DeathNote;
        private bool _Youngest;

        public PlayerState(GameState parent, Player self)
        {
            game = parent;
            ID = self;
        }
    }

    [Flags]
    enum DayState
    {
        NONE,
        BLACKMAILED
    }

    [Flags]
    enum NightState
    {
        NONE,
        JAILED,
        DUEL_ATTACKING = JAILED << 1,
        DUEL_DEFENDING = DUEL_ATTACKING << 1
    }
}
