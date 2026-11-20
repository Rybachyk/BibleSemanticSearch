
using System.Text.RegularExpressions;
using BibleSearch.Api.Models;

namespace BibleSearch.Api.Services
{
    public class BibleTextParserService
    {
        // Обробляє заголовки з повтореним кодом:
        // 2sa102sa10 -> розділ 10
        private static readonly Regex DuplicateChapterHeaderRegex = new(
            @"^(?<code>[a-z0-9]{3})(?<chapter>\d+)\k<code>\k<chapter>\s*(?<title>.*)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Звичайний формат заголовка:
        // gen1 Буття 1
        // joh3 Івана 3
        private static readonly Regex ChapterHeaderRegex = new(
            @"^(?<code>[a-z0-9]{3})(?<chapter>\d+)\s*(?<title>.*)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex VerseRegex = new(
            @"^(?<number>\d+)\s+(?<text>\S.*)$",
            RegexOptions.Compiled);

        private sealed record BookInfo(
            string Name,
            string Testament,
            int Order);

        private static readonly Dictionary<string, BookInfo> Books = new()
        {
            // Старий Заповіт
            ["gen"] = new("Буття", "Old Testament", 1),
            ["exo"] = new("Вихід", "Old Testament", 2),
            ["lev"] = new("Левит", "Old Testament", 3),
            ["num"] = new("Числа", "Old Testament", 4),
            ["deu"] = new("Повторення Закону", "Old Testament", 5),
            ["jos"] = new("Ісус Навин", "Old Testament", 6),
            ["jud"] = new("Судді", "Old Testament", 7),
            ["rut"] = new("Рут", "Old Testament", 8),
            ["1sa"] = new("1 Самуїлова", "Old Testament", 9),
            ["2sa"] = new("2 Самуїлова", "Old Testament", 10),
            ["1ki"] = new("1 Царів", "Old Testament", 11),
            ["2ki"] = new("2 Царів", "Old Testament", 12),
            ["1ch"] = new("1 Хроніки", "Old Testament", 13),
            ["2ch"] = new("2 Хроніки", "Old Testament", 14),
            ["ezr"] = new("Ездра", "Old Testament", 15),
            ["neh"] = new("Неемія", "Old Testament", 16),
            ["est"] = new("Естер", "Old Testament", 17),
            ["job"] = new("Йов", "Old Testament", 18),
            ["psa"] = new("Псалми", "Old Testament", 19),
            ["pro"] = new("Приповістки", "Old Testament", 20),
            ["ecc"] = new("Екклезіяст", "Old Testament", 21),
            ["son"] = new("Пісня Пісень", "Old Testament", 22),
            ["isa"] = new("Ісая", "Old Testament", 23),
            ["jer"] = new("Єремія", "Old Testament", 24),
            ["lam"] = new("Плач Єремії", "Old Testament", 25),
            ["eze"] = new("Єзекіїл", "Old Testament", 26),
            ["dan"] = new("Даниїл", "Old Testament", 27),
            ["hos"] = new("Осія", "Old Testament", 28),
            ["joe"] = new("Йоіл", "Old Testament", 29),
            ["amo"] = new("Амос", "Old Testament", 30),
            ["oba"] = new("Овдій", "Old Testament", 31),
            ["jon"] = new("Йона", "Old Testament", 32),
            ["mic"] = new("Михей", "Old Testament", 33),
            ["nah"] = new("Наум", "Old Testament", 34),
            ["hab"] = new("Авакум", "Old Testament", 35),
            ["zep"] = new("Софонія", "Old Testament", 36),
            ["hag"] = new("Огій", "Old Testament", 37),
            ["zec"] = new("Захарія", "Old Testament", 38),
            ["mal"] = new("Малахія", "Old Testament", 39),

            // Новий Заповіт
            ["mat"] = new("Матвія", "New Testament", 40),
            ["mar"] = new("Марка", "New Testament", 41),
            ["luk"] = new("Луки", "New Testament", 42),
            ["joh"] = new("Івана", "New Testament", 43),
            ["act"] = new("Дії", "New Testament", 44),
            ["rom"] = new("До Римлян", "New Testament", 45),
            ["1co"] = new("1 до Коринтян", "New Testament", 46),
            ["2co"] = new("2 до Коринтян", "New Testament", 47),
            ["gal"] = new("До Галатів", "New Testament", 48),
            ["eph"] = new("До Ефесян", "New Testament", 49),
            ["phi"] = new("До Филип'ян", "New Testament", 50),
            ["col"] = new("До Колосян", "New Testament", 51),
            ["1th"] = new("1 до Солунян", "New Testament", 52),
            ["2th"] = new("2 до Солунян", "New Testament", 53),
            ["1ti"] = new("1 Тимофію", "New Testament", 54),
            ["2ti"] = new("2 Тимофію", "New Testament", 55),
            ["tit"] = new("Титу", "New Testament", 56),
            ["flm"] = new("Филимону", "New Testament", 57),
            ["heb"] = new("До Євреїв", "New Testament", 58),
            ["jam"] = new("Якова", "New Testament", 59),
            ["1pe"] = new("1 Петра", "New Testament", 60),
            ["2pe"] = new("2 Петра", "New Testament", 61),
            ["1jo"] = new("1 Івана", "New Testament", 62),
            ["2jo"] = new("2 Івана", "New Testament", 63),
            ["3jo"] = new("3 Івана", "New Testament", 64),
            ["rev"] = new("Об'явлення", "New Testament", 66)
        };

        public async Task<List<BibleImportData>> ParseFileAsync(
            string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    "Bible text file was not found.",
                    filePath);
            }

            var lines = await File.ReadAllLinesAsync(filePath);

            var result = new Dictionary<int, BibleImportData>();

            BibleImportData? currentBook = null;
            BibleChapterData? currentChapter = null;

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Спочатку перевіряємо заголовок із повтореним кодом.
                var headerMatch = DuplicateChapterHeaderRegex.Match(line);

                // Якщо повтореного коду немає, перевіряємо звичайний формат.
                if (!headerMatch.Success)
                {
                    headerMatch = ChapterHeaderRegex.Match(line);
                }

                if (headerMatch.Success)
                {
                    string code = headerMatch.Groups["code"]
                        .Value.ToLowerInvariant();

                    string title = headerMatch.Groups["title"].Value;

                    if (!Books.TryGetValue(code, out var bookInfo))
                        continue;

                    // Скорочення jud використовується для двох книг.
                    if (code == "jud" &&
                        title.Contains(
                            "Юд",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        bookInfo = new BookInfo(
                            "Юди",
                            "New Testament",
                            65);
                    }

                    if (!result.TryGetValue(
                        bookInfo.Order,
                        out currentBook))
                    {
                        currentBook = new BibleImportData
                        {
                            Book = bookInfo.Name,
                            Testament = bookInfo.Testament,
                            Order = bookInfo.Order,
                            Translation = "Ohienko"
                        };

                        result.Add(bookInfo.Order, currentBook);
                    }

                    int chapterNumber = int.Parse(
                        headerMatch.Groups["chapter"].Value);

                    // Якщо розділ уже існує, використовуємо його.
                    currentChapter = currentBook.Chapters
                        .FirstOrDefault(
                            chapter => chapter.Chapter == chapterNumber);

                    if (currentChapter == null)
                    {
                        currentChapter = new BibleChapterData
                        {
                            Chapter = chapterNumber
                        };

                        currentBook.Chapters.Add(currentChapter);
                    }

                    continue;
                }

                // Розпізнаємо окремий вірш.
                if (currentChapter == null)
                    continue;

                var verseMatch = VerseRegex.Match(line);

                if (!verseMatch.Success)
                    continue;

                int verseNumber = int.Parse(
                    verseMatch.Groups["number"].Value);

                string verseText = verseMatch.Groups["text"].Value.Trim();

                currentChapter.Verses.Add(new BibleVerseData
                {
                    Number = verseNumber,
                    Text = verseText
                });
            }

            return result.Values
                .OrderBy(book => book.Order)
                .ToList();
        }
    }
}
