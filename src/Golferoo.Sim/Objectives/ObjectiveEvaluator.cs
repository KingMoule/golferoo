using Golferoo.Sim.Golfers;
using Golferoo.Sim.League;

namespace Golferoo.Sim.Objectives;

/// <summary>Lit dans le monde le compteur de chaque objectif et dit lesquels viennent d'être atteints.</summary>
internal static class ObjectiveEvaluator
{
    internal static int Progress(World world, ObjectiveDef def) => def.Condition switch
    {
        ObjectiveCondition.OpenHoles => world.OpenHoles,
        ObjectiveCondition.Members => world.Visitors.Visitors.Count(v => v.Level != MemberLevel.Visitor),
        ObjectiveCondition.GoldMembers => world.Visitors.Visitors.Count(v => v.Level >= MemberLevel.Gold),
        ObjectiveCondition.PositiveYears => world.State.Reports.Count(r => r.Result > 0),
        ObjectiveCondition.ProPromotions => world.League?.Events.Count(e => e.Type == LeagueEventType.Promoted && e.MemberId == world.ProMemberId) ?? 0,
        _ => 0,
    };

    /// <summary>Indices des objectifs pas encore accomplis dont le compteur a atteint le seuil.</summary>
    internal static IEnumerable<int> Reached(World world)
    {
        var all = ObjectiveCatalog.Default;
        for (var i = 0; i < all.Count; i++)
        {
            if (!world.State.IsCompleted(i) && Progress(world, all[i]) >= all[i].Threshold)
            {
                yield return i;
            }
        }
    }
}
