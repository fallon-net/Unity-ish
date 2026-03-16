using System;
using System.Collections;
using UnityEngine;

namespace UnityIsh.Core
{
    /// <summary>
    /// Manages exponential reconnect backoff.
    /// Attach to the same GameObject as IntercomController.
    /// </summary>
    public sealed class ReconnectService : MonoBehaviour
    {
        // Delays in seconds: 1, 2, 3, 5, 5, 5, ...
        private static readonly int[] BackoffSeconds = { 1, 2, 3, 5, 5 };

        private int _attempt;

        public void Reset() => _attempt = 0;

        /// <summary>
        /// Coroutine: yields for the next backoff interval then returns.
        /// </summary>
        public IEnumerator Wait()
        {
            int delay = BackoffSeconds[Math.Min(_attempt, BackoffSeconds.Length - 1)];
            _attempt++;
            Debug.Log($"[ReconnectService] Attempt {_attempt}, waiting {delay}s...");
            yield return new WaitForSeconds(delay);
        }
    }
}
