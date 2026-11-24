using System;
using System.Collections.Generic;

namespace Shaolin36.Core.Data
{
    /// <summary>CSV helpers shared by every sheet reader.</summary>
    internal static class CsvText
    {
        internal static bool IsBlankOrComment(string line)
        {
            var t = line.Trim();
            return t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal);
        }

        internal static List<string> SplitLines(string text)
        {
            if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1); // Excel's UTF-8 BOM
            return new List<string>(text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
        }

        /// <summary>Splits one CSV row. Supports "quoted, fields" and "" escapes.</summary>
        internal static List<string> SplitRow(string line)
        {
            var cells = new List<string>();
            var cur = new System.Text.StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (quoted)
                {
                    if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                    else if (ch == '"') quoted = false;
                    else cur.Append(ch);
                }
                else if (ch == '"') quoted = true;
                else if (ch == ',') { cells.Add(cur.ToString()); cur.Clear(); }
                else cur.Append(ch);
            }
            cells.Add(cur.ToString());
            return cells;
        }
    }
}
