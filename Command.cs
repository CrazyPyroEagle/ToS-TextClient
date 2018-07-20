using Optional;
using System;
using System.Collections.Generic;
using System.Linq;
using ToSParser;

namespace ToSTextClient
{
    interface IDocumented
    {
        string Description { get; }
        IEnumerable<string> Documentation { get; }
    }

    class Command : IDocumented
    {
        public virtual string UsageLine => "";
        public virtual string Description => description;
        public virtual IEnumerable<string> Documentation => ToDocs(Parsers);
        public virtual ArgumentParser[] Parsers => new ArgumentParser[0];
        public CommandContext UsableContexts { get; protected set; }
        
        protected Action<string> runner;
        protected string description;

        public Command(string description, CommandContext usableContexts, Action<string> runner) : this(description, usableContexts) => this.runner = runner;

        protected Command(string description, CommandContext usableContexts)
        {
            this.description = description;
            UsableContexts = usableContexts;
        }

        public virtual void Run(string cmd, string[] args, int index) => runner(cmd);

        protected IEnumerable<string> ToDocs(params ArgumentParser[] args) => args.SelectMany(p => new string[] { string.Format("  {0}", p.DisplayName), p.Description });
    }

    class Command<T1> : Command
    {
        public override string UsageLine => parser1.DisplayName;
        public override string Description => string.Format(description, parser1.DisplayName);
        public override ArgumentParser[] Parsers => new ArgumentParser[] { parser1 };

        protected ArgumentParser<T1> parser1;
        protected new Action<string, T1> runner;

        public Command(string description, CommandContext usableContexts, ArgumentParser<T1> parser1, Action<string, T1> runner) : this(description, usableContexts, parser1) => this.runner = runner;

        protected Command(string description, CommandContext usableContexts, ArgumentParser<T1> parser1) : base(description, usableContexts) => this.parser1 = parser1;

        public override void Run(string cmd, string[] args, int index)
        {
            if (parser1.Parse(args, ref index, out T1 v1)) runner(cmd, v1);
        }
    }

    class Command<T1, T2> : Command<T1>
    {
        public override string UsageLine => string.Format("{0} {1}", parser1.DisplayName, parser2.DisplayName);
        public override string Description => string.Format(description, parser1.DisplayName, parser2.DisplayName);
        public override ArgumentParser[] Parsers => new ArgumentParser[] { parser1, parser2 };

        protected ArgumentParser<T2> parser2;
        protected new Action<string, T1, T2> runner;

        public Command(string description, CommandContext usableContexts, ArgumentParser<T1> parser1, ArgumentParser<T2> parser2, Action<string, T1, T2> runner) : this(description, usableContexts, parser1, parser2) => this.runner = runner;

        protected Command(string description, CommandContext usableContexts, ArgumentParser<T1> parser1, ArgumentParser<T2> parser2) : base(description, usableContexts, parser1) => this.parser2 = parser2;

        public override void Run(string cmd, string[] args, int index)
        {
            if (parser1.Parse(args, ref index, out T1 v1) && parser2.Parse(args, ref index, out T2 v2)) runner(cmd, v1, v2);
        }
    }

    class Command<T1, T2, T3> : Command<T1, T2>
    {
        public override string UsageLine => string.Format("{0} {1} {2}", parser1.DisplayName, parser2.DisplayName, parser3.DisplayName);
        public override string Description => string.Format(description, parser1.DisplayName, parser2.DisplayName, parser3.DisplayName);
        public override ArgumentParser[] Parsers => new ArgumentParser[] { parser1, parser2, parser3 };

        protected ArgumentParser<T3> parser3;
        protected new Action<string, T1, T2, T3> runner;

        public Command(string description, CommandContext usableContexts, ArgumentParser<T1> parser1, ArgumentParser<T2> parser2, ArgumentParser<T3> parser3, Action<string, T1, T2, T3> runner) : this(description, usableContexts, parser1, parser2, parser3) => this.runner = runner;

        protected Command(string description, CommandContext usableContexts, ArgumentParser<T1> parser1, ArgumentParser<T2> parser2, ArgumentParser<T3> parser3) : base(description, usableContexts, parser1, parser2) => this.parser3 = parser3;

        public override void Run(string cmd, string[] args, int index)
        {
            if (parser1.Parse(args, ref index, out T1 v1) && parser2.Parse(args, ref index, out T2 v2) && parser3.Parse(args, ref index, out T3 v3)) runner(cmd, v1, v2, v3);
        }
    }

    class CommandGroup : Command
    {
        public override string UsageLine => string.Format("[{0}]", subName);
        public override string Description => string.Format(description, string.Format("[{0}]", subName));
        public override IEnumerable<string> Documentation => base.Documentation.Concat(commands.GroupBy(c => c.Value).Select(c => c.First()).Where(c => (c.Value.UsableContexts & ui.CommandContext) > 0).SelectMany(c => new string[] { string.Format("  {0} {1}", c.Key, c.Value.UsageLine), c.Value.Description }).ListHeader(string.Format(" ~ {0}", subPlural)));

        protected IDictionary<string, Command> commands = new Dictionary<string, Command>();
        protected ITextUI ui;
        protected string subName;
        protected string subPlural;
        
        public CommandGroup(string description, ITextUI ui, string subName, string subPlural, Action<string> runner = null, CommandContext usableContexts = CommandContext.NONE) : base(description, usableContexts, runner)
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
                    if ((command.UsableContexts & ui.CommandContext) != 0) command.Run(cmdn, args, index);
                    else ui.StatusLine = string.Format("{0} not allowed in the current context: {1}", subName, cmdn);
                    return;
                }
            }
            ui.StatusLine = string.Format("{0} not found", subName);
        }

        public CommandGroup Register(Command cmd, params string[] names)
        {
            foreach (string name in names) commands[name] = cmd;
            UsableContexts |= cmd.UsableContexts;
            return this;
        }
    }
    
    abstract class ArgumentParser : IDocumented
    {
        public virtual string DisplayName { get; protected set; }
        public virtual string[] HelpNames { get; protected set; } = Array.Empty<string>();
        public virtual string Description { get; protected set; }
        public abstract IEnumerable<string> Documentation { get; }
        protected Action onFailed;

        public ArgumentParser(string displayName, string description, Action onFailed = null)
        {
            DisplayName = displayName;
            Description = description;
            this.onFailed = onFailed;
        }
    }

    class ArgumentParser<Type> : ArgumentParser
    {
        public delegate bool TryParser(string value, out Type result);

        public override IEnumerable<string> Documentation => getValueDocs().ListHeader(" ~ Possible Values");
        public TryParser Parser { get; protected set; }

        protected Func<IEnumerable<string>> getValueDocs;

        public ArgumentParser(string displayName, string description, TryParser parser, Func<IEnumerable<string>> getValueDocs, Action onFailed = null) : base(displayName, description, onFailed)
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
            result = default(Type);
            onFailed?.Invoke();
            return false;
        }
    }

    static class ArgumentParsers
    {
        public static ArgumentParser<string> Text(ITextUI ui, string name) => new ArgumentParser<string>(string.Format("[{0}]", name), "Text body", (string value, out string copy) =>
        {
            if (value.Length == 0)
            {
                copy = default(string);
                return false;
            }
            copy = value;
            return true;
        }, Enumerable.Empty<string>, () => ui.StatusLine = string.Format("{0} cannot be empty", name));
        public static ArgumentParser<string> Text(string name) => new ArgumentParser<string>(string.Format("<{0}>", name), "Text body", (string value, out string copy) =>
        {
            copy = value;
            return true;
        }, Enumerable.Empty<string>);
        public static ArgumentParser<string> Username(ITextUI ui) => new ArgumentParser<string>("[Username]", "A username", (string value, out string copy) =>
        {
            copy = value;
            return !value.Contains(' ');
        }, Enumerable.Empty<string>);
        
        public static ArgumentParser<Player> Player(ITextUI ui) => new ArgumentParser<Player>("[Player]", "The name or number of a player", (string value, out Player player) => ui.GameState.TryParsePlayer(value, out player), () => ui.GameState.Players.Select(ps => ui.GameState.ToName(ps, true)), () => ui.StatusLine = "Invalid player");
        public static ArgumentParser<TEnum> ForEnum<TEnum>(ITextUI ui, string name = null, bool an = false) where TEnum : struct => new ArgumentParser<TEnum>(name ?? string.Format("[{0}]", typeof(TEnum).Name.AddSpacing().ToDisplayName()), string.Format("The name or ID of {0} {1}", an ? "an" : "a", typeof(TEnum).Name.AddSpacing().ToLower()), TryParseEnum, GetEnumValueDocs<TEnum>, () => ui.StatusLine = string.Format("Invalid {0}", typeof(TEnum).Name.AddSpacing().ToLower()));
        public static ArgumentParser<byte> Position(ITextUI ui) => new ArgumentParser<byte>("[Position]", "A position number", ModifyResult<byte, byte>(byte.TryParse, b => (byte)(b - 1u)), Enumerable.Empty<string>, () => ui.StatusLine = "Invalid position");

        public static ArgumentParser<Option<T>> Optional<T>(ArgumentParser<T> parser) => new ArgumentParser<Option<T>>(OptionalDisplay(parser.DisplayName), string.Format("{0} (optional)", parser.Description), (string value, out Option<T> result) =>
        {
            if (parser.Parser(value, out T rawResult)) result = rawResult.Some();
            else result = Option.None<T>();
            return true;
        }, () => parser.Documentation.Skip(1));

        public static ArgumentParser<TOut>.TryParser ModifyResult<TIn, TOut>(ArgumentParser<TIn>.TryParser parser, Func<TIn, TOut> func) => (string value, out TOut result) =>
        {
            bool valid;
            result = (valid = parser(value, out TIn rawResult)) ? func(rawResult) : default(TOut);
            return valid;
        };

        private static bool TryParseEnum<TEnum>(string value, out TEnum result) where TEnum : struct => Enum.TryParse(value.ToUpper().Replace(' ', '_'), out result);

        private static string OptionalDisplay(string display) => string.Format("<{0}>", display[0] == '[' && display[display.Length - 1] == ']' ? display.Substring(1, display.Length - 2) : display);

        private static IEnumerable<string> GetEnumValueDocs<Type>() => Enum.GetNames(typeof(Type)).Select(n => string.Format("({0}) {1}", Enum.Format(typeof(Type), Enum.Parse(typeof(Type), n), "d"), n.ToDisplayName()));
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
        POST_GAME = JUDGEMENT << 1,
        DUEL_DEFENDING = POST_GAME << 1,
        DUEL_ATTACKING = POST_GAME << 1
    }
}
