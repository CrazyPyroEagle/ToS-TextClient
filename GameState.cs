using System;
using ToSParser;

namespace ToSTextClient
{
    class GameState
    {
        public GameModeID GameMode { get; protected set; }
        public RoleID Role
        {
            get { return _Role; }
            set { Console.WriteLine("Your role is {0}.", _Role = value); }
        }
        public PlayerID Self { get; set; }
        public PlayerID Target
        {
            get { return _Target; }
            set { Console.WriteLine("Your target is {0}.", ToName(_Target = value)); }
        }
        public PlayerState[] Players { get; protected set; }
        public int PlayerCount { get; set; }
        public int Day
        {
            get { return _Day; }
            set { Console.WriteLine("Day {0}", _Day = value); }
        }
        public int Night
        {
            get { return _Night; }
            set { Console.WriteLine("Night {0}", _Night = value); }
        }
        public int AbilitiesLeft { get; set; }
        public bool Host
        {
            get { return _Host; }
            set { if (_Host != value) Console.WriteLine((_Host = value) ? "You are now host." : "You are no longer host."); }
        }

        protected RoleID _Role;
        protected PlayerID _Target;
        protected int _Day;
        protected int _Night;
        protected bool _Host;

        public GameState(GameModeID gameMode)
        {
            GameMode = gameMode;
            Players = new PlayerState[15];
            PopulatePlayers();
        }

        public void OnStart()
        {
            Players = new PlayerState[PlayerCount];
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

        private void PopulatePlayers()
        {
            for (int index = 0; index < PlayerCount; index++) Players[index] = new PlayerState(this, (PlayerID)index);
        }
    }

    class PlayerState
    {
        public PlayerID Self { get; protected set; }
        public string Name { get; set; }
        public CharacterID SelectedCharacter { get; set; }
        public HouseID SelectedHouse { get; set; }
        public PetID SelectedPet { get; set; }
        public LobbyIconID SelectedLobbyIcon { get; set; }
        public DeathAnimationID SelectedDeathAnimation { get; set; }
        public bool Dead
        {
            get { return _Dead; }
            set { if (_Dead = value) Console.WriteLine("{0} died last night.", game.ToName(Self)); }
        }
        public string LastWill
        {
            get { return _LastWill; }
            set { Console.WriteLine("They had a last will.\n{0}", _LastWill = value); }
        }
        public string DeathNote
        {
            get { return _DeathNote; }
            set { Console.WriteLine("We found a death note lying next to their body.\n{0}", _DeathNote = value); }
        }

        private GameState game;
        private bool _Dead;
        private string _LastWill;
        private string _DeathNote;

        public PlayerState(GameState parent, PlayerID self)
        {
            this.game = parent;
            Self = self;
        }
    }
}
