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

        public string ToName(PlayerID playerID)
        {
            int rawID = (int)playerID;
            if (rawID < Players.Length) return string.Format(playerID == HostID ? "{0} (Host)" : "{0}", Players[rawID]?.Name ?? string.Format("#{0}", rawID + 1));
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

        public string ToName(PlayerState playerState)
        {
            if (playerState.Role == null) return ToName(playerState.Self);
            return string.Format("{0} ({1})", ToName(playerState.Self), playerState.Role?.ToString()?.ToDisplayName());
        }

        private void PopulatePlayers()
        {
            for (int index = 0; index < Players.Length; index++) Players[index] = new PlayerState(this, (PlayerID)index);
        }
    }

    class PlayerState
    {
        public PlayerID Self { get; protected set; }
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
            set { if (_Dead = value) game.UI.AppendLine("{0} died", game.ToName(Self)); game.UI.RedrawView(game.UI.GraveyardView); game.UI.RedrawView(game.UI.TeamView); }
        }
        public string LastWill { get; set; }
        public string DeathNote { get; set; }

        private GameState game;
        private string _Name;
        private RoleID? _Role;
        private bool _Dead;

        public PlayerState(GameState parent, PlayerID self)
        {
            game = parent;
            Self = self;
        }
    }
}
