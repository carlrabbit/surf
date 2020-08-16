
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Surf.Core
{
    /// <summary>
    /// Low level wrapper around time operations
    /// /// </summary>
    public interface ITimeProvider
    {
        ValueTask<DateTime> UtcNow();

        object NowForDiff();

        long Diff(object now);

        /// <summary>
        /// Waits for x milliseconds and executes action aftwerwards
        /// </summary>
        Task ExecuteAfter(int milliseconds, CancellationToken ct, Func<CancellationToken, Task> action);

        /// <summary>
        /// Executes the async action and waits for x milliseconds
        /// </summary>
        Task ExecuteAndWait(int milliseconds, CancellationToken ct, Func<CancellationToken, Task> action);
    }
}
