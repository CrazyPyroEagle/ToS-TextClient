using ToSParser;

namespace ToSTextClient
{
    interface TextUI
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

        void SetMainView(IView view);
        void OpenSideView(IView view);
        void CloseSideView(IView view);
        void RedrawView(params IView[] views);
        void RedrawMainView();
        void RedrawSideViews();
    }

    interface IView
    {

    }

    interface ITextView : IView
    {
        void AppendLine(string text);
        void AppendLine(string format, params object[] args);
        void ReplaceLine(int index, string text);
        void ReplaceLine(int index, string format, params object[] args);
        void Clear();
    }

    interface IListView<T> : IView
    {

    }

    interface IWillView : IView
    {
        string Title { get; set; }
        string Value { get; set; }
    }

    enum RedrawResult
    {
        SUCCESS,
        WIDTH_CHANGED,
        HEIGHT_CHANGED
    }
}
