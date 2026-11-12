using System.Runtime.CompilerServices;
using QuerySeek.Models;
using QuerySeek.Services.Helpers;

namespace QuerySeek.Services.Searching.Scoring;

/// <summary>
/// Стандартная реализация просчета текстового скора для сущностей
/// </summary>
/// <remarks>
/// Считает скор для сущности из совпадений слов: сущности и ее линков. Множители совпадений из линков можно переопределить.
/// </remarks>
/// <typeparam name="TContext">Тип конеткста</typeparam>
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
    /// <param name="queryWordsMatches">Совпадения для слов из запроса</param>
    public virtual void UseRules(TContext context, EntitySearchResult entity, in Span<TokenMatch> queryWordsMatches) { }
    #endregion

    /// <summary>
    /// Просчитываем скор текстовых совпадний для сущности
    /// </summary>
    public void CalculateTextScore(TContext context, EntitySearchResult entitySearchResult)
    {
        if (entitySearchResult.Score != 0) return;

        Span<TokenMatch> queryWordsMatches = stackalloc TokenMatch[context.SplittedAndNormalizedQuery.Length];

        //Просчитываем совпадения для слов запроса из сущнности и ее линков, заполняем queryWordsMatches
        foreach ((Key LinkedEntity, List<TokenMatch> Matches) in GetMatches(context, entitySearchResult))
        {
            double nodeMultiplier = GetLinkedEntityMatchMultiplier(context, entitySearchResult.Key, LinkedEntity);
            if (nodeMultiplier != 0)
            {
                ProcessNodeMatches(in queryWordsMatches, LinkedEntity.Type, Matches, context, nodeMultiplier);
            }
        }

        //Складываем скор совпадений слов
        int resultScore = 0;
        foreach (TokenMatch ws in queryWordsMatches)
            resultScore += ws.Score;

        entitySearchResult.Score = resultScore;

        UseRules(context, entitySearchResult, in queryWordsMatches);
    }

    /// <summary>
    /// Возвращает наборы совпавших слов для сущности и прилинкованных к ней
    /// </summary>
    private IEnumerable<(Key Entity, List<TokenMatch> Matches)> GetMatches(
        TContext context,
        EntitySearchResult entitySearchResult)
    {
        byte currentEntityType = entitySearchResult.Key.Type;
        //Сущность может быть привязана к нескольким сущностям одного типа.
        //Если линк с таким типом уже был просчитан - пропускаем просчеты для сущностей этого типа
        BitArray256 matchedTypes = new();

        yield return (entitySearchResult.Key, entitySearchResult.Matches);
        matchedTypes[currentEntityType] = true;

        foreach (Key linkKey in entitySearchResult.Meta.Links)
        {
            byte linkType = linkKey.Type;

            if (matchedTypes[linkType]
                || !context.TryGetSearchedEntity(linkKey, out EntitySearchResult? link)
                || link.Matches.Count == 0)
                continue;

            yield return (linkKey, link.Matches);
            matchedTypes[linkType] = true;

            //Пробуем провалится на уровень выше по условию и просчитать линков с данного родителя
            if (OnLinkedMatchNeedMergeLinks(currentEntityType, linkType))
            {
                Key[] linkedEntityLinks = link.Meta.Links;

                foreach (Key mergedEntityKey in linkedEntityLinks)
                {
                    byte mergingLinkType = mergedEntityKey.Type;

                    if (matchedTypes[mergingLinkType]
                        || !context.TryGetSearchedEntity(mergedEntityKey, out EntitySearchResult? mergingLink)
                        || mergingLink.Matches.Count == 0)
                        continue;

                    yield return (mergedEntityKey, mergingLink.Matches);
                    matchedTypes[mergingLinkType] = true;
                }
            }
        }
    }

    /// <summary>
    /// Заполняет матчи для слов из запроса <paramref name="queryWordsMatches"/> из совпадений для сущности <paramref name="matches"/>
    /// </summary>
    /// <param name="queryWordsMatches">Совпадения со словами из запроса</param>
    /// <param name="type">Тип сущности матчей</param>
    /// <param name="matches">Матчи слов с сущностью</param>
    /// <param name="context">Контекст поиска</param>
    /// <param name="nodeMultiplier">Мультиплер</param>
    private void ProcessNodeMatches(in Span<TokenMatch> queryWordsMatches, byte type, List<TokenMatch> matches, TContext context, double nodeMultiplier)
    {
        //Сначала выбираем матчи по сущности пытаемся собрать совпадения для слов из запроса
        Span<TokenMatch> nodeScores = stackalloc TokenMatch[queryWordsMatches.Length];
        for (int i = 0; i < matches.Count; i++)
        {
            TokenMatch compareResult = matches[i];
            int queryWordPosition = GetCurrentQueryWordPosition(in nodeScores, compareResult.SearchTokensBundlePosition);
            TokenMatch previouslyCalculatedResult = nodeScores[queryWordPosition];

            bool isNewQueryPosition = false;
            if (PreviouslyMatchedWordToNewPositionName(compareResult, previouslyCalculatedResult))
            {
                isNewQueryPosition = true;
                queryWordPosition = GetNewQueryWordPosition(in nodeScores, compareResult.SearchTokensBundlePosition);
            }

            if (queryWordPosition == -1) continue;

            double nameTypeMultiplier = GetNameMultiplierInternal(type, compareResult.NameType);

            byte score = (byte)Math.Round(compareResult.Score * nameTypeMultiplier * nodeMultiplier);

            if (isNewQueryPosition || previouslyCalculatedResult.Score < score)
                nodeScores[queryWordPosition] = new(compareResult.NameTokenPosition, compareResult.NameType, compareResult.SearchTokensBundlePosition, score);
        }

        //Мерж между линками
        for (int i = 0; i < queryWordsMatches.Length; i++)
        {
            TokenMatch nodeMatch = nodeScores[i];
            TokenMatch previouslyCalculatedResult = queryWordsMatches[i];

            if (nodeMatch.IsEmpty) continue;
            if (!previouslyCalculatedResult.IsEmpty)
            {
                int nextQueryWordPosition = GetNewQueryWordPosition(queryWordsMatches, nodeMatch.SearchTokensBundlePosition);
                if (nextQueryWordPosition != -1) queryWordsMatches[nextQueryWordPosition] = nodeMatch;
            }
            else if (previouslyCalculatedResult.Score < nodeMatch.Score)
            {
                queryWordsMatches[i] = nodeMatch;
            }
        }

        int GetNewQueryWordPosition(in Span<TokenMatch> scores, int wordsBundlePosition)
        {
            foreach (int position in context.SearchTokensBundle[wordsBundlePosition].PositionsInRequest)
            {
                if (scores[position].IsEmpty) return position;
            }

            return -1;
        }

        int GetCurrentQueryWordPosition(in Span<TokenMatch> scores, int wordsBundlePosition)
        {
            int previousNotEmpty = -1;

            int[] positions = context.SearchTokensBundle[wordsBundlePosition].PositionsInRequest;

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

        static bool PreviouslyMatchedWordToNewPositionName(TokenMatch compareResult, TokenMatch previouslyCalculatedResult)
            => !previouslyCalculatedResult.IsEmpty
                && previouslyCalculatedResult.NameType == compareResult.NameType
                && previouslyCalculatedResult.NameTokenPosition != compareResult.NameTokenPosition
                && previouslyCalculatedResult.SearchTokensBundlePosition == compareResult.SearchTokensBundlePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal double GetNameMultiplierInternal(byte entityType, byte nameType)
    {
        if (nameType == 0)
            return 1;

        return GetNameTypeMultiplier(entityType, nameType);
    }
}
