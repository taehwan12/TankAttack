using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronKeep.Editor
{
    public static class DefenseFeatureChecks
    {
        public static string Result="Not run";
        static int phase,savedTank,savedDifficulty;
        static double deadline,readyAt;
        static DefenseUnit first,second;
        static float firstHp,secondHp;
        [MenuItem("Iron Keep/Check Lobby And Upgrades (Play Mode)")]
        public static string Run()
        {
            if(!EditorApplication.isPlaying || SceneManager.GetActiveScene().name!="Intro")throw new InvalidOperationException("Start in the lobby in Play Mode.");
            savedTank=DefenseOptions.TankIndex;savedDifficulty=DefenseOptions.DifficultyIndex;
            Result="Running";phase=0;deadline=EditorApplication.timeSinceStartup+60;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;return Result;
        }
        static void Require(bool value,string name){if(!value)throw new Exception(name);}
        static void Click(string name)
        {UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Single(b=>b.name==name).onClick.Invoke();}
        static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup>deadline)throw new Exception("Stopped or timed out");
                var game=DefenseGame.Instance;string scene=SceneManager.GetActiveScene().name;
                if(!game)return;
                if(phase==0)
                {
                    for(int i=0;i<3;i++)
                    {
                        Click(DefenseOptions.Tanks[i].name);Require(DefenseOptions.TankIndex==i,"Tank button");
                        Require(GameObject.Find("Showcase Tank").GetComponent<DefenseModel>().assetName==game.playerPrefabs[i].GetComponent<DefenseModel>().assetName,"Selected asset model preview");
                        Click(DefenseOptions.DifficultyNames[i]);Require(DefenseOptions.DifficultyIndex==i,"Difficulty button");
                    }
                    Click("EASY");Click("DEPLOY SELECTED TANK / ENTER");phase=1;return;
                }
                if(phase==1 && scene=="Main" && game.playerUnit.maxHp==300)
                {
                    game.StopAllCoroutines();Time.timeScale=0;
                    var player=game.playerUnit.GetComponent<DefenseTank>();player.enabled=false;
                    Require(game.baseUnit.maxHp==950&&game.RepairPrice==40,"Easy settings");
                    Require(player.moveSpeed==8&&player.damage==48&&player.reload==1.05f,"Heavy tank tradeoff");
                    for(int i=0;i<4;i++){game.SpawnEnemy(i,3);var e=game.enemies.Last();e.GetComponent<DefenseTank>().enabled=false;}
                    Require(game.enemies.Select(e=>e.GetComponent<DefenseTank>().enemyKind).Distinct().Count()==4,"Four enemy classes");
                    for(int i=0;i<3;i++)game.enemies[0].Damage(10000);
                    var drops=UnityEngine.Object.FindObjectsByType<DefensePickup>(FindObjectsSortMode.None);
                    Require(new[]{1,2,3}.All(k=>drops.Any(d=>d.kind==k)),"Kill drops include all three upgrades");
                    foreach(var d in drops)d.transform.position=game.playerUnit.transform.position;
                    phase=2;return;
                }
                if(phase==2)
                {
                    var tank=game.playerUnit.GetComponent<DefenseTank>();
                    Require(tank.areaLevel==1&&tank.hasteLevel==1&&tank.damageLevel==1,"Pickup collection");
                    Require(tank.SplashRadius==4&&tank.CurrentReload<tank.reload&&tank.DamageMultiplier==1.25f,"Upgrade combat effects");
                    tank.Shoot(Vector3.forward);
                    var shot=UnityEngine.Object.FindAnyObjectByType<DefenseShell>();
                    Require(shot&&shot.Splash==4&&Mathf.Approximately(shot.DamageValue,60),"Upgraded shot payload");
                    foreach(var s in UnityEngine.Object.FindObjectsByType<DefenseShell>(FindObjectsSortMode.None))UnityEngine.Object.Destroy(s.gameObject);
                    game.SpawnEnemy(2,3);first=game.enemies.Last();game.SpawnEnemy(3,3);second=game.enemies.Last();
                    first.GetComponent<DefenseTank>().enabled=second.GetComponent<DefenseTank>().enabled=false;
                    first.GetComponent<Rigidbody>().isKinematic=second.GetComponent<Rigidbody>().isKinematic=true;
                    first.transform.position=new Vector3(0,.35f,0);second.transform.position=new Vector3(2,.35f,0);
                    Physics.SyncTransforms();firstHp=first.hp;secondHp=second.hp;
                    DefenseShell.Create(new Vector3(0,1,-6),Vector3.forward,game.playerUnit,20,4);
                    Time.timeScale=1;readyAt=EditorApplication.timeSinceStartup+.7;phase=3;return;
                }
                if(phase==3 && EditorApplication.timeSinceStartup>=readyAt)
                {
                    Time.timeScale=0;
                    Require(Mathf.Approximately(first.hp,firstHp-20)&&Mathf.Approximately(second.hp,secondHp-20),"Real shell splash damages two enemies once each");
                    var tank=game.playerUnit.GetComponent<DefenseTank>();
                    for(int i=0;i<12;i++){tank.Upgrade(1);tank.Upgrade(2);tank.Upgrade(3);}
                    Require(tank.areaLevel==3&&tank.hasteLevel==5&&tank.damageLevel==5,"Upgrade caps");
                    game.baseUnit.hp=500;game.credits=100;game.Repair();Require(game.baseUnit.hp==600&&game.credits==60,"Difficulty repair cost");
                    DefenseOptions.SelectedTank=1;DefenseOptions.SelectedDifficulty=1;DefenseGame.Load("Main");phase=4;return;
                }
                if(phase==4 && scene=="Main" && game.playerUnit.maxHp==130)
                {
                    var t=game.playerUnit.GetComponent<DefenseTank>();
                    Require(t.moveSpeed==17&&t.damage==23&&t.reload==.42f&&game.baseUnit.maxHp==800,"Scout and normal settings");
                    Require(t.areaLevel==0&&t.hasteLevel==0&&t.damageLevel==0,"Upgrades reset on new run");
                    DefenseOptions.SelectedTank=0;DefenseOptions.SelectedDifficulty=2;DefenseGame.Load("Main");phase=5;return;
                }
                if(phase==5 && scene=="Main" && game.playerUnit.maxHp==190)
                {
                    var t=game.playerUnit.GetComponent<DefenseTank>();
                    Require(t.moveSpeed==12&&t.damage==32&&game.baseUnit.maxHp==600,"Balanced tank and hard settings");
                    Require(DefenseOptions.TotalEnemies(0)==25&&DefenseOptions.TotalEnemies(1)==30&&DefenseOptions.TotalEnemies(2)==45,"Difficulty wave counts");
                    game.baseUnit.Damage(10000);phase=6;return;
                }
                if(phase==6 && scene=="Defeat")
                {
                    Require(DefenseGame.LastTank==0&&DefenseGame.LastDifficulty==2&&DefenseGame.LastReason.Contains("core"),"Defeat report snapshot");
                    Click("RETURN TO LOADOUT LOBBY");phase=7;return;
                }
                if(phase==7 && scene=="Intro")Finish("PASS: lobby buttons / 3 player tanks / 3 difficulties / 4 enemies / kill drops / pickup stacking / real splash / reset / defeat and return");
            }
            catch(Exception error){Finish("FAIL: "+error.Message);}
        }
        static void Finish(string result)
        {
            EditorApplication.update-=Tick;Time.timeScale=1;Result=result;
            DefenseOptions.SelectedTank=savedTank;DefenseOptions.SelectedDifficulty=savedDifficulty;
            if(SceneManager.GetActiveScene().name=="Intro")
            {
                var hud=UnityEngine.Object.FindAnyObjectByType<DefenseHUD>();
                if(hud){hud.SelectTank(savedTank);hud.SelectDifficulty(savedDifficulty);}
            }
            Debug.Log("Iron Keep feature checks: "+result);
        }
    }
}
