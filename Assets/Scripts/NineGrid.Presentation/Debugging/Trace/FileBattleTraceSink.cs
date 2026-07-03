using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Trace
{
    internal sealed class FileBattleTraceSink : IDisposable
    {
        private readonly StreamWriter mWriter;
        private readonly object mWriteLock = new();

        public FileBattleTraceSink(string jsonlPath)
        {
            JsonlPath = jsonlPath;
            string directory = Path.GetDirectoryName(jsonlPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            mWriter = new StreamWriter(jsonlPath, append: false, Encoding.UTF8)
            {
                AutoFlush = true,
            };
        }

        public string JsonlPath { get; }

        public string SummaryPath => Path.ChangeExtension(JsonlPath, null) + "_summary.md";

        public void AppendLine(string jsonLine)
        {
            if (string.IsNullOrEmpty(jsonLine))
            {
                return;
            }

            lock (mWriteLock)
            {
                mWriter.WriteLine(jsonLine);
            }
        }

        public void WriteSummary(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return;
            }

            File.WriteAllText(SummaryPath, markdown, Encoding.UTF8);
        }

        public void Dispose()
        {
            mWriter?.Dispose();
        }
    }
}
