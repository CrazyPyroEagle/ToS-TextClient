using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Security;
using System.Text;

using Console = Colorful.Console;

namespace ToSTextClient
{
    public interface ITextUI
    {
        TextClient Game { get; }
        ViewRegistry Views { get; }
        IReadOnlyDictionary<string, Command> Commands { get; }
        string StatusLine { get; set; }
        CommandContext CommandContext { get; set; }
        bool RunInput { get; set; }
        bool TimerVisible { get; set; }

        void RegisterCommand(Command command, params string[] names);
        void RegisterMainView(IView view, params string[] names);
        void RegisterSideView(IView view, params string[] names);
        void SetMainView(IView view);
        void OpenSideView(IView view);
        void CloseSideView(IView view);
        void SetInputContext(IInputView view);
        void RedrawTimer();
        void AudioAlert();
        void Run();
    }

    public interface IView : IContextual
    {
        int MinimumWidth { get; }
        int MinimumHeight { get; }
        IView PinnedView { get; }

        event Action<string, Exception> OnException;
        event Action OnTextChange;

        IEnumerable<FormattedString> Lines(int width);
        void Redraw();
    }

    public interface ITextView : IView
    {
        event Action<int, FormattedString> OnAppend;
        event Action<int, FormattedString> OnReplace;

        void AppendLine(FormattedString text);
        void AppendLine(FormattedString format, params object[] args);
        void ReplaceLine(int index, FormattedString text);
        void ReplaceLine(int index, FormattedString format, params object[] args);
        void Clear();
    }

    public interface IWillView : IView
    {
        string Title { get; set; }
        string Value { get; set; }
    }

    public interface IEditableWillView : IView
    {
        string Title { get; set; }

        event Action<string> OnSave;
    }

    public interface IHelpView : IView
    {
        (IDocumented cmd, string[] names)? Topic { get; set; }

        event Action OnShowHelp;
    }

    public interface IExceptionView : IView
    {
        Exception Exception { get; set; }
    }

    public interface IInputView : IView
    {
        (int x, int y) Cursor { get; }

        event Action OnCursorChange;

        void KeyPress(ConsoleKeyInfo key);

        void Close();
    }

    public interface IAuthView : IInputView
    {
        FormattedString Status { set; }

        event Action<Socket, string, SecureString> OnAuthenticate;
    }

    public class FormattedString
    {
        public string RawValue => values.Select(v => v.raw).Aggregate(new StringBuilder(), (sb, raw) => sb.Append(raw)).ToString();
        protected (string raw, Color? fg, Color? bg)[] values;

        protected FormattedString(params (string raw, Color? fg, Color? bg)[] values) => this.values = values;

        public void Render(int width)
        {
            for (int index = 0; index < values.Length && width > 0; width -= values[index++].raw.Length)
            {
                Console.ForegroundColor = values[index].fg ?? Color.White;
                Console.BackgroundColor = values[index].bg ?? Color.Black;
                Console.Write(values[index].raw.Limit(width));
            }
            if (width > 0) Console.Write("".PadRight(width));
            Console.ForegroundColor = Color.White;
            Console.BackgroundColor = Color.Black;
            //Console.ResetColor();   // Reset colour after padding to allow padding to have last set BG colour
        }

        public override string ToString() => RawValue;

        internal static FormattedString Format(FormattedString format, params object[] args)
        {
            if (format == null) return null;
            return new FormattedString(format.values.SelectMany(v => Format(v, args)).ToArray());
        }

        private static IEnumerable<(string raw, Color? fg, Color? bg)> Format((string raw, Color? fg, Color? bg) value, params object[] args)
        {
            StringBuilder sb = new StringBuilder(value.raw.Length);
            StringBuilder nb = new StringBuilder();
            StringBuilder eb = new StringBuilder();
            bool inBlock = false, indexEnd = false;
            foreach (char c in value.raw)
            {
                switch (c)
                {
                    default:
                        if (inBlock)
                        {
                            if (indexEnd) eb.Append(c);
                            else nb.Append(c);
                        }
                        else sb.Append(c);
                        break;
                    case '{':
                        if (inBlock)
                        {
                            sb.Append('{');
                            sb.Append(nb.ToString());
                            if (indexEnd)
                            {
                                sb.Append(':');
                                sb.Append(eb.ToString());
                            }
                        }
                        nb.Clear();
                        eb.Clear();
                        inBlock = true;
                        indexEnd = false;
                        break;
                    case '}':
                        if (inBlock && uint.TryParse(nb.ToString(), out uint parsed) && parsed < args.Length && args[parsed] is FormattedString formatted)
                        {
                            yield return (string.Format(sb.ToString(), args), value.fg, value.bg);
                            sb.Clear();
                            foreach ((string raw, Color? fg, Color? bg) rawValue in formatted.values) yield return rawValue;
                        }
                        else if (inBlock)
                        {
                            sb.Append('{');
                            sb.Append(nb.ToString());
                            if (indexEnd)
                            {
                                sb.Append(':');
                                sb.Append(eb.ToString());
                            }
                            sb.Append('}');
                        }
                        inBlock = false;
                        break;
                    case ':':
                        if (!inBlock || indexEnd) goto default;
                        indexEnd = true;
                        break;
                }
            }
            yield return (string.Format(sb.ToString(), args), value.fg, value.bg);
        }

        public static FormattedString Concat(IEnumerable<FormattedString> values) => new FormattedString(values.SelectMany(sr => sr.values).ToArray());
        public static FormattedString Concat(params FormattedString[] values) => Concat((IEnumerable<FormattedString>)values);

        public static FormattedString From(params (string raw, Color? fg, Color? bg)[] values) => new FormattedString(values);

        public static implicit operator FormattedString(string value) => new FormattedString((value, null, null));
        public static implicit operator FormattedString((string value, Color? bg) value) => new FormattedString((value.value, null, value.bg));
        public static implicit operator FormattedString((string value, Color? fg, Color? bg) value) => new FormattedString(value);

        public static FormattedString operator +(FormattedString a, string b)
        {
            FormattedString result = new FormattedString(new(string raw, Color? fg, Color? bg)[a.values.Length]);
            Array.Copy(a.values, result.values, result.values.Length);
            result.values[result.values.Length - 1].raw += b;
            return result;
        }
        public static FormattedString operator +(string a, FormattedString b)
        {
            FormattedString result = new FormattedString(new (string raw, Color? fg, Color? bg)[b.values.Length]);
            Array.Copy(b.values, result.values, result.values.Length);
            result.values[0].raw = a + result.values[0].raw;
            return result;
        }
        public static FormattedString operator +(FormattedString a, FormattedString b) => new FormattedString(a.values.Concat(b.values).ToArray());
    }

    public enum RedrawResult
    {
        SUCCESS,
        WIDTH_CHANGED,
        HEIGHT_CHANGED
    }
}
