using UnityEngine;
using Runefall.Characters;

namespace Runefall.Combat
{
    /// <summary>
    /// Orquestador de la pasiva de Hielo "Dominio Glacial". Coordina las cláusulas que dependen entre sí
    /// (el buff del héroe escala con la cantidad de Hipotermia en campo, que cambia al aplicar/expirar/morir),
    /// por eso es un único behavior y no varios sueltos.
    ///
    /// Cláusulas:
    ///   1. Inicio del turno del héroe → Hipotermia a todos los enemigos (1 turno).
    ///   2. Por CADA Hipotermia en campo → héroe +15% ATK y -20% daño recibido (aura dinámica, recalculada).
    ///   3. Si los PS del héroe caen < 30% durante el TURNO ENEMIGO → congela a todos los enemigos CON
    ///      Hipotermia y aplica Monarca del Hielo al héroe (1 turno).
    ///   4. Con Monarca del Hielo activo → x2 daño a enemigos congelados.
    ///
    /// Estado per-activación vive en locals capturados por las closures (Bind), nunca en campos del SO.
    /// </summary>
    [CreateAssetMenu(menuName = "Runefall/Passives/Behaviors/Dominio Glacial")]
    public class DominioGlacialBehavior : PassiveBehavior
    {
        [Header("Effects — assign the .asset instances")]
        public HipotermiaEffectDef hipotermia;
        [Tooltip("MarkerEffectDef con behavior = SkipTurn (Congelado).")]
        public MarkerEffectDef congelado;
        [Tooltip("Monarca del Hielo: OutgoingModifierEffectDef con +50% ATK (applyStat) y un modifier " +
                 "x2 daño condicionado a TargetHasBehavior(SkipTurn). El x2 lo resuelve el pipeline.")]
        public EffectDefinition monarcaDelHielo;

        [Header("Tuning — aura por Hipotermia")]
        [Tooltip("ATK del héroe por cada Hipotermia en campo. 0.15 = +15% c/u.")]
        public float atkPerHipotermia = 0.15f;
        [Tooltip("Reducción de daño recibido del héroe por cada Hipotermia. 0.20 = -20% c/u.")]
        public float dmgReductionPerHipotermia = 0.20f;

        [Header("Tuning — trigger de congelamiento")]
        [Range(0f, 1f)]
        [Tooltip("Umbral de PS del héroe que dispara el congelamiento masivo (durante el turno enemigo).")]
        public float lowHpThreshold = 0.30f;

        private const string AuraGroup = "dominio_glacial_aura";

        public override void Bind(PassiveContext ctx)
        {
            // ── per-activation state (closures, no SO fields) ──────────────────────
            bool triggerArmed = true;   // re-arma cuando los PS suben por encima del umbral
            int  lastAuraCount = -1;    // evita recalcular el aura si la cuenta no cambió

            // ── helpers ────────────────────────────────────────────────────────────
            int CountHipotermia()
            {
                int n = 0;
                var enemies = ctx.Ctx.Enemies;
                for (int i = 0; i < enemies.Count; i++)
                    if (enemies[i] != null && enemies[i].IsAlive
                        && enemies[i].Effects.HasEffect<HipotermiaEffectDef>())
                        n++;
                return n;
            }

            void RecomputeHeroAura()
            {
                int count = CountHipotermia();
                if (count == lastAuraCount) return;   // sin cambios → no churn
                lastAuraCount = count;

                ctx.Owner.Effects.RemoveByGroup(AuraGroup);   // quita el aura anterior (limpia su StatMod)
                if (count <= 0) return;

                ctx.Owner.Effects.Apply(new ActiveEffect
                {
                    Source              = null,   // aura interna (no UI)
                    Tag                 = EffectTag.Advantage,
                    TurnsRemaining      = -1,     // gestionada por recálculo, no por Tick
                    Stacks              = 1,
                    StatMod             = new StatModifier { ataqueBonus = atkPerHipotermia * count },
                    IncomingDamageBonus = -dmgReductionPerHipotermia * count,
                    GroupId             = AuraGroup
                });
            }

            void FreezeHipotermiaEnemiesAndApplyMonarca()
            {
                var enemies = ctx.Ctx.Enemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (e != null && e.IsAlive && e.Effects.HasEffect<HipotermiaEffectDef>())
                        ctx.ApplyEffect(congelado, e);
                }
                ctx.ApplyEffect(monarcaDelHielo, ctx.Owner);   // +50% ATK, 1 turno (effectTarget = Caster)
            }

            bool HpAboveThreshold() =>
                ctx.Owner.Model.CurrentHP >= ctx.Owner.Model.MaxHP * lowHpThreshold;

            // ── Cláusula 1: inicio de turno del héroe ─────────────────────────────
            ctx.OnPlayerTurnStarted(_ =>
            {
                if (hipotermia != null)
                    ctx.ApplyEffectToAll(hipotermia, ctx.Ctx.Enemies);
                RecomputeHeroAura();
                if (HpAboveThreshold()) triggerArmed = true;
            });

            // ── Cláusulas 2/3/4: por acción resuelta ──────────────────────────────
            ctx.OnActionResolved(result =>
            {
                // (2) el aura escala con la cantidad de Hipotermia (cambia por muertes/expiración)
                RecomputeHeroAura();

                if (HpAboveThreshold()) triggerArmed = true;

                // (3) PS < umbral durante el turno enemigo → congelar Hipotermia'd + Monarca
                if (triggerArmed
                    && ctx.Tm.Phase == CombatPhase.EnemyTurn
                    && result.Target == ctx.Owner
                    && ctx.Owner.Model.CurrentHP < ctx.Owner.Model.MaxHP * lowHpThreshold)
                {
                    triggerArmed = false;
                    FreezeHipotermiaEnemiesAndApplyMonarca();
                    RecomputeHeroAura();   // por si el conteo cambió tras congelar
                }

                // (4) El x2 a congelados (Monarca del Hielo) lo resuelve el pipeline de daño vía el
                //     OutgoingModifierEffectDef de Monarca — no hace falta lógica post-hoc acá.
            });
        }
    }
}
