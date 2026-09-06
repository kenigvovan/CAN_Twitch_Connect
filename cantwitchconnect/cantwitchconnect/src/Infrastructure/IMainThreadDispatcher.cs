using System;
using Vintagestory.API.Server;

namespace cantwitchconnect.Infrastructure
{
    public interface IMainThreadDispatcher
    {
        void Enqueue(Action action);
    }

    public sealed class ServerMainThreadDispatcher : IMainThreadDispatcher
    {
        private readonly ICoreServerAPI sapi;
        private const string TaskCode = "cantwitchconnect";

        public ServerMainThreadDispatcher(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        public void Enqueue(Action action)
        {
            if (action == null) return;
            sapi.Event.EnqueueMainThreadTask(action, TaskCode);
        }
    }
}
