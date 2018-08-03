using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security;
using System.Xml.Linq;
using ToSParser;

namespace ToSTextClient
{
    class Localization
    {
        protected const string LOCALIZATION_ROOT = "file://C:/Program Files (x86)/steam/steamapps/common/Town of Salem/XMLData/Localization/en-US";
        protected const string FALLBACK_ROOT = "http://blankmediagames.com/TownOfSalem/XMLData/Localization/en-US";

        protected ITextUI ui;
        protected XDocument game;
        protected XDocument mod;

        public Localization(ITextUI ui)
        {
            this.ui = ui;
            game = LoadResource("Game.xml");
            mod = LoadResource("Mod.xml");
        }

        public FormattedString Of(LocalizationTable value) => Get(game, ((byte)value).ToString()) ?? value.ToString().ToDisplayName();

        public FormattedString OfSpyResult(LocalizationTable value) => Get(game, string.Format("SpyResult_{0}", (byte)value)) ?? string.Format("Spy Result: {0}", Of(value));

        public FormattedString Of(ModeratorMessage value) => Get(mod, ((byte)(value + 1)).ToString()) ?? value.ToString().ToDisplayName();

        public FormattedString Of(Role role)        // TODO: Use exact colours from official client
        {
            string name = role.ToString().ToDisplayName();
            switch (role)
            {
                default:
                    return name;
                case Role.BODYGUARD:
                case Role.DOCTOR:
                case Role.ESCORT:
                case Role.INVESTIGATOR:
                case Role.JAILOR:
                case Role.LOOKOUT:
                case Role.MAYOR:
                case Role.MEDIUM:
                case Role.RETRIBUTIONIST:
                case Role.SHERIFF:
                case Role.SPY:
                case Role.TRANSPORTER:
                case Role.VAMPIRE_HUNTER:
                case Role.VETERAN:
                case Role.VIGILANTE:
                case Role.CRUSADER:
                case Role.TRACKER:
                case Role.TRAPPER:
                case Role.PSYCHIC:
                    return (name, TextClient.GREEN, null);
                case Role.BLACKMAILER:
                case Role.CONSIGLIERE:
                case Role.CONSORT:
                case Role.DISGUISER:
                case Role.FORGER:
                case Role.FRAMER:
                case Role.GODFATHER:
                case Role.JANITOR:
                case Role.MAFIOSO:
                case Role.HYPNOTIST:
                case Role.AMBUSHER:
                    return (name, TextClient.RED, null);
                case Role.AMNESIAC:
                    return (name, TextClient.BLUE, null);
                case Role.ARSONIST:
                    return (name, TextClient.ORANGE, null);
                case Role.EXECUTIONER:
                case Role.STONED:
                    return (name, TextClient.GRAY, null);
                case Role.JESTER:
                    return (name, TextClient.PINK, null);
                case Role.SERIAL_KILLER:
                    return (name, TextClient.BLUE, null);
                case Role.SURVIVOR:
                    return (name, TextClient.YELLOW, null);
                case Role.VAMPIRE:
                    return (name, TextClient.GRAY, null);
                case Role.WEREWOLF:
                    return (name, TextClient.BROWN, null);
                case Role.WITCH:
                    return (name, TextClient.PURPLE, null);
                case Role.RANDOM_TOWN:
                    return FormattedString.From(("Random ", TextClient.BLUE, null), ("Town", TextClient.GREEN, null));
                case Role.TOWN_INVESTIGATIVE:
                    return FormattedString.From(("Town", TextClient.GREEN, null), (" Investigative", TextClient.BLUE, null));
                case Role.TOWN_PROTECTIVE:
                    return FormattedString.From(("Town", TextClient.GREEN, null), (" Protective", TextClient.BLUE, null));
                case Role.TOWN_KILLING:
                    return FormattedString.From(("Town", TextClient.GREEN, null), (" Killing", TextClient.BLUE, null));
                case Role.TOWN_SUPPORT:
                    return FormattedString.From(("Town", TextClient.GREEN, null), (" Support", TextClient.BLUE, null));
                case Role.RANDOM_MAFIA:
                    return FormattedString.From(("Random ", TextClient.BLUE, null), ("Mafia", TextClient.RED, null));
                case Role.MAFIA_SUPPORT:
                    return FormattedString.From(("Mafia", TextClient.RED, null), (" Support", TextClient.BLUE, null));
                case Role.MAFIA_DECEPTION:
                    return FormattedString.From(("Mafia", TextClient.RED, null), (" Deception", TextClient.BLUE, null));
                case Role.RANDOM_NEUTRAL:
                    return FormattedString.From(("Random ", TextClient.BLUE, null), ("Neutral", TextClient.GRAY, null));
                case Role.NEUTRAL_BENIGN:
                    return FormattedString.From(("Neutral", TextClient.GRAY, null), (" Benign", TextClient.BLUE, null));
                case Role.NEUTRAL_EVIL:
                    return FormattedString.From(("Neutral", TextClient.GRAY, null), (" Evil", TextClient.BLUE, null));
                case Role.NEUTRAL_KILLING:
                    return FormattedString.From(("Neutral", TextClient.GRAY, null), (" Killing", TextClient.BLUE, null));
                case Role.ANY:
                case Role.GUARDIAN_ANGEL:
                case Role.CLEANED:
                    return (name, TextClient.WHITE, null);
                case Role.PLAGUEBEARER:
                    return (name, TextClient.LIME, null);
                case Role.JUGGERNAUT:
                    return (name, TextClient.DARK_RED, null);
                case Role.PIRATE:
                    return (name, TextClient.DARK_YELLOW, null);
                case Role.COVEN_LEADER:
                case Role.POTION_MASTER:
                case Role.HEX_MASTER:
                case Role.NECROMANCER:
                case Role.POISONER:
                case Role.MEDUSA:
                    return (name, TextClient.PURPLE, null);
                case Role.COVEN_RANDOM_COVEN:
                    return FormattedString.From(("Coven Random ", TextClient.BLUE, null), ("Coven", TextClient.PURPLE, null));
                case Role.COVEN_RANDOM_TOWN:
                    return FormattedString.From(("Coven Random ", TextClient.BLUE, null), ("Town", TextClient.GREEN, null));
                case Role.COVEN_TOWN_INVESTIGATIVE:
                    return FormattedString.From(("Coven ", TextClient.BLUE, null), ("Town", TextClient.GREEN, null), (" Investigative", TextClient.BLUE, null));
                case Role.COVEN_TOWN_PROTECTIVE:
                    return FormattedString.From(("Coven ", TextClient.BLUE, null), ("Town", TextClient.GREEN, null), (" Protective", TextClient.BLUE, null));
                case Role.COVEN_TOWN_KILLING:
                    return FormattedString.From(("Coven ", TextClient.BLUE, null), ("Town", TextClient.GREEN, null), (" Killing", TextClient.BLUE, null));
                case Role.COVEN_TOWN_SUPPORT:
                    return FormattedString.From(("Coven ", TextClient.BLUE, null), ("Town", TextClient.GREEN, null), (" Support", TextClient.BLUE, null));
                case Role.COVEN_RANDOM_MAFIA:
                    return FormattedString.From(("Coven Random ", TextClient.BLUE, null), ("Mafia", TextClient.RED, null));
                case Role.COVEN_MAFIA_SUPPORT:
                    return FormattedString.From(("Coven ", TextClient.BLUE, null), ("Mafia", TextClient.RED, null), (" Support", TextClient.BLUE, null));
                case Role.COVEN_MAFIA_DECEPTION:
                    return FormattedString.From(("Coven ", TextClient.BLUE, null), ("Mafia", TextClient.RED, null), (" Deception", TextClient.BLUE, null));
                case Role.COVEN_RANDOM_NEUTRAL:
                    return FormattedString.From(("Coven Random ", TextClient.BLUE, null), ("Neutral", TextClient.GRAY, null));
                case Role.COVEN_NEUTRAL_BENIGN:
                    return FormattedString.From(("Coven ", TextClient.BLUE, null), ("Neutral", TextClient.GRAY, null), (" Benign", TextClient.BLUE, null));
                case Role.COVEN_NEUTRAL_EVIL:
                    return FormattedString.From(("Coven ", TextClient.BLUE, null), ("Neutral", TextClient.GRAY, null), (" Evil", TextClient.BLUE, null));
                case Role.COVEN_NEUTRAL_KILLING:
                    return FormattedString.From(("Coven ", TextClient.BLUE, null), ("Neutral", TextClient.GRAY, null), (" Killing", TextClient.BLUE, null));
                case Role.COVEN_ANY:
                    return FormattedString.From(("Coven ", TextClient.BLUE, null), ("Any", TextClient.WHITE, null));
                case Role.COVEN_NEUTRAL_CHAOS:
                    return FormattedString.From(("Coven ", TextClient.BLUE, null), ("Neutral", TextClient.GRAY, null), (" Chaos", TextClient.BLUE, null));
                case Role.PESTILENCE:
                    return (name, TextClient.DARK_GRAY, null);
            }
        }

        protected FormattedString Get(XDocument doc, string id)
        {
            XElement element = doc.Element("Entries").Elements("Entry").Where(entry => entry.Element("id").Value == id).FirstOrDefault();
            return element == null ? null : EncodeColor(element.Element("Text").Value, element.Element("Color").Value);
        }

        protected FormattedString EncodeColor(string message, string rawColor) => (message, ColorTranslator.FromHtml(rawColor));

        protected XDocument LoadResource(string path)
        {
            try
            {
                return XDocument.Load(path);
            }
            catch (Exception ex) when (ex is IOException || ex is SecurityException)
            {
                try
                {
                    return XDocument.Load(Combine(LOCALIZATION_ROOT, path));
                }
                catch (Exception ex2) when (ex2 is IOException || ex2 is SecurityException)
                {
                    try
                    {
                        return XDocument.Load(Combine(FALLBACK_ROOT, path));
                    }
                    catch (Exception ex3) when (ex3 is IOException || ex3 is SecurityException)
                    {
                        ui.StatusLine = "Failed to load localization files: check your internet connection";
                        return null;
                    }
                }
            }
        }

        protected string Combine(string dirUri, string path) => string.Format("{0}/{1}", dirUri, path);
    }
}
