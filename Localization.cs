using System;
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

        public Localization(ITextUI ui)
        {
            this.ui = ui;
            game = LoadResource("Game.xml");
        }

        public FormattedString Of(LocalizationTable value)
        {
            string id = ((byte)value).ToString();
            XElement element = game.Element("Entries").Elements("Entry").Where(entry => entry.Element("id").Value == id).FirstOrDefault();
            return element == null ? value.ToString().ToDisplayName() : EncodeColor(element.Element("Text").Value, element.Element("Color").Value);
        }

        public FormattedString OfSpyResult(LocalizationTable value)
        {
            string id = string.Format("SpyResult_{0}", (byte)value);
            XElement element = game.Element("Entries").Elements("Entry").Where(entry => entry.Element("id").Value == id).FirstOrDefault();
            return element == null ? string.Format("Spy Result: {0}", Of(value)) : EncodeColor(element.Element("Text").Value, element.Element("Color").Value);
        }

        protected FormattedString EncodeColor(string message, string rawColor)
        {
            ConsoleColor bg = ConsoleColor.Black;
            ConsoleColor fg = ConsoleColor.White;
            if (rawColor == "0xFF0000") bg = ConsoleColor.DarkRed;
            else if (rawColor == "0x00FF00") bg = ConsoleColor.DarkGreen;
            else if (rawColor == "0x505050") fg = ConsoleColor.Green;
            else if (rawColor == "0x00CCFF") bg = ConsoleColor.Blue;
            return (message, fg, bg);
        }

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
