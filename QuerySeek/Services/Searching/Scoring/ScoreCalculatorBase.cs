using System.Runtime.CompilerServices;
using QuerySeek.Models;
using QuerySeek.Services.Helpers;

namespace QuerySeek.Services.Searching.Scoring;

/// <summary>
/// Стандартная реализация просчета текстового скора для сущностей
/// </summary>
/// <typeparam name="TContext">Тип конеткста</typeparam>
public class ScoreCalculatorBase<TContext> : IScoreCalculator<TContext> where TContext : SearchContextBase
{
    public static readonly ScoreCalculatorBase<TContext> Intance = new();

    #region Overrides
    /// <summary>
    /// (SCORING SETTINGS) Позволяет при совпадении линка, добавить просчет его звасимостей (зайти в иерархию привязанной)
    /// </summary>
    /// <param name="entityType">Тип обрабатываемой сущности</param>
    /// <param name="linkedType">Тип привязанной сущности</param>
    public virtual bool OnLinkedMatchNeedMergeLinks(byte entityType, byte linkedType)
        => false;

    /// <summary>
    /// (SCORING SETTINGS) Множитель совпадений из связанных сущностей
    /// </summary>
    /// <param name="entityType">Тип обрабатываемой сущности</param>
    /// <param name="linkedType">Тип привязанной сущности</param>
    public virtual double GetLinkedEntityMatchMultiplier(byte entityType, byte linkedType)
        => 1;

    /// <summary>
    /// (SCORING SETTINGS) Множитель типа имени для сущностей
    /// </summary>
    /// <param name="entityType">Тип сущности</param>
    /// <param name="nameType">Тип имени</param>
    public virtual double GetNameTypeMultiplier(byte entityType, byte nameType)
        => 1;

    /// <summary>
    /// (EVENT) Событие просчета совпадений для сущности. После (SCORING)
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    /// <param name="entity">Обработанная сущность</param>
    /// <param name="queryWordsMatches">Совпадения для слов из запроса</param>
    public virtual void OnEntityMatched(TContext context, EntitySearchResult entity, in Span<WordMatch> queryWordsMatches) { }
    #endregion

    /// <summary>
    /// Просчитываем скор текстовых совпадний для сущности
    /// </summary>
    public void CalculateTextScore(TContext context, EntitySearchResult entitySearchResult)
    {
        if (entitySearchResult.Score != 0) return;

        byte currentEntityType = entitySearchResult.Key.Type;

        Span<WordMatch> queryWordsMatches = stackalloc WordMatch[context.SplittedAndNormalizedQuery.Length];

        //Просчитываем совпадения для слов запроса из сущнности и ее линков, заполняем queryWordsMatches
        foreach ((byte Type, List<WordMatch> Matches) in GetMatches(context, entitySearchResult))
        {
            double nodeMultiplier = GetLinkedEntityMatchMultiplier(currentEntityType, Type);
            if (nodeMultiplier != 0)
            {
                ProcessNodeMatches(in queryWordsMatches, Type, Matches, context, nodeMultiplier);
            }
        }

        //Складываем скор совпадений слов
        int resultScore = 0;
        foreach (WordMatch ws in queryWordsMatches)
            resultScore += ws.Score;

        entitySearchResult.Score = resultScore;

        OnEntityMatched(context, entitySearchResult, in queryWordsMatches);
    }

    /// <summary>
    /// Возвращает наборы совпавших слов для сущности и прилинкованных к ней
    /// </summary>
    private IEnumerable<(byte Type, List<WordMatch> Matches)> GetMatches(
        TContext context,
        EntitySearchResult entitySearchResult)
    {
        byte currentEntityType = entitySearchResult.Key.Type;

        //Сущность может быть привязана к нескольким сущностям одного типа.
        //Если линк с таким типом уже был просчитан - пропускаем просчеты для сущностей этого типа
        BitArray256 matchedTypes = new();

        yield return (currentEntityType, entitySearchResult.WordsMatches);
        matchedTypes[currentEntityType] = true;

        foreach (Key linkKey in entitySearchResult.Meta.Links)
        {
            byte linkType = linkKey.Type;

            if (matchedTypes[linkType]
                || !context.TryGetSearchedEntity(linkKey, out EntitySearchResult? link)
                || link.WordsMatches.Count == 0)
                continue;

            yield return (linkType, link.WordsMatches);
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
                        || mergingLink.WordsMatches.Count == 0)
                        continue;

                    yield return (mergingLinkType, mergingLink.WordsMatches);
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
    private void ProcessNodeMatches(in Span<WordMatch> queryWordsMatches, byte type, List<WordMatch> matches, TContext context, double nodeMultiplier)
    {
        //Сначала выбираем матчи по сущности пытаемся собрать совпадения для слов из запроса
        Span<WordMatch> nodeScores = stackalloc WordMatch[queryWordsMatches.Length];
        for (int i = 0; i < matches.Count; i++)
        {
            WordMatch compareResult = matches[i];
            int queryWordPosition = GetCurrentQueryWordPosition(in nodeScores, compareResult.WordsBundlePosition);
            WordMatch previouslyCalculatedResult = nodeScores[queryWordPosition];

            bool isNewQueryPosition = false;
            if (PreviouslyMatchedWordToNewPositionName(compareResult, previouslyCalculatedResult))
            {
                isNewQueryPosition = true;
                queryWordPosition = GetNewQueryWordPosition(in nodeScores, compareResult.WordsBundlePosition);
            }

            if (queryWordPosition == -1) continue;

            double nameTypeMultiplier = GetNameMultiplierInternal(type, compareResult.NameType);

            byte score = (byte)Math.Ceiling(compareResult.Score * nameTypeMultiplier * nodeMultiplier);

            if (isNewQueryPosition || previouslyCalculatedResult.Score < score)
                nodeScores[queryWordPosition] = new(compareResult.NameWordPosition, compareResult.NameType, compareResult.WordsBundlePosition, score);
        }

        //Мерж между линками
        for (int i = 0; i < queryWordsMatches.Length; i++)
        {
            WordMatch nodeMatch = nodeScores[i];
            WordMatch previouslyCalculatedResult = queryWordsMatches[i];

            if (nodeMatch.IsEmpty) continue;
            if (!previouslyCalculatedResult.IsEmpty)
            {
                int nextQueryWordPosition = GetNewQueryWordPosition(queryWordsMatches, nodeMatch.WordsBundlePosition);
                if (nextQueryWordPosition != -1) queryWordsMatches[nextQueryWordPosition] = nodeMatch;
            }
            else if (previouslyCalculatedResult.Score < nodeMatch.Score)
            {
                queryWordsMatches[i] = nodeMatch;
            }
        }

        int GetNewQueryWordPosition(in Span<WordMatch> scores, int wordsBundlePosition)
        {
            foreach (int position in context.SearchWordsBundle[wordsBundlePosition].PositionsInRequest)
            {
                if (scores[position].IsEmpty) return position;
            }

            return -1;
        }

        int GetCurrentQueryWordPosition(in Span<WordMatch> scores, int wordsBundlePosition)
        {
            int previousNotEmpty = -1;

            int[] positions = context.SearchWordsBundle[wordsBundlePosition].PositionsInRequest;

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

        static bool PreviouslyMatchedWordToNewPositionName(WordMatch compareResult, WordMatch previouslyCalculatedResult)
            => !previouslyCalculatedResult.IsEmpty
                && previouslyCalculatedResult.NameType == compareResult.NameType
                && previouslyCalculatedResult.NameWordPosition != compareResult.NameWordPosition
                && previouslyCalculatedResult.WordsBundlePosition == compareResult.WordsBundlePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal double GetNameMultiplierInternal(byte entityType, byte nameType)
    {
        if (nameType == 0)
            return 1;

        return GetNameTypeMultiplier(entityType, nameType);
    }
}
