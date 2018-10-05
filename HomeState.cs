using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ToSParser;

namespace ToSTextClient
{
    public class FriendList : IReadOnlyDictionary<uint, FriendState>, IReadOnlyDictionary<string, FriendState>, ICollection<FriendState>, IReadOnlyCollection<FriendState>, IEnumerable<FriendState>
    {
        private readonly ITextUI ui;
        private readonly Dictionary<uint, FriendState> byID;
        private readonly Dictionary<string, FriendState> byName;

        public FriendState this[uint key] => byID[key];
        public FriendState this[string key] => byName[key];

        IEnumerable<uint> IReadOnlyDictionary<uint, FriendState>.Keys => byID.Keys;
        IEnumerable<string> IReadOnlyDictionary<string, FriendState>.Keys => byName.Keys;

        public IEnumerable<FriendState> Values => byID.Values;

        public int Count => byID.Count;

        public bool IsReadOnly => false;

        public FriendList(ITextUI ui)
        {
            this.ui = ui;
            byID = new Dictionary<uint, FriendState>();
            byName = new Dictionary<string, FriendState>();
        }

        public bool ContainsKey(uint key) => byID.ContainsKey(key);
        public bool ContainsKey(string key) => byName.ContainsKey(key);

        IEnumerator<KeyValuePair<uint, FriendState>> IEnumerable<KeyValuePair<uint, FriendState>>.GetEnumerator() => byID.GetEnumerator();
        IEnumerator<KeyValuePair<string, FriendState>> IEnumerable<KeyValuePair<string, FriendState>>.GetEnumerator() => byName.GetEnumerator();

        public bool TryGetValue(uint key, out FriendState value) => byID.TryGetValue(key, out value);
        public bool TryGetValue(string key, out FriendState value) => byName.TryGetValue(key, out value);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public IEnumerator<FriendState> GetEnumerator() => byID.Values.GetEnumerator();

        public void Add(FriendState item)
        {
            byID.Add(item.UserID, item);
            byName.Add(item.Username, item);
        }

        public void Clear()
        {
            byID.Clear();
            byName.Clear();
        }

        public bool Contains(FriendState item) => byID.ContainsValue(item);

        public void CopyTo(FriendState[] array, int arrayIndex) => byID.Values.CopyTo(array, arrayIndex);

        public bool Remove(FriendState item) => byID.Remove(item.UserID) && byName.Remove(item.Username);
    }

    public class FriendState
    {
        public TextClient Game { get; }
        public uint UserID { get; }
        public string Username { get => _Username; set { _Username = value; Game.UI.Views.Friends.Redraw(); } }
        public OnlineStatus OnlineStatus { get => _OnlineStatus; set { _OnlineStatus = value; Game.UI.Views.Friends.Redraw(); } }
        public bool OwnsCoven { get => _OwnsCoven; set { _OwnsCoven = value; Game.UI.Views.Friends.Redraw(); } }

        private string _Username;
        private OnlineStatus _OnlineStatus;
        private bool _OwnsCoven;

        public FriendState(TextClient game, uint userID, string username)
        {
            Game = game;
            Username = username;
            UserID = userID;
        }

        public FriendState(TextClient game, uint userID, string username, OnlineStatus onlineStatus, bool ownsCoven) : this(game, userID, username)
        {
            _OnlineStatus = onlineStatus;
            _OwnsCoven = ownsCoven;
        }

        public FormattedString ToDisplay()
        {
            Color? color = null;
            switch (OnlineStatus)
            {
                case OnlineStatus.OFFLINE:
                    color = TextClient.DARK_GRAY;
                    break;
                case OnlineStatus.ONLINE:
                    color = TextClient.GREEN;
                    break;
                case OnlineStatus.ACTIVE:
                    color = TextClient.LIME;
                    break;
                case OnlineStatus.AWAY:
                    color = TextClient.YELLOW;
                    break;
                case OnlineStatus.IN_GAME:
                    color = TextClient.RED;
                    break;
                case OnlineStatus.IN_LOBBY:
                    color = TextClient.BLUE;
                    break;
            }
            return (Username, color, null);
        }
    }

    public class PartyState
    {
        public TextClient Game { get; }
        public Brand Brand { get => _Brand; set { _Brand = value; Game.UI.Views.Party.Redraw(); } }
        public GameMode SelectedMode { get => _SelectedMode; set { _SelectedMode = value; Game.UI.Views.Party.Redraw(); } }
        public IReadOnlyDictionary<string, PartyMember> Members => members;
        public IReadOnlyDictionary<string, PartyInvitation> Invitations => invitations;

        private readonly Dictionary<string, PartyMember> members;
        private readonly Dictionary<string, PartyInvitation> invitations;
        private Brand _Brand;
        private GameMode _SelectedMode;

        public PartyState(TextClient game, Brand brand, bool isHost)
        {
            Game = game;
            Brand = brand;
            members = new Dictionary<string, PartyMember>();
            invitations = new Dictionary<string, PartyInvitation>();
            PartyMember member = AddMember(game.Username);
            if (isHost) member.PermissionLevel = PartyPermissionLevel.Host;
        }

        public PartyMember AddMember(string username)
        {
            PartyMember member = new PartyMember(this, username);
            members.Add(username, member);
            Game.UI.Views.Party.Redraw();
            Game.UI.Views.Home.AppendLine(("{0} has joined the party", TextClient.GREEN, null), username);
            return member;
        }

        public void RemoveMember(string username, bool kicked = false)
        {
            members.Remove(username);
            Game.UI.Views.Party.Redraw();
            Game.UI.Views.Home.AppendLine((kicked ? "{0} was kicked from the party" : "{0} has left the party", TextClient.GREEN, null), username);
        }

        public PartyInvitation AddInvitation(string username)
        {
            PartyInvitation invitation = new PartyInvitation(this, username);
            invitations.Add(username, invitation);
            Game.UI.Views.Home.Redraw();
            return invitation;
        }

        public void RemoveInvitation(string username)
        {
            invitations.Remove(username);
            Game.UI.Views.Party.Redraw();
        }
    }

    public class PartyMember
    {
        public string Username { get; }
        public PartyPermissionLevel PermissionLevel { get => _PermissionLevel; set { _PermissionLevel = value; party.Game.UI.Views.Home.Redraw(); } }

        private readonly PartyState party;
        private PartyPermissionLevel _PermissionLevel;

        public PartyMember(PartyState party, string username)
        {
            this.party = party;
            Username = username;
        }

        public FormattedString ToDisplay() => ToString();

        public override string ToString()
        {
            switch (PermissionLevel)
            {
                case PartyPermissionLevel.None:
                    return Username;
                case PartyPermissionLevel.CanInvite:
                    return string.Format("{0} (I)", Username);
                case PartyPermissionLevel.Host:
                    return string.Format("{0} (H)", Username);
            }
            return string.Format("{0} ({1})", Username, PermissionLevel);
        }
    }

    public class PartyInvitation
    {
        public string Username { get; }
        public PendingInvitationStatus Status { get => _Status; set { if ((_Status = value) == PendingInvitationStatus.ACCEPTED) party.AddMember(Username); else party.Game.UI.Views.Party.Redraw(); } }
        
        private readonly PartyState party;
        private PendingInvitationStatus _Status;

        public PartyInvitation(PartyState party, string username)
        {
            this.party = party;
            Username = username;
        }

        public FormattedString ToDisplay()
        {
            switch (Status)
            {
                case PendingInvitationStatus.PENDING:
                    return FormattedString.Format("{0} ({1})", Username, (FormattedString)("Pending", TextClient.YELLOW, null));
                case PendingInvitationStatus.DENIED:
                case PendingInvitationStatus.FAILED:
                case PendingInvitationStatus.CANCELED:
                case PendingInvitationStatus.LEFT:
                case PendingInvitationStatus.LOCALE:
                case PendingInvitationStatus.NO_COVEN:
                    return FormattedString.Format("{0} ({1})", Username, (FormattedString)(Status.ToString().ToDisplayName(), TextClient.RED, null));
                case PendingInvitationStatus.ACCEPTED:
                case PendingInvitationStatus.LOADING:
                    return FormattedString.Format("{0} ({1})", Username, (FormattedString)(Status.ToString().ToDisplayName(), TextClient.GREEN, null));
            }
            return string.Format("{0} ({1})", Username, Status.ToString().ToDisplayName());
        }

        public override string ToString() => ToDisplay().RawValue;
    }

    public enum PartyPermissionLevel
    {
        None,
        CanInvite,
        Host
    }
}
