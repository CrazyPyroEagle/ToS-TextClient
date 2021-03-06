﻿using Optional;
using System;
using System.Collections.Generic;
using System.Linq;
using ToSParser;

namespace ToSTextClient
{
    public interface IDocumented
    {
        string Description { get; }
        IEnumerable<FormattedString> Documentation { get; }
    }

    public interface IContextual
    {
        bool IsAllowed(CommandContext activeContext);
    }

    public class Command : IDocumented, IContextual
    {
        public virtual string UsageLine => "";
        public virtual string Description => description;
        public virtual IEnumerable<FormattedString> Documentation => ToDocs(Parsers);
        public virtual ArgumentParser[] Parsers => new ArgumentParser[0];
        protected Func<CommandContext, bool> isAllowed;
        
        protected Action<string> runner;
        protected string description;

        public Command(string description, Func<CommandContext, bool> isAllowed, Action<string> runner) : this(description, isAllowed) => this.runner = runner;

        protected Command(string description, Func<CommandContext, bool> isAllowed)
        {
            this.description = description;
            this.isAllowed = isAllowed;
        }

        public virtual void Run(string cmd, string[] args, int index) => runner(cmd);

        public bool IsAllowed(CommandContext activeContext) => isAllowed(activeContext);

        protected IEnumerable<FormattedString> ToDocs(params ArgumentParser[] args) => args.SelectMany(p => new FormattedString[] { string.Format("  {0}", p.DisplayName), p.Description });
    }

    public class Command<T1> : Command
    {
        public override string UsageLine => parser1.DisplayName;
        public override string Description => string.Format(description, parser1.DisplayName);
        public override ArgumentParser[] Parsers => new ArgumentParser[] { parser1 };

        protected ArgumentParser<T1> parser1;
        protected new Action<string, T1> runner;

        public Command(string description, Func<CommandContext, bool> isAllowed, ArgumentParser<T1> parser1, Action<string, T1> runner) : this(description, isAllowed, parser1) => this.runner = runner;

        protected Command(string description, Func<CommandContext, bool> isAllowed, ArgumentParser<T1> parser1) : base(description, isAllowed) => this.parser1 = parser1;

        public override void Run(string cmd, string[] args, int index)
        {
            if (parser1.Parse(args, ref index, out T1 v1)) runner(cmd, v1);
        }
    }

    public class Command<T1, T2> : Command<T1>
    {
        public override string UsageLine => string.Format("{0} {1}", parser1.DisplayName, parser2.DisplayName);
        public override string Description => string.Format(description, parser1.DisplayName, parser2.DisplayName);
        public override ArgumentParser[] Parsers => new ArgumentParser[] { parser1, parser2 };

        protected ArgumentParser<T2> parser2;
        protected new Action<string, T1, T2> runner;

        public Command(string description, Func<CommandContext, bool> isAllowed, ArgumentParser<T1> parser1, ArgumentParser<T2> parser2, Action<string, T1, T2> runner) : this(description, isAllowed, parser1, parser2) => this.runner = runner;

        protected Command(string description, Func<CommandContext, bool> isAllowed, ArgumentParser<T1> parser1, ArgumentParser<T2> parser2) : base(description, isAllowed, parser1) => this.parser2 = parser2;

        public override void Run(string cmd, string[] args, int index)
        {
            if (parser1.Parse(args, ref index, out T1 v1) && parser2.Parse(args, ref index, out T2 v2)) runner(cmd, v1, v2);
        }
    }

    public class Command<T1, T2, T3> : Command<T1, T2>
    {
        public override string UsageLine => string.Format("{0} {1} {2}", parser1.DisplayName, parser2.DisplayName, parser3.DisplayName);
        public override string Description => string.Format(description, parser1.DisplayName, parser2.DisplayName, parser3.DisplayName);
        public override ArgumentParser[] Parsers => new ArgumentParser[] { parser1, parser2, parser3 };

        protected ArgumentParser<T3> parser3;
        protected new Action<string, T1, T2, T3> runner;

        public Command(string description, Func<CommandContext, bool> isAllowed, ArgumentParser<T1> parser1, ArgumentParser<T2> parser2, ArgumentParser<T3> parser3, Action<string, T1, T2, T3> runner) : this(description, isAllowed, parser1, parser2, parser3) => this.runner = runner;

        protected Command(string description, Func<CommandContext, bool> isAllowed, ArgumentParser<T1> parser1, ArgumentParser<T2> parser2, ArgumentParser<T3> parser3) : base(description, isAllowed, parser1, parser2) => this.parser3 = parser3;

        public override void Run(string cmd, string[] args, int index)
        {
            if (parser1.Parse(args, ref index, out T1 v1) && parser2.Parse(args, ref index, out T2 v2) && parser3.Parse(args, ref index, out T3 v3)) runner(cmd, v1, v2, v3);
        }
    }

    public class CommandGroup : Command
    {
        public override string UsageLine => string.Format("[{0}]", subName);
        public override string Description => string.Format(description, string.Format("[{0}]", subName));
        public override IEnumerable<FormattedString> Documentation => base.Documentation.Concat(commands.GroupBy(c => c.Value).Select(c => c.First()).Where(c => c.Value.IsAllowed(ui.CommandContext)).SelectMany(c => new FormattedString[] { string.Format("  {0} {1}", c.Key, c.Value.UsageLine), c.Value.Description }).Prepend(string.Format(" ~ {0}", subPlural)));

        protected IDictionary<string, Command> commands = new Dictionary<string, Command>();
        protected ITextUI ui;
        protected string subName;
        protected string subPlural;
        
        public CommandGroup(string description, ITextUI ui, string subName, string subPlural, Action<string> runner = null, Func<CommandContext, bool> isAllowed = null) : base(description, isAllowed ?? (activeContext => false), runner)
        {
            this.ui = ui;
            this.subName = subName;
            this.subPlural = subPlural;
        }

        public override void Run(string cmd, string[] args, int index)
        {
            if (args.Length <= index)
            {
                if (runner == null) ui.StatusLine = "Missing parameters";
                else runner(cmd);
                return;
            }
            for (int length = args.Length - index; length >= 0; length--)
            {
                string cmdn = string.Join(" ", args, index, length);
                if (commands.TryGetValue(cmdn, out Command command))
                {
                    index += length;
                    if (command.IsAllowed(ui.CommandContext)) command.Run(cmdn, args, index);
                    else ui.StatusLine = string.Format("{0} not allowed in the current context: {1}", subName, cmdn);
                    return;
                }
            }
            ui.StatusLine = string.Format("{0} not found", subName);
        }

        public CommandGroup Register(Command cmd, params string[] names)
        {
            foreach (string name in names) commands[name] = cmd;
            isAllowed = isAllowed.Or(cmd.IsAllowed);
            return this;
        }
    }
    
    public abstract class ArgumentParser : IDocumented
    {
        public virtual string DisplayName { get; protected set; }
        public virtual string[] HelpNames { get; protected set; } = Array.Empty<string>();
        public virtual string Description { get; protected set; }
        public abstract IEnumerable<FormattedString> Documentation { get; }
        protected Action onFailed;

        public ArgumentParser(string displayName, string description, Action onFailed = null)
        {
            DisplayName = displayName;
            Description = description;
            this.onFailed = onFailed;
        }
    }

    public class ArgumentParser<Type> : ArgumentParser
    {
        public delegate bool TryParser(string value, out Type result);

        public override IEnumerable<FormattedString> Documentation => getValueDocs().Prepend(" ~ Possible Values");
        public TryParser Parser { get; protected set; }

        protected Func<IEnumerable<FormattedString>> getValueDocs;

        public ArgumentParser(string displayName, string description, TryParser parser, Func<IEnumerable<FormattedString>> getValueDocs, Action onFailed = null) : base(displayName, description, onFailed)
        {
            Parser = parser;
            this.getValueDocs = getValueDocs;
            HelpNames = new string[] { typeof(Type).IsEnum ? typeof(Type).Name.AddSpacing().ToLower() : displayName.Substring(1, displayName.Length - 2).ToLower() };
        }

        public bool Parse(string[] args, ref int index, out Type result)
        {
            for (int length = args.Length - index; length >= 0; length--)
            {
                if (Parser(string.Join(" ", args, index, length), out result))
                {
                    index += length;
                    return true;
                }
            }
            result = default;
            onFailed?.Invoke();
            return false;
        }
    }

    public static class ArgumentParsers
    {
        public static ArgumentParser<string> Text(ITextUI ui, string name) => new ArgumentParser<string>(string.Format("[{0}]", name), "Text body", (string value, out string copy) =>
        {
            if (value.Length == 0)
            {
                copy = default;
                return false;
            }
            copy = value;
            return true;
        }, Enumerable.Empty<FormattedString>, () => ui.StatusLine = string.Format("{0} cannot be empty", name));
        public static ArgumentParser<string> Text(string name) => new ArgumentParser<string>(string.Format("<{0}>", name), "Text body", (string value, out string copy) =>
        {
            copy = value;
            return true;
        }, Enumerable.Empty<FormattedString>);
        public static ArgumentParser<string> Username(ITextUI ui) => new ArgumentParser<string>("[Username]", "A username", (string value, out string copy) =>
        {
            copy = value;
            return !value.Contains(' ');
        }, Enumerable.Empty<FormattedString>);
        
        public static ArgumentParser<Player> Player(ITextUI ui) => new ArgumentParser<Player>("[Player]", "The name or number of a player", (string value, out Player player) => ui.Game.GameState.TryParsePlayer(value, out player), () => ui.Game.GameState.Players.Select(ps => ui.Game.GameState.ToName(ps, true)), () => ui.StatusLine = "Invalid player");
        public static ArgumentParser<TEnum> ForEnum<TEnum>(ITextUI ui, string name = null, bool an = false, Func<TEnum, FormattedString> map = null) where TEnum : struct => new ArgumentParser<TEnum>(name ?? string.Format("[{0}]", typeof(TEnum).Name.AddSpacing().ToDisplayName()), string.Format("The name or ID of {0} {1}", an ? "an" : "a", typeof(TEnum).Name.AddSpacing().ToLower()), TryParseEnum, map == null ? GetEnumValueDocs<TEnum> : GetEnumValueDocs(map), () => ui.StatusLine = string.Format("Invalid {0}", typeof(TEnum).Name.AddSpacing().ToLower()));
        public static ArgumentParser<byte> Position(ITextUI ui) => new ArgumentParser<byte>("[Position]", "A position number", ModifyResult<byte, byte>(byte.TryParse, b => (byte)(b - 1u)), Enumerable.Empty<FormattedString>, () => ui.StatusLine = "Invalid position");

        public static ArgumentParser<Option<T>> Optional<T>(ArgumentParser<T> parser) => new ArgumentParser<Option<T>>(OptionalDisplay(parser.DisplayName), string.Format("{0} (optional)", parser.Description), (string value, out Option<T> result) =>
        {
            if (parser.Parser(value, out T rawResult)) result = rawResult.Some();
            else result = Option.None<T>();
            return true;
        }, () => parser.Documentation.Skip(1));

        public static ArgumentParser<TOut>.TryParser ModifyResult<TIn, TOut>(ArgumentParser<TIn>.TryParser parser, Func<TIn, TOut> func) => (string value, out TOut result) =>
        {
            bool valid;
            result = (valid = parser(value, out TIn rawResult)) ? func(rawResult) : default;
            return valid;
        };

        private static bool TryParseEnum<TEnum>(string value, out TEnum result) where TEnum : struct => Enum.TryParse(value.ToUpper().Replace(' ', '_'), out result);

        private static string OptionalDisplay(string display) => string.Format("<{0}>", display[0] == '[' && display[display.Length - 1] == ']' ? display.Substring(1, display.Length - 2) : display);

        private static IEnumerable<FormattedString> GetEnumValueDocs<Type>() => Enum.GetNames(typeof(Type)).Select(n => FormattedString.Format("({0}) {1}", Enum.Format(typeof(Type), Enum.Parse(typeof(Type), n), "d"), n.ToDisplayName()));

        private static Func<IEnumerable<FormattedString>> GetEnumValueDocs<Type>(Func<Type, FormattedString> map) => () => Enum.GetNames(typeof(Type)).Select(n => Enum.Parse(typeof(Type), n)).Select(n => FormattedString.Format("({0}) {1}", Enum.Format(typeof(Type), n, "d"), map((Type)n)));
    }
    
    public enum CommandContext
    {
        AUTHENTICATING,
        HOME,
        LOBBY,
        PICK_NAMES,
        ROLE_SELECTION,
        NIGHT,
        DAY,
        DISCUSSION,
        VOTING,
        JUDGEMENT,
        GAME_END,
        POST_GAME
    }

    public static class CommandExtensions
    {
        public static Func<CommandContext, bool> Set(this CommandContext value) => context => context == value;

        public static bool IsAuthenticated(this CommandContext context) => context != CommandContext.AUTHENTICATING;

        public static bool IsInLobbyOrGame(this CommandContext context) => context.IsInGame() || context == CommandContext.LOBBY;

        public static bool IsInGame(this CommandContext context)
        {
            switch (context)
            {
                case CommandContext.PICK_NAMES:
                case CommandContext.ROLE_SELECTION:
                case CommandContext.NIGHT:
                case CommandContext.DAY:
                case CommandContext.DISCUSSION:
                case CommandContext.VOTING:
                case CommandContext.JUDGEMENT:
                case CommandContext.GAME_END:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsDay(this CommandContext context)
        {
            switch (context)
            {
                case CommandContext.DAY:
                case CommandContext.DISCUSSION:
                case CommandContext.VOTING:
                case CommandContext.JUDGEMENT:
                case CommandContext.GAME_END:
                    return true;
                default:
                    return false;
            }
        }
    }
}
