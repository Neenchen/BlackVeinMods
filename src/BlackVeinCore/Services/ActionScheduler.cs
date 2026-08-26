using System;
using System.Collections.Generic;
using System.Threading;
using HarmonyLib;
using ProjectM;

namespace BlackVeinCore.Services;

[HarmonyPatch(typeof(VivoxConnectionSystem), nameof(VivoxConnectionSystem.OnUpdate))]
public static class ActionScheduler
{
    private static readonly Queue<Action> MainThreadActions = new();
    private static readonly List<ScheduledAction> ScheduledActions = new();
    private static readonly HashSet<Timer> ActiveTimers = new();
    private static readonly object ScheduledActionsLock = new();

    public static int CurrentFrameCount { get; private set; }

    public static void Postfix()
    {
        CurrentFrameCount++;

        List<Action>? dueActions = null;
        lock (ScheduledActionsLock)
        {
            for (int index = ScheduledActions.Count - 1; index >= 0; index--)
            {
                var scheduledAction = ScheduledActions[index];
                if (scheduledAction.TargetFrame > CurrentFrameCount)
                {
                    continue;
                }

                dueActions ??= new List<Action>();
                dueActions.Add(scheduledAction.Action);
                ScheduledActions.RemoveAt(index);
            }

            while (MainThreadActions.Count > 0)
            {
                dueActions ??= new List<Action>();
                dueActions.Add(MainThreadActions.Dequeue());
            }
        }

        if (dueActions == null)
        {
            return;
        }

        foreach (var action in dueActions)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[{Plugin.PluginName}] Scheduled action failed: {ex}");
            }
        }
    }

    public static void RunActionOnceAfterFrames(Action action, int frameDelay)
    {
        lock (ScheduledActionsLock)
        {
            ScheduledActions.Add(new ScheduledAction(
                CurrentFrameCount + Math.Max(0, frameDelay),
                action));
        }
    }

    public static Timer RunActionOnceAfterDelay(Action action, double delayInSeconds)
    {
        Timer? timer = null;
        timer = new Timer(_ =>
        {
            RunActionOnMainThread(() =>
            {
                try
                {
                    action();
                }
                finally
                {
                    timer?.Dispose();
                    if (timer != null)
                    {
                        lock (ScheduledActionsLock)
                        {
                            ActiveTimers.Remove(timer);
                        }
                    }
                }
            });
        }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        lock (ScheduledActionsLock)
        {
            ActiveTimers.Add(timer);
        }

        timer.Change(TimeSpan.FromSeconds(Math.Max(0, delayInSeconds)), Timeout.InfiniteTimeSpan);

        return timer;
    }

    public static void RunActionOnMainThread(Action action)
    {
        lock (ScheduledActionsLock)
        {
            MainThreadActions.Enqueue(action);
        }
    }

    public static void Clear()
    {
        lock (ScheduledActionsLock)
        {
            MainThreadActions.Clear();
            ScheduledActions.Clear();
            foreach (var timer in ActiveTimers)
            {
                timer.Dispose();
            }

            ActiveTimers.Clear();
            CurrentFrameCount = 0;
        }
    }

    private readonly record struct ScheduledAction(int TargetFrame, Action Action);
}
