using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Runefall.Combat;

namespace Runefall.Presentation.Network
{
    /// <summary>
    /// Streamlined local UI Presenter for cooperative networked combat.
    /// Reactively binds to the turn countdown timer, displaying only the gold circular countdown timer.
    /// </summary>
    public class NetworkedCombatHUD : MonoBehaviour
    {
        [Header("Turn Timer (Circular Gold)")]
        [SerializeField] private Text turnTimerText;
        [SerializeField] private Image turnTimerGlowRing;

        [Header("Boss HP Bar")]
        [SerializeField] private Slider bossHPSlider;
        [SerializeField] private Text bossHPText;

        private NetworkedTurnManager _turnManager;
        private bool _isInitialized = false;

        private void Start()
        {
            if (_turnManager == null)
            {
                _turnManager = FindObjectOfType<NetworkedTurnManager>();
            }

            if (_turnManager != null)
            {
                InitializeHUD();
            }
        }

        private void Update()
        {
            if (!_isInitialized)
            {
                if (_turnManager == null)
                {
                    _turnManager = FindObjectOfType<NetworkedTurnManager>();
                }

                if (_turnManager != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    InitializeHUD();
                }
                return;
            }

            // 1. Smooth tick timer visually
            if (_turnManager != null && _turnManager.CurrentPhase.Value == CombatPhase.PlayerTurn)
            {
                float timeRemaining = _turnManager.TurnTimer.Value;
                if (turnTimerText != null)
                {
                    turnTimerText.text = Mathf.CeilToInt(timeRemaining).ToString();
                }
                if (turnTimerGlowRing != null)
                {
                    turnTimerGlowRing.fillAmount = timeRemaining / 30f;
                    
                    // Smooth breathing pulse effect
                    float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 5f);
                    turnTimerGlowRing.color = timeRemaining <= 7f 
                        ? new Color(0.9f, 0.1f, 0.1f, pulse) // Warning Red
                        : new Color(0.85f, 0.65f, 0.1f, pulse); // Glowing Gold
                }
            }

            // 2. Reactively bind Boss HP Slider and Text
            if (_turnManager != null)
            {
                float currentHP = _turnManager.BossHP.Value;
                float maxHP = _turnManager.BossMaxHP.Value;
                if (bossHPSlider != null)
                {
                    bossHPSlider.maxValue = maxHP;
                    bossHPSlider.value = currentHP;
                }
                if (bossHPText != null)
                {
                    bossHPText.text = $"ORCO (JEFE ANCIANO): {Mathf.Max(0, Mathf.RoundToInt(currentHP))} / {Mathf.RoundToInt(maxHP)}";
                }
            }
        }

        private void InitializeHUD()
        {
            Debug.Log("[NetworkedCombatHUD] Streamlined Networked Combat HUD initialized with Boss HP tracking.");
            _isInitialized = true;
        }
    }
}
