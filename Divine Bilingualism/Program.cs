using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Bilingualism.Model;
using LSLib.LS;
using LSLib.LS.Enums;

namespace Bilingualism;

internal static class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CancelIoEx(IntPtr handle, IntPtr lpOverlapped);

    private static readonly Dictionary<string, Word> _dic = new();
    private static Dictionary<string, Word>? _newDic;

    private static bool _isCreateNewDictionary;
    private static bool _isStartGame;

    public static void Main(string[] args)
    {
        Console.CursorVisible = false;
        Console.Title = "Divine Bilingualism";

        _isStartGame = true;
#if DEBUG
        //_isCreateNewDictionary = true;
#else
        _isCreateNewDictionary = args.Any(a => a == "-nd");
#endif
        if (_isCreateNewDictionary)
            _newDic = new Dictionary<string, Word>();

        ParseDictionary();
        if (_dic.Count == 0) return;

        Translate(Const.English_PAK);

        if (_isCreateNewDictionary)
            SaveNewDictionary();

        if (_isStartGame)
            Loop(StartGame());
    }

    private static Process StartGame()
    {
        Console.WriteLine("Game launched");
        Console.WriteLine("Enter the words to be removed from the dictionary separated by a space.\n");
        Console.CursorVisible = true;
        return Process.Start(Const.Game_EXE);
    }

    private static void Loop(Process game)
    {
        var isDicChanged = false;
        var deletedCount = 0;
        var inGame = true;

        game.EnableRaisingEvents = true;
        game.Exited += (_, _) =>
        {
            inGame = false;
            CancelIoEx(GetStdHandle(-10), IntPtr.Zero);
        };

        while (inGame)
        {
            var line = string.Empty;
            try { line = Console.ReadLine(); }
            catch { if (inGame is false) break; }

            Console.SetCursorPosition(0, Console.CursorTop - 1 < 0 ? 0 : Console.CursorTop - 1);
            Console.Write("".PadRight(line.Length, ' '));
            Console.Write("\r");

            var words = line
                .Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.ToLower().Trim());

            foreach (var word in words)
            {
                var count = 0;
                if (_dic.ContainsKey(word))
                {
                    count = _dic[word].CountInGame;
                    _dic.Remove(word);
                    deletedCount++;
                    isDicChanged = true;
                    Console.ForegroundColor = ConsoleColor.Green;
                }
                else Console.ForegroundColor = ConsoleColor.Red;

                Console.Write($"{word}[{count}] ");
            }

            if (isDicChanged)
            {
                DictionarySave();
                isDicChanged = false;
            }

            Console.ResetColor();
            Console.WriteLine();
        }

        Console.WriteLine($"\nDictionary reduced by {deletedCount}: \nСurrent word count: {_dic.Count}");
        try
        { Console.ReadKey(); }
        catch { }
    }

    private static void DictionarySave()
    {
        using var sw = new StreamWriter(Const.WordsDictionary, false);
        foreach (var word in _dic.Values)
            sw.WriteLine($"{word.English}\t{word.Other}\t{word.CountInGame}");
    }

    private static void Translate(string filePath, TranslateType type = TranslateType.Colors)
    {
        DecompilePackage(filePath);
        InjectTranslation(type);
        RecompilePackage(filePath);
    }

    private static void SaveNewDictionary()
    {
        using var fw = new FileStream(Path.Combine(Const.NewWordsDictionary), FileMode.Truncate, FileAccess.Write);
        using var sw = new StreamWriter(fw);

        var set = new Dictionary<char, List<Word>>();
        foreach (var kvp in _newDic!)
        {
            var c = kvp.Value.English[0];
            if (set.ContainsKey(c) is false)
                set.Add(c, new List<Word>());
            set[c].Add(kvp.Value);
        }

        var sortedSet = set
            .OrderBy(d => d.Key);

        foreach (var kvp in sortedSet)
        {
            kvp.Value.Sort((a, b) =>
            {
                var result = a.CountInGame.CompareTo(b.CountInGame);
                result = result == -1 ? 1
                    : result == 1 ? -1
                    : 0;
                return result == 0
                    ? string.Compare(a.English, b.English, StringComparison.Ordinal)
                    : result;
            });
            foreach (var word in kvp.Value)
                sw.WriteLine($"{word.English}\t{word.Other}\t{word.CountInGame}");
        }
    }

    private static void ParseDictionary()
    {
        Console.Write("Load dictionary    ");
        
        using (var fs = new FileStream(Const.WordsDictionary, FileMode.Open, FileAccess.Read))
        using (var sr = new StreamReader(fs))
        {
            for (int c = 0, i = 1; sr.EndOfStream is false; c++)
            {
                var arr = sr.ReadLine()?.Split('\t');
                if (arr == null) continue;

                var countParsed = int.TryParse(arr[2], out var countInGame);
                if (_dic.ContainsKey(arr[0]) is false)
                    _dic.Add(arr[0], new Word(arr[0])
                    {
                        Other = arr[1].Trim('`'),
                        CountInGame = countParsed ? countInGame : 1,
                    });

                if (c % 10 != 0) continue;
                LoadProcess(ref i);
            }
        }

        Console.WriteLine($"\r{_dic.Count} words loaded     ");
    }

    private static void InjectTranslation(TranslateType translateType)
    {
        XElement xmlFile;
        using (var stream = new FileStream(Const.English_XML, FileMode.Open, FileAccess.Read))
        {
            xmlFile = XElement.Load(stream);
        }

        Console.Write("Injecting translation    ");
        int i = 1, c = -1;
        foreach (var content in xmlFile.Elements("content"))
        {
            c++;
            var words = ParseWords(content.Value);
            if (words.Count == 0) continue;

            if (_isCreateNewDictionary)
                words.ForEach(word =>
                {
                    if (word.Length < 3) return;
                    if (_newDic!.ContainsKey(word))
                        _newDic[word].CountInGame++;
                    else _newDic.Add(word, new Word(word));
                });

            content.Value += GetTranslate(words, translateType);

            if (c % 10 != 0) continue;
            LoadProcess(ref i);
        }

        xmlFile.Save(Const.English_XML);

        Console.WriteLine("\rTranslation injected     ");
    }

    private static void LoadProcess(ref int i)
    {
        if (Console.CursorLeft < 4)
            return;

        Console.SetCursorPosition(Console.CursorLeft - 3, Console.CursorTop);
        Console.Write(i switch
        {
            1 => "[\\]",
            2 => "[|]",
            3 => "[/]",
            _ => "[-]"
        });
        i = i == 4 ? 1 : i + 1;
    }

    private static readonly char[] _chars =
    {
        '.', ',', '!', '?', '"',
        ';', ':', '@', '#', '$',
        '%', '^', '&', '*', '(',
        ')', '-', '=', '_', '+',
        '[', ']', '{', '}', '|',
        '/', '`', '~', '<', '>',
        '\'', '\\', '…'
    };

    private static List<string> ParseWords(string row)
    {
        row = row.ToLower();

        row = row.Replace(@"&lt;", "<");
        row = row.Replace(@"&gt;", ">");
        row = row.Replace("&apos;", "'");
        row = row.Replace("&quot;", "\"");

        row = Regex.Replace(row, @"\(\[.*\]\)", " ");
        row = Regex.Replace(row, @"<.*>", " ");
        row = Regex.Replace(row, @"\[\d\]", " ");

        row = _chars.Aggregate(row, (current, c) =>
            current.Replace(c, ' '));

        var words = row
            .Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.TrimEnd('’'))
            .ToList();

        var result = new List<string>();
        words.ForEach(word =>
        {
            if (_dic.ContainsKey(word) && result.Contains(word) is false)
                result.Add(word);
        });

        return result;
    }

    private static string GetTranslate(List<string> rowWords, TranslateType type)
    {
        // <font color=\"ffffff\">TEXT</font>;
        // <i>TEXT</i>
        // <b>TEXT</b>
        // <br>

        switch (type)
        {
            case TranslateType.Colors:
                return _Colors();

            case TranslateType.Stars:
                return _Stars();

            default: return string.Empty;
        }

        string _Colors()
        {
            var sb = new StringBuilder();
            sb.Append(" \"");
            foreach (var word in rowWords)
            {
                var percent = _dic[word].CountInGame > 244 ? 244 : _dic[word].CountInGame;
                percent = percent < 55 ? 55 : percent;
                var colorHex = $"b9{percent.ToString("X2").ToLower()}00";
                sb.Append($"<font color=\"{colorHex}\">{_dic[word].English}</font>|<i>{_dic[word].Other}</i> ");
            }

            sb.Append("\"");

            return sb.ToString();
        }

        string _Stars()
        {
            var sb = new StringBuilder();
            sb.Append(" \"");
            foreach (var word in rowWords)
            {
                const string colorHex = "#FFF400";
                var status = _dic[word].CountInGame < 10 ? "" :
                    _dic[word].CountInGame < 100 ? "*" :
                    _dic[word].CountInGame < 1000 ? "**" : "***";
                sb.Append(
                    $"<font color=\"{colorHex.TrimStart('#')}\">{_dic[word].English}</font>/<i>{_dic[word].Other}</i>{status} ");
            }

            sb.Append("\"");

            return sb.ToString();
        }
    }

    private static void DecompilePackage(string filePath)
    {
        if (Directory.Exists(Const.Package))
            Directory.Delete(Const.Package, true);
        Directory.CreateDirectory(Const.Package);

        var originFileName = new FileInfo(filePath).Name
            .Replace(".pak", "_origin.pak");
        var originFilePath = Path.Combine(Const.App, originFileName);
        if (File.Exists(originFilePath) is false)
            File.Copy(filePath, originFilePath);

        var handler = new Packager();
        var package = new PackageReader(originFilePath)
            .Read();
        handler.UncompressPackage(package, Const.Package);
    }

    private static void RecompilePackage(string filePath)
    {
        var handler = new Packager();
        handler.CreatePackage(filePath, Const.Package, compression: CompressionMethod.LZ4);

        Directory.Delete(Const.Package, true);
    }
}