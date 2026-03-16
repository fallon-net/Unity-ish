using System;

namespace UnityIsh.Core.State
{
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        ConnectedGood,
        ConnectedWarn,
        ConnectedBad,
        Reconnecting
    }

    public sealed class ConnectionStateMachine
    {
        public ConnectionState Current { get; private set; } = ConnectionState.Disconnected;
        public event Action<ConnectionState> OnChanged;

        public void Transition(ConnectionState next)
        {
            if (Current == next)
            {
                return;
            }

            Current = next;
            OnChanged?.Invoke(Current);
        }
    }
}
