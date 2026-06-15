using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Runefall.Audio;
using Runefall.Data;
using Runefall.Presentation.Combat;

/// <summary>
/// Setup automatizado del sistema de voces de combate.
///   1. Construye 3 VoiceSetData (Male/Female/Enemy) desde los .wav de Shared/Audio.
///   2. Asigna el voiceSet correcto a cada CharacterData / EnemyData.
///   3. Cablea CombatAudioPlayer + AudioManager en la escena abierta.
/// Replica el patrón de los demás Runefall*.cs en Assets/Editor.
/// </summary>
public static class RunefallAudioSetup
{
    private const string AudioClipsDir = "Assets/_Project/Shared/Audio";
    private const string VoiceSetDir   = "Assets/_Project/Systems/Audio/ScriptableObjects";
    private const string CharactersDir = "Assets/_Project/Characters/";   // roster producción
    private const string EnemiesDir    = "Assets/_Project/Enemies/";      // roster producción
    private const string ImpactEventPath =
        "Assets/_Project/Systems/Combat/ScriptableObjects/Combat/ImpactEvent.asset";

    private const string MalePath   = VoiceSetDir + "/Voice_Male.asset";
    private const string FemalePath = VoiceSetDir + "/Voice_Female.asset";
    private const string EnemyPath  = VoiceSetDir + "/Voice_Enemy.asset";

    [MenuItem("Runefall/Audio/Setup All (build + assign + wire)")]
    public static void SetupAll()
    {
        BuildVoiceSets();
        AssignVoiceSets();
        WireSceneAudio();
        Debug.Log("[AudioSetup] Setup completo. Entra a combate y deberías oír attack/hit/death.");
    }

    // ── 1. Build VoiceSetData from clips ────────────────────────────────────────

    [MenuItem("Runefall/Audio/1. Build Voice Sets From Clips")]
    public static void BuildVoiceSets()
    {
        EnsureFolder(VoiceSetDir);

        var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioClipsDir })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
            .Where(c => c != null)
            .ToList();

        if (clips.Count == 0)
        {
            Debug.LogError($"[AudioSetup] No se encontraron AudioClips en {AudioClipsDir}.");
            return;
        }

        BuildOne(MalePath,   "Male",   clips);
        BuildOne(FemalePath, "Female", clips);
        BuildOne(EnemyPath,  "Enemy",  clips);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AudioSetup] Voice sets construidos desde {clips.Count} clips.");
    }

    private static void BuildOne(string path, string token, List<AudioClip> all)
    {
        var set = AssetDatabase.LoadAssetAtPath<VoiceSetData>(path);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<VoiceSetData>();
            AssetDatabase.CreateAsset(set, path);
        }

        set.attackClips = Filter(all, token, "Attack");
        set.hitClips    = Filter(all, token, "Hit");
        set.deathClips  = Filter(all, token, "Death");
        EditorUtility.SetDirty(set);

        Debug.Log($"[AudioSetup] {System.IO.Path.GetFileName(path)}: " +
                  $"{set.attackClips.Length} attack / {set.hitClips.Length} hit / {set.deathClips.Length} death");
    }

    private static AudioClip[] Filter(List<AudioClip> all, string voiceToken, string topicToken) =>
        all.Where(c => c.name.Contains(voiceToken) && c.name.Contains(topicToken))
           .OrderBy(c => c.name)
           .ToArray();

    // ── 2. Assign to CharacterData / EnemyData ──────────────────────────────────

    [MenuItem("Runefall/Audio/2. Assign Voice Sets To Characters & Enemies")]
    public static void AssignVoiceSets()
    {
        var male   = AssetDatabase.LoadAssetAtPath<VoiceSetData>(MalePath);
        var female = AssetDatabase.LoadAssetAtPath<VoiceSetData>(FemalePath);
        var enemy  = AssetDatabase.LoadAssetAtPath<VoiceSetData>(EnemyPath);

        if (male == null || female == null || enemy == null)
        {
            Debug.LogError("[AudioSetup] Faltan voice sets. Corre primero 'Build Voice Sets From Clips'.");
            return;
        }

        int chars = 0, charsSkipped = 0;
        foreach (var cd in LoadAll<CharacterData>("t:CharacterData"))
        {
            // Solo el roster de producción (no los placeholders de Blockout).
            string path = AssetDatabase.GetAssetPath(cd);
            if (!path.StartsWith(CharactersDir)) { charsSkipped++; continue; }

            string n = (cd.name + " " + (cd.characterName ?? "")).ToLowerInvariant();
            cd.voiceSet = n.Contains("queen") ? female : male;
            EditorUtility.SetDirty(cd);
            chars++;
        }

        int enemies = 0, enemiesSkipped = 0;
        foreach (var ed in LoadAll<EnemyData>("t:EnemyData"))
        {
            // Solo enemigos de producción; excluye el boss (data + fases) y los placeholders de Blockout.
            string path = AssetDatabase.GetAssetPath(ed);
            bool isBoss = ed is BossEnemyData || path.Contains("/Tharok/");
            if (!path.StartsWith(EnemiesDir) || isBoss) { enemiesSkipped++; continue; }

            ed.voiceSet = enemy;
            EditorUtility.SetDirty(ed);
            enemies++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[AudioSetup] Asignado: {chars} personajes ({charsSkipped} omitidos), " +
                  $"{enemies} enemigos ({enemiesSkipped} omitidos: boss/fases/blockout).");
    }

    // ── 3. Wire scene components ────────────────────────────────────────────────

    [MenuItem("Runefall/Audio/3. Wire Combat Audio In Open Scene")]
    public static void WireSceneAudio()
    {
        var bootstrappers = Object.FindObjectsByType<CombatBootstrapper>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (bootstrappers.Length == 0)
        {
            Debug.LogWarning("[AudioSetup] No hay CombatBootstrapper en la escena abierta. " +
                             "Abre la escena de juego (p. ej. DungeonExploration) y reintenta.");
            return;
        }

        var impactEvent = AssetDatabase.LoadAssetAtPath<ImpactEvent>(ImpactEventPath);
        var male   = AssetDatabase.LoadAssetAtPath<VoiceSetData>(MalePath);
        var enemy  = AssetDatabase.LoadAssetAtPath<VoiceSetData>(EnemyPath);

        // AudioManager en escena (opcional — si falta, se autocrea en runtime).
        if (Object.FindFirstObjectByType<AudioManager>() == null)
        {
            var amGo = new GameObject("AudioManager");
            amGo.AddComponent<AudioManager>();
            EditorSceneManager.MarkSceneDirty(amGo.scene);
            Debug.Log("[AudioSetup] AudioManager creado en escena.");
        }

        foreach (var boot in bootstrappers)
        {
            var audio = boot.GetComponent<CombatAudioPlayer>();
            if (audio == null) audio = boot.gameObject.AddComponent<CombatAudioPlayer>();

            var so = new SerializedObject(audio);
            SetRef(so, "_impactEvent", impactEvent);
            SetRef(so, "_defaultPlayerVoice", male);
            SetRef(so, "_defaultEnemyVoice", enemy);
            so.ApplyModifiedPropertiesWithoutUndo();

            boot.audioPlayer = audio;
            EditorUtility.SetDirty(boot);
            EditorUtility.SetDirty(audio);
            EditorSceneManager.MarkSceneDirty(boot.gameObject.scene);
            Debug.Log($"[AudioSetup] CombatAudioPlayer cableado en '{boot.name}'.");
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static void SetRef(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning($"[AudioSetup] Propiedad '{prop}' no encontrada.");
    }

    private static IEnumerable<T> LoadAll<T>(string filter) where T : Object =>
        AssetDatabase.FindAssets(filter)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(o => o != null)
            .Distinct();

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        var leaf   = System.IO.Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
