namespace QuerySeek.Services.Searching.PipelineSteps;

internal class DelegateStep<TContext>(Action<TContext> action)
    : IPipelineStep<TContext> where TContext : SearchContextBase
{
    public void Execute(TContext context) => action(context);
}
