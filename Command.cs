﻿using Optional;
using System;
using System.Collections.Generic;
using ToSParser;

namespace ToSTextClient
{
    class Command
    {
        public virtual string UsageLine { get => ""; }
        public virtual string Description { get => description; }
        public virtual ArgumentParser[] Parsers { get => new ArgumentParser[0]; }
        public virtual string SubcommandHeader { get => "Subcommands"; }
        public virtual IReadOnlyDictionary<string, Command> Subcommands { get; } = new Dictionary<string, Command>();
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
    }

    class Command<T1> : Command
    {
        public override string UsageLine { get => parser1.DisplayName; }
        public override string Description { get => string.Format(description, parser1.DisplayName); }
        public override ArgumentParser[] Parsers { get => new ArgumentParser[] { parser1 }; }

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
        public override string UsageLine { get => string.Format("{0} {1}", parser1.DisplayName, parser2.DisplayName); }
        public override string Description { get => string.Format(description, parser1.DisplayName, parser2.DisplayName); }
        public override ArgumentParser[] Parsers { get => new ArgumentParser[] { parser1, parser2 }; }

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
        public override string UsageLine { get => string.Format("{0} {1} {2}", parser1.DisplayName, parser2.DisplayName, parser3.DisplayName); }
        public override string Description { get => string.Format(description, parser1.DisplayName, parser2.DisplayName, parser3.DisplayName); }
        public override ArgumentParser[] Parsers { get => new ArgumentParser[] { parser1, parser2, parser3 }; }

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
        public override string UsageLine { get => string.Format("[{0}]", subName); }
        public override string Description { get => string.Format(description, string.Format("[{0}]", subName)); }
        public override string SubcommandHeader { get => subPlural; }
        public override IReadOnlyDictionary<string, Command> Subcommands { get => (IReadOnlyDictionary<string, Command>)commands; }

        protected IDictionary<string, Command> commands = new Dictionary<string, Command>();
        protected ITextUI ui;
        protected string subName;
        protected string subPlural;

        public CommandGroup(string description, CommandContext usableContexts, ITextUI ui, string subName = "Subcommand", string subPlural = "Subcommands", Action<string> runner = null) : base(description, usableContexts, runner)
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
            string cmdn = args[index++].ToLower();
            if (commands.TryGetValue(cmdn, out Command command))
            {
                if ((command.UsableContexts & ui.CommandContext) != 0) command.Run(cmdn, args, index);
                else ui.StatusLine = string.Format("{0} not allowed in the current context: {1}", subName, cmdn);
            }
            else ui.StatusLine = string.Format("{0} not found: {1}", subName, cmdn);
        }

        public CommandGroup Register(Command cmd, params string[] names)
        {
            foreach (string name in names) commands.Add(name, cmd);
            return this;
        }
    }

    class ArgumentParser
    {
        public string DisplayName { get; protected set; }
        public string Description { get; protected set; }
        protected Action onFailed;

        protected ArgumentParser(string displayName, string description, Action onFailed)
        {
            DisplayName = displayName;
            Description = description;
            this.onFailed = onFailed;
        }
    }

    class ArgumentParser<Type> : ArgumentParser
    {
        public delegate bool TryParser(string value, out Type result);

        public TryParser Parser { get; protected set; }
        
        public ArgumentParser(string displayName, string description, TryParser parser, Action onFailed = null) : base(displayName, description, onFailed) => Parser = parser;

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
        public static ArgumentParser<string> Text(ITextUI ui, string name) => new ArgumentParser<string>(string.Format("[{0}]", name), "Non-empty text body", (string value, out string copy) =>
        {
            if (value.Length == 0)
            {
                copy = default(string);
                return false;
            }
            copy = value;
            return true;
        }, () => ui.StatusLine = string.Format("{0} cannot be empty", name));
        public static ArgumentParser<string> Text(string name) => new ArgumentParser<string>(string.Format("<{0}>", name), "Text body", (string value, out string copy) =>
        {
            copy = value;
            return true;
        });

        public static ArgumentParser<GameModeID> GameMode(ITextUI ui) => new ArgumentParser<GameModeID>("[Game Mode]", "The name or ID of a game mode", TryParseEnum, () => ui.StatusLine = "Invalid game mode");
        public static ArgumentParser<RoleID> Role(ITextUI ui) => new ArgumentParser<RoleID>("[Role]", "The name or ID of a role", TryParseEnum, () => ui.StatusLine = "Invalid role");
        public static ArgumentParser<PlayerID> Player(ITextUI ui) => new ArgumentParser<PlayerID>("[Player]", "The name or number of a player", (string value, out PlayerID player) => ui.GameState.TryParsePlayer(value, out player), () => ui.StatusLine = "Invalid player");
        public static ArgumentParser<ExecuteReasonID> ExecuteReason(ITextUI ui) => new ArgumentParser<ExecuteReasonID>("[Reason]", "The name or ID of an execute reason", TryParseEnum, () => ui.StatusLine = "Invalid execute reason");
        public static ArgumentParser<ReportReasonID> ReportReason(ITextUI ui) => new ArgumentParser<ReportReasonID>("[Reason]", "The name or ID of a report reason", TryParseEnum, () => ui.StatusLine = "Invalid report reason");
        public static ArgumentParser<byte> Position(ITextUI ui) => new ArgumentParser<byte>("[Position]", "A position number", ModifyResult<byte, byte>(byte.TryParse, b => (byte)(b - 1u)), () => ui.StatusLine = "Invalid position");

        public static ArgumentParser<Option<T>> Optional<T>(ArgumentParser<T> parser) => new ArgumentParser<Option<T>>(OptionalDisplay(parser.DisplayName), string.Format("{0} (optional)", parser.Description), (string value, out Option<T> result) =>
        {
            if (parser.Parser(value, out T rawResult)) result = rawResult.Some();
            else result = Option.None<T>();
            return true;
        });

        public static ArgumentParser<TOut>.TryParser ModifyResult<TIn, TOut>(ArgumentParser<TIn>.TryParser parser, Func<TIn, TOut> func) => (string value, out TOut result) =>
        {
            bool valid;
            result = (valid = parser(value, out TIn rawResult)) ? func(rawResult) : default(TOut);
            return valid;
        };

        private static bool TryParseEnum<TEnum>(string value, out TEnum result) where TEnum : struct => Enum.TryParse(value.ToUpper().Replace(' ', '_'), out result);

        private static string OptionalDisplay(string display) => string.Format("<{0}>", display[0] == '[' && display[display.Length - 1] == ']' ? display.Substring(1, display.Length - 2) : display);
    }
}
