using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Runefall.Combat;
using Runefall.Data;
using Runefall.Characters;
using Runefall.Presentation.Combat;

namespace Runefall.Presentation.Network
{
    /// <summary>
    /// Reactive multiplayer UI controller for the premium CombatUI_Canvas.
    /// Disables the offline CombatHUDPresenter, builds a private local hand of 5 cards,
    /// instantiates 6 global action slots, and binds interactions to NetworkedTurnManager RPCs.
    /// </summary>
    public class NetworkedCombatUIAdapter : MonoBehaviour
    {
        private CombatHUDPresenter _presenter;
        private NetworkedTurnManager _turnManager;
        private List<GameObject> _instantiatedSlots = new();
        private List<CardView> _handCards = new();
        
        private bool _isBound = false;
        private bool _hasSubmittedThisTurn = false;
        private CharacterData _localCharData;

        public void SetLocalCharacterData(CharacterData charData)
        {
            _localCharData = charData;
            Debug.Log($"[NetworkedCombatUIAdapter] Local CharacterData assigned: {(charData != null ? charData.characterName : "null")}");
            if (_presenter != null && _presenter.cardHandContainer != null)
            {
                InitializeLocalHand();
            }
        }

        private void Awake()
        {
            // Bypassed: the premium CombatHUDPresenter is now fully enabled and driven by the network proxy TurnManager!
            Debug.Log("[NetworkedCombatUIAdapter] Premium multiplayer mode active: letting offline CombatHUDPresenter run.");
            enabled = false;
        }

        private void Start()
        {
            _turnManager = FindAnyObjectByType<NetworkedTurnManager>();
            
            InitializeGlobalSlots();
            InitializeLocalHand();

            if (_turnManager != null)
            {
                BindToTurnManager();
            }
        }

        private void Update()
        {
            if (_turnManager == null)
            {
                _turnManager = FindAnyObjectByType<NetworkedTurnManager>();
                if (_turnManager != null)
                {
                    BindToTurnManager();
                }
            }
        }

        private void OnDestroy()
        {
            UnbindFromTurnManager();
        }

        private void BindToTurnManager()
        {
            if (_isBound || _turnManager == null) return;

            _turnManager.ReplicatedActions.OnValueChanged += HandleActionsChanged;
            _turnManager.CurrentPhase.OnValueChanged += HandlePhaseChanged;
            _turnManager.RoundNumber.OnValueChanged += HandleRoundChanged;
            _isBound = true;

            Debug.Log("[NetworkedCombatUIAdapter] Bound successfully to NetworkedTurnManager.");

            // Perform initial synchronization
            SyncActionSlots(_turnManager.ReplicatedActions.Value);
            SyncHandState(_turnManager.CurrentPhase.Value);
        }

        private void UnbindFromTurnManager()
        {
            if (!_isBound || _turnManager == null) return;

            _turnManager.ReplicatedActions.OnValueChanged -= HandleActionsChanged;
            _turnManager.CurrentPhase.OnValueChanged -= HandlePhaseChanged;
            _turnManager.RoundNumber.OnValueChanged -= HandleRoundChanged;
            _isBound = false;
        }

        /// <summary>
        /// Spawns the 6 global action slots inside the action slots container.
        /// </summary>
        private void InitializeGlobalSlots()
        {
            if (_presenter == null || _presenter.actionSlotContainer == null || _presenter.actionSlotPrefab == null)
            {
                Debug.LogError("[NetworkedCombatUIAdapter] Cannot build slots: missing presenter references!");
                return;
            }

            // Clean existing children
            foreach (Transform child in _presenter.actionSlotContainer)
            {
                Destroy(child.gameObject);
            }
            _instantiatedSlots.Clear();

            // Setup HorizontalLayoutGroup for clean spacing of 6 slots
            var hlg = _presenter.actionSlotContainer.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.enabled = true;
                hlg.spacing = 15f;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childForceExpandWidth = false;
            }

            // Spawn exactly 6 global slots
            for (int i = 0; i < 6; i++)
            {
                var slotObj = Instantiate(_presenter.actionSlotPrefab, _presenter.actionSlotContainer);
                slotObj.name = $"GlobalActionSlot_{i}";
                
                // Add a CanvasGroup for fading/alpha logic if missing
                if (slotObj.GetComponent<CanvasGroup>() == null)
                {
                    slotObj.AddComponent<CanvasGroup>();
                }
                
                _instantiatedSlots.Add(slotObj);
            }

            Debug.Log("[NetworkedCombatUIAdapter] Instantiated 6 Global Action Slots.");
        }

        /// <summary>
        /// Populates the local player's private hand with 5 card views using their Ice Queen character skills.
        /// </summary>
        private void InitializeLocalHand()
        {
            if (_presenter == null || _presenter.cardHandContainer == null || _presenter.cardPrefab == null)
            {
                Debug.LogError("[NetworkedCombatUIAdapter] Cannot build hand: missing presenter references!");
                return;
            }

            // Clean existing cards
            foreach (Transform child in _presenter.cardHandContainer)
            {
                Destroy(child.gameObject);
            }
            _handCards.Clear();

            // Enable HorizontalLayoutGroup to center and layout the 5 private cards cleanly
            var hlg = _presenter.cardHandContainer.GetComponent<HorizontalLayoutGroup>()
                   ?? _presenter.cardHandContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.enabled = true;
            hlg.spacing = 12f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Retrieve local character data from TurnManager
            CharacterData charData = _localCharData;
            if (charData == null && _turnManager != null)
            {
                charData = _turnManager.PlayerCharacterData;
            }

            if (charData == null)
            {
                Debug.LogError("[NetworkedCombatUIAdapter] CharacterData could not be resolved! Cannot render hand.");
                return;
            }

            // Define the 5 local hand cards: 2x Skill 1, 2x Skill 2, 1x Ultimate
            var cardDefinitions = new List<(int skillType, int rank, bool isUlt)>
            {
                (0, 1, false), // Skill 1 (Rank 1)
                (0, 1, false), // Skill 1 (Rank 1)
                (1, 1, false), // Skill 2 (Rank 1)
                (1, 1, false), // Skill 2 (Rank 1)
                (2, 3, true)   // Ultimate (Rank 3)
            };

            Color elemColor = ElementColor(charData.element);

            for (int i = 0; i < cardDefinitions.Count; i++)
            {
                var def = cardDefinitions[i];
                var cardViewObj = Instantiate(_presenter.cardPrefab, _presenter.cardHandContainer);
                cardViewObj.name = $"LocalCard_{i}";

                var cv = cardViewObj.GetComponent<CardView>();
                if (cv != null)
                {
                    BattleCard bc;
                    if (def.isUlt)
                    {
                        bc = new BattleCard(charData.ultimate);
                    }
                    else
                    {
                        var skill = def.skillType == 0 ? charData.skill1 : charData.skill2;
                        bc = new BattleCard(skill, def.rank);
                    }

                    cv.Setup(bc, elemColor);
                    cv.HandIndex = i;
                    
                    // Bind button click for RPC submission
                    var btn = cv.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.interactable = true;
                        int sType = def.skillType;
                        int sRank = def.rank;
                        btn.onClick.AddListener(() => OnCardSelected(sType, sRank, cv));
                    }

                    _handCards.Add(cv);
                }
            }

            Debug.Log("[NetworkedCombatUIAdapter] Initialized 5 local premium cards for the private hand.");
        }

        private void OnCardSelected(int skillType, int rank, CardView selectedCv)
        {
            if (_hasSubmittedThisTurn || _turnManager == null) return;

            Debug.Log($"[NetworkedCombatUIAdapter] Card clicked! Type: {skillType}, Rank: {rank}. Sending ServerRpc...");

            // Find the Boss NetworkObjectId to target
            ulong bossNetId = 0;
            if (NetworkManager.Singleton != null)
            {
                foreach (var netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
                {
                    var pawn = netObj.GetComponent<NetworkedCombatPawn>();
                    if (pawn != null && pawn.Role.Value == CombatRole.Boss)
                    {
                        bossNetId = netObj.NetworkObjectId;
                        break;
                    }
                }
            }

            // Submit selection to host
            _turnManager.PlayCardServerRpc(skillType, rank, bossNetId);
            
            // Mark submitted locally
            _hasSubmittedThisTurn = true;
            
            // Apply visual "Submitted" feedback: disable buttons and fade slightly
            foreach (var cv in _handCards)
            {
                if (cv != null)
                {
                    var btn = cv.GetComponent<Button>();
                    if (btn != null) btn.interactable = false;

                    var cg = cv.GetComponent<CanvasGroup>();
                    if (cg != null) cg.alpha = (cv == selectedCv) ? 0.9f : 0.3f;
                }
            }
        }

        private void HandleActionsChanged(NetworkedActionState oldState, NetworkedActionState newState)
        {
            SyncActionSlots(newState);
        }

        private void HandlePhaseChanged(CombatPhase oldPhase, CombatPhase newPhase)
        {
            SyncHandState(newPhase);
        }

        private void HandleRoundChanged(int oldRound, int newRound)
        {
            Debug.Log($"[NetworkedCombatUIAdapter] Round changed to {newRound}. Resetting turn submission.");
            _hasSubmittedThisTurn = false;
            
            // Restore private hand cards to full interactable state
            foreach (var cv in _handCards)
            {
                if (cv != null)
                {
                    var btn = cv.GetComponent<Button>();
                    if (btn != null) btn.interactable = true;

                    var cg = cv.GetComponent<CanvasGroup>();
                    if (cg != null) cg.alpha = 1.0f;
                }
            }
        }

        /// <summary>
        /// Syncs the 6 global action slots visually based on replicated network actions.
        /// </summary>
        private void SyncActionSlots(NetworkedActionState state)
        {
            for (int i = 0; i < 6; i++)
            {
                if (i >= _instantiatedSlots.Count) break;

                GameObject slotObj = _instantiatedSlots[i];
                Transform slotT = slotObj.transform;

                // Get replicated slot action
                NetworkedActionSlot action = i switch
                {
                    0 => state.Slot0,
                    1 => state.Slot1,
                    2 => state.Slot2,
                    3 => state.Slot3,
                    4 => state.Slot4,
                    _ => state.Slot5
                };

                // Remove existing CardView if any
                foreach (Transform child in slotT)
                {
                    if (child.name.StartsWith("SlotCard_"))
                    {
                        Destroy(child.gameObject);
                    }
                }

                var inner = slotT.Find("Inner");

                if (action.IsActive)
                {
                    // Hide the dark empty placeholder
                    if (inner != null) inner.gameObject.SetActive(false);

                    // Instantiate a card to represent the played action
                    if (_presenter != null && _presenter.cardPrefab != null)
                    {
                        var cardObj = Instantiate(_presenter.cardPrefab, slotT);
                        cardObj.name = $"SlotCard_{i}";

                        // Match action slot size and center it
                        cardObj.transform.localPosition = Vector3.zero;
                        cardObj.transform.localScale = Vector3.one;
                        var rt = cardObj.GetComponent<RectTransform>();
                        if (rt != null)
                        {
                            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                            rt.anchoredPosition = Vector2.zero;
                            rt.sizeDelta = new Vector2(130f, 170f);
                        }

                        // Retrieve Skill/Ultimate data based on player ClientId
                        CharacterData charData = null;
                        if (_turnManager != null)
                        {
                            charData = (action.ClientId == 0) ? _turnManager.PlayerCharacterData : (_turnManager.ClientCharacterData != null ? _turnManager.ClientCharacterData : _turnManager.PlayerCharacterData);
                        }
                        if (charData != null)
                        {
                            var cv = cardObj.GetComponent<CardView>();
                            if (cv != null)
                            {
                                BattleCard bc;
                                if (action.SkillType == 2)
                                {
                                    bc = new BattleCard(charData.ultimate);
                                }
                                else
                                {
                                    var skill = action.SkillType == 0 ? charData.skill1 : charData.skill2;
                                    bc = new BattleCard(skill, action.Rank);
                                }

                                cv.Setup(bc, ElementColor(charData.element));

                                // Disable interactions on the slots
                                var btn = cv.GetComponent<Button>();
                                if (btn != null) btn.interactable = false;
                            }
                        }
                    }
                }
                else
                {
                    // Re-enable dark placeholder
                    if (inner != null)
                    {
                        inner.gameObject.SetActive(true);
                        var img = inner.GetComponent<Image>();
                        if (img != null)
                        {
                            img.color = new Color(0.06f, 0.06f, 0.10f, 0.92f); // basal empty style
                        }
                    }
                }
            }
        }

        private void SyncHandState(CombatPhase phase)
        {
            bool isPlayerTurn = (phase == CombatPhase.PlayerTurn);
            
            foreach (var cv in _handCards)
            {
                if (cv != null)
                {
                    var btn = cv.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.interactable = isPlayerTurn && !_hasSubmittedThisTurn;
                    }

                    var cg = cv.GetComponent<CanvasGroup>();
                    if (cg != null)
                    {
                        cg.alpha = (isPlayerTurn && !_hasSubmittedThisTurn) ? 1.0f : 0.3f;
                    }
                }
            }
        }

        private static Color ElementColor(ElementType element) => element switch
        {
            ElementType.Fire   => new Color(0.78f, 0.25f, 0.10f),
            ElementType.Ice    => new Color(0.16f, 0.43f, 0.75f),
            ElementType.Shadow => new Color(0.35f, 0.17f, 0.48f),
            ElementType.Light  => new Color(0.90f, 0.85f, 0.30f),
            ElementType.Earth  => new Color(0.29f, 0.48f, 0.16f),
            _                  => new Color(0.35f, 0.35f, 0.35f),
        };
    }
}
