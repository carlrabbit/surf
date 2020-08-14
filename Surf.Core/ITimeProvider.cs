
using System;
using System.Threading.Tasks;

namespace Surf.Core
{
    /// <summary>
    /// Low level wrapper around time operations
    /// /// </summary>
    public interface ITimeProvider
    {
        DateTime UtcNow();

        object NowForDiff();

        long Diff(object now);

        Task TaskDelay(int milliseconds);
    }
}
