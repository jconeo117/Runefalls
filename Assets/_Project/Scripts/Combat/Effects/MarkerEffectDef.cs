using UnityEngine;

namespace Runefall.Combat
{
    /// <summary>
    /// Generic behavioral marker — no stat changes, no damage.
    /// Applies an ActiveEffect whose mechanical impact comes entirely from
    /// the EffectBehavior flags declared on this SO (SkipTurn, BlockHeal, etc.).
    ///
    /// Use cases:
    ///   Congelado.asset   → behavior = SkipTurn,    duration = -1 (permanent until cleansed)
    ///   Aturdido.asset    → behavior = SkipTurn,    duration = 1
    ///   Silenciado.asset  → behavior = Silenced,    duration = 2
    ///   Invulnerable.asset→ behavior = Invulnerable, duration = -1
    ///
    /// Configure in Inspector:
    ///   effectName    = display name for UI
    ///   behavior      = the mechanical override(s) to activate
    ///   stackBySource = true  (prevent duplicate entries on same actor)
    ///   stackable     = false (reapply refreshes duration only)
    ///   durationByRank[*] = -1 for permanent, positive int for timed
    /// </summary>
    [CreateAssetMenu(menuName = "Runefall/Effects/MarkerEffect")]
    public class MarkerEffectDef : EffectDefinition
    {
        [Header("Targeting")]
        public EffectTarget effectTarget = EffectTarget.Target;

        [Header("Duration")]
        [Tooltip("Rounds the marker lasts. -1 = permanent. [0]=R1 [1]=R2 [2]=R3")]
        public int[] durationByRank = { 2, 3, 4 };

        public override void Execute(EffectExecutionContext ctx)
        {
            var recipient = effectTarget == EffectTarget.Caster ? ctx.Caster : ctx.Target;
            if (recipient == null) return;

            int durIdx = Mathf.Clamp(ctx.Rank - 1, 0, durationByRank.Length - 1);

            recipient.Effects.Apply(new ActiveEffect
            {
                Source         = this,
                Tag            = tag,
                Applier        = ctx.Caster,
                TurnsRemaining = durationByRank[durIdx],
                Stacks         = 1
            });
        }
    }
}
