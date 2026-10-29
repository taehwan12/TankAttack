using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronKeep.Editor
{
    public static class DefenseV3Checks
    {
        public static string Result="Not run";
        static int phase,savedTank,savedDifficulty;static double deadline,readyAt;
        static string savedRanking,savedName;static bool hadRanking;static DefenseUnit target;static float health;
        static void Require(bool v,string message){if(!v)throw new Exception(message);}
        public static string Run()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Play mode required");
            savedTank=DefenseOptions.SelectedTank;savedDifficulty=DefenseOptions.SelectedDifficulty;
            hadRanking=PlayerPrefs.HasKey(DefenseRanking.Key);savedRanking=PlayerPrefs.GetString(DefenseRanking.Key,"");savedName=DefenseRanking.Nickname;
            PlayerPrefs.DeleteKey(DefenseRanking.Key);
            DefenseRanking.Submit("점검",2,10,50);DefenseRanking.Submit("점검",1,99,90);
            Require(DefenseRanking.Load().Single().waves==2,"Worse run must not replace nickname best");
            DefenseRanking.Submit("점검",3,1,60);DefenseRanking.Submit("다른 전차장",3,2,60);
            Require(DefenseRanking.Load().Count==2&&DefenseRanking.Load()[0].nickname=="다른 전차장","Ranking order and JSON roundtrip");
            PlayerPrefs.DeleteKey(DefenseRanking.Key);DefenseRanking.Nickname="기능점검";
            DefenseOptions.SelectedTank=1;DefenseOptions.SelectedDifficulty=3;DefenseGame.Load("Main");
            phase=0;Result="Running";deadline=EditorApplication.timeSinceStartup+120;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;return Result;
        }
        static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup>deadline)throw new Exception("Stopped or timed out");
                var g=DefenseGame.Instance;if(!g)return;string scene=SceneManager.GetActiveScene().name;
                if(phase==0&&scene=="Main"&&g.playerUnit.maxHp==130)
                {
                    g.StopAllCoroutines();g.playerUnit.GetComponent<DefenseTank>().enabled=false;
                    Require(g.Endless&&g.playerPrefabs.Select(p=>p.GetComponent<DefenseModel>().assetName).Distinct().Count()==3,"Endless mode / three distinct player asset meshes");
                    g.playerUnit.transform.position=g.baseUnit.transform.position+new Vector3(0,0,10);g.credits=349;
                    Require(!g.BuildOrUpgradeTurret()&&g.credits==349&&g.TurretCount==0,"Insufficient credits must not build");
                    g.credits=1000;Require(g.BuildOrUpgradeTurret()&&g.turrets[0]&&!g.turrets[1]&&g.credits==650,"Left first / 350 credits");
                    Require(g.BuildOrUpgradeTurret()&&g.turrets[1]&&g.credits==300,"Right second / 350 credits");
                    Require(!g.BuildOrUpgradeTurret()&&g.credits==300,"Upgrade costs more than repairs");
                    g.credits=10000;Require(g.BuildOrUpgradeTurret()&&g.turrets[0].level==2&&g.turrets[1].level==1,"Upgrade lower level / left tie break");
                    Require(g.BuildOrUpgradeTurret()&&g.turrets[1].level==2,"Upgrade right next");
                    while(g.TurretPrice>0)Require(g.BuildOrUpgradeTurret(),"Upgrade to cap");
                    int money=g.credits;Require(!g.BuildOrUpgradeTurret()&&g.credits==money&&g.turrets.All(t=>t.level==5),"Cap level 5 without charging");
                    g.baseUnit.hp=500;g.credits=100;g.Repair();Require(g.baseUnit.hp==600&&g.credits==40,"R core repair still works");
                    g.SpawnEnemy(0,1);target=g.enemies.Last();target.GetComponent<DefenseTank>().enabled=false;target.GetComponent<Rigidbody>().isKinematic=true;
                    target.transform.position=new Vector3(-12,.35f,-15);target.hp=target.maxHp=1000;Physics.SyncTransforms();health=target.hp;
                    readyAt=EditorApplication.timeSinceStartup+5;phase=1;return;
                }
                if(phase==1&&EditorApplication.timeSinceStartup>=readyAt)
                {
                    Require(target.hp<health,"Asset turret auto aim and real projectile damage");
                    DefenseGame.Load("Main");phase=2;return;
                }
                if(phase==2&&scene=="Main"&&g.playerUnit.maxHp==130)
                {
                    Time.timeScale=35;g.playerUnit.GetComponent<DefenseTank>().enabled=false;
                    g.playerUnit.hp=g.playerUnit.maxHp;g.baseUnit.hp=g.baseUnit.maxHp;
                    foreach(var e in g.enemies.ToArray())if(e)e.Damage(100000);
                    if(g.wave<7)return;
                    Require(!g.finished&&g.clearedWaves>=6,"Endless must continue past campaign wave five");
                    g.StopAllCoroutines();Time.timeScale=1;
                    g.SpawnEnemy(0,1);var first=g.enemies.Last();g.SpawnEnemy(0,9);var later=g.enemies.Last();
                    // Same type in waves 1 and 9: index 0 selects SIEGE in both.
                    Require(later.maxHp>first.maxHp&&later.GetComponent<DefenseTank>().damage>first.GetComponent<DefenseTank>().damage,"Gradual enemy health and damage scaling");
                    g.baseUnit.Damage(100000);phase=3;return;
                }
                if(phase==3&&scene=="Defeat")
                {
                    var record=DefenseRanking.Load().Single(e=>e.nickname=="기능점검");
                    Require(record.waves==DefenseGame.LastClearedWaves&&record.waves>=6&&record.kills==DefenseGame.LastKills,"Run end records completed waves and kills");
                    DefenseGame.Load("Intro");phase=4;return;
                }
                if(phase==4&&scene=="Intro")Finish("PASS: asset variants / local ranking persistence and best-per-name / left-right construction / costs / upgrade order and cap / core repair / real turret damage / 7 endless waves / scaling / defeat recording");
            }
            catch(Exception e){Finish("FAIL: "+e.Message);}
        }
        static void Finish(string result)
        {
            EditorApplication.update-=Tick;Time.timeScale=1;Result=result;
            if(hadRanking)PlayerPrefs.SetString(DefenseRanking.Key,savedRanking);else PlayerPrefs.DeleteKey(DefenseRanking.Key);
            DefenseRanking.Nickname=savedName;DefenseOptions.SelectedTank=savedTank;DefenseOptions.SelectedDifficulty=savedDifficulty;PlayerPrefs.Save();
            Debug.Log("Iron Keep V3 checks: "+result);
        }
    }
}
