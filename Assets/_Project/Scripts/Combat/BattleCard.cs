using Runefall.Data;

namespace Runefall.Combat
{
    public readonly struct BattleCard
    {
        public readonly SkillData    Skill;
        public readonly UltimateData Ultimate;
        public readonly int          Rank;
        public readonly bool         IsUltimate;

        public BattleCard(SkillData skill, int rank)
        {
            Skill      = skill;
            Ultimate   = null;
            Rank       = rank;
            IsUltimate = false;
        }

        public BattleCard(UltimateData ultimate)
        {
            Skill      = null;
            Ultimate   = ultimate;
            Rank       = 3;
            IsUltimate = true;
        }

        public BattleCard WithRank(int newRank) => new BattleCard(Skill, newRank);
    }
}
