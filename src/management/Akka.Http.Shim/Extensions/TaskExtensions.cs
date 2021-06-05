namespace System.Threading.Tasks
{
    public static class TaskExtensions
    {
        public static Task<TResult> Map<TResult>(this Task source, Func<Task, TResult> selector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            return source.ContinueWith(selector, TaskContinuationOptions.NotOnCanceled);
        }        

        public static Task<TResult> Map<TSource, TResult>(this Task<TSource> source, Func<TSource, TResult> selector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            return source.ContinueWith(t => selector(t.Result), TaskContinuationOptions.NotOnCanceled);
        }

        public static Task WhenComplete<TSource>(this Task<TSource> source, Action<TSource, Exception> continuationAction) =>
            source.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    var exception = t.Exception?.InnerExceptions != null && t.Exception.InnerExceptions.Count == 1
                        ? t.Exception.InnerExceptions[0]
                        : t.Exception;

                    continuationAction(default, exception);
                }
                else
                {
                    continuationAction(t.Result, null);
                }
            }, TaskContinuationOptions.NotOnCanceled);
    }
}