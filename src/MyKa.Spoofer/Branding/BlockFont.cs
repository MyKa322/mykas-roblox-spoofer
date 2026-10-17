namespace MyKa.Spoofer.Branding;

/// <summary>A bold eight-row pixel font with two-pixel strokes, containing only the letters in the wordmark.</summary>
public static class BlockFont
{
    public const int Height = 8;
    public const int LetterGap = 2;

    private static readonly Dictionary<char, string[]> Glyphs = new()
    {
        ['A'] = [".####.", "######", "##..##", "##..##", "######", "######", "##..##", "##..##"],
        ['B'] = ["#####.", "######", "##..##", "#####.", "#####.", "##..##", "######", "#####."],
        ['E'] = ["######", "######", "##....", "#####.", "#####.", "##....", "######", "######"],
        ['F'] = ["######", "######", "##....", "#####.", "#####.", "##....", "##....", "##...."],
        ['K'] = ["##..##", "##.##.", "####..", "###...", "###...", "####..", "##.##.", "##..##"],
        ['L'] = ["##....", "##....", "##....", "##....", "##....", "##....", "######", "######"],
        ['M'] = ["##...##", "###.###", "#######", "##.#.##", "##...##", "##...##", "##...##", "##...##"],
        ['O'] = [".####.", "######", "##..##", "##..##", "##..##", "##..##", "######", ".####."],
        ['P'] = ["#####.", "######", "##..##", "######", "#####.", "##....", "##....", "##...."],
        ['R'] = ["#####.", "######", "##..##", "######", "#####.", "##.##.", "##..##", "##..##"],
        ['S'] = [".#####", "######", "##....", ".####.", "..####", "....##", "######", "#####."],
        ['X'] = ["##..##", "##..##", ".####.", "..##..", "..##..", ".####.", "##..##", "##..##"],
        ['Y'] = ["##..##", "##..##", ".####.", "..##..", "..##..", "..##..", "..##..", "..##.."],
        ['\''] = ["##", "##", "#.", "..", "..", "..", "..", ".."],
    };

    public static bool Supports(char letter) => Glyphs.ContainsKey(letter);

    /// <summary>Renders upper-case text into one bitmap, two empty columns between letters so side walls do not merge.</summary>
    public static PixelMask Word(string text)
    {
        var letters = new List<PixelMask>();
        foreach (var letter in text)
        {
            if (!Glyphs.TryGetValue(letter, out var rows))
                throw new ArgumentException("The wordmark font does not contain that letter.");
            letters.Add(PixelMask.FromRows(rows));
        }
        return PixelMask.Concat(letters, LetterGap);
    }
}
