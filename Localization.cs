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

        protected XDocument game;

        public Localization()
        {
            game = LoadResource("Game.xml");
        }

        public FormattedString Of(LocalizationTable value)
        {
            XElement element = game.Element("Entries").Elements("Entry").Where(entry => entry.Element("id").Value == ((byte)value).ToString()).FirstOrDefault();
            if (element == null) return value.ToString().ToDisplayName();
            ConsoleColor bg = ConsoleColor.Black;
            ConsoleColor fg = ConsoleColor.White;
            string rawColor = element.Element("Color").Value;
            if (rawColor == "0xFF0000") bg = ConsoleColor.DarkRed;
            else if (rawColor == "0x00FF00") bg = ConsoleColor.DarkGreen;
            else if (rawColor == "0x505050") fg = ConsoleColor.Green;
            else if (rawColor == "0x00CCFF") bg = ConsoleColor.Blue;
            return (element.Element("Text").Value, fg, bg);
        }

        protected XDocument LoadResource(string path)
        {
            try
            {
                return XDocument.Load(Combine(LOCALIZATION_ROOT, path));
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is SecurityException)
            {
                return XDocument.Load(Combine(FALLBACK_ROOT, path));
            }
        }

        protected string Combine(string dirUri, string path) => string.Format("{0}/{1}", dirUri, path);
    }
}
