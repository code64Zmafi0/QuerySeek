using System.Globalization;
using System.Runtime.CompilerServices;

namespace QuerySeek.Services.Normalizing;

public class DefaultPhraseSplitter : IPhraseSplitter
{
    public static readonly DefaultPhraseSplitter Instance = new();

    public IEnumerable<string> Tokenize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            yield break;

        int pos = 0;
        int length = value.Length;

        while (pos < length)
        {
            // Пропускаем разделители
            while (pos < length)
            {
                CharClass cls = ClassifyAt(value, pos, out int skip);
                if (cls != CharClass.Separator)
                    break;
                pos += skip;
            }

            if (pos >= length)
                yield break;

            int start = pos;
            CharClass startClass = ClassifyAt(value, pos, out int charLen);

            // CJK: каждый иероглиф — отдельный токен
            if (startClass == CharClass.Ideograph)
            {
                pos += charLen;
                yield return value[start..pos];
                continue;
            }

            // Набираем последовательность одного класса (Letter или Digit)
            pos += charLen;
            while (pos < length)
            {
                CharClass cls = ClassifyAt(value, pos, out int cl);
                if (cls != startClass || cls == CharClass.Ideograph)
                    break;
                pos += cl;
            }

            yield return value[start..pos];
        }
    }

    private enum CharClass : byte
    {
        Separator = 0,
        Letter = 1,
        Digit = 2,
        Ideograph = 3
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CharClass ClassifyAt(string s, int index, out int charLength)
    {
        char c = s[index];

        // Суррогатная пара
        if (char.IsHighSurrogate(c))
        {
            charLength = 2;
            if (index + 1 < s.Length && char.IsLowSurrogate(s[index + 1]))
            {
                int cp = char.ConvertToUtf32(c, s[index + 1]);
                return ClassifyCodePoint(cp);
            }
            return CharClass.Separator; // битая пара
        }

        charLength = 1;
        return ClassifyChar(c);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CharClass ClassifyChar(char c)
    {
        // CJK в BMP
        if (IsCjkBmp(c))
            return CharClass.Ideograph;

        // Остальной Unicode
        return CategorizeUnicode(CharUnicodeInfo.GetUnicodeCategory(c));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CharClass ClassifyCodePoint(int cp)
    {
        // CJK Extension B–H (SIP: U+20000 – U+3134F)
        if (cp >= 0x20000 && cp <= 0x3134F)
            return CharClass.Ideograph;

        return CategorizeUnicode(CharUnicodeInfo.GetUnicodeCategory(cp));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CharClass CategorizeUnicode(UnicodeCategory cat) => cat switch
    {
        UnicodeCategory.UppercaseLetter => CharClass.Letter,
        UnicodeCategory.LowercaseLetter => CharClass.Letter,
        UnicodeCategory.TitlecaseLetter => CharClass.Letter,
        UnicodeCategory.ModifierLetter => CharClass.Letter,
        UnicodeCategory.OtherLetter => CharClass.Letter,
        UnicodeCategory.NonSpacingMark => CharClass.Letter,
        UnicodeCategory.SpacingCombiningMark => CharClass.Letter,
        UnicodeCategory.EnclosingMark => CharClass.Letter,
        UnicodeCategory.LetterNumber => CharClass.Letter,
        UnicodeCategory.DecimalDigitNumber => CharClass.Digit,
        _ => CharClass.Separator
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCjkBmp(char c) =>
        (c >= '\u4E00' && c <= '\u9FFF') ||   // CJK Unified Ideographs
        (c >= '\u3400' && c <= '\u4DBF') ||   // CJK Extension A
        (c >= '\uF900' && c <= '\uFAFF') ||   // CJK Compatibility
        (c >= '\u3040' && c <= '\u309F') ||   // Hiragana
        (c >= '\u30A0' && c <= '\u30FF') ||   // Katakana
        (c >= '\u31F0' && c <= '\u31FF') ||   // Katakana Phonetic Ext
        (c >= '\uAC00' && c <= '\uD7AF') ||   // Hangul Syllables
        (c >= '\u1100' && c <= '\u11FF') ||   // Hangul Jamo
        (c >= '\u3100' && c <= '\u312F') ||   // Bopomofo
        (c >= '\uA000' && c <= '\uA48F') ||   // Yi Syllables
        (c >= '\u2E80' && c <= '\u2FDF');      // CJK Radicals / Kangxi
}
