// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// The production of one program on demand (<see cref="OnDemandPrograms"/>): when it was first asked for, the queue it
/// waits in and its outcome. Prefetched work a command list then waits for is queued again as urgent; whichever entry a
/// worker takes first runs it.
/// </summary>
internal sealed class ProgramJob
{
    private const int NotQueued = 0;
    private const int Prefetch = 1;
    private const int Urgent = 2;

    private readonly TaskCompletionSource<bool> _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _queued;
    private int _started;

    public ProgramJob(string name, long requested)
    {
        Name = name;
        Requested = requested;
    }

    public string Name { get; }

    /// <summary>The timestamp of the first request.</summary>
    public long Requested { get; }

    /// <summary>Ends true when the program's DXIL is ready, false when it cannot be made.</summary>
    public Task<bool> Done => _done.Task;

    /// <summary>Whether a request needs a queue entry: the first one, or the first urgent one while only prefetched.</summary>
    public bool Queue(bool urgent)
    {
        var level = Prefetch;

        if (urgent)
        {
            level = Urgent;
        }

        while (true)
        {
            var current = Volatile.Read(ref _queued);

            if (current >= level || Volatile.Read(ref _started) != 0)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _queued, level, current) == current)
            {
                return true;
            }
        }
    }

    /// <summary>Whether the calling worker is the one that runs the job.</summary>
    public bool Start()
    {
        return Interlocked.Exchange(ref _started, 1) == 0;
    }

    public void Finish(bool ready)
    {
        _done.TrySetResult(ready);
    }
}
