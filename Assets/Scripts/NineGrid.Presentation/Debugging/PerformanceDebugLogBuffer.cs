using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Presentation.Debugging
{
    public sealed class PerformanceDebugLogBuffer
    {
        private readonly List<string> lines = new();
        private const int MaxLines = 120;

        public IReadOnlyList<string> Lines => lines;

        public void Info(string message)
        {
            Append("INFO", message);
        }

        public void Warn(string message)
        {
            Append("WARN", message);
        }

        public void Error(string message)
        {
            Append("ERR", message);
        }

        public void Clear()
        {
            lines.Clear();
        }

        private void Append(string level, string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {level} {message}";
            lines.Add(line);
            if (lines.Count > MaxLines)
            {
                lines.RemoveAt(0);
            }

            Debug.Log($"[PerformanceDebug] {line}");
        }
    }
}
