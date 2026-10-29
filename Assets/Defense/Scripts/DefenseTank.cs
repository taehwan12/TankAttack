using Photon.Pun;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IronKeep
{
    public class DefenseTank : MonoBehaviourPun, IPunInstantiateMagicCallback
    {
        public Transform turret, barrel, muzzle;
        public float moveSpeed = 12, turnSpeed = 95, damage = 28, range = 28, reload = .65f;
        public bool player, heavy;
        public int ammo;
        public float pitch;
        public float speedUntil, powerUntil;
        public int enemyKind, areaLevel, hasteLevel, damageLevel;
        public float DamageMultiplier => (1+damageLevel*.25f)*(Time.time<powerUntil?1.5f:1);
        public float FireRateMultiplier => 1+hasteLevel*.2f;
        public float CurrentReload => reload*(player&&ammo==1?1.8f:1)/FireRateMultiplier;
        public float SplashRadius => player ? (ammo==1?5+areaLevel:areaLevel>0?3+areaLevel:0) : enemyKind==3?3:0;
        public float NextShot => nextShot;
        // Local simulation: our own tank, or an enemy on the host. Everything else is driven by DefenseSync.
        public bool Local => !photonView || photonView.IsMine;
        public const float ShellSpeed = 42, ShellGravity = 4.5f;
        float nextShot;
        Rigidbody body;
        DefenseUnit unit;
        Vector3 destination;
        bool reachedLane;
        float steer, throttle;
        public void OnPhotonInstantiate(PhotonMessageInfo info)
        {
            var data = photonView.InstantiationData;
            Setup((bool)data[0], (int)data[1], (int)data[2]);
            if (DefenseGame.Instance) DefenseGame.Instance.Register(this);
        }
        // kind is the tank index for players and the enemy kind for enemies.
        public void Setup(bool isPlayer, int kind, int wave)
        {
            player = isPlayer; enemyKind=player?0:Mathf.Clamp(kind,0,3); heavy = !player&&enemyKind==1;
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = !Local;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = Local ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            body.linearDamping = 4;
            unit = GetComponent<DefenseUnit>();
            if(player)
            {
                int index=Mathf.Clamp(kind,0,DefenseOptions.Tanks.Length-1);
                var spec=DefenseOptions.Tanks[index];
                unit.Initialize(DefenseUnit.Team.Player,spec.hp);
                moveSpeed=spec.speed;damage=spec.damage;reload=spec.reload;
                turnSpeed=index==2?72:index==1?115:95;
                DefenseOptions.ApplyLook(gameObject,index,.32f);
            }
            else
            {
                var spec=DefenseOptions.Enemies[enemyKind];
                int difficulty=DefenseGame.Instance.difficultyIndex;
                unit.Initialize(DefenseUnit.Team.Enemy,(spec.hp+(wave-1)*7)*DefenseOptions.EnemyHealth(difficulty),heavy);
                float growth=DefenseGame.Instance.Endless?1+Mathf.Max(0,wave-1)*.035f:1;
                moveSpeed=spec.speed*(DefenseGame.Instance.Endless?1+Mathf.Min(.5f,(wave-1)*.008f):1);
                damage=spec.damage*DefenseOptions.EnemyDamage(difficulty)*growth;range=spec.range;
                reload=spec.reload/(DefenseGame.Instance.Endless?1+Mathf.Min(.5f,(wave-1)*.01f):1);
                destination = new Vector3(transform.position.x, 0, -8);
                nextShot = Time.time + 2;
            }
        }
        void Update()
        {
            var g = DefenseGame.Instance;
            if (!Local || !g || g.finished || g.Frozen || unit.Dead) { throttle = steer = 0; return; }
            if (player)
            {
                if (g.paused) { throttle = steer = 0; return; }
                steer = Input.GetAxisRaw("Horizontal"); throttle = Input.GetAxisRaw("Vertical");
                if (Input.GetKeyDown(KeyCode.Alpha1)) ammo = 0;
                if (Input.GetKeyDown(KeyCode.Alpha2)) ammo = 1;
                // Third-person aiming: the turret follows the crosshair and the barrel elevates to land the shell on it.
                var cam = DefenseCamera.Active;
                Vector3 aimPoint = cam && cam.target == transform ? cam.AimPoint(transform) : muzzle.position + transform.forward * 30;
                Aim(aimPoint);
                pitch = Mathf.Clamp(BallisticPitch(muzzle.position, aimPoint), -8, 28);
                barrel.localRotation = Quaternion.Euler(-pitch, 0, 0);
                bool overUi = Cursor.lockState != CursorLockMode.Locked && EventSystem.current && EventSystem.current.IsPointerOverGameObject();
                if (Input.GetMouseButton(0) && !overUi) Shoot(muzzle.forward);
                if (Input.GetKeyDown(KeyCode.R)) g.RequestRepair();
            }
            else
            {
                if (!g.baseUnit) return;
                DefenseUnit target = g.baseUnit; float nearest = 20;
                foreach (var p in g.players)
                {
                    if (!p || p.Dead) continue;
                    float d = Vector3.Distance(transform.position, p.transform.position);
                    if (d < nearest) { nearest = d; target = p; }
                }
                Vector3 to = target.transform.position - transform.position;
                Aim(target.transform.position + Vector3.up * 1.2f);
                if (to.magnitude <= range)
                {
                    throttle = steer = 0;
                    Vector3 delta = target.transform.position + Vector3.up * 1.2f - muzzle.position;
                    float travel = delta.magnitude / ShellSpeed;
                    delta.y += .5f * ShellGravity * travel * travel;
                    if (!Physics.Linecast(muzzle.position, target.transform.position + Vector3.up * 1.2f, out RaycastHit hit) || hit.collider.GetComponentInParent<DefenseUnit>() == target)
                        Shoot(delta.normalized);
                }
                else
                {
                    if (Vector3.Distance(transform.position, destination) < 3) reachedLane = true;
                    Vector3 goal = reachedLane ? g.baseUnit.transform.position : destination;
                    Vector3 dir = goal - transform.position; dir.y = 0;
                    float angle = Vector3.SignedAngle(transform.forward, dir, Vector3.up);
                    steer = Mathf.Clamp(angle / 35, -1, 1);
                    throttle = Mathf.Abs(angle) < 65 ? 1 : .2f;
                }
            }
        }
        void FixedUpdate()
        {
            if (!Local || !body || !DefenseGame.Instance || DefenseGame.Instance.finished || DefenseGame.Instance.Frozen) return;
            float speed = moveSpeed * (Time.time < speedUntil ? 1.5f : 1);
            body.MoveRotation(body.rotation * Quaternion.Euler(0, steer * turnSpeed * Time.fixedDeltaTime, 0));
            Vector3 p = body.position + transform.forward * (throttle * speed * Time.fixedDeltaTime);
            p.x = Mathf.Clamp(p.x, -42, 42); p.z = Mathf.Clamp(p.z, -46, 48);
            body.MovePosition(p);
        }
        void Aim(Vector3 point)
        {
            Vector3 d = point - turret.position; d.y = 0;
            if (d.sqrMagnitude > .01f) turret.rotation = Quaternion.RotateTowards(turret.rotation, Quaternion.LookRotation(d), 220 * Time.deltaTime);
        }
        // Low-angle launch elevation (degrees) that lands a shell on the target point; 45 when out of reach.
        public static float BallisticPitch(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from; float y = d.y; d.y = 0; float x = d.magnitude;
            if (x < .5f) return 0;
            float v2 = ShellSpeed * ShellSpeed, g = ShellGravity;
            float root = v2 * v2 - g * (g * x * x + 2 * y * v2);
            if (root < 0) return 45;
            return Mathf.Atan((v2 - Mathf.Sqrt(root)) / (g * x)) * Mathf.Rad2Deg;
        }
        public void Shoot(Vector3 direction)
        {
            var game=DefenseGame.Instance;
            if(!game || game.Frozen || game.finished || Time.time < nextShot) return;
            nextShot = Time.time + CurrentReload;
            game.FireShell(muzzle.position, direction, unit, damage*DamageMultiplier*(player&&ammo==1?1.25f:1),SplashRadius);
        }
        public void Upgrade(int kind)
        {
            if(kind==1)areaLevel=Mathf.Min(3,areaLevel+1);
            if(kind==2){hasteLevel=Mathf.Min(5,hasteLevel+1);nextShot=Mathf.Min(nextShot,Time.time+CurrentReload);}
            if(kind==3)damageLevel=Mathf.Min(5,damageLevel+1);
        }
    }
}
