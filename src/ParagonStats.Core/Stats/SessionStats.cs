using ParagonStats.Core.Parsing;

namespace ParagonStats.Core.Stats;

/// <summary>
/// The in-memory fold for one session: MVP counters accumulated from events.
/// Every event applies - same-second identical lines are legitimate (AoE
/// multi-target, DoT ticks, proc rolls), so there is deliberately no dedupe.
/// </summary>
public sealed class SessionStats
{
    private readonly Dictionary<string, decimal> _damageByPower = new(StringComparer.Ordinal);
    private readonly Dictionary<EventCategory, long> _categoryCounts = [];

    public long Experience { get; private set; }

    public long Influence { get; private set; }

    public long Defeats { get; private set; }

    public long Activations { get; private set; }

    public decimal TotalDamage { get; private set; }

    public IReadOnlyDictionary<string, decimal> DamageByPower => _damageByPower;

    public IReadOnlyDictionary<EventCategory, long> CategoryCounts => _categoryCounts;

    public void Apply(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        _categoryCounts[logEvent.Category] = _categoryCounts.GetValueOrDefault(logEvent.Category) + 1;

        switch (logEvent)
        {
            case DamageDealt damage:
                string key = damage.SourcePrefix is null ? damage.Power : damage.SourcePrefix + ": " + damage.Power;
                _damageByPower[key] = _damageByPower.GetValueOrDefault(key) + damage.Amount;
                this.TotalDamage += damage.Amount;
                break;
            case Defeat { Attacker: null }:
                this.Defeats++;
                break;
            case RewardGained reward:
                this.Experience += reward.Experience ?? 0;
                this.Influence += reward.Influence ?? 0;
                break;
            case PowerActivated:
                this.Activations++;
                break;
            default:
                break;
        }
    }
}
