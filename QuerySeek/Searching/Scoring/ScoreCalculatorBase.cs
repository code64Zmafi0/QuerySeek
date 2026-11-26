using System.Runtime.CompilerServices;
using QuerySeek.Helpers;
using QuerySeek.Models;

namespace QuerySeek.Searching.Scoring;

/// <summary>
/// Стандартная реализация расчета <see cref="EntitySearchResult.SummaryMatchesCount"/> и <see cref="EntitySearchResult.Score"/>
/// </summary>
/// <remarks>
/// Считает <see cref="EntitySearchResult.SummaryMatchesCount"/> и <see cref="EntitySearchResult.Score"/> из совпадений слов: сущности и ее линков
/// <para>Позволяет переопределить множители совпадений из линков</para>
/// </remarks>
/// <typeparam name="TContext">Тип поискового контекста</typeparam>
public class ScoreCalculatorBase<TContext> : IScoreCalculator<TContext> where TContext : SearchContextBase
{
    public static readonly ScoreCalculatorBase<TContext> Intance = new();

    #region Overrides
    /// <summary>
    /// (SCORING SETTINGS) Множитель совпадений из связанных сущностей
    /// </summary>
    /// <param name="context">Контекст поиска</param>
    /// <param name="currentEntity">Обрабатываемой сущность</param>
    /// <param name="linkedEntity">Привязанной сущность</param>
    public virtual double GetLinkedEntityMatchMultiplier(TContext context, Key currentEntity, Key linkedEntity)
        => 1;

    /// <summary>
    /// (SCORING SETTINGS) Множитель типа имени для сущностей
    /// </summary>
    /// <param name="entityType">Тип сущности</param>
    /// <param name="nameType">Тип имени</param>
    public virtual double GetNameTypeMultiplier(byte entityType, byte nameType)
        => 1;

    /// <summary>
    /// (SCORING SETTINGS) Позволяет при совпадении линка, добавить просчет его звасимостей (зайти в иерархию привязанной)
    /// </summary>
    /// <param name="entityType">Тип обрабатываемой сущности</param>
    /// <param name="linkedType">Тип привязанной сущности</param>
    public virtual bool OnLinkedMatchNeedMergeLinks(byte entityType, byte linkedType)
        => false;

    /// <summary>
    /// (SCORING SETTINGS) Использование дополнительных правил
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    /// <param name="entity">Обработанная сущность</param>
    /// <param name="summaryMatches">Общие совпадения токенов</param>
    public virtual void UseRules(TContext context, EntitySearchResult entity, in Span<TokenMatch> summaryMatches) { }
    #endregion

    /// <summary>
    /// Просчитываем скор текстовых совпадний для сущности
    /// </summary>
    public void CalculateTextScore(TContext context, EntitySearchResult entitySearchResult)
    {
        if (entitySearchResult.Score != 0) return;

        Key currentEntitykey = entitySearchResult.Key;
        Span<TokenMatch> queryTokensMatches = stackalloc TokenMatch[context.SplittedAndNormalizedQuery.Length];

        //Сущность может быть привязана к нескольким сущностям одного типа.
        //Если линк с таким типом уже был просчитан - пропускаем просчеты для сущностей этого типа
        BitArray256 matchedTypes = new();

        //Просчитываем совпадения для слов запроса из сущнности и ее линков, заполняем queryTokensMatches
        ProcessNodeMatches(context, queryTokensMatches, currentEntitykey, entitySearchResult);
        matchedTypes[currentEntitykey.Type] = true;

        foreach (Key linkKey in entitySearchResult.Meta.Links)
        {
            byte linkType = linkKey.Type;

            if (matchedTypes[linkType]
                || !context.TryGetSearchedEntity(linkKey, out EntitySearchResult? link)
                || link.Matches.Count == 0)
                continue;

            ProcessNodeMatches(context, queryTokensMatches, currentEntitykey, link);
            matchedTypes[linkType] = true;

            //Пробуем провалится на уровень выше по условию и просчитать линков с данного родителя
            if (OnLinkedMatchNeedMergeLinks(currentEntitykey.Type, linkType))
            {
                Key[] linkedEntityLinks = link.Meta.Links;

                foreach (Key mergedEntityKey in linkedEntityLinks)
                {
                    byte mergingLinkType = mergedEntityKey.Type;

                    if (matchedTypes[mergingLinkType]
                        || !context.TryGetSearchedEntity(mergedEntityKey, out EntitySearchResult? mergingLink)
                        || mergingLink.Matches.Count == 0)
                        continue;

                    ProcessNodeMatches(context, in queryTokensMatches, currentEntitykey, mergingLink);
                    matchedTypes[mergingLinkType] = true;
                }
            }
        }

        int resultScore = 0;
        int matchesCount = 0;

        for (byte i = 0; i < queryTokensMatches.Length; i++)
        {
            TokenMatch match = queryTokensMatches[i];

            if (!match.IsEmpty)
            {
                resultScore += match.Score;
                matchesCount++;
            }
        }

        entitySearchResult.SummaryMatchesCount = matchesCount;
        entitySearchResult.Score = resultScore;

        UseRules(context, entitySearchResult, in queryTokensMatches);
    }

    /// <summary>
    /// Заполняет матчи для слов из запроса <paramref name="queryTokensMatches"/> из совпадений для сущности <paramref name="matchesEntity"/>
    /// </summary>
    /// <param name="context">Поиска</param>
    /// <param name="queryTokensMatches">Совпадения со словами из запроса</param>
    /// <param name="currentKey">Тип сущность для которой идет просчет</param>
    /// <param name="matchesEntity">Матчи слов с сущностью</param>
    private void ProcessNodeMatches(TContext context, in Span<TokenMatch> queryTokensMatches, Key currentKey, EntitySearchResult matchesEntity)
    {
        double nodeMultiplier = GetLinkedEntityMatchMultiplier(context, currentKey, matchesEntity.Key);
        if (nodeMultiplier == 0) return;

        //Сначала выбираем матчи по сущности пытаемся собрать совпадения для слов из запроса
        Span<TokenMatch> currentMatches = stackalloc TokenMatch[queryTokensMatches.Length];

        List<TokenMatch> matches = matchesEntity.Matches;
        for (int i = 0; i < matches.Count; i++)
        {
            TokenMatch tokenMatch = GetValidMatch(matches, i);
            QueryTokenContainer queryToken = context.SearchTokens[tokenMatch.SearchTokenIndex];

            int queryWordPosition = GetCurrentQueryWordPosition(in currentMatches, queryToken);
            TokenMatch previouslyCalculatedResult = currentMatches[queryWordPosition];

            bool isNewQueryPosition = false;
            if (PreviouslyMatchedWordToNewPositionName(tokenMatch, previouslyCalculatedResult))
            {
                isNewQueryPosition = true;
                queryWordPosition = GetNewQueryWordPosition(in currentMatches, queryToken);
            }

            if (queryWordPosition == -1) continue;

            double nameTypeMultiplier = GetNameMultiplierInternal(matchesEntity.Key.Type, tokenMatch.NameType);

            byte score = (byte)Math.Round(tokenMatch.Score * nameTypeMultiplier * nodeMultiplier);

            if (isNewQueryPosition || previouslyCalculatedResult.Score < score)
                currentMatches[queryWordPosition] = new(tokenMatch.NameTokenPosition, tokenMatch.NameType, tokenMatch.SearchTokenIndex, score);
        }

        //Мерж между линками
        for (int i = 0; i < queryTokensMatches.Length; i++)
        {
            TokenMatch nodeMatch = currentMatches[i];
            TokenMatch previouslyCalculatedResult = queryTokensMatches[i];

            if (nodeMatch.IsEmpty) continue;
            if (!previouslyCalculatedResult.IsEmpty)
            {
                QueryTokenContainer queryToken = context.SearchTokens[nodeMatch.SearchTokenIndex];
                int nextQueryWordPosition = GetNewQueryWordPosition(queryTokensMatches, queryToken);
                if (nextQueryWordPosition != -1) queryTokensMatches[nextQueryWordPosition] = nodeMatch;
            }
            else if (previouslyCalculatedResult.Score < nodeMatch.Score)
            {
                queryTokensMatches[i] = nodeMatch;
            }
        }
    }

    private static TokenMatch GetValidMatch(List<TokenMatch> matches, int index)
    {
        TokenMatch current = matches[index];

        for (int i = index + 1; i < matches.Count; i++)
        {
            TokenMatch over = matches[i];

            if (over.NameTokenPosition == current.NameTokenPosition
                && over.NameType == current.NameType
                && over.Score > current.Score)
            {
                return over;
            }
        }

        return current;
    }

    private static int GetNewQueryWordPosition(in Span<TokenMatch> scores, QueryTokenContainer queryToken)
    {
        foreach (int position in queryToken.PositionsInRequest)
        {
            if (scores[position].IsEmpty) return position;
        }

        return -1;
    }

    private static int GetCurrentQueryWordPosition(in Span<TokenMatch> scores, QueryTokenContainer queryToken)
    {
        int previousNotEmpty = -1;

        int[] positions = queryToken.PositionsInRequest;

        foreach (int position in positions)
        {
            if (scores[position].IsEmpty)
            {
                if (previousNotEmpty != -1) return previousNotEmpty;
            }
            else
            {
                previousNotEmpty = position;
            }
        }

        return positions[0];
    }

    private static bool PreviouslyMatchedWordToNewPositionName(TokenMatch compareResult, TokenMatch previouslyCalculatedResult)
        => !previouslyCalculatedResult.IsEmpty
        && previouslyCalculatedResult.NameType == compareResult.NameType
        && previouslyCalculatedResult.NameTokenPosition != compareResult.NameTokenPosition
        && previouslyCalculatedResult.SearchTokenIndex == compareResult.SearchTokenIndex;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal double GetNameMultiplierInternal(byte entityType, byte nameType)
    {
        if (nameType == 0) return 1;
        return GetNameTypeMultiplier(entityType, nameType);
    }
}
