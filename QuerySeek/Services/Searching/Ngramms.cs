using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuerySeek.Services.Searching;

public static class Ngramms
{
    public const byte NGRAM_LENGTH = 3;

    /// <summary>
    /// Преобразовние строки в массив нграммов (их хеш кодов)
    /// </summary>
    /// <param name="word"></param>
    /// <returns></returns>
    public static int[] GetNgramms(string word)
    {
        const char SpaceChar = ' ';

        int spaceLength = NGRAM_LENGTH - 1;
        int totalLength = word.Length + spaceLength * 2;

        //Буффер нормализации
        Span<char> buffer = totalLength <= 256
            ? stackalloc char[totalLength]
            : new char[totalLength];

        //Заполняем спейсы
        buffer[..spaceLength].Fill(SpaceChar);
        buffer[^spaceLength..].Fill(SpaceChar);

        //Заполняем слово
        word.CopyTo(buffer[spaceLength..]);

        //Просчитываем результат слова
        int[] result = new int[word.Length + NGRAM_LENGTH - 1];

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
    /// Поиск похожих слов по n-gramm
    /// </summary>
    /// <returns>Словарь id слова количество совпадений и пропусков</returns>
    public static void NgrammSearch(
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
                    byte matches = (byte)(matchInfo.Mathes + 1);
                    byte misses = CalculateMiss(in matchInfo, queryWordNgrammIndex);

                    matchInfo = new()
                    {
                        Mathes = matches,
                        Misses = misses,
                        PreviousMatch = queryWordNgrammIndex,
                    };

                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    static byte CalculateMiss(in WordNgrammSearchState compareFactor, int queryWordNgrammIndex)
                    {
                        if (queryWordNgrammIndex == 0) return 0;

                        byte missCount = (byte)(queryWordNgrammIndex - compareFactor.PreviousMatch - 1);

                        return (byte)(compareFactor.Misses + missCount);
                    }
                }
                //Попытка отбить добавление в словарь уже точно не совпавщих по treshold
                else if (queryWordNgrammIndex == 0 || (!queryWord.IsDigit && queryWordNgrammIndex <= treshold))
                    words[wordId] = new(1, 0, queryWordNgrammIndex);
            }
        }
    }
}
