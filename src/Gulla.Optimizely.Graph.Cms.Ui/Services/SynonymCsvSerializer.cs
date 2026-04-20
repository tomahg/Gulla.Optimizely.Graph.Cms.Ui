using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Gulla.Optimizely.Graph.Cms.Ui.Models;

namespace Gulla.Optimizely.Graph.Cms.Ui.Services
{
    /// <summary>
    /// Round-trips between Optimizely Graph's plain-text synonym body, the CMS 12 Find CSV export
    /// (<c>phrase,bidirectional,synonym</c>), and the in-memory <see cref="SynonymEntry"/> model.
    /// </summary>
    public class SynonymCsvSerializer
    {
        private const string CsvHeader = "phrase,bidirectional,synonym";

        public IList<SynonymEntry> ParseGraphBody(string body)
        {
            var entries = new List<SynonymEntry>();
            if (string.IsNullOrWhiteSpace(body))
            {
                return entries;
            }

            foreach (var rawLine in body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                {
                    continue;
                }

                if (line.Contains("=>"))
                {
                    var parts = line.Split(new[] { "=>" }, 2, StringSplitOptions.None);
                    var phrases = SplitPhrases(parts[0]);
                    var synonym = parts[1].Trim();
                    if (phrases.Count == 0 || synonym.Length == 0)
                    {
                        continue;
                    }

                    entries.Add(new SynonymEntry
                    {
                        Phrases = phrases,
                        Synonym = synonym,
                        Bidirectional = false
                    });
                }
                else
                {
                    var parts = SplitPhrases(line);
                    if (parts.Count < 2)
                    {
                        continue;
                    }

                    var synonym = parts.Last();
                    var phrases = parts.Take(parts.Count - 1).ToList();
                    entries.Add(new SynonymEntry
                    {
                        Phrases = phrases,
                        Synonym = synonym,
                        Bidirectional = true
                    });
                }
            }

            return entries;
        }

        public string ToGraphBody(IEnumerable<SynonymEntry> entries)
        {
            var lines = new List<string>();
            foreach (var entry in entries ?? Enumerable.Empty<SynonymEntry>())
            {
                if (entry.Phrases == null || entry.Phrases.Count == 0 || string.IsNullOrWhiteSpace(entry.Synonym))
                {
                    continue;
                }

                var phraseList = string.Join(", ", entry.Phrases.Select(p => p.Trim()));
                lines.Add(entry.Bidirectional
                    ? $"{phraseList}, {entry.Synonym.Trim()}"
                    : $"{phraseList} => {entry.Synonym.Trim()}");
            }
            return string.Join("\n", lines);
        }

        public IList<SynonymEntry> ParseCsv(Stream csvStream)
        {
            var entries = new List<SynonymEntry>();
            using var reader = new StreamReader(csvStream, Encoding.UTF8);
            string line;
            var firstLine = true;

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (firstLine)
                {
                    firstLine = false;
                    if (line.Replace(" ", string.Empty).Equals(CsvHeader, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                var fields = ParseCsvLine(line);
                if (fields.Count < 3)
                {
                    continue;
                }

                var phrases = SplitPhrases(fields[0]);
                var bidirectional = string.Equals(fields[1].Trim(), "true", StringComparison.OrdinalIgnoreCase);
                var synonym = fields[2].Trim();

                if (phrases.Count == 0 || synonym.Length == 0)
                {
                    continue;
                }

                entries.Add(new SynonymEntry
                {
                    Phrases = phrases,
                    Synonym = synonym,
                    Bidirectional = bidirectional
                });
            }

            return entries;
        }

        public string ToCsv(IEnumerable<SynonymEntry> entries)
        {
            var builder = new StringBuilder();
            builder.AppendLine(CsvHeader);
            foreach (var entry in entries ?? Enumerable.Empty<SynonymEntry>())
            {
                if (entry.Phrases == null || entry.Phrases.Count == 0 || string.IsNullOrWhiteSpace(entry.Synonym))
                {
                    continue;
                }

                var phraseField = QuoteIfNeeded(string.Join(", ", entry.Phrases.Select(p => p.Trim())));
                var synonymField = QuoteIfNeeded(entry.Synonym.Trim());
                builder.AppendLine($"{phraseField},{(entry.Bidirectional ? "true" : "false")},{synonymField}");
            }
            return builder.ToString();
        }

        private static List<string> SplitPhrases(string value)
        {
            return value
                .Split(',')
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToList();
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result;
        }

        private static string QuoteIfNeeded(string value)
        {
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }
    }
}
