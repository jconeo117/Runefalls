using UnityEngine;
using Runefall.Combat;

namespace Runefall.Presentation.Combat
{
    public class HPBarPresenter : MonoBehaviour
    {
        private ICombatActor  _actor;
        private RectTransform _fillRT;
        private float         _displayedHP;
        private Transform     _followTarget;
        private Vector3       _worldOffset;

        public void Bind(ICombatActor actor, RectTransform fillRT)
        {
            _actor       = actor;
            _fillRT      = fillRT;
            _displayedHP = actor.Model.CurrentHP;
            Refresh();
        }

        public void SetFollow(Transform target, Vector3 worldOffset)
        {
            _followTarget = target;
            _worldOffset  = worldOffset;
        }

        // Advance displayed HP by exactly one hit's damage — called from ApplyImpactGroup.
        public void ApplyVisualDamage(float amount)
        {
            _displayedHP = Mathf.Max(0f, _displayedHP - amount);
            Refresh();
        }

        public void ApplyVisualHeal(float amount)
        {
            _displayedHP = Mathf.Min(_actor.Model.MaxHP, _displayedHP + amount);
            Refresh();
        }

        // Snap displayed HP to actual — called at player turn start to cover regen / drift.
        public void ForceRefresh()
        {
            if (_actor == null) return;
            _displayedHP = _actor.Model.CurrentHP;
            Refresh();
        }

        private void LateUpdate()
        {
            if (_followTarget != null)
                transform.position = _followTarget.position + _worldOffset;
            if (Camera.main == null) return;
            transform.rotation = Camera.main.transform.rotation;
        }

        private void Refresh()
        {
            if (_fillRT == null || _actor == null) return;
            float pct = _actor.Model.MaxHP > 0f ? _displayedHP / _actor.Model.MaxHP : 0f;
            _fillRT.anchorMax = new Vector2(pct, 1f);
        }
    }
}
