using System.Text.Json.Serialization;

namespace KazakoraAgent.Core.Models;

/// <summary>
/// Espelha GET /api/print-agent/dashboard/daily-text
/// (DashboardAgentController::dailyText no Laravel) — texto diário das
/// Testemunhas de Jeová, raspado da wol.jw.org e salvo no banco do
/// Kazakora a cada 12h (App\Console\Commands\FetchDailyText). Null quando
/// ainda não existe nenhuma linha em daily_texts (primeiro deploy antes do
/// comando agendado rodar pela primeira vez).
/// </summary>
public sealed class DailyTextDto
{
    [JsonPropertyName("date")]
    public required string Date { get; init; }

    [JsonPropertyName("weekday_label")]
    public required string WeekdayLabel { get; init; }

    [JsonPropertyName("scripture_quote")]
    public required string ScriptureQuote { get; init; }

    [JsonPropertyName("scripture_reference")]
    public required string ScriptureReference { get; init; }

    [JsonPropertyName("commentary")]
    public string? Commentary { get; init; }

    [JsonPropertyName("fetched_at")]
    public required DateTimeOffset FetchedAt { get; init; }
}
