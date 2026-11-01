using Golferoo.Sim.Economy;
using Golferoo.Sim.Pro;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;

namespace Golferoo.Sim;

/// <summary>
/// Conseils du pro (C19 lot 2) : la première année de la partie, chaque conseil de <see cref="AdviceCatalog"/> se donne une seule fois, au changement de jour,
/// dès que sa condition est vraie. La sim lève <see cref="AdviceGiven"/> ; l'affichage (et l'option qui le coupe) est l'affaire du jeu.
/// </summary>
public sealed partial class World
{
    /// <summary>Le pro donne un conseil, avec le numéro du trou concerné (0 si aucun) ; levé au changement de jour.</summary>
    public event Action<AdviceDef, int>? AdviceGiven;

    /// <summary>Un club vit : activé et pourvu d'un pavillon (les parcours de démonstration, sans pavillon, n'ont ni conseil ni suivi des trous).</summary>
    private bool ClubLives => State.ClubActive && Clubhouse is not null;

    /// <summary>Les conseils ne se donnent que durant l'année 1 du calendrier, le club en vie.</summary>
    private bool InAdviceYear => ClubLives && Calendar.YearAndMonth(CurrentMonth).Year == 1;

    /// <summary>Un trou vient de se jouer en entier : il n'est plus « sans passage ».</summary>
    private void MarkHolePlayed(int hole)
    {
        if (ClubLives)
        {
            State.SetHoleIdle(hole - 1, -1);
        }
    }

    /// <summary>Changement de jour : compte les jours sans partie finie de chaque trou ouvert, puis donne les conseils dont la condition est vraie.</summary>
    private void EvaluateAdvice()
    {
        if (Terrain is not { } terrain)
        {
            return;
        }

        for (var i = 0; i < _holesOpen.Length; i++)
        {
            State.SetHoleIdle(i, _holesOpen[i] && ClubLives ? State.HoleIdle(i) + 1 : 0);
        }

        if (!InAdviceYear)
        {
            return;
        }

        var all = AdviceCatalog.Default;
        for (var i = 0; i < all.Count; i++)
        {
            if (State.AdviceDone(i) || Reached(terrain, all[i]) is not { } hole)
            {
                continue;
            }

            State.GiveAdvice(i);
            AdviceGiven?.Invoke(all[i], hole);
        }
    }

    /// <summary>Le numéro du trou concerné (0 si le conseil n'en vise pas) si la condition est vraie, sinon nul.</summary>
    private int? Reached(TerrainGrid terrain, AdviceDef def)
    {
        switch (def.Condition)
        {
            case AdviceCondition.FirstWalkout:
                return State.Walkouts >= def.Threshold ? 0 : null;
            case AdviceCondition.LowCash:
                return Ledger.Balance < def.Threshold ? 0 : null;
            case AdviceCondition.UnplayedHole:
                for (var i = 0; i < _holesOpen.Length; i++)
                {
                    if (_holesOpen[i] && State.HoleIdle(i) >= def.Threshold)
                    {
                        return i + 1;
                    }
                }

                return null;
            default:
                return WornShare(terrain) * 100 >= def.Threshold ? 0 : null;
        }
    }

    /// <summary>Part des cellules de fairway et de green usées au moins au seuil de <see cref="MoodParams.WornTurf"/> (0 sans gazon de jeu).</summary>
    internal double WornShare(TerrainGrid terrain)
    {
        int turf = 0, worn = 0;
        for (var z = 0; z < terrain.CellsZ; z++)
        {
            for (var x = 0; x < terrain.CellsX; x++)
            {
                if (terrain.GetCellSurface(x, z) is Surface.Fairway or Surface.Green)
                {
                    turf++;
                    worn += terrain.Turf.Wear(x, z) >= Moods.WornTurf ? 1 : 0;
                }
            }
        }

        return turf == 0 ? 0.0 : (double)worn / turf;
    }
}
