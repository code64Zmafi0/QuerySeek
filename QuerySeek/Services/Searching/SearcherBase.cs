using System.Runtime.CompilerServices;
using QuerySeek.Models;
using QuerySeek.Services.Helpers;
using QuerySeek.Services.Normalizing;
using QuerySeek.Services.Searching.Requests;

namespace QuerySeek.Services.Searching;

/// <summary>
/// Позволяет определить стратегию поиска, что-то вроде фабричного метода
/// </summary>
/// <typeparam name="TContext">Тип конеткса поиска</typeparam>
/// <param name="nameTokenizer">Токенайзер</param>
/// <param name="normalizer">Нормалайзер</param>
public abstract class SearcherBase<TContext>(INormalizer normalizer, INameTokenizer nameTokenizer) where TContext : SearchContextBase
{
    #region Overrides
    /// <summary>
    /// (0. WORDS SEARCH) Определение настроек поиска схожих слов для слов из запроса
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    public virtual WordsSearchSettings GetWordsSearchSettings(TContext context)
        => WordsSearchSettings.Default;

    /// <summary>
    /// (0. WORDS SEARCH) Определение возможных альтернатив для слов из запроса
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    public virtual Dictionary<string, string[]> GetQueryWordsAlternatives(TContext context)
        => [];

    /// <summary>
    /// (0. WORDS SEARCH) Определение моножителей для слов из запроса (позволяет уменьшать значимость предлогов и тд)
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    public virtual Dictionary<string, double> GetQueryWordsMultiplers(TContext context)
        => [];

    /// <summary>
    /// (1. SELECTION) Определяет запрос на поиск сущностей в индексе.
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    /// <returns>Перечисление запросов вниз по дереву зависимостей</returns>
    public abstract IEnumerable<RequestBase> GetRequest(TContext context);

    /// <summary>
    /// (2. PREPROCESSING) Позволяет осуществить фильтрацию выборки по типам перед сортировкой или обогатить правилами
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    /// <param name="type">Текущий тип сущностей</param>
    /// <param name="result">Выборка сущностей данного типа</param>
    public virtual IEnumerable<EntitySearchResult> TypeResultPreprocessing(TContext context, byte type, ICollection<EntitySearchResult> result)
        => result;

    /// <summary>
    /// (3. RANGING) Определяет сортировку
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    /// <param name="result">Выборка сущностей для сортировки</param>
    public virtual IEnumerable<EntitySearchResult> Ranging(TContext context, IEnumerable<EntitySearchResult> result)
        => result.OrderByDescending(i => i.ScoreWithRules);

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
    public virtual double GetLinkedEntityMatchMultipler(byte entityType, byte linkedType)
        => 1;

    /// <summary>
    /// (SCORING SETTINGS) Множитель типа имени для сущностей
    /// </summary>
    /// <param name="entityType">Тип сущности</param>
    /// <param name="nameType">Тип имени</param>
    public virtual double GetNameTypeMultipler(byte entityType, byte nameType)
        => 1;

    /// <summary>
    /// (EVENT) Событие заполнения в контексте набора поисковых токенов. После (0. WORDS SEARCH)
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    public virtual void OnContextReady(TContext context) { }

    /// <summary>
    /// (EVENT) Событие просчета совпадений для сущности. После (SCORING)
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    /// <param name="entity">Обработанная сущность</param>
    /// <param name="queryWordsMatches">Совпадения для слов из запроса</param>
    public virtual void OnEntityMatched(TContext context, EntitySearchResult entity, in Span<WordMatch> queryWordsMatches) { }

    /// <summary>
    /// (EVENT) Событие после выборки (1. SELECTION)
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    public virtual void OnRequestsProcessed(TContext context) { }
    #endregion

    #region Search logic
    /// <summary>
    /// Поиск топа всех типов
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    /// <param name="take">Количество элементов</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Отранжированный результат из всех типов</returns>
    public EntitySearchResult[] Search(
        TContext context,
        int take,
        CancellationToken? cancellationToken = null)
    {
        CancellationToken ct = cancellationToken ?? CancellationToken.None;

        FillContext(context);
        ProcessRequests(context, ct);

        return [.. Ranging(context, GetAllResults()).Take(take)];

        IEnumerable<EntitySearchResult> GetAllResults()
        {
            foreach (KeyValuePair<byte, Dictionary<Key, EntitySearchResult>> typeResults in context.SearchResult)
            {
                foreach (EntitySearchResult item in TypeResultPreprocessing(context, typeResults.Key, typeResults.Value.Values))
                    yield return item;
            }
        }
    }

    /// <summary>
    /// Поиск топов по типам
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    /// <param name="take">Количество элементов каждого типа</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Блоки отранжированных результатов по типам</returns>
    public TypeSearchResult[] SearchTypes(
        TContext context,
        int take,
        CancellationToken? cancellationToken = null)
    {
        CancellationToken ct = cancellationToken ?? CancellationToken.None;

        FillContext(context);
        ProcessRequests(context, ct);

        TypeSearchResult[] result = [.. context.SearchResult.Select(typeSearchResult =>
        {
            byte currentType = typeSearchResult.Key;

            IEnumerable<EntitySearchResult> preprocessed = TypeResultPreprocessing(context, currentType, typeSearchResult.Value.Values);

            EntitySearchResult[] typeResult = [.. Ranging(context, preprocessed).Take(take)];

            return new TypeSearchResult(currentType, typeResult);
        }).Where(i => i.Result.Length != 0)];

        return result;
    }

    public void FillContext(TContext context)
    {
        context.SplittedAndNormalizedQuery = TextPreprocessor.PreprocessText(nameTokenizer, normalizer, context.Query);

        Dictionary<string, string[]> alternativeWords = GetQueryWordsAlternatives(context);
        Dictionary<string, double> queryWordMultiplers = GetQueryWordsMultiplers(context);

        context.WordsSearchSettings = GetWordsSearchSettings(context);
        context.SearchWordsBundle = NgrammsWordsSearchHelper.CreateSearchWordsBundle(context, alternativeWords, queryWordMultiplers);

        OnContextReady(context);
    }

    public void ProcessRequests(TContext context, CancellationToken ct)
    {
        foreach (RequestBase request in GetRequest(context))
        {
            request.ProcessRequest(context, ct);

            if (!context.TryGetResultsByType(request.TargetType, out Dictionary<Key, EntitySearchResult>? result))
                continue;

            foreach (EntitySearchResult item in result.Values)
                CalculateTextScore(context, item);
        }

        OnRequestsProcessed(context);
    }

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
            double nodeMultipler = GetLinkedEntityMatchMultipler(currentEntityType, Type);
            if (nodeMultipler != 0)
            {
                ProcessNodeMatches(in queryWordsMatches, Type, Matches, context, nodeMultipler);
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
    /// <param name="nodeMultipler">Мультиплер</param>
    private void ProcessNodeMatches(in Span<WordMatch> queryWordsMatches, byte type, List<WordMatch> matches, TContext context, double nodeMultipler)
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

            double nameTypeMultipler = GetNameMultiplerInternal(type, compareResult.NameType);

            byte score = (byte)Math.Ceiling(compareResult.Score * nameTypeMultipler * nodeMultipler);

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

            foreach (int position in context.SearchWordsBundle[wordsBundlePosition].PositionsInRequest)
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
    internal double GetNameMultiplerInternal(byte entityType, byte nameType)
    {
        if (nameType == 0)
            return 1;

        return GetNameTypeMultipler(entityType, nameType);
    }
    #endregion

}
