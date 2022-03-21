using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VaporNetworking
{
    public class PeerLoggingExtension
    {
        private readonly bool isServerLog;
        private readonly List<string> log;
        private readonly StringBuilder sb;

        public PeerLoggingExtension(bool isServerLog)
        {
            this.isServerLog = isServerLog;
            log = new List<string>();
            sb = new StringBuilder();
        }

        public void Log(string format)
        {
            sb.Append($"({ServerTime.Time}) {format}");
            log.Add(sb.ToString());
            sb.Clear();
        }
        public void Log(string format, params object[] args)
        {
            sb.Append($"({ServerTime.Time}) {string.Format(format, args)}");
            log.Add(sb.ToString());
            sb.Clear();
        }

        public void WriteToFile()
        {
            System.IO.StringWriter w = new();
            for (int i = 0; i < log.Count; i++)
            {
                w.WriteLine(log[i]);
            }
            string path = Application.persistentDataPath;
            if (isServerLog)
            {
                path += "/ServerPlayerLog.txt";
            }
            else
            {
                path += "/ClientPlayerLog.txt";
            }
            System.IO.File.WriteAllText(path, w.ToString());
        }
    }
}