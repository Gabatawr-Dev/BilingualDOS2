namespace Bilingualism.Model;

public class Word : IComparable<Word>
{
    public string English { get; set; }
    public string Other { get; set; }
    public int CountInGame { get; set; }

    public Word(string english)
    {
        English = english;
        Other = "`NULL`";
        CountInGame = 1;
    }

    public int CompareTo(Word other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (ReferenceEquals(null, other)) return 1;
        return string.Compare(English, other.English, StringComparison.Ordinal);
    }
}