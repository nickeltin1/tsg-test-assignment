using System;
using System.Diagnostics;

namespace Game.Scripts
{
    /// <summary>
    /// Stopwatch wrapper to record and log time
    /// </summary>
    public readonly struct StopwatchScope : IDisposable
    {
        private readonly Stopwatch _stopwatch;
        private readonly string _name;
        
        public StopwatchScope(string name)
        {
            _name = name;
            _stopwatch = Stopwatch.StartNew();
            UnityEngine.Debug.Log($"'{name}' started");
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            UnityEngine.Debug.Log($"'{_name}' ended in {_stopwatch.ElapsedMilliseconds} ms");
        }
    }
}