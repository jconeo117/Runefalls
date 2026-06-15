using UnityEditor;
using UnityEngine;
using Runefall.Data;

namespace Runefall.EditorTools
{
    /// <summary>One-shot: assign the difficulty-themed HP bar frame sprite to each enemy's EnemyData.</summary>
    public static class RunefallEnemyHpBars
    {
        private const string Dir = "Assets/_Project/Shared/UI/HP Bars/Barras de Vida";

        // EnemyData asset -> HP bar sprite
        private static readonly (string data, string sprite)[] Enemies =
        {
            ("Assets/_Project/Enemies/Vigilante Novato/VigilanteNovato_EnemyData.asset", Dir + "/Barra Vida Enemigo Easy.png"),
            ("Assets/_Project/Enemies/Carcelero Runico/CarceleroRunico_EnemyData.asset", Dir + "/Barra Vida Enemigo Mid.png"),
            ("Assets/_Project/Enemies/Gran Verdugo/GranVerdugo_EnemyData.asset",         Dir + "/Barra Vida Enemigo Hard.png"),
            ("Assets/_Project/Enemies/Tharok/Tharok_BossData.asset",                     Dir + "/Barra Vida Enemigo Boss.png"),
        };

        // CharacterData asset -> HP bar sprite (Lyra == Ice Queen)
        private static readonly (string data, string sprite)[] Characters =
        {
            ("Assets/_Project/Characters/Kael/Kael_CharacterData.asset",          Dir + "/Barra Vida Kael.png"),
            ("Assets/_Project/Characters/Vorn/Vorn_CharacterData.asset",          Dir + "/Barra Vida Vorn.png"),
            ("Assets/_Project/Characters/Ice Queen/IceQueen_CharacterData.asset", Dir + "/Barra Vida Lyra.png"),
        };

        [MenuItem("Tools/Runefall/Assign Enemy HP Bars")]
        public static void Run()
        {
            int done = 0;

            foreach (var (dataPath, spritePath) in Enemies)
            {
                var ed = AssetDatabase.LoadAssetAtPath<EnemyData>(dataPath);
                if (ed == null) { Debug.LogWarning($"[EnemyHpBars] EnemyData missing: {dataPath}"); continue; }
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite == null) { Debug.LogWarning($"[EnemyHpBars] Sprite missing: {spritePath}"); continue; }
                ed.hpBarFrame = sprite;
                EditorUtility.SetDirty(ed);
                done++;
                Debug.Log($"[EnemyHpBars] {ed.name} -> {sprite.name}");
            }

            foreach (var (dataPath, spritePath) in Characters)
            {
                var cd = AssetDatabase.LoadAssetAtPath<CharacterData>(dataPath);
                if (cd == null) { Debug.LogWarning($"[EnemyHpBars] CharacterData missing: {dataPath}"); continue; }
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite == null) { Debug.LogWarning($"[EnemyHpBars] Sprite missing: {spritePath}"); continue; }
                cd.hpBarFrame = sprite;
                EditorUtility.SetDirty(cd);
                done++;
                Debug.Log($"[EnemyHpBars] {cd.name} -> {sprite.name}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[EnemyHpBars] Done. {done}");
        }
    }
}
