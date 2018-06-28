using System;
using System.Collections.Generic;
using System.Linq;
using ToSParser;

namespace ToSTextClient
{
    class GameState
    {
        public TextUI UI { get { return game.UI; } }
        public GameModeID GameMode { get; protected set; }
        public bool Started { get; protected set; }
        public RoleID Role
        {
            get { return _Role; }
            set { game.UI.AppendLine("Your role is {0}", (_Role = value).ToString().ToDisplayName()); }
        }
        public PlayerID Self { get; set; }
        public PlayerID Target
        {
            get { return _Target; }
            set { game.UI.AppendLine("Your target is {0}", ToName(_Target = value)); }
        }
        public PlayerState[] Players { get; protected set; } = new PlayerState[15];
        public RoleID[] Roles { get; protected set; } = new RoleID[0];
        public List<PlayerState> Team { get; set; } = new List<PlayerState>();
        public List<PlayerState> Graveyard { get; set; } = new List<PlayerState>();
        public int PlayerCount { get; set; }
        public int Day
        {
            get { return _Day; }
            set { game.UI.AppendLine("Day {0}", _Day = value); }
        }
        public int Night
        {
            get { return _Night; }
            set { game.UI.AppendLine("Night {0}", _Night = value); }
        }
        public int AbilitiesLeft { get; set; }
        public bool Host
        {
            get { return _Host; }
            set { if (_Host != value) game.UI.AppendLine((_Host = value) ? "You are now host" : "You are no longer host"); }
        }
        public PlayerID? HostID
        {
            get { return _HostID; }
            set { _HostID = value; game.UI.RedrawView(game.UI.PlayerListView); }
        }

        protected TextClient game;
        protected RoleID _Role;
        protected PlayerID _Target;
        protected int _Day;
        protected int _Night;
        protected bool _Host;
        protected PlayerID? _HostID;

        public GameState(TextClient game, GameModeID gameMode)
        {
            this.game = game;
            GameMode = gameMode;
            PopulatePlayers();
        }

        public void OnStart()
        {
            Started = true;
            Players = new PlayerState[PlayerCount];
            Roles = new RoleID[PlayerCount];
            PopulatePlayers();
            HostID = null;
            game.UI.RedrawSideViews();
        }

        public void RemovePlayer(PlayerID player)
        {
            for (int index = (int)player + 1; index < Players.Length; index++)
            {
                Players[index].Self--;
                Players[index - 1] = Players[index];
            }
            Players[14] = new PlayerState(this, PlayerID.PLAYER_15);
            game.UI.RedrawView(game.UI.PlayerListView);
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

        private void PopulatePlayers()
        {
            for (int index = 0; index < Players.Length; index++) Players[index] = new PlayerState(this, (PlayerID)index);
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
            set { if (_Dead = value) game.UI.AppendLine("{0} died", game.ToName(Self)); game.Graveyard.Add(this); game.UI.RedrawView(game.UI.GraveyardView); game.UI.RedrawView(game.UI.TeamView); }
        }
        public bool Left
        {
            get { return _Left; }
            set { _Left = value; game.UI.RedrawView(game.UI.PlayerListView); game.UI.RedrawView(game.UI.TeamView); game.UI.RedrawView(game.UI.GraveyardView); }
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
