using UnityEngine;

namespace IronKeep
{
    public class DefenseUnit : MonoBehaviour
    {
        public enum Team { Player, Enemy, Base }
        public Team team;
        public float maxHp = 100;
        public float hp;
        public bool heavy;
        public bool Dead => hp <= 0;
        public void Initialize(Team value, float health, bool armored = false)
        { team = value; maxHp = hp = health; heavy = armored; }
        // HP is authoritative on the host; clients receive it through DefenseGame.SyncHp / state stream.
        public void Damage(float amount)
        {
            var game = DefenseGame.Instance;
            if (Dead || game == null || game.finished || !DefenseGame.Authority) return;
            hp = Mathf.Max(0, hp - amount);
            game.SyncHp(this);
            if (Dead) game.UnitDestroyed(this);
        }
        public void Heal(float amount)
        {
            if (Dead || !DefenseGame.Authority) return;
            hp = Mathf.Min(maxHp, hp + amount);
            if (DefenseGame.Instance) DefenseGame.Instance.SyncHp(this);
        }
        void OnDestroy()
        {
            var game = DefenseGame.Instance;
            if (!game) return;
            game.enemies.Remove(this); game.players.Remove(this);
        }
    }
}
