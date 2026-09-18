using System;
using System.Collections.Generic;
using System.IO;
using TsiYuki.Core.Editor;

namespace TsiYuki.Wardrobe.Editor
{
    // Labels the wardrobe writes into the avatar's menu when the user hasn't
    // named something. Always English: the menu is seen in game by everyone,
    // and a mix of the editor's language and the user's own labels looks odd.
    public static class MenuText
    {
        public const string None = "None";
        public const string Wear = "Wear";
        public const string Looks = "Looks";
        public const string Colors = "Colors";
        public static string ColorN(int n) => "Color " + n;
    }

    // UI strings (Localization/<code>.txt) for both the editor UI and NDMF's
    // error report window.
    public static class WardrobeText
    {
        public const string Package = "moe.tsiyuki.wardrobe";
        public static readonly YukiLocalizer L = new YukiLocalizer(Package);

        static nadena.dev.ndmf.localization.Localizer _ndmf;

        // NDMF shows errors in its own language setting; we feed it our tables.
        public static nadena.dev.ndmf.localization.Localizer Ndmf => _ndmf ?? (_ndmf = new nadena.dev.ndmf.localization.Localizer("en-us", () => new List<(string, Func<string, string>)>
        {
            ("en-us", Table("en")),
            ("zh-hans", Table("zh-Hans")),
            ("ja-jp", Table("ja")),
        }));

        static Func<string, string> Table(string code)
        {
            var table = new Dictionary<string, string>();
            var path = Path.GetFullPath($"Packages/{Package}/Localization/{code}.txt");
            if (File.Exists(path))
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    var eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    table[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim().Replace("\\n", "\n");
                }
            return key => table.TryGetValue(key, out var v) ? v : null;
        }
    }
}
