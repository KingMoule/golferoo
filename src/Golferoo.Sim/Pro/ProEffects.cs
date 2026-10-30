using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Pro;

/// <summary>Les quatre groupes de la fiche du pro (C25).</summary>
public enum ProGroup : byte
{
    Clubs = 0,
    Shapes = 1,
    Qualities = 2,
    Situations = 3,
}

/// <summary>Effet d'un niveau : ce qui change (<see cref="Label"/>) et de combien par rapport au niveau 0 (<see cref="Percent"/>, signé).</summary>
public readonly record struct SkillEffect(string Label, double Percent)
{
    /// <summary>« dispersion −13 % » ; sans effet encore, le libellé seul.</summary>
    public override string ToString() => Label.Length == 0 ? "" : $"{Label} {Value}";

    /// <summary>« −13 % », « +4 % » ou « 0 % ».</summary>
    public string Value
    {
        get
        {
            var rounded = Math.Round(Percent);
            return $"{(rounded < 0 ? "−" : rounded > 0 ? "+" : "")}{Math.Abs(rounded):0} %";
        }
    }
}

/// <summary>Noms, groupes et effets des 17 habiletés du pro, pour la fiche et les avis du jeu (C25) ; aucune règle de jeu ici.</summary>
public static class ProEffects
{
    public static string Name(ProSkill skill) => skill switch
    {
        ProSkill.Driver => "Driver",
        ProSkill.Wood => "Bois",
        ProSkill.LongIrons => "Fers longs",
        ProSkill.MidIrons => "Fers moyens",
        ProSkill.ShortIrons => "Fers courts",
        ProSkill.Wedge => "Wedge",
        ProSkill.Putter => "Putter",
        ProSkill.Precision => "Précision",
        ProSkill.Dosage => "Dosage",
        ProSkill.Distance => "Distance",
        ProSkill.Draw => "Draw",
        ProSkill.Fade => "Fade",
        ProSkill.Chip => "Chip",
        ProSkill.RoughExit => "Sortie de rough",
        ProSkill.BunkerExit => "Sortie de bunker",
        ProSkill.Wind => "Jeu dans le vent",
        _ => "Lecture du green",
    };

    public static ProGroup GroupOf(ProSkill skill) => skill switch
    {
        <= ProSkill.Putter => ProGroup.Clubs,
        ProSkill.Draw or ProSkill.Fade or ProSkill.Chip => ProGroup.Shapes,
        ProSkill.Precision or ProSkill.Dosage or ProSkill.Distance => ProGroup.Qualities,
        _ => ProGroup.Situations,
    };

    public static string GroupName(ProGroup group) => group switch
    {
        ProGroup.Clubs => "Les clubs",
        ProGroup.Shapes => "Les trajectoires",
        ProGroup.Qualities => "Les qualités du coup",
        _ => "Les situations",
    };

    /// <summary>Effet de l'habileté <paramref name="skill"/> au niveau <paramref name="level"/>, en pourcentage du niveau 0.</summary>
    public static SkillEffect Of(ProSkill skill, int level)
    {
        level = Math.Clamp(level, 0, ProSkills.MaxLevel);
        double Ratio() => (ProSkills.Factor(level) / ProSkills.Factor(0) - 1.0) * 100.0;
        switch (skill)
        {
            case ProSkill.Precision:
                return new SkillEffect("erreur de direction", Ratio());
            case ProSkill.Dosage:
                return new SkillEffect("erreur de force", Ratio());
            case ProSkill.Distance:
                return new SkillEffect("vitesse de balle", ((ProSkills.SpeedFactorAt(level) / ProSkills.SpeedFactorAt(0)) - 1.0) * 100.0);
            case ProSkill.Draw or ProSkill.Fade:
                return new SkillEffect("erreur de courbure", Ratio());
            case ProSkill.Chip:
                return new SkillEffect("", 0.0); // l'effet du chip vient au lot 4
            case ProSkill.RoughExit or ProSkill.BunkerExit:
                {
                    var surface = skill == ProSkill.RoughExit ? Surface.Rough : Surface.Bunker;
                    double Loss(int n) => 1.0 - LieParams.Default.At(surface, ProSkills.HandicapOf(n)).Speed;
                    var start = Loss(0);
                    return new SkillEffect("pénalité de lie", start <= 0.0 ? 0.0 : ((Loss(level) / start) - 1.0) * 100.0);
                }

            case ProSkill.Wind or ProSkill.GreenReading:
                return new SkillEffect("erreur de l'aperçu", (ProSkills.BiasAmplitude(level) - 1.0) * 100.0);
            default:
                return new SkillEffect("dispersion", Ratio());
        }
    }

    /// <summary>« dispersion −13 % », ou « effet à venir » pour le chip.</summary>
    public static string Text(ProSkill skill, int level) => Of(skill, level) is { Label.Length: > 0 } effect ? effect.ToString() : "effet à venir";

    /// <summary>Ligne de la fiche : « niv. 12 : dispersion −12 %, niv. 13 : −13 % » ; au niveau maximal, « niv. 50 : dispersion −31 % (maximum) ».</summary>
    public static string Line(ProSkill skill, int level)
    {
        var now = Of(skill, level);
        if (now.Label.Length == 0)
        {
            return $"niv. {level} : effet à venir";
        }

        return level >= ProSkills.MaxLevel
            ? $"niv. {level} : {now} (maximum)"
            : $"niv. {level} : {now}, niv. {level + 1} : {Of(skill, level + 1).Value}";
    }

    /// <summary>Avis d'un niveau gagné : « Fers longs : niveau 13, dispersion −13 % ».</summary>
    public static string LevelMessage(ProSkill skill, int level) => $"{Name(skill)} : niveau {level}, {Text(skill, level)}";
}
