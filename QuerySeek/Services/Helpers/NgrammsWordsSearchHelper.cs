using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using QuerySeek.Services.Searching;

namespace QuerySeek.Services.Helpers;

public static class NgrammsWordsSearchHelper
{
    public const byte NGRAM_LENGTH = 3;
    public const byte MAX_WORD_LENGTH = 250;

    /// <summary>
    /// Производит расчет минимальной схожести для слов с заданным минимальным совпадением 
    /// </summary>
    /// <param name="word"></param>
    /// <returns>Минимальное количество нграмм для совпадения</returns>
    public static int CalculateWordSimilarityTreshold(Word word, double minSimilarity)
        => (int)(word.NGrammsHashes.Length * minSimilarity);

    /// <summary>
    /// Производит расчет минимальной схожести для цифр (без последних нграммов)
    /// </summary>
    /// <param name="word"></param>
    /// <returns>Минимальное количество нграмм для совпадения</returns>
    public static int CalculateDigitSimilarityTreshold(Word word)
        => word.NGrammsHashes.Length - NGRAM_LENGTH + 1;

    /// <summary>
    /// Преобразовние строки в массив нграммов (их хеш кодов)
    /// </summary>
    /// <param name="word"></param>
    /// <returns></returns>
    public static int[] GetNgramms(string word)
    {
        const char SpaceChar = ' ';

        ReadOnlySpan<char> wordSpan = word.Length > MAX_WORD_LENGTH
            ? word.AsSpan(0, MAX_WORD_LENGTH)
            : word;

        int spaceLength = NGRAM_LENGTH - 1;
        int totalLength = wordSpan.Length + spaceLength * 2;

        //Буффер нормализации
        Span<char> buffer = stackalloc char[totalLength];

        //Заполняем спейсы
        buffer[..spaceLength].Fill(SpaceChar);
        buffer[^spaceLength..].Fill(SpaceChar);

        //Заполняем слово
        wordSpan.CopyTo(buffer[spaceLength..]);

        //Просчитываем результат слова
        int[] result = new int[wordSpan.Length + NGRAM_LENGTH - 1];

        for (int i = 0; i <= buffer.Length - NGRAM_LENGTH; i++)
        {
            //Вычисляем хеш нграмма
            Span<char> nGramm = buffer.Slice(i, NGRAM_LENGTH);

            int num = 5381;
            int num2 = num;
            for (int k = 0; k < nGramm.Length; k += 2)
            {
                num = (num << 5) + num ^ nGramm[k];

                if (k + 1 < nGramm.Length)
                    num2 = (num2 << 5) + num2 ^ nGramm[k + 1];
            }
            int hash = num + num2 * 1566083941;

            result[i] = hash;
        }

        return result;
    }

    /// <summary>
    /// Поиск схожих слов в индексе по словам из запроса
    /// </summary>
    /// <param name="splittedQuery"></param>
    /// <param name="wordsSearchSettings"></param>
    /// <param name="wordsIdsByNgramms"></param>
    /// <returns></returns>
    public static List<KeyValuePair<int, byte>>[] SearchSimlarIndexWordsByQuery(
        QueryWordContainer[] splittedQuery,
        WordsSearchSettings wordsSearchSettings,
        Dictionary<int, int[]> wordsIdsByNgramms)
    {
        var result = new List<KeyValuePair<int, byte>>[splittedQuery.Length];

        //Используем один словарь для расчета совпавщих слов для каждого слова из запроса дабы лишний раз не аллоцировать
        Dictionary<int, WordNgrammSearchState> wordsSearchProcessDict = new(wordsSearchSettings.WordsSearchDictionaryPreallocate);

        for (int i = 0; i < result.Length; i++)
        {
            QueryWordContainer currentWord = splittedQuery[i];

            //Проверка на введеное слово ранее, чтоб не повторять вычисления
            for (int j = i - 1; j >= 0; j--)
            {
                if (splittedQuery[j].QueryWord.Equals(currentWord.QueryWord))
                {
                    result[i] = result[j];
                    break;
                }
            }

            if (result[i] is null)
            {
                result[i] = SearchSimilarsByQueryWordAndAlternatives(
                    wordsSearchProcessDict,
                    wordsIdsByNgramms,
                    currentWord,
                    wordsSearchSettings);
            }
        }

        return result;
    }

    private static List<KeyValuePair<int, byte>> SearchSimilarsByQueryWordAndAlternatives(
        Dictionary<int, WordNgrammSearchState> wordsSearchProcessDict,
        Dictionary<int, int[]> wordsIdsByNgramms,
        QueryWordContainer wordContainer,
        WordsSearchSettings wordsSearchSettings)
    {
        List<KeyValuePair<int, byte>> result = [];

        //Ищем по одной полностью совпавшей алтернативе
        foreach (Word altWord in wordContainer.Alternatives)
            SearchSimilars(altWord, (byte)altWord.NGrammsHashes.Length);

        Word queryWord = wordContainer.QueryWord;
        int treshold = wordsSearchSettings.SimilarityCalculator(wordContainer.QueryWord);

        SearchSimilars(queryWord, treshold);

        return result;

        void SearchSimilars(Word word, int treshold)
        {
            NgrammSearch(wordsSearchProcessDict, wordsIdsByNgramms, word, treshold);

            //Ищем бандл схожих слов и сортируем по количеству совпадений (вычисляется в свойстве Score. Попадания - наказание за промахи)
            foreach (KeyValuePair<int, WordNgrammSearchState> item in wordsSearchProcessDict
                .Where(i => i.Value.Score >= treshold)
                .OrderByDescending(i => i.Value.Score)
                .Take(wordsSearchSettings.MaxCheckingWordsCount))
            {
                result.Add(new(item.Key, (byte)(item.Value.Score * wordContainer.Multipler)));
            }

            //Чистка переиспользуемого словаря
            wordsSearchProcessDict.Clear();
        }
    }

    /// <summary>
    /// Поиск похожих слов по n-gramm
    /// </summary>
    /// <returns>Словарь id слова количество совпадений и пропусков</returns>
    private static void NgrammSearch(
        Dictionary<int, WordNgrammSearchState> wordsSearchProcessDict,
        Dictionary<int, int[]> wordsIdsByNgramms,
        Word queryWord,
        int treshold)
    {
        byte wordLength = (byte)queryWord.NGrammsHashes.Length;
        treshold = wordLength - treshold;

        Dictionary<int, WordNgrammSearchState> words = wordsSearchProcessDict;

        //Ищем в индексе слов, считаем совпавшие ngramm-ы и пропуски
        for (byte queryWordNgrammIndex = 0; queryWordNgrammIndex < wordLength; queryWordNgrammIndex++)
        {
            if (!wordsIdsByNgramms.TryGetValue(queryWord.NGrammsHashes[queryWordNgrammIndex], out int[]? wordsIds))
                continue;

            foreach (int wordId in wordsIds)
            {
                ref WordNgrammSearchState matchInfo = ref CollectionsMarshal.GetValueRefOrNullRef(words, wordId);

                if (!Unsafe.IsNullRef(ref matchInfo))
                {
                    byte matches = (byte)(matchInfo.Matches + 1);
                    byte misses = (byte)(queryWordNgrammIndex == 0
                        ? 0
                        : matchInfo.Misses + queryWordNgrammIndex - matchInfo.PreviousMatch - 1);

                    matchInfo = new()
                    {
                        Matches = matches,
                        Misses = misses,
                        PreviousMatch = queryWordNgrammIndex,
                    };
                }
                //Попытка отбить добавление в словарь уже точно не совпавщих по treshold
                else if (queryWordNgrammIndex == 0 || (!queryWord.IsDigit && queryWordNgrammIndex <= treshold))
                    words[wordId] = new(1, 0, queryWordNgrammIndex);
            }
        }
    }
    
    private readonly record struct WordNgrammSearchState(
        byte Matches,
        byte Misses,
        byte PreviousMatch)
    {
        public byte Score => (byte)(Matches > Misses ? Matches - (Misses * 0.5) : 0);
    }
}
