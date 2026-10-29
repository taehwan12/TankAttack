using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronKeep
{
    // The host (master client) owns waves, damage, credits and turrets; other clients mirror its state.
    // Solo play runs the same code in Photon offline mode.
    public class DefenseGame : MonoBehaviourPun, IPunObservable
    {
        public static DefenseGame Instance { get; private set; }
        public GameObject tankPrefab, explosionPrefab, turretPrefab;
        public GameObject[] playerPrefabs,enemyPrefabs;
        public readonly DefenseTurret[] turrets=new DefenseTurret[2];
        public bool Endless=>difficultyIndex==3;
        public int clearedWaves;
        public string runNickname;
        public int TurretCount=>(turrets[0]?1:0)+(turrets[1]?1:0);
        public int NextTurretIndex=>!turrets[0]?0:!turrets[1]?1:turrets[0].level<=turrets[1].level?0:1;
        public int TurretPrice=>TurretCount<2?350:turrets[NextTurretIndex].level>=5?0:250+turrets[NextTurretIndex].level*125;
        public string TurretAction=>TurretPrice==0?"포탑 최대 강화":$"{(NextTurretIndex==0?"왼쪽":"오른쪽")} 포탑 {(TurretCount<2?"건설":"강화")} {TurretPrice}";
        public AudioClip shotClip;
        public Material shellMaterial, lightEnemyMaterial, heavyEnemyMaterial;
        public Material[] pickupMaterials;
        public TMPro.TMP_FontAsset font;
        public DefenseUnit playerUnit, baseUnit;
        public int totalWaves = 5;
        public int wave, kills, credits;
        public int tankIndex, difficultyIndex;
        public int RepairPrice => DefenseOptions.RepairCost(difficultyIndex);
        public float elapsed;
        public bool finished, paused;
        public int Alive => enemies.Count;
        public bool waveActive;
        public float countdown;
        public static int LastKills, LastWave, LastCredits, LastClearedWaves;
        public static string LastNickname;
        public static int LastTank,LastDifficulty,LastArea,LastHaste,LastDamage;
        public static float LastDuration;
        public static float LastBaseHp;
        public static string LastReason;
        public readonly List<DefenseUnit> enemies = new List<DefenseUnit>();
        public readonly List<DefenseUnit> players = new List<DefenseUnit>();
        public static bool Authority => !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;
        public bool Multiplayer => DefenseNetwork.Online;
        // Only solo play can freeze time; online the pause menu just blocks local input.
        public bool Frozen => paused && !Multiplayer;
        readonly Dictionary<int,DefensePickup> pickups=new Dictionary<int,DefensePickup>();
        int pickupSequence;
        DefenseHUD hud;
        bool IsMain => SceneManager.GetActiveScene().name == "Main";
        void Awake()
        {
            Instance = this; Time.timeScale = 1;
            if (!IsMain) return;
            if (!PhotonNetwork.InRoom && !PhotonNetwork.IsConnected) DefenseNetwork.StartOffline();
            var view = gameObject.AddComponent<PhotonView>();
            view.ObservedComponents = new List<Component> { this };
            view.Synchronization = ViewSynchronization.UnreliableOnChange;
            view.ViewID = 900;
            PhotonNetwork.PrefabPool = new DefensePool(this);
            tankIndex=DefenseOptions.TankIndex; difficultyIndex=DefenseNetwork.RoomDifficulty();
        }
        void Start()
        {
            tankIndex=DefenseOptions.TankIndex; if(!IsMain) difficultyIndex=DefenseOptions.DifficultyIndex;
            hud = gameObject.AddComponent<DefenseHUD>();
            hud.Build(this, SceneManager.GetActiveScene().name);
            if (!IsMain) {RefreshShowcase();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return;}
            runNickname=DefenseRanking.Nickname;
            if(!PhotonNetwork.InRoom){SceneManager.LoadScene("Intro");return;}
            baseUnit.Initialize(DefenseUnit.Team.Base, DefenseOptions.CoreHealth(difficultyIndex));
            Vector3 pos=playerUnit.transform.position;Destroy(playerUnit.gameObject);playerUnit=null;
            int slot=(PhotonNetwork.LocalPlayer.ActorNumber-1)%DefenseNetwork.MaxPlayers;
            if(Multiplayer)pos.x+=(slot-1.5f)*6;
            PhotonNetwork.Instantiate("P"+tankIndex,pos,Quaternion.identity,0,new object[]{true,tankIndex,1});
            if(!Authority)return;
            credits = difficultyIndex==2?100:150;
            SpawnPickup(new Vector3(-6,1.2f,-19),0);
            StartCoroutine(Waves());
        }
        // Called on every client when a networked tank appears.
        public void Register(DefenseTank tank)
        {
            var unit=tank.GetComponent<DefenseUnit>();
            if(!tank.player){if(!enemies.Contains(unit))enemies.Add(unit);return;}
            if(!players.Contains(unit))players.Add(unit);
            var view=tank.photonView;
            if(view&&view.IsMine)
            {
                playerUnit=unit;tank.name="Player Tank";
                var cam=FindAnyObjectByType<DefenseCamera>();if(cam)cam.target=tank.transform;
            }
            else tank.name=(view&&view.Owner!=null?view.Owner.NickName:"Ally")+" Tank";
        }
        void Update()
        {
            if (!IsMain)
            { if (Input.GetKeyDown(KeyCode.Return)&&!hud.EditingNickname) hud.Deploy(); return; }
            bool locked=!finished&&!paused&&Application.isFocused;
            Cursor.lockState=locked?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!locked;
            if (finished) return;
            if(Authority&&!Frozen)elapsed+=Time.deltaTime;
            if(!paused&&Input.GetKeyDown(KeyCode.T))RequestTurret();
            if (Input.GetKeyDown(KeyCode.Escape)) SetPaused(!paused);
        }
        public void SetPaused(bool value)
        {
            paused=value; Time.timeScale = Frozen ? 0 : 1; if(hud)hud.SetPause(paused);
        }
        IEnumerator Waves()
        {
            for (wave = 1; Endless || wave <= totalWaves; wave++)
            {
                waveActive = false;
                countdown = wave == 1 ? 10 : difficultyIndex==2?10:13;
                Broadcast(wave == 1 ? "DEFEND THE COMMAND CORE" : "SECTOR CLEAR  /  REPAIR & REARM");
                while (countdown > 0) { countdown -= Time.deltaTime; yield return null; }
                waveActive = true;
                int count = DefenseOptions.WaveCount(wave,difficultyIndex);
                Broadcast("WAVE " + wave + "  /  HOSTILES INBOUND");
                for (int i = 0; i < count; i++)
                {
                    if (finished) yield break;
                    SpawnEnemy(i, wave);
                    yield return new WaitForSeconds(Mathf.Max(Endless?1.4f:1.3f,(difficultyIndex==2?2.3f:3.1f)-wave*(Endless?.05f:.12f)));
                }
                while (enemies.Count > 0) {if(finished)yield break;yield return null;}
                if(finished)yield break;
                clearedWaves=wave;
                credits += 60 + Mathf.Min(wave,20) * 15;
                if (Endless || wave < totalWaves)
                {
                    foreach(var p in players) if(p) p.Heal(difficultyIndex==2?20:35);
                    SpawnPickup(new Vector3(0, 1.2f, -18),0);
                }
            }
            wave = totalWaves;
            End(true);
        }
        public void SpawnEnemy(int index, int stage)
        {
            if(!Authority)return;
            int kind=EnemyKind(index,stage);
            var obj=PhotonNetwork.Instantiate("E"+kind,new Vector3((index%3-1)*24,.35f,43),Quaternion.Euler(0,180,0),0,new object[]{false,kind,stage});
            obj.name=DefenseOptions.Enemies[kind].name+" Enemy Tank";
        }
        public void EnemyKilled(DefenseUnit unit)
        {
            enemies.Remove(unit); kills++; credits += unit.heavy ? 45 : 25;
            SpawnPickup(unit.transform.position+Vector3.up,1+(kills-1)%3);
            if(kills%4==0)SpawnPickup(unit.transform.position+new Vector3(2,1,0),0);
        }
        // Host only: a unit reached 0 HP.
        public void UnitDestroyed(DefenseUnit unit)
        {
            Boom(unit.transform.position + Vector3.up, unit.heavy ? 3 : 2);
            if (unit.team == DefenseUnit.Team.Enemy)
            {
                EnemyKilled(unit);
                if(unit.GetComponent<PhotonView>())PhotonNetwork.Destroy(unit.gameObject);else Destroy(unit.gameObject);
            }
            else if (unit.team == DefenseUnit.Team.Player && Multiplayer && PhotonNetwork.CurrentRoom.PlayerCount > 1) StartCoroutine(Respawn(unit));
            else End(false);
        }
        IEnumerator Respawn(DefenseUnit unit)
        {
            var view=unit.GetComponent<PhotonView>();
            Broadcast($"{(view.Owner!=null?view.Owner.NickName:"아군")} 전차 파괴 / 6초 후 코어 앞 재배치");
            yield return new WaitForSeconds(6);
            if(finished||!unit)yield break;
            photonView.RPC(nameof(RpcRespawn),RpcTarget.All,view.ViewID);
        }
        [PunRPC] void RpcRespawn(int viewId)
        {
            var view=PhotonView.Find(viewId);if(!view)return;
            var unit=view.GetComponent<DefenseUnit>();unit.hp=unit.maxHp;
            if(!view.IsMine)return;
            int slot=(PhotonNetwork.LocalPlayer.ActorNumber-1)%DefenseNetwork.MaxPlayers;
            var p=new Vector3((slot-1.5f)*6,.35f,-21);
            var body=view.GetComponent<Rigidbody>();if(body){body.position=p;body.linearVelocity=Vector3.zero;}
            view.transform.SetPositionAndRotation(p,Quaternion.identity);
            Message("재배치 완료 / 다시 출격");
        }
        public static int EnemyKind(int index,int stage)
        { int unlocked=stage==1?2:stage==2?3:4;return (index+stage-1)%unlocked; }
        public void RefreshShowcase()
        {
            var model=GameObject.Find("Showcase Tank");if(!model)return;
            string scene=SceneManager.GetActiveScene().name;
            int index=scene=="Intro"?DefenseOptions.TankIndex:LastTank;
            if(playerPrefabs!=null&&playerPrefabs.Length==3)
            {
                var marker=model.GetComponent<DefenseModel>();
                if(!marker||marker.assetName!=playerPrefabs[index].GetComponent<DefenseModel>().assetName)
                {
                    Vector3 pos=model.transform.position;Quaternion rotation=model.transform.rotation;
                    model.name="Retired Showcase";Destroy(model);
                    model=Instantiate(playerPrefabs[index],pos,rotation);model.name="Showcase Tank";
                    model.transform.localScale*=2.2f;model.GetComponent<Rigidbody>().isKinematic=true;
                    model.GetComponent<DefenseTank>().enabled=false;
                }
            }
            if(scene=="Defeat")
            {
                var block=new MaterialPropertyBlock();block.SetColor("_Color",new Color(.13f,.12f,.12f));
                foreach(var r in model.GetComponentsInChildren<Renderer>())r.SetPropertyBlock(block);
            }
        }
        public void SpawnPickup(Vector3 point, int kind)
        {
            if(!Authority)return;
            photonView.RPC(nameof(RpcPickup),RpcTarget.All,++pickupSequence,point,kind);
        }
        [PunRPC] void RpcPickup(int id, Vector3 point, int kind)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = new[] { "Repair Pickup", "Area Shell Upgrade", "Rapid Fire Upgrade", "Damage Upgrade" }[kind];
            obj.transform.position = point; obj.transform.localScale = Vector3.one * 1.3f;
            Destroy(obj.GetComponent<Collider>());
            obj.GetComponent<Renderer>().sharedMaterial = pickupMaterials[kind];
            var pickup = obj.AddComponent<DefensePickup>(); pickup.kind = kind; pickup.id = id;
            pickups[id] = pickup;
            // Cross / chevrons make the pickups distinguishable beyond color.
            for (int i = 0; i <= kind; i++)
            {
                var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(stripe.GetComponent<Collider>());
                stripe.transform.SetParent(obj.transform, false);
                stripe.transform.localPosition = new Vector3((i - kind * .5f) * .25f, .52f, 0);
                stripe.transform.localScale = new Vector3(.13f, .07f, .65f);
                stripe.GetComponent<Renderer>().sharedMaterial = shellMaterial;
            }
        }
        public void ForgetPickup(int id) => pickups.Remove(id);
        // Host only: a player tank touched a pickup.
        public void TakePickup(DefensePickup pickup, DefenseUnit unit)
        {
            var view=unit.GetComponent<PhotonView>();
            photonView.RPC(nameof(RpcTakePickup),RpcTarget.All,pickup.id,pickup.kind,view?view.ViewID:0);
        }
        [PunRPC] void RpcTakePickup(int id, int kind, int viewId)
        {
            if(pickups.TryGetValue(id,out var pickup)){pickups.Remove(id);if(pickup)Destroy(pickup.gameObject);}
            var view=PhotonView.Find(viewId);if(!view)return;
            var unit=view.GetComponent<DefenseUnit>();var tank=view.GetComponent<DefenseTank>();
            if(kind==0){if(Authority)unit.Heal(45);if(view.IsMine)Message("REPAIR KIT  /  +45 ARMOR");return;}
            if(!view.IsMine)return;
            tank.Upgrade(kind);
            Message(kind==1?$"AREA SHELLS  /  LEVEL {tank.areaLevel}  /  THIS RUN":kind==2?$"RAPID FIRE  /  LEVEL {tank.hasteLevel}  /  THIS RUN":$"SHELL DAMAGE  /  LEVEL {tank.damageLevel}  /  THIS RUN");
        }
        public void RequestRepair()
        {
            if(!playerUnit)return;
            if(Authority)Repair(playerUnit);else photonView.RPC(nameof(RpcRepair),RpcTarget.MasterClient,playerUnit.GetComponent<PhotonView>().ViewID);
        }
        [PunRPC] void RpcRepair(int viewId){var v=PhotonView.Find(viewId);if(v)Repair(v.GetComponent<DefenseUnit>());}
        public void Repair(DefenseUnit requester = null)
        {
            requester = requester ? requester : playerUnit;
            if (finished || Frozen || !requester || !Authority) return;
            if (Vector3.Distance(requester.transform.position, baseUnit.transform.position) > 14)
            { Reply(requester,"MOVE CLOSER TO THE CORE TO REPAIR"); return; }
            if (credits < RepairPrice) { Reply(requester,$"REPAIR REQUIRES {RepairPrice} CREDITS"); return; }
            if (baseUnit.hp >= baseUnit.maxHp) { Reply(requester,"CORE ARMOR IS ALREADY FULL"); return; }
            credits -= RepairPrice; baseUnit.Heal(100); Broadcast("CORE REPAIRED  /  +100 ARMOR");
        }
        public void RequestTurret()
        {
            if(!playerUnit)return;
            if(Authority)BuildOrUpgradeTurret(playerUnit);else photonView.RPC(nameof(RpcTurret),RpcTarget.MasterClient,playerUnit.GetComponent<PhotonView>().ViewID);
        }
        [PunRPC] void RpcTurret(int viewId){var v=PhotonView.Find(viewId);if(v)BuildOrUpgradeTurret(v.GetComponent<DefenseUnit>());}
        public bool BuildOrUpgradeTurret(DefenseUnit requester = null)
        {
            requester = requester ? requester : playerUnit;
            if(finished||Frozen||!baseUnit||!requester||!Authority)return false;
            if(Vector3.Distance(requester.transform.position,baseUnit.transform.position)>18)
            {Reply(requester,"코어 근처에서 포탑을 건설하거나 강화하세요.");return false;}
            int price=TurretPrice,index=NextTurretIndex;
            if(price==0){Reply(requester,"두 포탑 모두 최대 레벨입니다.");return false;}
            if(credits<price){Reply(requester,$"크레딧 부족 / {TurretAction}");return false;}
            if(!turrets[index])
            {
                if(!turretPrefab){Reply(requester,"포탑 모델을 불러오지 못했습니다.");return false;}
                var obj=PhotonNetwork.Instantiate("T",baseUnit.transform.position+new Vector3(index==0?-12:12,0,0),Quaternion.identity,0,new object[]{index});
                obj.name=index==0?"Left Defense Turret":"Right Defense Turret";
            }
            else turrets[index].level++;
            credits-=price;Broadcast($"{(index==0?"왼쪽":"오른쪽")} 포탑 레벨 {turrets[index].level} / -{price} 크레딧");return true;
        }
        public void FireShell(Vector3 point, Vector3 direction, DefenseUnit source, float power, float radius)
        {
            var view=source?source.GetComponent<PhotonView>():null;
            photonView.RPC(nameof(RpcShell),RpcTarget.All,point,direction,power,radius,(int)(source?source.team:DefenseUnit.Team.Base),view?view.ViewID:0);
        }
        [PunRPC] void RpcShell(Vector3 point, Vector3 direction, float power, float radius, int team, int viewId)
        {
            var view=viewId!=0?PhotonView.Find(viewId):null;
            var owner=view?view.GetComponent<DefenseUnit>():null;
            DefenseShell.Create(point,direction,owner,power,radius,(DefenseUnit.Team)team);
            if (shotClip) AudioSource.PlayClipAtPoint(shotClip, point, team==(int)DefenseUnit.Team.Player ? .5f : .18f);
        }
        public void SyncHp(DefenseUnit unit)
        {
            var view=unit.GetComponent<PhotonView>();
            if(view&&Multiplayer&&Authority)photonView.RPC(nameof(RpcHp),RpcTarget.Others,view.ViewID,unit.hp);
        }
        [PunRPC] void RpcHp(int viewId,float hp){var v=PhotonView.Find(viewId);if(v)v.GetComponent<DefenseUnit>().hp=hp;}
        public void Boom(Vector3 point, float scale)
        {
            if(Multiplayer)photonView.RPC(nameof(RpcBoom),RpcTarget.Others,point,scale);
            Explosion(point,scale);
        }
        [PunRPC] void RpcBoom(Vector3 point,float scale)=>Explosion(point,scale);
        public void Explosion(Vector3 point, float scale)
        {
            if (explosionPrefab)
            { var fx = Instantiate(explosionPrefab, point, Quaternion.identity); fx.transform.localScale *= scale; Destroy(fx, 3); }
            else
            {
                var fx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(fx.GetComponent<Collider>()); fx.transform.position = point;
                fx.transform.localScale = Vector3.one * scale;
                fx.GetComponent<Renderer>().sharedMaterial = shellMaterial; Destroy(fx,.18f);
            }
        }
        public void Message(string value) { if (hud) hud.Message(value); }
        void Broadcast(string value){ if(IsMain&&photonView)photonView.RPC(nameof(RpcNotify),RpcTarget.All,value);else Message(value); }
        void Reply(DefenseUnit requester,string value)
        {
            var view=requester?requester.GetComponent<PhotonView>():null;
            if(!view||view.IsMine||!Multiplayer)Message(value);else photonView.RPC(nameof(RpcNotify),view.Owner,value);
        }
        [PunRPC] void RpcNotify(string value)=>Message(value);
        public void End(bool won)
        {
            if (finished || !Authority) return;
            photonView.RPC(nameof(RpcEnd),RpcTarget.All,won);
        }
        [PunRPC] void RpcEnd(bool won)
        {
            if (finished) return;
            finished = true; paused = false; Time.timeScale = 1; if(hud)hud.SetPause(false);
            LastClearedWaves=clearedWaves;LastNickname=runNickname;
            if(Endless){won=false;DefenseRanking.Submit(runNickname,clearedWaves,kills,elapsed);}
            LastKills = kills; LastWave = wave; LastCredits = credits; LastBaseHp = baseUnit ? baseUnit.hp : 0;
            LastTank=tankIndex;LastDifficulty=difficultyIndex;LastDuration=elapsed;
            var upgrades=playerUnit?playerUnit.GetComponent<DefenseTank>():null;
            LastArea=upgrades?upgrades.areaLevel:0;LastHaste=upgrades?upgrades.hasteLevel:0;LastDamage=upgrades?upgrades.damageLevel:0;
            LastReason = won ? "All hostile waves have been eliminated." : baseUnit && baseUnit.Dead ? "The command core was destroyed." : "Your tank was destroyed.";
            string key=DefenseOptions.RecordKey(difficultyIndex);
            if (won && kills > PlayerPrefs.GetInt(key,0)) { PlayerPrefs.SetInt(key, kills); PlayerPrefs.Save(); }
            if(Authority)StartCoroutine(Ending(won));
        }
        IEnumerator Ending(bool won) { yield return new WaitForSeconds(1.1f); PhotonNetwork.LoadLevel(won ? "Victory" : "Defeat"); }
        public static void Load(string scene) => DefenseNetwork.LoadScene(scene);
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(wave); stream.SendNext(countdown); stream.SendNext(waveActive); stream.SendNext(credits);
                stream.SendNext(kills); stream.SendNext(clearedWaves); stream.SendNext(elapsed); stream.SendNext(baseUnit ? baseUnit.hp : 0f);
                stream.SendNext(turrets[0] ? turrets[0].level : 0); stream.SendNext(turrets[1] ? turrets[1].level : 0);
                return;
            }
            wave=(int)stream.ReceiveNext(); countdown=(float)stream.ReceiveNext(); waveActive=(bool)stream.ReceiveNext(); credits=(int)stream.ReceiveNext();
            kills=(int)stream.ReceiveNext(); clearedWaves=(int)stream.ReceiveNext(); elapsed=(float)stream.ReceiveNext();
            float coreHp=(float)stream.ReceiveNext(); if(baseUnit)baseUnit.hp=coreHp;
            int left=(int)stream.ReceiveNext(), right=(int)stream.ReceiveNext();
            if(turrets[0])turrets[0].level=left; if(turrets[1])turrets[1].level=right;
        }
        void OnDestroy()
        {
            Time.timeScale = 1; if (Instance == this) Instance = null;
            if (PhotonNetwork.PrefabPool is DefensePool) PhotonNetwork.PrefabPool = new DefaultPool();
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
    }
}
