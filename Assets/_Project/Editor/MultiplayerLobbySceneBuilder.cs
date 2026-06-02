using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Runefall.Presentation.Network;

namespace Runefall.Editor
{
    /// <summary>
    /// Editor script to generate the 3D Multiplayer Lobby Scene programmatically.
    /// Spawns walls, columns, floor, Huge Boss Door, interactive proximity triggers,
    /// a first-person Player setup, lighting, and wires the entire canvas hierarchy automatically.
    /// </summary>
    public class MultiplayerLobbySceneBuilder : EditorWindow
    {
        [MenuItem("Runefalls/Build Lobby Scene")]
        public static void BuildLobbyScene()
        {
            string scenePath = "Assets/_Project/Scenes/Scene_MultiplayerLobby.unity";
            var activeScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

            // 1. Clean existing scene duplicates for a pristine rebuild
            var existingPlayer = GameObject.Find("LobbyPlayer");
            if (existingPlayer != null) DestroyImmediate(existingPlayer);
            
            var existingEnvironment = GameObject.Find("LobbyEnvironment");
            if (existingEnvironment != null) DestroyImmediate(existingEnvironment);

            var existingUI = GameObject.Find("Lobby_NetworkUI");
            if (existingUI != null) DestroyImmediate(existingUI);

            var existingLight = GameObject.Find("Lobby_DirectionalLight");
            if (existingLight != null) DestroyImmediate(existingLight);

            var existingNetworkManager = GameObject.Find("NetworkManager");
            if (existingNetworkManager != null) DestroyImmediate(existingNetworkManager);

            var existingMultiplayerManager = GameObject.Find("MultiplayerManager");
            if (existingMultiplayerManager != null) DestroyImmediate(existingMultiplayerManager);

            // 2. Create Environment Root
            GameObject envParent = new GameObject("LobbyEnvironment");

            // Floor
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(envParent.transform);
            floor.transform.position = new Vector3(0, -0.5f, 0);
            floor.transform.localScale = new Vector3(20, 1, 20);
            var floorRenderer = floor.GetComponent<Renderer>();
            if (floorRenderer != null)
            {
                floorRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                floorRenderer.sharedMaterial.color = new Color(0.12f, 0.12f, 0.15f); // Deep obsidian gray
            }

            // Walls (North, South, East, West)
            string[] wallNames = { "Wall_North", "Wall_South", "Wall_East", "Wall_West" };
            Vector3[] wallPositions = {
                new Vector3(0, 3, 10), new Vector3(0, 3, -10),
                new Vector3(10, 3, 0), new Vector3(-10, 3, 0)
            };
            Vector3[] wallScales = {
                new Vector3(20, 6, 1), new Vector3(20, 6, 1),
                new Vector3(1, 6, 20), new Vector3(1, 6, 20)
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
                    wallRenderer.sharedMaterial.color = new Color(0.18f, 0.18f, 0.22f); // Medium stone gray
                }
            }

            // Column Pillars (Cylinders in corners)
            Vector3[] colPositions = {
                new Vector3(-8.5f, 3, -8.5f), new Vector3(8.5f, 3, -8.5f),
                new Vector3(-8.5f, 3, 8.5f), new Vector3(8.5f, 3, 8.5f)
            };
            for (int i = 0; i < 4; i++)
            {
                GameObject col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                col.name = $"Column_Pillar_{i}";
                col.transform.SetParent(envParent.transform);
                col.transform.position = colPositions[i];
                col.transform.localScale = new Vector3(1.2f, 3.0f, 1.2f);
                var colRenderer = col.GetComponent<Renderer>();
                if (colRenderer != null)
                {
                    colRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    colRenderer.sharedMaterial.color = new Color(0.28f, 0.28f, 0.32f); // Ancient gray
                }
            }

            // Huge Boss Door
            GameObject bossDoor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bossDoor.name = "HugeBossDoor";
            bossDoor.transform.SetParent(envParent.transform);
            bossDoor.transform.position = new Vector3(0, 3, 9.4f);
            bossDoor.transform.localScale = new Vector3(5, 6, 0.4f);
            var doorRenderer = bossDoor.GetComponent<Renderer>();
            if (doorRenderer != null)
            {
                doorRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                doorRenderer.sharedMaterial.color = new Color(0.05f, 0.05f, 0.05f); // Intimidating dark gates
            }

            // --- DECORACIONES INMERSIVAS (Altar Central y Braseros) ---
            // A. Altar de Runas Central
            GameObject altar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            altar.name = "Rune_Altar_Center";
            altar.transform.SetParent(envParent.transform);
            altar.transform.position = new Vector3(0, 0.2f, 0);
            altar.transform.localScale = new Vector3(3.0f, 0.25f, 3.0f);
            var altarRenderer = altar.GetComponent<Renderer>();
            if (altarRenderer != null)
            {
                altarRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                altarRenderer.sharedMaterial.color = new Color(0.20f, 0.20f, 0.25f); // Stone gray
            }

            // B. Cristal Rúnico Flotante en el Centro
            GameObject runeCrystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            runeCrystal.name = "Floating_Rune_Crystal";
            runeCrystal.transform.SetParent(envParent.transform);
            runeCrystal.transform.position = new Vector3(0, 1.4f, 0);
            runeCrystal.transform.localScale = new Vector3(0.5f, 0.8f, 0.5f);
            runeCrystal.transform.rotation = Quaternion.Euler(45, 45, 45); // Rotated to look octaedral
            var crystalRenderer = runeCrystal.GetComponent<Renderer>();
            if (crystalRenderer != null)
            {
                crystalRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                crystalRenderer.sharedMaterial.color = new Color(0.1f, 0.75f, 0.85f); // Cyan místico brillante
                crystalRenderer.sharedMaterial.EnableKeyword("_EMISSION");
                crystalRenderer.sharedMaterial.SetColor("_EmissionColor", new Color(0.05f, 0.35f, 0.45f));
            }

            // C. Braseros de Fuego Sagrado a los lados de la Puerta del Boss
            Vector3[] braseroPositions = { new Vector3(-3.5f, 1.0f, 8.8f), new Vector3(3.5f, 1.0f, 8.8f) };
            string[] braseroNames = { "Left_Boss_Brazer", "Right_Boss_Brazer" };

            for (int i = 0; i < 2; i++)
            {
                // Pedestal
                GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pedestal.name = braseroNames[i] + "_Pedestal";
                pedestal.transform.SetParent(envParent.transform);
                pedestal.transform.position = braseroPositions[i];
                pedestal.transform.localScale = new Vector3(0.6f, 1.0f, 0.6f);
                var pedRenderer = pedestal.GetComponent<Renderer>();
                if (pedRenderer != null)
                {
                    pedRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    pedRenderer.sharedMaterial.color = new Color(0.25f, 0.25f, 0.28f);
                }

                // Gema de Fuego (Brasero superior)
                GameObject flameOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flameOrb.name = braseroNames[i] + "_Flame";
                flameOrb.transform.SetParent(envParent.transform);
                flameOrb.transform.position = braseroPositions[i] + new Vector3(0, 1.1f, 0);
                flameOrb.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
                var flameRenderer = flameOrb.GetComponent<Renderer>();
                if (flameRenderer != null)
                {
                    flameRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    flameRenderer.sharedMaterial.color = new Color(0.95f, 0.35f, 0.1f); // Orange fire
                    flameRenderer.sharedMaterial.EnableKeyword("_EMISSION");
                    flameRenderer.sharedMaterial.SetColor("_EmissionColor", new Color(0.45f, 0.15f, 0.05f));
                }
            }

            // 3. Create Network UI Canvas with styled premium panels
            GameObject uiCanvas = new GameObject("Lobby_NetworkUI");
            var canvas = uiCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            var scaler = uiCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            uiCanvas.AddComponent<GraphicRaycaster>();
            
            // Add UI logic components
            var lobbyView = uiCanvas.AddComponent<LobbyView>();
            var lobbyPresenter = uiCanvas.AddComponent<LobbyPresenter>();

            // Universal Dark Panel Material/Color
            Color panelColor = new Color(0.06f, 0.06f, 0.08f, 0.94f); // Deep obsidian semi-transparent
            Color buttonGreen = new Color(0.08f, 0.45f, 0.25f, 1f); // Vibrant emerald
            Color buttonBlue = new Color(0.1f, 0.35f, 0.55f, 1f); // Safe deep ocean blue
            Color buttonGold = new Color(0.85f, 0.55f, 0.1f, 1f); // Golden yellow

            // ==========================================
            // A. CREATE PANEL MENU (Main Panel)
            // ==========================================
            GameObject panelMenu = new GameObject("Panel_Menu");
            panelMenu.transform.SetParent(uiCanvas.transform, false);
            var menuRt = panelMenu.AddComponent<RectTransform>();
            menuRt.anchorMin = menuRt.anchorMax = new Vector2(0.5f, 0.5f);
            menuRt.pivot = new Vector2(0.5f, 0.5f);
            menuRt.sizeDelta = new Vector2(600, 450);
            var menuImg = panelMenu.AddComponent<Image>();
            menuImg.color = panelColor;

            // Panel Title
            GameObject titleMenuObj = new GameObject("Text_Title");
            titleMenuObj.transform.SetParent(panelMenu.transform, false);
            var titleMenuRt = titleMenuObj.AddComponent<RectTransform>();
            titleMenuRt.anchoredPosition = new Vector2(0, 150);
            titleMenuRt.sizeDelta = new Vector2(500, 50);
            var titleMenuTxt = titleMenuObj.AddComponent<TMPro.TextMeshProUGUI>();
            titleMenuTxt.text = "ANTECÁMARA DEL JEFE";
            titleMenuTxt.fontSize = 32;
            titleMenuTxt.fontStyle = TMPro.FontStyles.Bold;
            titleMenuTxt.color = Color.white;
            titleMenuTxt.alignment = TMPro.TextAlignmentOptions.Center;

            // Subtitle
            GameObject subtitleMenuObj = new GameObject("Text_Subtitle");
            subtitleMenuObj.transform.SetParent(panelMenu.transform, false);
            var subMenuRt = subtitleMenuObj.AddComponent<RectTransform>();
            subMenuRt.anchoredPosition = new Vector2(0, 110);
            subMenuRt.sizeDelta = new Vector2(500, 30);
            var subMenuTxt = subtitleMenuObj.AddComponent<TMPro.TextMeshProUGUI>();
            subMenuTxt.text = "Elige tu modo de combate cooperativo";
            subMenuTxt.fontSize = 16;
            subMenuTxt.color = new Color(0.7f, 0.7f, 0.75f);
            subMenuTxt.alignment = TMPro.TextAlignmentOptions.Center;

            // Button Close UI ("X" Button in upper-right corner of the panel)
            GameObject btnCloseObj = new GameObject("Button_Close");
            btnCloseObj.transform.SetParent(panelMenu.transform, false);
            var btnCloseRt = btnCloseObj.AddComponent<RectTransform>();
            btnCloseRt.anchorMin = btnCloseRt.anchorMax = new Vector2(1f, 1f);
            btnCloseRt.pivot = new Vector2(1f, 1f);
            btnCloseRt.anchoredPosition = new Vector2(-15, -15);
            btnCloseRt.sizeDelta = new Vector2(40, 40);
            var btnCloseImg = btnCloseObj.AddComponent<Image>();
            btnCloseImg.color = new Color(0.35f, 0.12f, 0.15f, 0.95f); // Crimson red
            var buttonClose = btnCloseObj.AddComponent<Button>();

            GameObject btnCloseTextObj = new GameObject("Text");
            btnCloseTextObj.transform.SetParent(btnCloseObj.transform, false);
            var btnCloseTextRt = btnCloseTextObj.AddComponent<RectTransform>();
            btnCloseTextRt.anchorMin = btnCloseTextRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnCloseTextRt.sizeDelta = new Vector2(30, 30);
            var btnCloseTxt = btnCloseTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            btnCloseTxt.text = "X";
            btnCloseTxt.fontSize = 20;
            btnCloseTxt.fontStyle = TMPro.FontStyles.Bold;
            btnCloseTxt.color = Color.white;
            btnCloseTxt.alignment = TMPro.TextAlignmentOptions.Center;

            // Button Create Match
            GameObject btnCreateObj = new GameObject("Button_Create");
            btnCreateObj.transform.SetParent(panelMenu.transform, false);
            var btnCreateRt = btnCreateObj.AddComponent<RectTransform>();
            btnCreateRt.anchoredPosition = new Vector2(0, 30);
            btnCreateRt.sizeDelta = new Vector2(320, 60);
            var btnCreateImg = btnCreateObj.AddComponent<Image>();
            btnCreateImg.color = buttonGreen;
            var buttonCreate = btnCreateObj.AddComponent<Button>();

            GameObject btnCreateTextObj = new GameObject("Text");
            btnCreateTextObj.transform.SetParent(btnCreateObj.transform, false);
            var btnCreateTextRt = btnCreateTextObj.AddComponent<RectTransform>();
            btnCreateTextRt.sizeDelta = new Vector2(300, 40);
            var btnCreateTxt = btnCreateTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            btnCreateTxt.text = "CREAR PARTIDA (HOST)";
            btnCreateTxt.fontSize = 18;
            btnCreateTxt.fontStyle = TMPro.FontStyles.Bold;
            btnCreateTxt.color = Color.white;
            btnCreateTxt.alignment = TMPro.TextAlignmentOptions.Center;

            // InputField Room Code
            GameObject inputCodeObj = new GameObject("InputField_Code");
            inputCodeObj.transform.SetParent(panelMenu.transform, false);
            var inputCodeRt = inputCodeObj.AddComponent<RectTransform>();
            inputCodeRt.anchoredPosition = new Vector2(0, -60);
            inputCodeRt.sizeDelta = new Vector2(320, 45);
            var inputCodeImg = inputCodeObj.AddComponent<Image>();
            inputCodeImg.color = new Color(0.12f, 0.12f, 0.15f, 1f);

            var inputFieldCode = inputCodeObj.AddComponent<TMPro.TMP_InputField>();
            
            // Text area for input
            GameObject textArea = new GameObject("TextArea");
            textArea.transform.SetParent(inputCodeObj.transform, false);
            var textAreaRt = textArea.AddComponent<RectTransform>();
            textAreaRt.anchorMin = Vector2.zero;
            textAreaRt.anchorMax = Vector2.one;
            textAreaRt.offsetMin = new Vector2(10, 5);
            textAreaRt.offsetMax = new Vector2(-10, -5);

            GameObject textPlaceholderObj = new GameObject("Placeholder");
            textPlaceholderObj.transform.SetParent(textArea.transform, false);
            var placeholderRt = textPlaceholderObj.AddComponent<RectTransform>();
            placeholderRt.anchorMin = placeholderRt.anchorMax = new Vector2(0.5f, 0.5f);
            placeholderRt.sizeDelta = new Vector2(300, 35);
            var placeholderTxt = textPlaceholderObj.AddComponent<TMPro.TextMeshProUGUI>();
            placeholderTxt.text = "Ingresar código de sala...";
            placeholderTxt.fontSize = 16;
            placeholderTxt.fontStyle = TMPro.FontStyles.Italic;
            placeholderTxt.color = new Color(0.5f, 0.5f, 0.5f);
            placeholderTxt.alignment = TMPro.TextAlignmentOptions.Left;

            GameObject textInputObj = new GameObject("Text");
            textInputObj.transform.SetParent(textArea.transform, false);
            var textInputRt = textInputObj.AddComponent<RectTransform>();
            textInputRt.anchorMin = placeholderRt.anchorMax = new Vector2(0.5f, 0.5f);
            textInputRt.sizeDelta = new Vector2(300, 35);
            var textInputTxt = textInputObj.AddComponent<TMPro.TextMeshProUGUI>();
            textInputTxt.text = "";
            textInputTxt.fontSize = 18;
            textInputTxt.color = Color.white;
            textInputTxt.alignment = TMPro.TextAlignmentOptions.Left;

            inputFieldCode.textViewport = textAreaRt;
            inputFieldCode.textComponent = textInputTxt;
            inputFieldCode.placeholder = placeholderTxt;

            // Button Join Match
            GameObject btnJoinObj = new GameObject("Button_Join");
            btnJoinObj.transform.SetParent(panelMenu.transform, false);
            var btnJoinRt = btnJoinObj.AddComponent<RectTransform>();
            btnJoinRt.anchoredPosition = new Vector2(0, -125);
            btnJoinRt.sizeDelta = new Vector2(320, 50);
            var btnJoinImg = btnJoinObj.AddComponent<Image>();
            btnJoinImg.color = buttonBlue;
            var buttonJoin = btnJoinObj.AddComponent<Button>();

            GameObject btnJoinTextObj = new GameObject("Text");
            btnJoinTextObj.transform.SetParent(btnJoinObj.transform, false);
            var btnJoinTextRt = btnJoinTextObj.AddComponent<RectTransform>();
            btnJoinTextRt.sizeDelta = new Vector2(300, 35);
            var btnJoinTxt = btnJoinTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            btnJoinTxt.text = "UNIRSE POR CÓDIGO";
            btnJoinTxt.fontSize = 16;
            btnJoinTxt.fontStyle = TMPro.FontStyles.Bold;
            btnJoinTxt.color = Color.white;
            btnJoinTxt.alignment = TMPro.TextAlignmentOptions.Center;

            // ==========================================
            // B. CREATE PANEL WAITING ROOM
            // ==========================================
            GameObject panelWaiting = new GameObject("Panel_WaitingRoom");
            panelWaiting.transform.SetParent(uiCanvas.transform, false);
            var waitingRt = panelWaiting.AddComponent<RectTransform>();
            waitingRt.anchorMin = waitingRt.anchorMax = new Vector2(0.5f, 0.5f);
            waitingRt.pivot = new Vector2(0.5f, 0.5f);
            waitingRt.sizeDelta = new Vector2(600, 450);
            var waitingImg = panelWaiting.AddComponent<Image>();
            waitingImg.color = panelColor;

            // Title Join Code
            GameObject textJoinCodeObj = new GameObject("Text_JoinCode");
            textJoinCodeObj.transform.SetParent(panelWaiting.transform, false);
            var joinCodeRt = textJoinCodeObj.AddComponent<RectTransform>();
            joinCodeRt.anchoredPosition = new Vector2(0, 140);
            joinCodeRt.sizeDelta = new Vector2(500, 50);
            var textJoinCode = textJoinCodeObj.AddComponent<TMPro.TextMeshProUGUI>();
            textJoinCode.text = "CÓDIGO DE SALA: XXXXXX";
            textJoinCode.fontSize = 26;
            textJoinCode.fontStyle = TMPro.FontStyles.Bold;
            textJoinCode.color = buttonGold;
            textJoinCode.alignment = TMPro.TextAlignmentOptions.Center;

            // Player count text
            GameObject textPlayerCountObj = new GameObject("Text_PlayerCount");
            textPlayerCountObj.transform.SetParent(panelWaiting.transform, false);
            var pCountRt = textPlayerCountObj.AddComponent<RectTransform>();
            pCountRt.anchoredPosition = new Vector2(0, 80);
            pCountRt.sizeDelta = new Vector2(500, 35);
            var textPlayerCount = textPlayerCountObj.AddComponent<TMPro.TextMeshProUGUI>();
            textPlayerCount.text = "JUGADORES EN SALA: 1 / 2";
            textPlayerCount.fontSize = 18;
            textPlayerCount.color = Color.white;
            textPlayerCount.alignment = TMPro.TextAlignmentOptions.Center;

            // Status Player 1 (Host)
            GameObject textP1StatusObj = new GameObject("Text_Player1Status");
            textP1StatusObj.transform.SetParent(panelWaiting.transform, false);
            var p1Rt = textP1StatusObj.AddComponent<RectTransform>();
            p1Rt.anchoredPosition = new Vector2(0, 20);
            p1Rt.sizeDelta = new Vector2(400, 30);
            var textPlayer1Status = textP1StatusObj.AddComponent<TMPro.TextMeshProUGUI>();
            textPlayer1Status.text = "Jugador 1 (Host): LISTO";
            textPlayer1Status.fontSize = 16;
            textPlayer1Status.alignment = TMPro.TextAlignmentOptions.Center;

            // Status Player 2 (Client)
            GameObject textP2StatusObj = new GameObject("Text_Player2Status");
            textP2StatusObj.transform.SetParent(panelWaiting.transform, false);
            var p2Rt = textP2StatusObj.AddComponent<RectTransform>();
            p2Rt.anchoredPosition = new Vector2(0, -20);
            p2Rt.sizeDelta = new Vector2(400, 30);
            var textPlayer2Status = textP2StatusObj.AddComponent<TMPro.TextMeshProUGUI>();
            textPlayer2Status.text = "Jugador 2: ESPERANDO...";
            textPlayer2Status.fontSize = 16;
            textPlayer2Status.alignment = TMPro.TextAlignmentOptions.Center;

            // Button Start Game
            GameObject btnStartObj = new GameObject("Button_StartGame");
            btnStartObj.transform.SetParent(panelWaiting.transform, false);
            var btnStartRt = btnStartObj.AddComponent<RectTransform>();
            btnStartRt.anchoredPosition = new Vector2(0, -165);
            btnStartRt.sizeDelta = new Vector2(320, 60);
            var btnStartImg = btnStartObj.AddComponent<Image>();
            btnStartImg.color = buttonGold;
            var buttonStartGame = btnStartObj.AddComponent<Button>();

            GameObject btnStartTextObj = new GameObject("Text");
            btnStartTextObj.transform.SetParent(btnStartObj.transform, false);
            var btnStartTextRt = btnStartTextObj.AddComponent<RectTransform>();
            btnStartTextRt.sizeDelta = new Vector2(300, 40);
            var btnStartTxt = btnStartTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            btnStartTxt.text = "INICIAR COMBATE";
            btnStartTxt.fontSize = 20;
            btnStartTxt.fontStyle = TMPro.FontStyles.Bold;
            btnStartTxt.color = Color.white;
            btnStartTxt.alignment = TMPro.TextAlignmentOptions.Center;

            // Button Ready (Listo)
            GameObject btnReadyObj = new GameObject("Button_Ready");
            btnReadyObj.transform.SetParent(panelWaiting.transform, false);
            var btnReadyRt = btnReadyObj.AddComponent<RectTransform>();
            btnReadyRt.anchoredPosition = new Vector2(110, -95);
            btnReadyRt.sizeDelta = new Vector2(200, 50);
            var btnReadyImg = btnReadyObj.AddComponent<Image>();
            btnReadyImg.color = new Color(0.08f, 0.45f, 0.25f, 1f); // Emerald Green
            var buttonReady = btnReadyObj.AddComponent<Button>();

            GameObject btnReadyTextObj = new GameObject("Text");
            btnReadyTextObj.transform.SetParent(btnReadyObj.transform, false);
            var btnReadyTextRt = btnReadyTextObj.AddComponent<RectTransform>();
            btnReadyTextRt.sizeDelta = new Vector2(180, 35);
            var btnReadyTxt = btnReadyTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            btnReadyTxt.text = "LISTO";
            btnReadyTxt.fontSize = 16;
            btnReadyTxt.fontStyle = TMPro.FontStyles.Bold;
            btnReadyTxt.color = Color.white;
            btnReadyTxt.alignment = TMPro.TextAlignmentOptions.Center;

            // Button Cancel Prep (Cancelar)
            GameObject btnCancelObj = new GameObject("Button_CancelPrep");
            btnCancelObj.transform.SetParent(panelWaiting.transform, false);
            var btnCancelRt = btnCancelObj.AddComponent<RectTransform>();
            btnCancelRt.anchoredPosition = new Vector2(-110, -95);
            btnCancelRt.sizeDelta = new Vector2(200, 50);
            var btnCancelImg = btnCancelObj.AddComponent<Image>();
            btnCancelImg.color = new Color(0.55f, 0.15f, 0.15f, 1f); // Dark Red
            var buttonCancelPrep = btnCancelObj.AddComponent<Button>();

            GameObject btnCancelTextObj = new GameObject("Text");
            btnCancelTextObj.transform.SetParent(btnCancelObj.transform, false);
            var btnCancelTextRt = btnCancelTextObj.AddComponent<RectTransform>();
            btnCancelTextRt.sizeDelta = new Vector2(180, 35);
            var btnCancelTxt = btnCancelTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            btnCancelTxt.text = "CANCELAR";
            btnCancelTxt.fontSize = 16;
            btnCancelTxt.fontStyle = TMPro.FontStyles.Bold;
            btnCancelTxt.color = Color.white;
            btnCancelTxt.alignment = TMPro.TextAlignmentOptions.Center;

            // Button Leave Lobby (Salir) - upper left corner
            GameObject btnLeaveObj = new GameObject("Button_LeaveLobby");
            btnLeaveObj.transform.SetParent(panelWaiting.transform, false);
            var btnLeaveRt = btnLeaveObj.AddComponent<RectTransform>();
            btnLeaveRt.anchorMin = new Vector2(0f, 1f); // Anchor top-left
            btnLeaveRt.anchorMax = new Vector2(0f, 1f);
            btnLeaveRt.pivot = new Vector2(0f, 1f);
            btnLeaveRt.anchoredPosition = new Vector2(15, -15);
            btnLeaveRt.sizeDelta = new Vector2(140, 40);
            var btnLeaveImg = btnLeaveObj.AddComponent<Image>();
            btnLeaveImg.color = new Color(0.35f, 0.12f, 0.15f, 0.95f); // Crimson red
            var buttonLeaveLobby = btnLeaveObj.AddComponent<Button>();

            GameObject btnLeaveTextObj = new GameObject("Text");
            btnLeaveTextObj.transform.SetParent(btnLeaveObj.transform, false);
            var btnLeaveTextRt = btnLeaveTextObj.AddComponent<RectTransform>();
            btnLeaveTextRt.sizeDelta = new Vector2(120, 30);
            var btnLeaveTxt = btnLeaveTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            btnLeaveTxt.text = "SALIR";
            btnLeaveTxt.fontSize = 14;
            btnLeaveTxt.fontStyle = TMPro.FontStyles.Bold;
            btnLeaveTxt.color = Color.white;
            btnLeaveTxt.alignment = TMPro.TextAlignmentOptions.Center;

            // ==========================================
            // C. CREATE LOADING OVERLAY
            // ==========================================
            GameObject loadingOverlay = new GameObject("Panel_LoadingOverlay");
            loadingOverlay.transform.SetParent(uiCanvas.transform, false);
            var loadRt = loadingOverlay.AddComponent<RectTransform>();
            loadRt.anchorMin = Vector2.zero;
            loadRt.anchorMax = Vector2.one;
            loadRt.offsetMin = loadRt.offsetMax = Vector2.zero;
            var loadImg = loadingOverlay.AddComponent<Image>();
            loadImg.color = new Color(0.04f, 0.04f, 0.05f, 0.96f);

            GameObject loadTextObj = new GameObject("Text");
            loadTextObj.transform.SetParent(loadingOverlay.transform, false);
            var loadTextRt = loadTextObj.AddComponent<RectTransform>();
            loadTextRt.sizeDelta = new Vector2(600, 50);
            var loadTxt = loadTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            loadTxt.text = "CONECTANDO A LA NUBE DE RUNEFALLS...";
            loadTxt.fontSize = 20;
            loadTxt.fontStyle = TMPro.FontStyles.Bold;
            loadTxt.color = buttonGold;
            loadTxt.alignment = TMPro.TextAlignmentOptions.Center;

            // ==========================================
            // D. INJECT ALL UI REFERENCES INTO LOBBYVIEW
            // ==========================================
            var viewType = typeof(LobbyView);
            viewType.GetField("panelMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(lobbyView, panelMenu);
            viewType.GetField("panelWaitingRoom", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(lobbyView, panelWaiting);
            viewType.GetField("buttonCreate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(lobbyView, buttonCreate);
            viewType.GetField("buttonJoin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(lobbyView, buttonJoin);
            viewType.GetField("inputFieldCode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(lobbyView, inputFieldCode);
            viewType.GetField("textJoinCode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(lobbyView, textJoinCode);
            viewType.GetField("textPlayerCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(lobbyView, textPlayerCount);
            viewType.GetField("textPlayer1Status", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(lobbyView, textPlayer1Status);
            viewType.GetField("textPlayer2Status", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(lobbyView, textPlayer2Status);
            viewType.GetField("buttonStartGame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(lobbyView, buttonStartGame);
            viewType.GetField("buttonClose", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(lobbyView, buttonClose);
            viewType.GetField("loadingOverlay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(lobbyView, loadingOverlay);
            viewType.GetField("buttonReady", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(lobbyView, buttonReady);
            viewType.GetField("buttonCancelPrep", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(lobbyView, buttonCancelPrep);
            viewType.GetField("buttonLeaveLobby", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(lobbyView, buttonLeaveLobby);

            // Re-call initialization inside LobbyView now that references are in place
            lobbyView.ShowMainMenu();

            // 4. Create Interactive Proximity Trigger
            GameObject doorTrigger = new GameObject("BossDoorTrigger");
            doorTrigger.transform.SetParent(envParent.transform);
            doorTrigger.transform.position = new Vector3(0, 1.5f, 7.5f);
            var boxCol = doorTrigger.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector3(6, 4, 4);

            var triggerScript = doorTrigger.AddComponent<BossRoomDoorTrigger>();
            typeof(BossRoomDoorTrigger).GetField("lobbyUICanvas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(triggerScript, uiCanvas);

            // 4.5 Create WaitRoomAssembler, Player Slots and Camera Anchor
            var waitRoomAssembler = envParent.AddComponent<WaitRoomAssembler>();

            GameObject slotLeft = new GameObject("Slot_Left");
            slotLeft.transform.SetParent(envParent.transform);
            slotLeft.transform.position = new Vector3(-1.5f, 1.0f, 4.5f);
            slotLeft.transform.rotation = Quaternion.identity;

            GameObject slotRight = new GameObject("Slot_Right");
            slotRight.transform.SetParent(envParent.transform);
            slotRight.transform.position = new Vector3(1.5f, 1.0f, 4.5f);
            slotRight.transform.rotation = Quaternion.identity;

            GameObject prepCameraAnchor = new GameObject("PrepCameraAnchor");
            prepCameraAnchor.transform.SetParent(envParent.transform);
            prepCameraAnchor.transform.position = new Vector3(-0.47f, 3.78f, -1.9f);
            prepCameraAnchor.transform.rotation = Quaternion.Euler(19.1f, -2.3f, 0.3f);

            // ==========================================
            // BUILD PHYSICAL PLAYER SLOT VISUAL INDICATORS
            // ==========================================
            Color rightCyan = new Color(0f, 0.6f, 0.9f, 0.8f);
            Color leftGold = new Color(0.95f, 0.55f, 0.1f, 0.8f);

            // 1. Right slot visual platform (Cyan glow)
            GameObject slotRightVisualGroup = new GameObject("Slot_Right_VisualGroup");
            slotRightVisualGroup.transform.SetParent(slotRight.transform, false);

            GameObject cylRight = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylRight.name = "Right_Platform_Cylinder";
            cylRight.transform.SetParent(slotRightVisualGroup.transform, false);
            cylRight.transform.localPosition = new Vector3(0f, -0.98f, 0f); // Set flat on floor (Y = 0.02f)
            cylRight.transform.localScale = new Vector3(1.4f, 0.02f, 1.4f);
            var rightRenderer = cylRight.GetComponent<Renderer>();
            if (rightRenderer != null)
            {
                rightRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                rightRenderer.sharedMaterial.color = rightCyan;
                rightRenderer.sharedMaterial.EnableKeyword("_EMISSION");
                rightRenderer.sharedMaterial.SetColor("_EmissionColor", new Color(0f, 0.3f, 0.45f));
            }
            var capColRight = cylRight.GetComponent<CapsuleCollider>();
            if (capColRight != null) DestroyImmediate(capColRight);

            // 2. Left slot visual platform (Gold/Orange glow)
            GameObject slotLeftVisualGroup = new GameObject("Slot_Left_VisualGroup");
            slotLeftVisualGroup.transform.SetParent(slotLeft.transform, false);

            GameObject cylLeft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylLeft.name = "Left_Platform_Cylinder";
            cylLeft.transform.SetParent(slotLeftVisualGroup.transform, false);
            cylLeft.transform.localPosition = new Vector3(0f, -0.98f, 0f);
            cylLeft.transform.localScale = new Vector3(1.4f, 0.02f, 1.4f);
            var leftRenderer = cylLeft.GetComponent<Renderer>();
            if (leftRenderer != null)
            {
                leftRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                leftRenderer.sharedMaterial.color = leftGold;
                leftRenderer.sharedMaterial.EnableKeyword("_EMISSION");
                leftRenderer.sharedMaterial.SetColor("_EmissionColor", new Color(0.45f, 0.18f, 0.02f));
            }
            var capColLeft = cylLeft.GetComponent<CapsuleCollider>();
            if (capColLeft != null) DestroyImmediate(capColLeft);

            // 3. Floating Invite "+" Sign on the Left Slot
            GameObject slotLeftPlus = new GameObject("Slot_Left_PlusSign");
            slotLeftPlus.transform.SetParent(slotLeftVisualGroup.transform, false);
            slotLeftPlus.transform.localPosition = new Vector3(0f, -0.1f, 0f); // Chest level (Y = 0.9f)

            GameObject horizontalBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            horizontalBar.name = "Bar_H";
            horizontalBar.transform.SetParent(slotLeftPlus.transform, false);
            horizontalBar.transform.localScale = new Vector3(0.4f, 0.08f, 0.08f);
            var hRenderer = horizontalBar.GetComponent<Renderer>();
            if (hRenderer != null) hRenderer.sharedMaterial = leftRenderer.sharedMaterial;
            var boxColH = horizontalBar.GetComponent<BoxCollider>();
            if (boxColH != null) DestroyImmediate(boxColH);

            GameObject verticalBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            verticalBar.name = "Bar_V";
            verticalBar.transform.SetParent(slotLeftPlus.transform, false);
            verticalBar.transform.localScale = new Vector3(0.08f, 0.4f, 0.08f);
            var vRenderer = verticalBar.GetComponent<Renderer>();
            if (vRenderer != null) vRenderer.sharedMaterial = leftRenderer.sharedMaterial;
            var boxColV = verticalBar.GetComponent<BoxCollider>();
            if (boxColV != null) DestroyImmediate(boxColV);

            // Wire references into WaitRoomAssembler via reflection
            var assemblerType = typeof(WaitRoomAssembler);
            assemblerType.GetField("altarCenter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(waitRoomAssembler, altar);
            assemblerType.GetField("floatingCrystal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(waitRoomAssembler, runeCrystal);
            assemblerType.GetField("slotLeft", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(waitRoomAssembler, slotLeft.transform);
            assemblerType.GetField("slotRight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(waitRoomAssembler, slotRight.transform);
            assemblerType.GetField("prepCameraAnchor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(waitRoomAssembler, prepCameraAnchor.transform);
            assemblerType.GetField("slotLeftVisual", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(waitRoomAssembler, slotLeftVisualGroup);
            assemblerType.GetField("slotRightVisual", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(waitRoomAssembler, slotRightVisualGroup);

            // Wire the WaitRoomAssembler into the LobbyPresenter
            typeof(LobbyPresenter).GetField("waitRoomAssembler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(lobbyPresenter, waitRoomAssembler);

            // 5. Create Local Player as a physical Capsule Primitive
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "LobbyPlayer";
            player.transform.position = new Vector3(0, 1f, -6f);
            
            // Remove the default CapsuleCollider since CharacterController has its own optimized collider
            var originalCollider = player.GetComponent<CapsuleCollider>();
            if (originalCollider != null) DestroyImmediate(originalCollider);

            // Add Rigidbody and set as Kinematic to guarantee trigger collision events fire correctly
            var rb = player.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            player.AddComponent<CharacterController>();
            var charController = player.AddComponent<SimpleCharacterController>();

            // Color the Player Capsule so it's beautifully visible in the editor
            var playerRenderer = player.GetComponent<Renderer>();
            if (playerRenderer != null)
            {
                playerRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                playerRenderer.sharedMaterial.color = new Color(0.1f, 0.6f, 0.9f); // Sleek cyan player body
            }

            GameObject camObj = new GameObject("LobbyCamera");
            camObj.transform.SetParent(player.transform, false);
            camObj.transform.localPosition = new Vector3(0, 1.8f, -3.5f); // Positioned behind the player for Third Person
            camObj.AddComponent<Camera>();
            
            typeof(SimpleCharacterController).GetField("playerCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(charController, camObj.transform);

            // 6. Create Ambient Directional Light
            GameObject lightObj = new GameObject("Lobby_DirectionalLight");
            var lightComp = lightObj.AddComponent<Light>();
            lightComp.type = LightType.Directional;
            lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
            lightComp.intensity = 1.0f;
            lightComp.color = new Color(0.85f, 0.9f, 1.0f); // Cool ambient daylight

            // 7. Create EventSystem required for UI button clicks to register
            var existingEventSystem = GameObject.Find("EventSystem");
            if (existingEventSystem != null) DestroyImmediate(existingEventSystem);

            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            // 7.5. Create NGO NetworkManager and MultiplayerManager Singletons if they don't exist
            GameObject networkManagerObj = new GameObject("NetworkManager");
            var netManager = networkManagerObj.AddComponent<NetworkManager>();
            var transport = networkManagerObj.AddComponent<UnityTransport>();
            
            // Wire the transport to the existing network manager config to preserve serialisation!
            netManager.NetworkConfig.NetworkTransport = transport;

            // 7.6. Load and register the NetworkedCombatPawn prefab (critical for NGO dynamic spawning)
            string pawnPrefabPath = "Assets/_Project/Prefabs/NetworkedCombatPawn.prefab";
            var pawnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(pawnPrefabPath);
            if (pawnPrefab != null)
            {
                // Create a NetworkPrefabsList scriptable object if not exists
                string listPath = "Assets/_Project/Prefabs/RuneFallsNetworkPrefabsList.asset";
                var prefabsList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(listPath);
                if (prefabsList == null)
                {
                    prefabsList = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                    AssetDatabase.CreateAsset(prefabsList, listPath);
                    Debug.Log("[MultiplayerLobbySceneBuilder] Created new NetworkPrefabsList asset at " + listPath);
                }
                
                // Add the prefab if it's not already in there
                bool alreadyInList = false;
                foreach (var item in prefabsList.PrefabList)
                {
                    if (item.Prefab == pawnPrefab)
                    {
                        alreadyInList = true;
                        break;
                    }
                }
                
                if (!alreadyInList)
                {
                    prefabsList.Add(new NetworkPrefab { Prefab = pawnPrefab });
                    EditorUtility.SetDirty(prefabsList);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[MultiplayerLobbySceneBuilder] Added NetworkedCombatPawn to NetworkPrefabsList.");
                }

                // Assign the list to the NetworkManager config NetworkPrefabsLists list using SerializedProperty to guarantee YAML serialization!
                var serializedManager = new SerializedObject(netManager);
                serializedManager.Update();
                var networkConfigProp = serializedManager.FindProperty("NetworkConfig");
                if (networkConfigProp != null)
                {
                    var prefabsProp = networkConfigProp.FindPropertyRelative("Prefabs");
                    if (prefabsProp != null)
                    {
                        var listsProp = prefabsProp.FindPropertyRelative("NetworkPrefabsLists");
                        if (listsProp != null)
                        {
                            listsProp.ClearArray();
                            listsProp.InsertArrayElementAtIndex(0);
                            var elementProp = listsProp.GetArrayElementAtIndex(0);
                            elementProp.objectReferenceValue = prefabsList;
                            serializedManager.ApplyModifiedProperties();
                            EditorUtility.SetDirty(netManager);
                            Debug.Log("[MultiplayerLobbySceneBuilder] Successfully registered and serialized NetworkPrefabsLists with NetworkedCombatPawn.");
                        }
                        else
                        {
                            Debug.LogWarning("[MultiplayerLobbySceneBuilder] Warning: NetworkPrefabsLists property not found in NetworkConfig.Prefabs.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[MultiplayerLobbySceneBuilder] Warning: Prefabs property not found in NetworkConfig.");
                    }
                }
                else
                {
                    Debug.LogWarning("[MultiplayerLobbySceneBuilder] Warning: NetworkConfig property not found in NetworkManager.");
                }
            }

            GameObject multiplayerManagerObj = new GameObject("MultiplayerManager");
            multiplayerManagerObj.AddComponent<MultiplayerManager>();

            // 8. Mark the active scene as dirty and save it to serialize all changes
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("[MultiplayerLobbySceneBuilder] ¡Habitación 3D, Puerta Gigante, Trigger e inyecciones de UI completadas con total éxito y escena guardada!");
        }
    }
}
