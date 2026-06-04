using Runefall.Data;

namespace Runefall.Combat
{
    public readonly struct BattleCard
    {
        private static int _nextId = 1;

        public readonly int          Id;
        public readonly SkillData    Skill;
        public readonly UltimateData Ultimate;
        public readonly int          Rank;
        public readonly bool         IsUltimate;

        public BattleCard(SkillData skill, int rank)
        {
            Id         = _nextId++;
            Skill      = skill;
            Ultimate   = null;
            Rank       = rank;
            IsUltimate = false;
        }

        public BattleCard(UltimateData ultimate)
        {
            Id         = _nextId++;
            Skill      = null;
            Ultimate   = ultimate;
            Rank       = 3;
            IsUltimate = true;
        }

        private BattleCard(int id, SkillData skill, UltimateData ultimate, int rank, bool isUltimate)
        {
            Id         = id;
            Skill      = skill;
            Ultimate   = ultimate;
            Rank       = rank;
            IsUltimate = isUltimate;
        }

        public BattleCard WithRank(int newRank) => new BattleCard(Id, Skill, Ultimate, newRank, IsUltimate);
    }
}
