using System;
using ToSParser;

namespace ToSTextClient
{
    interface ITextUI
    {
        GameState GameState { get; }
        IExceptionView ExceptionView { get; }
        IAuthView AuthView { get; }
        ITextView HomeView { get; }
        ITextView GameView { get; }
        IListView<GameMode> GameModeView { get; }
        IListView<PlayerState> PlayerListView { get; }
        IListView<Role> RoleListView { get; }
        IListView<PlayerState> GraveyardView { get; }
        IListView<PlayerState> TeamView { get; }
        IWillView LastWillView { get; }
        IListView<Player> WinnerView { get; }
        string StatusLine { get; set; }
        CommandContext CommandContext { get; set; }
        bool RunInput { get; set; }

        void SetMainView(IView view);
        void OpenSideView(IView view);
        void CloseSideView(IView view);
        void RedrawTimer();
        void RedrawView(params IView[] views);
        void RedrawMainView();
        void RedrawSideViews();
        void RegisterCommand(Command command, params string[] names);
        void SetInputContext(IInputView view);
        void AudioAlert();
        void Run();
    }

    interface IView : IContextual { }

    interface ITextView : IView
    {
        void AppendLine(FormattedString text);
        void AppendLine(FormattedString format, params object[] args);
        void ReplaceLine(int index, FormattedString text);
        void ReplaceLine(int index, FormattedString format, params object[] args);
        void Clear();
    }

    interface IListView<T> : IView { }

    interface IWillView : IView
    {
        string Title { get; set; }
        string Value { get; set; }
    }

    interface IExceptionView : IView
    {
        Exception Exception { get; set; }
    }

    interface IInputView : IView
    {
        void Insert(char c);
        void Enter();
        void Backspace();
        void Delete();
        void LeftArrow();
        void RightArrow();
        void Home();
        void End();
        void UpArrow();
        void DownArrow();
        void MoveCursor();
        void Close();
    }

    interface IAuthView : IInputView
    {
        FormattedString Status { get; set; }
    }

    class FormattedString
    {
        public string Value { get; }
        public ConsoleColor Foreground { get; }
        public ConsoleColor Background { get; }

        public FormattedString(string value)
        {
            Value = value;
            Foreground = ConsoleColor.White;
            Background = ConsoleColor.Black;
        }

        public FormattedString(string value, ConsoleColor bg)
        {
            Value = value;
            Foreground = ConsoleColor.White;
            Background = bg;
        }

        public FormattedString(string value, ConsoleColor fg, ConsoleColor bg)
        {
            Value = value;
            Foreground = fg;
            Background = bg;
        }

        public static implicit operator FormattedString(string value) => new FormattedString(value);
        public static implicit operator FormattedString((string value, ConsoleColor bg) value) => new FormattedString(value.value, value.bg);
        public static implicit operator FormattedString((string value, ConsoleColor fg, ConsoleColor bg) value) => new FormattedString(value.value, value.fg, value.bg);

        public static FormattedString operator +(FormattedString a, string b) => new FormattedString(a.Value + b, a.Foreground, a.Background);
        public static FormattedString operator +(string a, FormattedString b) => new FormattedString(a + b.Value, b.Foreground, b.Background);
    }

    enum RedrawResult
    {
        SUCCESS,
        WIDTH_CHANGED,
        HEIGHT_CHANGED
    }
}
