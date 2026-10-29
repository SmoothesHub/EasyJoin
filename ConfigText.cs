using System;
using System.Collections.Generic;

namespace EasyJoin
{
    internal static class ConfigText
    {
        internal static string Set(string text, string section, string key, string value)
        {
            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            var lines = new List<string>(text.Replace("\r\n", "\n").Split(new[] { '\n' }));
            bool inside = false, found = false;
            int insert = -1;
            for (int i = 0; i < lines.Count; ++i)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    inside = line == "[" + section + "]";
                    if (inside) insert = i + 1;
                    continue;
                }
                if (!inside) continue;
                insert = i + 1;
                int equal = line.IndexOf('=');
                if (equal < 0 || line.Substring(0, equal).Trim() != key) continue;
                lines[i] = key + " = " + value;
                found = true; // Replace duplicates too: the parser's final value must agree.
            }
            if (!found)
            {
                if (insert < 0)
                {
                    lines.Add("[" + section + "]");
                    lines.Add(key + " = " + value);
                }
                else lines.Insert(insert, key + " = " + value);
            }
            return String.Join(newline, lines);
        }
    }
}
