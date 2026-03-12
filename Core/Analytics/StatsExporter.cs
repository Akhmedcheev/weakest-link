using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WeakestLink.Core.Analytics
{
    /// <summary>
    /// Экспорт статистики игры в CSV и HTML (для печати в PDF через браузер)
    /// </summary>
    public class StatsExporter
    {
        private readonly GameEngine _engine;
        private readonly StatsAnalyzer _analyzer;

        public StatsExporter(GameEngine engine, StatsAnalyzer analyzer)
        {
            _engine = engine;
            _analyzer = analyzer;
        }

        /// <summary>
        /// Экспорт полной статистики в CSV
        /// </summary>
        public string ExportCSV(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("WEAKEST LINK — GAME REPORT");
            sb.AppendLine($"Date,{DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Total Bank,{_engine.TotalBank}");
            sb.AppendLine($"Rounds Played,{_engine.CurrentRound}");
            sb.AppendLine();

            // === PER-PLAYER CUMULATIVE STATS ===
            sb.AppendLine("PLAYER CUMULATIVE STATISTICS");
            sb.AppendLine("Player,Correct,Incorrect,Passes,Total Questions,Success %,Banked,Bank Presses,Avg Bank,Dropped Money");

            var allPlayers = _engine.PlayerStatistics.Keys.ToList();
            foreach (var player in allPlayers)
            {
                if (!_engine.PlayerStatistics.TryGetValue(player, out var rounds)) continue;

                int totalCorrect = rounds.Values.Sum(r => r.CorrectAnswers);
                int totalIncorrect = rounds.Values.Sum(r => r.IncorrectAnswers);
                int totalPasses = rounds.Values.Sum(r => r.Passes);
                int totalBanked = rounds.Values.Sum(r => r.BankedMoney);
                int totalBankPresses = rounds.Values.Sum(r => r.BankPressCount);
                int totalDropped = rounds.Values.Sum(r => r.ExactDroppedMoney);
                int totalQ = totalCorrect + totalIncorrect + totalPasses;
                double pct = totalQ > 0 ? Math.Round((double)totalCorrect / totalQ * 100, 1) : 0;
                double avgBank = totalBankPresses > 0 ? Math.Round((double)totalBanked / totalBankPresses, 0) : 0;

                sb.AppendLine($"{player},{totalCorrect},{totalIncorrect},{totalPasses},{totalQ},{pct}%,{totalBanked},{totalBankPresses},{avgBank},{totalDropped}");
            }

            sb.AppendLine();

            // === PER-ROUND BREAKDOWN ===
            sb.AppendLine("PER-ROUND BREAKDOWN");
            sb.AppendLine("Round,Player,Correct,Incorrect,Passes,Success %,Banked,Dropped");

            foreach (var player in allPlayers)
            {
                if (!_engine.PlayerStatistics.TryGetValue(player, out var rounds)) continue;
                foreach (var kv in rounds.OrderBy(r => r.Key))
                {
                    var r = kv.Value;
                    int tq = r.CorrectAnswers + r.IncorrectAnswers + r.Passes;
                    double pct = tq > 0 ? Math.Round((double)r.CorrectAnswers / tq * 100, 1) : 0;
                    sb.AppendLine($"{kv.Key},{player},{r.CorrectAnswers},{r.IncorrectAnswers},{r.Passes},{pct}%,{r.BankedMoney},{r.ExactDroppedMoney}");
                }
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            return filePath;
        }

        /// <summary>
        /// Экспорт в красивый HTML-отчёт (можно открыть в браузере → Ctrl+P → PDF)
        /// </summary>
        public string ExportHTML(string filePath)
        {
            var allPlayers = _engine.PlayerStatistics.Keys.ToList();

            // Build cumulative stats
            var cumulative = new List<(string Name, int Correct, int Incorrect, int Passes, int TotalQ, double Pct, int Banked, int Dropped, int Rounds)>();
            foreach (var player in allPlayers)
            {
                if (!_engine.PlayerStatistics.TryGetValue(player, out var rounds)) continue;
                int c = rounds.Values.Sum(r => r.CorrectAnswers);
                int i = rounds.Values.Sum(r => r.IncorrectAnswers);
                int p = rounds.Values.Sum(r => r.Passes);
                int b = rounds.Values.Sum(r => r.BankedMoney);
                int d = rounds.Values.Sum(r => r.ExactDroppedMoney);
                int tq = c + i + p;
                double pct = tq > 0 ? Math.Round((double)c / tq * 100, 1) : 0;
                cumulative.Add((player, c, i, p, tq, pct, b, d, rounds.Count));
            }

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang='en'><head><meta charset='UTF-8'>");
            sb.AppendLine("<title>Weakest Link — Game Report</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("* { margin: 0; padding: 0; box-sizing: border-box; }");
            sb.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; background: #0D1117; color: #E6EDF3; padding: 32px; }");
            sb.AppendLine("h1 { font-size: 28px; color: #58A6FF; margin-bottom: 4px; }");
            sb.AppendLine("h2 { font-size: 18px; color: #8B949E; margin: 24px 0 12px; text-transform: uppercase; letter-spacing: 1px; }");
            sb.AppendLine(".meta { color: #8B949E; font-size: 13px; margin-bottom: 20px; }");
            sb.AppendLine(".summary { display: flex; gap: 16px; margin-bottom: 24px; }");
            sb.AppendLine(".card { background: #161B22; border: 1px solid #30363D; border-radius: 10px; padding: 16px 20px; flex: 1; text-align: center; }");
            sb.AppendLine(".card .value { font-size: 32px; font-weight: 800; color: #FFA657; }");
            sb.AppendLine(".card .label { font-size: 11px; color: #8B949E; text-transform: uppercase; margin-top: 4px; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }");
            sb.AppendLine("th { background: #161B22; color: #58A6FF; font-size: 11px; text-transform: uppercase; padding: 8px 10px; text-align: left; border-bottom: 2px solid #30363D; }");
            sb.AppendLine("td { padding: 8px 10px; border-bottom: 1px solid #21262D; font-size: 13px; }");
            sb.AppendLine("tr:hover td { background: #161B22; }");
            sb.AppendLine(".good { color: #3FB950; }");
            sb.AppendLine(".bad { color: #F85149; }");
            sb.AppendLine(".neutral { color: #8B949E; }");
            sb.AppendLine(".highlight { background: #1F2937; }");
            sb.AppendLine("@media print { body { background: white; color: #1F2937; } th { background: #F3F4F6; color: #1F2937; } td { border-color: #E5E7EB; } .card { border-color: #E5E7EB; background: #F9FAFB; } .card .value { color: #D97706; } h1 { color: #1D4ED8; } h2 { color: #6B7280; } }");
            sb.AppendLine("</style></head><body>");

            // Header
            sb.AppendLine("<h1>🔗 WEAKEST LINK — GAME REPORT</h1>");
            sb.AppendLine($"<div class='meta'>{DateTime.Now:dddd, dd MMMM yyyy HH:mm} | Rounds: {_engine.CurrentRound} | Players: {allPlayers.Count}</div>");

            // Summary cards
            sb.AppendLine("<div class='summary'>");
            sb.AppendLine($"<div class='card'><div class='value'>{_engine.TotalBank:N0}</div><div class='label'>Total Bank</div></div>");
            sb.AppendLine($"<div class='card'><div class='value'>{_engine.CurrentRound}</div><div class='label'>Rounds</div></div>");
            int totalCorrect = cumulative.Sum(c => c.Correct);
            int totalQ = cumulative.Sum(c => c.TotalQ);
            double avgPct = totalQ > 0 ? Math.Round((double)totalCorrect / totalQ * 100, 1) : 0;
            sb.AppendLine($"<div class='card'><div class='value'>{avgPct}%</div><div class='label'>Avg Accuracy</div></div>");
            sb.AppendLine($"<div class='card'><div class='value'>{allPlayers.Count}</div><div class='label'>Players</div></div>");
            sb.AppendLine("</div>");

            // Player table
            sb.AppendLine("<h2>📊 Player Statistics</h2>");
            sb.AppendLine("<table><tr><th>Player</th><th>✓ Correct</th><th>✗ Wrong</th><th>— Pass</th><th>Total</th><th>Success %</th><th>💰 Banked</th><th>🔥 Dropped</th><th>Rounds</th></tr>");

            foreach (var p in cumulative.OrderByDescending(x => x.Pct))
            {
                string pctClass = p.Pct >= 70 ? "good" : p.Pct >= 40 ? "" : "bad";
                sb.AppendLine($"<tr><td><strong>{p.Name}</strong></td><td class='good'>{p.Correct}</td><td class='bad'>{p.Incorrect}</td><td class='neutral'>{p.Passes}</td><td>{p.TotalQ}</td><td class='{pctClass}'>{p.Pct}%</td><td>{p.Banked:N0}</td><td class='bad'>{p.Dropped:N0}</td><td>{p.Rounds}</td></tr>");
            }
            sb.AppendLine("</table>");

            // Per-round table
            sb.AppendLine("<h2>📋 Round-by-Round Breakdown</h2>");
            sb.AppendLine("<table><tr><th>Round</th><th>Player</th><th>✓</th><th>✗</th><th>—</th><th>%</th><th>Banked</th><th>Dropped</th></tr>");

            foreach (var player in allPlayers)
            {
                if (!_engine.PlayerStatistics.TryGetValue(player, out var rounds)) continue;
                foreach (var kv in rounds.OrderBy(r => r.Key))
                {
                    var r = kv.Value;
                    int tq = r.CorrectAnswers + r.IncorrectAnswers + r.Passes;
                    double pct = tq > 0 ? Math.Round((double)r.CorrectAnswers / tq * 100, 1) : 0;
                    sb.AppendLine($"<tr><td>{kv.Key}</td><td>{player}</td><td class='good'>{r.CorrectAnswers}</td><td class='bad'>{r.IncorrectAnswers}</td><td class='neutral'>{r.Passes}</td><td>{pct}%</td><td>{r.BankedMoney:N0}</td><td class='bad'>{r.ExactDroppedMoney:N0}</td></tr>");
                }
            }
            sb.AppendLine("</table>");

            // Footer
            sb.AppendLine("<div class='meta' style='margin-top: 32px; text-align: center;'>Generated by Weakest Link Operator v2.0 — Obsidian Edition</div>");
            sb.AppendLine("</body></html>");

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            return filePath;
        }
    }
}
