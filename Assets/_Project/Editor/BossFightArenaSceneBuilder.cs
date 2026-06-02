using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Netcode;
using Runefall.Presentation.Network;
using Runefall.Presentation.Combat;
using Runefall.Data;
using Runefall.Characters;

namespace Runefall.Editor
{
    /// <summary>
    /// Editor script to generate the 3D Boss Fight Arena Scene programmatically.
    /// Spawns a massive wide circular combat arena, ancient runic columns, URP lighting,
    /// dynamic NetworkedTurnManager, and a beautifully styled, premium Networked Combat HUD.
    /// </summary>
    public class BossFightArenaSceneBuilder : EditorWindow
    {
        [MenuItem("Runefalls/Build Boss Fight Arena Scene")]
        public static void BuildBossFightArenaScene()
        {
            string scenePath = "Assets/_Project/Scenes/BossFight_Arena.unity";
            
            // 1. Create a pristine empty scene
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            
            // 2. Create Environment Root
            GameObject envParent = new GameObject("ArenaEnvironment");

            // A. The Main Hall Floor (Basement Stone Foundation)
            GameObject baseFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseFloor.name = "Stone_Foundation";
            baseFloor.transform.SetParent(envParent.transform);
            baseFloor.transform.position = new Vector3(0, -0.5f, 0);
            baseFloor.transform.localScale = new Vector3(45, 1, 45);
            var baseFloorRenderer = baseFloor.GetComponent<Renderer>();
            if (baseFloorRenderer != null)
            {
                baseFloorRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                baseFloorRenderer.sharedMaterial.color = new Color(0.08f, 0.08f, 0.1f); // Deep obsidian gray
            }

            // B. The Central Circular Combat Arena (Giant Cylinder)
            GameObject arenaCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arenaCylinder.name = "Central_Combat_Arena";
            arenaCylinder.transform.SetParent(envParent.transform);
            arenaCylinder.transform.position = new Vector3(0, 0.01f, 0);
            arenaCylinder.transform.localScale = new Vector3(30f, 0.05f, 30f); // Wide circular platform
            var arenaRenderer = arenaCylinder.GetComponent<Renderer>();
            if (arenaRenderer != null)
            {
                arenaRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                arenaRenderer.sharedMaterial.color = new Color(0.15f, 0.15f, 0.18f); // Runic dark basalt gray
                arenaRenderer.sharedMaterial.EnableKeyword("_EMISSION");
                arenaRenderer.sharedMaterial.SetColor("_EmissionColor", new Color(0.02f, 0.12f, 0.18f)); // Dark cyan mystic pool glow
            }

            // C. Runic Border Ring around the Arena
            GameObject arenaBorder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arenaBorder.name = "Rune_Border_Ring";
            arenaBorder.transform.SetParent(envParent.transform);
            arenaBorder.transform.position = new Vector3(0, -0.01f, 0);
            arenaBorder.transform.localScale = new Vector3(31f, 0.04f, 31f);
            var borderRenderer = arenaBorder.GetComponent<Renderer>();
            if (borderRenderer != null)
            {
                borderRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                borderRenderer.sharedMaterial.color = new Color(0.25f, 0.12f, 0.02f); // Deep gold/amber runic frame
                borderRenderer.sharedMaterial.EnableKeyword("_EMISSION");
                borderRenderer.sharedMaterial.SetColor("_EmissionColor", new Color(0.2f, 0.08f, 0f)); // Gold fire glow
            }
            var borderCollider = arenaBorder.GetComponent<Collider>();
            if (borderCollider != null) DestroyImmediate(borderCollider);

            // =========================================================================
            // C1. PLAYER LINE AND BOSS LINE (Floor markers face-to-face, 10 units apart in Z)
            // =========================================================================
            GameObject playerLineObj = new GameObject("PlayerLine");
            playerLineObj.transform.SetParent(envParent.transform);
            playerLineObj.transform.position = new Vector3(0f, 0.02f, -5f);
            playerLineObj.transform.rotation = Quaternion.identity; // Facing North

            GameObject playerLineVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            playerLineVisual.name = "PlayerLine_Visual";
            playerLineVisual.transform.SetParent(playerLineObj.transform, false);
            playerLineVisual.transform.localPosition = Vector3.zero;
            playerLineVisual.transform.localScale = new Vector3(8f, 0.01f, 0.4f);
            var playerLineRenderer = playerLineVisual.GetComponent<Renderer>();
            if (playerLineRenderer != null)
            {
                playerLineRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                playerLineRenderer.sharedMaterial.color = new Color(0.1f, 0.12f, 0.15f);
                playerLineRenderer.sharedMaterial.EnableKeyword("_EMISSION");
                playerLineRenderer.sharedMaterial.SetColor("_EmissionColor", new Color(0f, 0.4f, 0.7f)); // Deep Cyan Runic Glow
            }
            var playerLineCol = playerLineVisual.GetComponent<Collider>();
            if (playerLineCol != null) DestroyImmediate(playerLineCol);

            GameObject bossLineObj = new GameObject("BossLine");
            bossLineObj.transform.SetParent(envParent.transform);
            bossLineObj.transform.position = new Vector3(0f, 0.02f, 5f);
            bossLineObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // Facing South

            GameObject bossLineVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bossLineVisual.name = "BossLine_Visual";
            bossLineVisual.transform.SetParent(bossLineObj.transform, false);
            bossLineVisual.transform.localPosition = Vector3.zero;
            bossLineVisual.transform.localScale = new Vector3(6f, 0.01f, 0.4f);
            var bossLineRenderer = bossLineVisual.GetComponent<Renderer>();
            if (bossLineRenderer != null)
            {
                bossLineRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                bossLineRenderer.sharedMaterial.color = new Color(0.12f, 0.1f, 0.1f);
                bossLineRenderer.sharedMaterial.EnableKeyword("_EMISSION");
                bossLineRenderer.sharedMaterial.SetColor("_EmissionColor", new Color(0.7f, 0.1f, 0f)); // Deep Crimson Runic Glow
            }
            var bossLineCol = bossLineVisual.GetComponent<Collider>();
            if (bossLineCol != null) DestroyImmediate(bossLineCol);

            // D. Surrounding Pillars (8 Column Pillars arranged in a circle)
            int numPillars = 8;
            float circleRadius = 16.5f;
            Color pillarColor = new Color(0.22f, 0.22f, 0.26f);
            Color pillarLightGlow = new Color(0f, 0.22f, 0.35f);

            for (int i = 0; i < numPillars; i++)
            {
                float angle = i * (360f / numPillars) * Mathf.Deg2Rad;
                float x = circleRadius * Mathf.Cos(angle);
                float z = circleRadius * Mathf.Sin(angle);

                GameObject pillarGroup = new GameObject($"Column_Pillar_Group_{i}");
                pillarGroup.transform.SetParent(envParent.transform);
                pillarGroup.transform.position = new Vector3(x, 0, z);

                GameObject basePedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                basePedestal.name = "Base_Pedestal";
                basePedestal.transform.SetParent(pillarGroup.transform, false);
                basePedestal.transform.localPosition = new Vector3(0, 0.4f, 0);
                basePedestal.transform.localScale = new Vector3(2.2f, 0.8f, 2.2f);
                var baseRenderer = basePedestal.GetComponent<Renderer>();
                if (baseRenderer != null)
                {
                    baseRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    baseRenderer.sharedMaterial.color = pillarColor * 0.8f;
                }

                GameObject cylinderColumn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cylinderColumn.name = "Main_Column_Shaft";
                cylinderColumn.transform.SetParent(pillarGroup.transform, false);
                cylinderColumn.transform.localPosition = new Vector3(0, 4.4f, 0);
                cylinderColumn.transform.localScale = new Vector3(1.6f, 3.8f, 1.6f);
                var colRenderer = cylinderColumn.GetComponent<Renderer>();
                if (colRenderer != null)
                {
                    colRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    colRenderer.sharedMaterial.color = pillarColor;
                    colRenderer.sharedMaterial.EnableKeyword("_EMISSION");
                    colRenderer.sharedMaterial.SetColor("_EmissionColor", pillarLightGlow * 0.4f);
                }

                GameObject runicBand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                runicBand.name = "Runic_Band";
                runicBand.transform.SetParent(pillarGroup.transform, false);
                runicBand.transform.localPosition = new Vector3(0, 4.4f, 0);
                runicBand.transform.localScale = new Vector3(1.9f, 0.6f, 1.9f);
                var bandRenderer = runicBand.GetComponent<Renderer>();
                if (bandRenderer != null)
                {
                    bandRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    bandRenderer.sharedMaterial.color = new Color(0.12f, 0.45f, 0.6f);
                    bandRenderer.sharedMaterial.EnableKeyword("_EMISSION");
                    bandRenderer.sharedMaterial.SetColor("_EmissionColor", pillarLightGlow * 1.5f);
                }
                var bandCollider = runicBand.GetComponent<Collider>();
                if (bandCollider != null) DestroyImmediate(bandCollider);

                GameObject topCapital = GameObject.CreatePrimitive(PrimitiveType.Cube);
                topCapital.name = "Top_Capital";
                topCapital.transform.SetParent(pillarGroup.transform, false);
                topCapital.transform.localPosition = new Vector3(0, 8.4f, 0);
                topCapital.transform.localScale = new Vector3(2.2f, 0.8f, 2.2f);
                var topRenderer = topCapital.GetComponent<Renderer>();
                if (topRenderer != null)
                {
                    topRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    topRenderer.sharedMaterial.color = pillarColor * 0.8f;
                }
            }

            // E. Surround Hall Walls (North, South, East, West)
            string[] wallNames = { "Wall_North", "Wall_South", "Wall_East", "Wall_West" };
            Vector3[] wallPositions = {
                new Vector3(0, 5, 22.5f), new Vector3(0, 5, -22.5f),
                new Vector3(22.5f, 5, 0), new Vector3(-22.5f, 5, 0)
            };
            Vector3[] wallScales = {
                new Vector3(45, 10, 1), new Vector3(45, 10, 1),
                new Vector3(1, 10, 45), new Vector3(1, 10, 45)
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = wallNames[i];
                wall.transform.SetParent(envParent.transform);
                wall.transform.position = wallPositions[i];
                wall.transform.localScale = wallScales[i];
                var wallRenderer = wall.GetComponent<Renderer>();
                if (wallRenderer != null)
                {
                    wallRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    wallRenderer.sharedMaterial.color = new Color(0.1f, 0.1f, 0.12f);
                }
            }

            // F. Entrance Boss Door Gate
            GameObject bossDoor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bossDoor.name = "BossFight_Entrance_Gate";
            bossDoor.transform.SetParent(envParent.transform);
            bossDoor.transform.position = new Vector3(0, 5, -22f);
            bossDoor.transform.localScale = new Vector3(6, 8, 0.4f);
            var doorRenderer = bossDoor.GetComponent<Renderer>();
            if (doorRenderer != null)
            {
                doorRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                doorRenderer.sharedMaterial.color = new Color(0.04f, 0.04f, 0.05f);
                doorRenderer.sharedMaterial.EnableKeyword("_EMISSION");
                doorRenderer.sharedMaterial.SetColor("_EmissionColor", new Color(0.01f, 0.05f, 0.08f));
            }

            // 3. Create Dynamic Atmospheric Lighting
            GameObject sunLightObj = new GameObject("Arena_DirectionalLight_Moon");
            var sunLight = sunLightObj.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLightObj.transform.rotation = Quaternion.Euler(55, 45, 0);
            sunLight.intensity = 0.8f;
            sunLight.color = new Color(0.6f, 0.75f, 1.0f); // Mystic icy moon blue
            sunLight.shadows = LightShadows.Soft;

            GameObject fireLightObj = new GameObject("Arena_DirectionalLight_FieryFill");
            var fireLight = fireLightObj.AddComponent<Light>();
            fireLight.type = LightType.Directional;
            fireLightObj.transform.rotation = Quaternion.Euler(-35, -135, 0);
            fireLight.intensity = 0.4f;
            fireLight.color = new Color(0.95f, 0.4f, 0.1f); // Hellfire amber fill
            fireLight.shadows = LightShadows.None;

            // 4. Spectator/Camera Anchor
            GameObject cameraAnchor = new GameObject("Default_Arena_Camera_Anchor");
            cameraAnchor.transform.SetParent(envParent.transform);
            cameraAnchor.transform.position = new Vector3(0, 8.5f, -15.5f);
            cameraAnchor.transform.rotation = Quaternion.Euler(22f, 0, 0);

            // 4.5. Main Camera with Tag "MainCamera"
            GameObject cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            var cameraComp = cameraObj.AddComponent<Camera>();
            cameraComp.farClipPlane = 1000f;
            cameraComp.nearClipPlane = 0.3f;
            cameraObj.AddComponent<AudioListener>();
            cameraObj.AddComponent<Unity.Cinemachine.CinemachineBrain>();

            cameraObj.transform.position = cameraAnchor.transform.position;
            cameraObj.transform.rotation = cameraAnchor.transform.rotation;

            // =========================================================================
            // G. LOAD COMBAT DATA & INITIALISE NETWORKED TURN MANAGER
            // =========================================================================
            string charDataPath = "Assets/_Project/Characters/Ice Queen/IceQueen_CharacterData.asset";
            var charData = AssetDatabase.LoadAssetAtPath<CharacterData>(charDataPath);

            // Create Fire Knight CharacterData asset for client if it does not exist
            string clientCharDataPath = "Assets/_Project/Characters/FireKnight_CharacterData.asset";
            var clientCharData = AssetDatabase.LoadAssetAtPath<CharacterData>(clientCharDataPath);
            if (clientCharData == null && charData != null)
            {
                clientCharData = ScriptableObject.CreateInstance<CharacterData>();
                clientCharData.characterName = "Fire Knight";
                clientCharData.element = ElementType.Fire;
                clientCharData.baseStats = charData.baseStats; // share stats block
                
                var gSkill1 = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/Systems/Combat/ScriptableObjects/Blockout/Skills/Golpe_Decisivo.asset");
                var gSkill2 = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/Systems/Combat/ScriptableObjects/Blockout/Skills/Grito_de_Guerra.asset");
                clientCharData.skill1 = gSkill1;
                clientCharData.skill2 = gSkill2;
                clientCharData.ultimate = charData.ultimate;
                clientCharData.animatorController = charData.animatorController;
                
                AssetDatabase.CreateAsset(clientCharData, clientCharDataPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[BossFightArenaSceneBuilder] Successfully constructed client CharacterData (Fire Knight).");
            }
            else if (clientCharData != null)
            {
                bool dirty = false;
                if (clientCharData.skill1 == null)
                {
                    clientCharData.skill1 = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/Systems/Combat/ScriptableObjects/Blockout/Skills/Golpe_Decisivo.asset");
                    dirty = true;
                }
                if (clientCharData.skill2 == null)
                {
                    clientCharData.skill2 = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/Systems/Combat/ScriptableObjects/Blockout/Skills/Grito_de_Guerra.asset");
                    dirty = true;
                }
                if (dirty)
                {
                    EditorUtility.SetDirty(clientCharData);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[BossFightArenaSceneBuilder] Repopulated missing/null skills on existing Fire Knight CharacterData.");
                }
            }

            string bossDataPath = "Assets/_Project/Systems/Enemies/ScriptableObjects/Blockout/Enemies/Orco.asset";
            var bossData = AssetDatabase.LoadAssetAtPath<EnemyData>(bossDataPath);

            if (charData == null || bossData == null)
            {
                Debug.LogWarning("[BossFightArenaSceneBuilder] Warning: Ice Queen CharacterData or Orco EnemyData could not be loaded at their respective paths.");
            }

            // 5. Create a single NetworkedTurnManager GameObject with a single NetworkObject
            GameObject turnManagerObj = new GameObject("NetworkedTurnManager");
            turnManagerObj.transform.SetParent(envParent.transform);
            var netObjTM = turnManagerObj.AddComponent<NetworkObject>();
            var turnManagerComp = turnManagerObj.AddComponent<NetworkedTurnManager>();

            // Wire domain assets programmatically
            typeof(NetworkedTurnManager).GetField("playerCharacterData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(turnManagerComp, charData);
            typeof(NetworkedTurnManager).GetField("clientCharacterData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(turnManagerComp, clientCharData);
            typeof(NetworkedTurnManager).GetField("bossEnemyData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(turnManagerComp, bossData);

            Debug.Log("[BossFightArenaSceneBuilder] Programmatically configured NetworkedTurnManager with separate Host and Client CharacterData.");

            // Create EventSystem to enable UI dragging and clicking!
            if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                Debug.Log("[BossFightArenaSceneBuilder] Successfully constructed programmatically clean EventSystem with InputSystemUIInputModule.");
            }

            // ===================================================
            // H. CREATE NETWORK COMBAT PREFAB & MULTIPLAYER SPAWNER
            // ===================================================
            string prefabPath = "Assets/_Project/Prefabs/NetworkedCombatPawn.prefab";
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            if (prefabAsset == null)
            {
                GameObject tempGO = new GameObject("NetworkedCombatPawn");
                tempGO.AddComponent<NetworkObject>();
                tempGO.AddComponent<Unity.Netcode.Components.NetworkTransform>();
                tempGO.AddComponent<NetworkedCombatPawn>();
                
                bool success;
                prefabAsset = PrefabUtility.SaveAsPrefabAsset(tempGO, prefabPath, out success);
                DestroyImmediate(tempGO);
                Debug.Log("[BossFightArenaSceneBuilder] Created new NetworkedCombatPawn prefab at " + prefabPath);
            }

            // Attach MultiplayerCombatSpawner to the NetworkedTurnManager GameObject so they share the single NetworkObject!
            // This guarantees absolutely ZERO GlobalObjectIdHash conflicts in NGO scene loading!
            var spawnerComp = turnManagerObj.AddComponent<MultiplayerCombatSpawner>();
            
            // Wire prefab asset reference into spawner
            typeof(MultiplayerCombatSpawner).GetField("networkedPawnPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(spawnerComp, prefabAsset);

            // Wire CombatUI_Canvas.prefab to NetworkedCombatPawn prefab
            string canvasPrefabPath = "Assets/_Project/Systems/Combat/Prefabs/UI/CombatUI_Canvas.prefab";
            GameObject canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(canvasPrefabPath);
            if (canvasPrefab == null)
            {
                Debug.LogWarning("[BossFightArenaSceneBuilder] Warning: canvasPrefab is null at " + canvasPrefabPath);
            }
            if (prefabAsset == null)
            {
                Debug.LogWarning("[BossFightArenaSceneBuilder] Warning: prefabAsset is null at " + prefabPath);
            }
            if (canvasPrefab != null && prefabAsset != null)
            {
                var pawnComp = prefabAsset.GetComponent<NetworkedCombatPawn>();
                if (pawnComp == null)
                {
                    Debug.LogWarning("[BossFightArenaSceneBuilder] Warning: pawnComp is null on prefabAsset!");
                }
                else
                {
                    typeof(NetworkedCombatPawn).GetField("combatCanvasPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(pawnComp, canvasPrefab);
                    EditorUtility.SetDirty(prefabAsset);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[BossFightArenaSceneBuilder] Successfully wired CombatUI_Canvas.prefab to NetworkedCombatPawn prefab.");
                }
            }

            // =========================================================================
            // I. PROGRAMMATICALLY BUILD NETWORKED COMBAT HUD CANVAS
            // =========================================================================
            GameObject uiCanvas = new GameObject("Combat_NetworkUI");
            var canvas = uiCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            var scaler = uiCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            uiCanvas.AddComponent<GraphicRaycaster>();
            var hudComp = uiCanvas.AddComponent<NetworkedCombatHUD>();

            Color panelColor = new Color(0.06f, 0.06f, 0.08f, 0.90f);
            Color neonCrimson = new Color(0.85f, 0.15f, 0.15f, 0.9f);

            // 1. Top Panel: Boss HP Bar
            GameObject panelBossObj = new GameObject("Panel_Boss_HP");
            panelBossObj.transform.SetParent(uiCanvas.transform, false);
            var bossRt = panelBossObj.AddComponent<RectTransform>();
            bossRt.anchorMin = bossRt.anchorMax = new Vector2(0.5f, 1f); // Top Center
            bossRt.anchoredPosition = new Vector2(0f, -40f);
            bossRt.sizeDelta = new Vector2(800f, 70f);

            var bossImg = panelBossObj.AddComponent<Image>();
            bossImg.color = panelColor;

            Slider bossHPSlider = CreatePremiumSlider("Slider_BossHP", panelBossObj.transform, neonCrimson, new Vector2(760f, 20f), new Vector3(0f, -12f, 0f));
            bossHPSlider.GetComponent<RectTransform>().anchorMin = bossHPSlider.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);

            Text bossHPText = CreatePremiumText("Text_BossHP", panelBossObj.transform, "ORCO (JEFE ANCIANO): 5000 / 5000", 18, Color.white, TextAnchor.MiddleCenter, new Vector2(760f, 30f), new Vector3(0f, 15f, 0f));
            bossHPText.GetComponent<RectTransform>().anchorMin = bossHPText.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
            bossHPText.fontStyle = FontStyle.Bold;

            // 2. Circular Golden Timer (Top Center, below Boss HP Panel)
            GameObject timerObj = new GameObject("Turn_Timer");
            timerObj.transform.SetParent(uiCanvas.transform, false);
            var timerRt = timerObj.AddComponent<RectTransform>();
            timerRt.anchorMin = timerRt.anchorMax = new Vector2(0.5f, 1f);
            timerRt.pivot = new Vector2(0.5f, 0.5f);
            timerRt.anchoredPosition = new Vector2(0f, -145f); // Positioned below Boss HP Panel
            timerRt.sizeDelta = new Vector2(100f, 100f);

            GameObject timerBg = new GameObject("Background_Circle");
            timerBg.transform.SetParent(timerObj.transform, false);
            var tBgRt = timerBg.AddComponent<RectTransform>();
            tBgRt.anchorMin = Vector2.zero;
            tBgRt.anchorMax = Vector2.one;
            tBgRt.sizeDelta = Vector2.zero;
            var tBgImg = timerBg.AddComponent<Image>();
            tBgImg.color = panelColor;

            GameObject timerGlow = new GameObject("Gold_Glow_Ring");
            timerGlow.transform.SetParent(timerObj.transform, false);
            var tGlowRt = timerGlow.AddComponent<RectTransform>();
            tGlowRt.anchorMin = Vector2.zero;
            tGlowRt.anchorMax = Vector2.one;
            tGlowRt.sizeDelta = Vector2.zero;
            var tGlowImg = timerGlow.AddComponent<Image>();
            tGlowImg.color = new Color(0.85f, 0.65f, 0.1f, 0.9f); // Amber Gold
            tGlowImg.type = Image.Type.Filled;
            tGlowImg.fillMethod = Image.FillMethod.Radial360;
            tGlowImg.fillAmount = 1f;

            Text timerText = CreatePremiumText("Text_TimerValue", timerObj.transform, "30", 32, Color.white, TextAnchor.MiddleCenter, new Vector2(90f, 90f), Vector3.zero);
            timerText.fontStyle = FontStyle.Bold;

            // 3. Inject Visual References to NetworkedCombatHUD Script
            var hudType = typeof(NetworkedCombatHUD);
            hudType.GetField("turnTimerText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(hudComp, timerText);
            hudType.GetField("turnTimerGlowRing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(hudComp, tGlowImg);
            hudType.GetField("bossHPSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(hudComp, bossHPSlider);
            hudType.GetField("bossHPText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(hudComp, bossHPText);

            Debug.Log("[BossFightArenaSceneBuilder] Successfully constructed and wired NetworkedCombatHUD with Boss HP Bar.");

            // 8. Save the newly built scene cleanly to disk
            EditorSceneManager.SaveScene(newScene, scenePath);
            
            // 8.5. Add scene to Build Settings if not already registered
            var buildScenes = EditorBuildSettings.scenes;
            bool alreadyAdded = false;
            foreach (var buildScene in buildScenes)
            {
                if (buildScene.path == scenePath)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                var newBuildScenes = new EditorBuildSettingsScene[buildScenes.Length + 1];
                for (int i = 0; i < buildScenes.Length; i++)
                {
                    newBuildScenes[i] = buildScenes[i];
                }
                newBuildScenes[buildScenes.Length] = new EditorBuildSettingsScene(scenePath, true);
                EditorBuildSettings.scenes = newBuildScenes;
                Debug.Log($"[BossFightArenaSceneBuilder] Registered scene '{scenePath}' in Build Settings.");
            }

            // 9. Open the scene automatically
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            Debug.Log($"[BossFightArenaSceneBuilder] ¡Escena de Combate Boss 'BossFight_Arena' y Network HUD creados exitosamente!");
        }

        // ── Helper UI Builders ───────────────────────────────────────────────────

        private static Slider CreatePremiumSlider(string name, Transform parent, Color fillColor, Vector2 size, Vector3 localPosition)
        {
            GameObject sliderObj = new GameObject(name);
            sliderObj.transform.SetParent(parent, false);
            var rt = sliderObj.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.localPosition = localPosition;

            // Background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            var bgRt = bgObj.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            var bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.12f, 0.15f, 0.85f);

            // Fill Area
            GameObject fillAreaObj = new GameObject("Fill Area");
            fillAreaObj.transform.SetParent(sliderObj.transform, false);
            var faRt = fillAreaObj.AddComponent<RectTransform>();
            faRt.anchorMin = Vector2.zero;
            faRt.anchorMax = Vector2.one;
            faRt.sizeDelta = Vector2.zero;

            // Fill
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillAreaObj.transform, false);
            var fillRt = fillObj.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = Vector2.zero;
            var fillImg = fillObj.AddComponent<Image>();
            fillImg.color = fillColor;

            var slider = sliderObj.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.targetGraphic = fillImg;
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0;
            slider.maxValue = 100;
            slider.value = 100;

            return slider;
        }

        private static Text CreatePremiumText(string name, Transform parent, string text, int fontSize, Color color, TextAnchor alignment, Vector2 size, Vector3 localPosition)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);
            var rt = textObj.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.localPosition = localPosition;

            var txt = textObj.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = text;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = alignment;

            return txt;
        }

        private static void AssignGlobalObjectIdHash(NetworkObject netObj, Scene scene)
        {
            // Removed in favor of robust PrefabUtility.InstantiatePrefab workflow
        }
    }
}
