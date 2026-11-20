namespace BibleSearch.Api.Models
{
    public class BibleImportData
    {
        public string Book { get; set; } = string.Empty;

        public string Testament { get; set; } = string.Empty;

        public int Order { get; set; }

        public string Translation { get; set; } = string.Empty;

        public List<BibleChapterData> Chapters { get; set; } = new();
    }

    public class BibleChapterData
    {
        public int Chapter { get; set; }

        public List<BibleVerseData> Verses { get; set; } = new();
    }

    public class BibleVerseData
    {
        public int Number { get; set; }

        public string Text { get; set; } = string.Empty;
    }
}