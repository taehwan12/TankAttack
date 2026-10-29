using UnityEngine;

namespace IronKeep
{
    // Spawned on every client by the host; only the host decides who collects it.
    public class DefensePickup : MonoBehaviour
    {
        public int kind, id;
        float age;
        Vector3 origin;
        void Start() { origin = transform.position; }
        void Update()
        {
            age += Time.deltaTime;
            transform.position = origin + Vector3.up * Mathf.Sin(age * 2) * .35f;
            transform.Rotate(0, 65 * Time.deltaTime, 0);
            var g = DefenseGame.Instance;
            if (g && !g.finished && !g.Frozen && DefenseGame.Authority)
            {
                foreach (var p in g.players)
                {
                    if (!p || p.Dead || Vector3.Distance(p.transform.position, transform.position) >= 3.8f) continue;
                    g.TakePickup(this, p);
                    return;
                }
            }
            if (age > 45) Destroy(gameObject);
        }
        void OnDestroy() { if (DefenseGame.Instance) DefenseGame.Instance.ForgetPickup(id); }
    }
}
