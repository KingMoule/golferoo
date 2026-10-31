using Golferoo.Sim.Pro;
using Xunit.Abstractions;

namespace Golferoo.Sim.Tests;

/// <summary>C25 lot 4 : calibrage de la progression par saisons simulées (voir <see cref="ProSeasonSim"/>).</summary>
public class ProSeasonTests(ITestOutputHelper output)
{
    /// <summary>
    /// Critère 7 : les habiletés les plus travaillées (Précision, Putter, Dosage : un gain à presque chaque coup) finissent la première saison type
    /// (cinq journées de ligue et l'entraînement au plafond) entre les niveaux 22 et 28 ; aucune habileté n'atteint 50 avant la dixième saison.
    /// </summary>
    [Fact]
    public void MostWorkedSkills_EndSeasonOneBetween22And28_AndNothingReaches50BeforeTenSeasons()
    {
        var levels = ProSeasonSim.Run(ProgressionParams.Default, 10, 1);
        for (var s = 0; s < levels.Length; s++)
        {
            var l = levels[s];
            output.WriteLine($"saison {s + 1} : Putter {l[(int)ProSkill.Putter]}, Précision {l[(int)ProSkill.Precision]}, Dosage {l[(int)ProSkill.Dosage]}, Fers moyens {l[(int)ProSkill.MidIrons]}, max {l.Max()}");
        }

        var first = levels[0];
        Assert.InRange(first[(int)ProSkill.Precision], 22, 28);
        Assert.InRange(first[(int)ProSkill.Putter], 22, 28);
        Assert.InRange(first[(int)ProSkill.Dosage], 22, 28);
        Assert.True(levels[9].Max() < ProSkills.MaxLevel, "il faut plus de dix saisons pour amener une habileté au maximum");
        Assert.InRange(levels[9].Max(), 44, 49);
    }

    /// <summary>Seuils de qualité : à chaque niveau, la qualité moyenne d'un coup du pro n'est ni toujours 0 ni toujours 1 (coups mêlés de draw, fade et chip, vent de 5 m/s).</summary>
    [Fact]
    public void MeanQualityPerSkill_IsNeverDegenerate_AtLevels0_25_And50()
    {
        foreach (var level in new[] { 0, 25, 50 })
        {
            var terrain = ProSeasonSim.LeNeuf();
            var resolver = ProSeasonSim.ResolverFor(terrain, new World(1, terrain));
            var pro = new ProSkills();
            for (var i = 0; i < ProSkills.Count; i++)
            {
                pro.SetLevel((ProSkill)i, level);
            }

            var sum = new double[ProSkills.Count];
            var count = new int[ProSkills.Count];
            for (var n = 0; n < 40; n++)
            {
                ProSeasonSim.PlayRound(pro, resolver, terrain, (ulong)n + 7, true, 1, awards =>
                {
                    foreach (var (skill, hundredths) in awards)
                    {
                        sum[(int)skill] += hundredths / 1000.0;
                        count[(int)skill]++;
                    }
                }, shapeMode: 1, wind: 5.0);
            }

            output.WriteLine($"niveau {level} : " + string.Join(", ", Enumerable.Range(0, ProSkills.Count).Select(i => $"{ProEffects.Name((ProSkill)i)} {(count[i] == 0 ? 0 : sum[i] / count[i]):F2}")));
            for (var i = 0; i < ProSkills.Count; i++)
            {
                if (count[i] >= 30)
                {
                    Assert.InRange(sum[i] / count[i], 0.05, 0.90);
                }
            }
        }
    }
}
