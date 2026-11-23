using QuerySeek.Services.Searching.Scoring;
using QuerySeek.Services.Searching.Selection;

namespace QuerySeek.Services.Searching.PipelineSteps;

public class SearchStep<TContext>(
    Func<TContext, IEnumerable<EntitiesSelectionOperation>> requestFactory, 
    IScoreCalculator<TContext> scoreCalculator)
    : IPipelineStep<TContext>
    where TContext : SearchContextBase
{
    public void Execute(TContext context)
    {
        foreach (EntitiesSelectionOperation request in requestFactory(context))
        {
            request.Process(context);

            foreach (EntitySearchResult item in context.GetResults(request.TargetType))
                scoreCalculator.CalculateTextScore(context, item);
        }
    }
}
