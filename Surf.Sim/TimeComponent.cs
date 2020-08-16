using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Surf.Core;
using System.Diagnostics.CodeAnalysis;

namespace Surf.Sim
{
    public class TimeComponent : ITimeProvider
    {
        /// <summary>
        /// Custom comparer to allow multiple entries with the 
        /// same key in a sorted list.
        /// </summary>
        private class Cmp : IComparer<long>
        {
            public int Compare([AllowNull] long x, [AllowNull] long y)
            {
                return x <= y ? -1 : 1;
            }
        }

        private static readonly List<TimeComponent> s_all = new List<TimeComponent>();
        private static readonly AsyncReaderWriterLock s_allLock = new AsyncReaderWriterLock();

        public static async Task<TimeComponent> NewComponent()
        {
            var newComponent = new TimeComponent();
            using (await s_allLock.WriterLockAsync())
            {
                s_all.Add(newComponent);
            }
            return newComponent;
        }

        public static async Task IterateAll(int milliseconds, CancellationToken token)
        {
            for (var i = 0; ; i++)
            {
                TimeComponent next;
                using (await s_allLock.ReaderLockAsync())
                {
                    if (i >= s_all.Count)
                    {
                        break;
                    }
                    next = s_all[i];
                }
                await next.ExecuteNextMilliseconds(milliseconds, token);
            }
        }

        private readonly SortedList<long, Func<CancellationToken, Task>> _queue = new SortedList<long, Func<CancellationToken, Task>>(new Cmp());
        private readonly AsyncReaderWriterLock _rw = new AsyncReaderWriterLock();

        private readonly DateTime _baseTime = DateTime.UtcNow;
        private long _currentTime = 0;

        private TimeComponent() { }

        public long Diff(object now)
        {
            return _currentTime - (long)now;
        }

        public async Task ExecuteAfter(int milliseconds, CancellationToken ct, Func<CancellationToken, Task> action)
        {
            using (await _rw.WriterLockAsync())
            {
                _queue.Add(_currentTime + milliseconds, action);
            }
        }

        public async Task ExecuteAndWait(int milliseconds, CancellationToken ct, Func<CancellationToken, Task> action)
        {
            await action(ct);

            var s = new TaskCompletionSource<object>();

            using (await _rw.WriterLockAsync())
            {
                _queue.Add(_currentTime + milliseconds, (token) =>
                {
                    if (token.IsCancellationRequested)
                    {
                        s.SetCanceled();
                    }
                    else
                    {
                        s.TrySetResult(null);
                    }
                    return Task.CompletedTask;
                });
            }

            await s.Task;
        }

        public object NowForDiff()
        {
            return _currentTime;
        }

        public async Task ExecuteNextMilliseconds(long milliseconds, CancellationToken token)
        {
            long finalMs = 0;

            using (await _rw.ReaderLockAsync())
            {
                finalMs = _currentTime + milliseconds;
            }

            while (true)
            {
                using (await _rw.ReaderLockAsync())
                {
                    if (_queue.Count == 0 || _queue.Keys[0] > _currentTime + milliseconds)
                    {
                        break;
                    }
                }

                KeyValuePair<long, Func<CancellationToken, Task>> next;
                using (await _rw.WriterLockAsync())
                {
                    next = _queue.First();
                    _queue.RemoveAt(0);
                    _currentTime = next.Key;
                }

                await next.Value(token);
            }

            using (await _rw.WriterLockAsync())
            {
                _currentTime = finalMs;
            }
        }

        public async ValueTask<DateTime> UtcNow()
        {
            using (await _rw.ReaderLockAsync())
            {
                return _baseTime.AddMilliseconds(_currentTime);
            }
        }
    }
}
