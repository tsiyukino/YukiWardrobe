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

        public static readonly YukiNdmfReport Errors = new YukiNdmfReport(L);
    }
}
