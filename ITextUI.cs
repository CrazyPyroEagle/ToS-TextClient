using System;
using ToSParser;

namespace ToSTextClient
{
    interface ITextUI
    {
        ITextView HomeView { get; }
        IListView<GameModeID> GameModeView { get; }
        ITextView GameView { get; }
        IListView<PlayerState> PlayerListView { get; }
        IListView<RoleID> RoleListView { get; }
        IListView<PlayerState> GraveyardView { get; }
        IListView<PlayerState> TeamView { get; }
        IWillView LastWillView { get; }
        string StatusLine { get; set; }
        CommandContext CommandContext { get; set; }
        bool RunInput { get; set; }

        void SetMainView(IView view);
        void OpenSideView(IView view);
        void CloseSideView(IView view);
        void RedrawView(params IView[] views);
        void RedrawMainView();
        void RedrawSideViews();
        void RegisterCommand(Command command, params string[] names);
        void SetCommandContext(CommandContext context, bool value);
    }

    interface IView { }

    interface ITextView : IView
    {
        void AppendLine(string text);
        void AppendLine(string format, params object[] args);
        void ReplaceLine(int index, string text);
        void ReplaceLine(int index, string format, params object[] args);
        void Clear();
    }

    interface IListView<T> : IView { }

    interface IWillView : IView
    {
        string Title { get; set; }
        string Value { get; set; }
    }

    class Command
    {
        public string UsageLine { get; protected set; }
        public string Description { get; protected set; }
        public CommandContext UsableContexts { get; protected set; }

        protected Action<string[]> runner;

        public Command(string usageLine, string description, CommandContext usableContexts, Action<string[]> runner)
        {
            UsageLine = usageLine;
            Description = description;
            UsableContexts = usableContexts;
            this.runner = runner;
        }

        public void Run(string[] cmd) => runner(cmd);
    }

    [Flags]
    enum CommandContext
    {
        NONE,
        AUTHENTICATING,
        HOME = AUTHENTICATING << 1,
        LOBBY = HOME << 1,
        HOST = LOBBY << 1,
        GAME = HOST << 1,
        PICK_NAMES = GAME << 1,
        NIGHT = PICK_NAMES << 1,
        DAY = NIGHT << 1,
        VOTING = DAY << 1,
        JUDGEMENT = VOTING << 1,
        POST_GAME = JUDGEMENT << 1
    }

    enum RedrawResult
    {
        SUCCESS,
        WIDTH_CHANGED,
        HEIGHT_CHANGED
    }
}
