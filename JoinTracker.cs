using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EasyJoin
{
    /// <summary>Tracks pending native preparation tasks; netState guards the actual connection.</summary>
    internal sealed class JoinTracker
    {
        private readonly List<Task> pending = new List<Task>(1);
        internal bool IsBusy
        {
            get
            {
                lock (pending)
                {
                    RemoveCompleted();
                    return pending.Count != 0;
                }
            }
        }

        internal void Track(Task task)
        {
            if (task == null || task.IsCompleted) return;
            lock (pending)
            {
                RemoveCompleted();
                if (!pending.Contains(task)) pending.Add(task);
            }
        }

        // Evaluated only on an input/request event, never on a timer or Unity Update.
        // IsCompleted covers success, fault and cancellation without relying on a
        // continuation being scheduled before the next OPTION 3 press.
        private void RemoveCompleted()
        {
            for (int i = pending.Count - 1; i >= 0; --i)
                if (pending[i].IsCompleted) pending.RemoveAt(i);
        }
    }
}
