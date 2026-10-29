using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronKeep.Editor
{
    public static class DefenseChecks
    {
        public static string Result = "Not run";
        static double deadline;
        static int observedWaves;
        static int savedBest;
        static bool hadBest;
        static int expectedEnemies;
        static string recordKey;
        // Integration check: accelerate a fresh mission, eliminate spawned units through
        // the real damage/reward path, and verify the wave coroutine reaches Victory.
        // This checks progression, not player skill or difficulty balance.
        [MenuItem("Iron Keep/Check Wave Progression (Play Mode)")]
        public static string Run()
        {
            if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "Main")
                throw new InvalidOperationException("Run from a fresh Main scene in Play Mode.");
            EditorApplication.update -= Tick;
            Result="Running"; observedWaves=0; deadline=EditorApplication.timeSinceStartup+60;
            expectedEnemies=DefenseOptions.TotalEnemies(DefenseGame.Instance.difficultyIndex);
            recordKey=DefenseOptions.RecordKey(DefenseGame.Instance.difficultyIndex);
            hadBest=PlayerPrefs.HasKey(recordKey);savedBest=PlayerPrefs.GetInt(recordKey,0);
            Time.timeScale=20;
            EditorApplication.update += Tick;
            return Result;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish("Stopped: Play Mode ended");return;}
            if(EditorApplication.timeSinceStartup>deadline){Finish("FAIL: progression timed out");return;}
            if(SceneManager.GetActiveScene().name=="Victory")
            {
                Finish(observedWaves==5 && DefenseGame.LastKills==expectedEnemies ? $"PASS: 5 waves / {expectedEnemies} enemies / Victory scene" : "FAIL: incorrect wave or kill count");
                return;
            }
            var game=DefenseGame.Instance;
            if(!game || !game.playerUnit || !game.baseUnit)return;
            observedWaves=Mathf.Max(observedWaves,game.wave);
            game.playerUnit.hp=game.playerUnit.maxHp;
            game.baseUnit.hp=game.baseUnit.maxHp;
            foreach(var unit in game.enemies.ToArray()) if(unit)unit.Damage(10000);
        }
        static void Finish(string result)
        {
            EditorApplication.update -= Tick; Time.timeScale=1;Result=result;
            if(hadBest)PlayerPrefs.SetInt(recordKey,savedBest);else PlayerPrefs.DeleteKey(recordKey);
            PlayerPrefs.Save();
            Debug.Log("Iron Keep check: "+result);
        }
    }
}
