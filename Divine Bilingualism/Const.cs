namespace Bilingualism;

internal static class Const
{
#if DEBUG
    public static string App = @"I:\Steam\steamapps\common\Divinity Original Sin 2\DefEd\Data\Localization";
#else
    public static readonly string App = Environment.CurrentDirectory;
#endif

    public static string Package = Path.Combine(App, "Package");

    public static string English_PAK => Path.Combine(App, "English.pak");
    public static string English_XML => Path.Combine(Package, @"Localization\English\english.xml");

    public static string Game_EXE => Path.Combine(App, @"..\..\bin\EoCApp.exe");

    public static string WordsDictionary => Path.Combine(App, @"Dictionary\en-ru.dic");
    public static string NewWordsDictionary => Path.Combine(App, @"Dictionary\_en-ru.dic");
}