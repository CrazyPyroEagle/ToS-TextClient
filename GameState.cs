using System;
using System.Collections.Generic;
using ToSParser;

namespace ToSTextClient
{
    class GameState
    {
        public TextUI UI { get { return game.UI; } }
        public GameModeID GameMode { get; protected set; }
        public RoleID Role
        {
            get { return _Role; }
            set { game.UI.AppendLine("Your role is {0}.", (_Role = value).ToString().ToDisplayName()); }
        }
        public PlayerID Self { get; set; }
        public PlayerID Target
        {
            get { return _Target; }
            set { game.UI.AppendLine("Your target is {0}.", ToName(_Target = value)); }
        }
        public PlayerState[] Players { get; protected set; } = new PlayerState[15];
        public RoleID[] Roles { get; protected set; } = new RoleID[15];
        public List<PlayerState> Team { get; set; } = new List<PlayerState>();
        public List<PlayerState> Graveyard { get; protected set; } = new List<PlayerState>();
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
            set { if (_Host != value) game.UI.AppendLine((_Host = value) ? "You are now host." : "You are no longer host."); }
        }

        protected TextClient game;
        protected RoleID _Role;
        protected PlayerID _Target;
        protected int _Day;
        protected int _Night;
        protected bool _Host;

        public GameState(TextClient game, GameModeID gameMode)
        {
            this.game = game;
            GameMode = gameMode;
            PopulatePlayers();
        }

        public void OnStart()
        {
            Players = new PlayerState[PlayerCount];
            Roles = new RoleID[PlayerCount];
            PopulatePlayers();
        }

        public string ToName(PlayerID playerID)
        {
            int rawID = (int)playerID;
            if (rawID < Players.Length) return Players[rawID]?.Name ?? string.Format("#{0}", rawID);
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
            return string.Format("#{0}", rawID);
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
        public string Name { get; set; }
        public RoleID? Role { get; set; }
        public CharacterID SelectedCharacter { get; set; }
        public HouseID SelectedHouse { get; set; }
        public PetID SelectedPet { get; set; }
        public LobbyIconID SelectedLobbyIcon { get; set; }
        public DeathAnimationID SelectedDeathAnimation { get; set; }
        public bool Dead
        {
            get { return _Dead; }
            set { if (_Dead = value) game.UI.AppendLine("{0} died last night.", game.ToName(Self)); }
        }
        public string LastWill { get; set; }
        public string DeathNote { get; set; }

        private GameState game;
        private bool _Dead;

        public PlayerState(GameState parent, PlayerID self)
        {
            game = parent;
            Self = self;
        }
    }
}
