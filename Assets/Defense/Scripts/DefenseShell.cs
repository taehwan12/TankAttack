using System.Collections.Generic;
using UnityEngine;

namespace IronKeep
{
    // Shells fly on every client for visuals; only the host applies damage.
    public class DefenseShell : MonoBehaviour
    {
        DefenseUnit owner;
        DefenseUnit.Team team;
        Vector3 velocity;
        float damage, splash, age;
        public float DamageValue => damage;
        public float Splash => splash;
        public static void Create(Vector3 point, Vector3 direction, DefenseUnit source, float power, float radius)
            => Create(point, direction, source, power, radius, source ? source.team : DefenseUnit.Team.Base);
        public static void Create(Vector3 point, Vector3 direction, DefenseUnit source, float power, float radius, DefenseUnit.Team sourceTeam)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.name = radius > 0 ? "HE Shell" : "AP Shell";
            Destroy(obj.GetComponent<Collider>());
            obj.transform.position = point; obj.transform.localScale = Vector3.one * (radius > 0 ? .35f : .22f);
            var material = DefenseGame.Instance.shellMaterial;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            var shell = obj.AddComponent<DefenseShell>();
            shell.owner = source; shell.team = sourceTeam; shell.damage = power; shell.splash = radius;
            shell.velocity = direction.normalized * DefenseTank.ShellSpeed;
            var trail = obj.AddComponent<TrailRenderer>(); trail.sharedMaterial = material;
            trail.time = .12f; trail.startWidth = .16f; trail.endWidth = .01f;
        }
        void Update()
        {
            if (!DefenseGame.Instance || DefenseGame.Instance.finished) { Destroy(gameObject); return; }
            age += Time.deltaTime;
            if (age > 4) { Destroy(gameObject); return; }
            velocity += Vector3.down * DefenseTank.ShellGravity * Time.deltaTime;
            Vector3 step = velocity * Time.deltaTime;
            var hits = Physics.RaycastAll(transform.position, step.normalized, step.magnitude, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var target = hit.collider.GetComponentInParent<DefenseUnit>();
                if (target && (target == owner || !Hostile(target))) continue;
                Impact(hit.point, target); return;
            }
            transform.position += step;
        }
        bool Hostile(DefenseUnit target) => team == DefenseUnit.Team.Enemy ? target.team != DefenseUnit.Team.Enemy : target.team == DefenseUnit.Team.Enemy;
        void Impact(Vector3 point, DefenseUnit target)
        {
            if (DefenseGame.Authority)
            {
                if (splash > 0)
                {
                    var seen = new HashSet<DefenseUnit>();
                    foreach (var c in Physics.OverlapSphere(point, splash))
                    {
                        var u = c.GetComponentInParent<DefenseUnit>();
                        if (u && Hostile(u) && seen.Add(u)) u.Damage(damage);
                    }
                }
                else if (target && Hostile(target)) target.Damage(damage);
            }
            DefenseGame.Instance.Explosion(point, splash > 0 ? 2.2f : .7f);
            Destroy(gameObject);
        }
    }
}
