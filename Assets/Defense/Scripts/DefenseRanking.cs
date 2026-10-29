using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IronKeep
{
    public static class DefenseRanking
    {
        public const string Key="IronKeepEndlessRankingV1";
        const string NameKey="IronKeepNickname";
        [Serializable] public class Entry { public string nickname; public int waves,kills; public float seconds; }
        [Serializable] class Table { public List<Entry> entries=new List<Entry>(); }
        public static string Nickname
        {
            get=>PlayerPrefs.GetString(NameKey,"전차장");
            set {PlayerPrefs.SetString(NameKey,CleanName(value));PlayerPrefs.Save();}
        }
        public static string CleanName(string value)
        {
            string clean=new string((value??"").Where(c=>!char.IsControl(c)&&c!='<'&&c!='>').Take(12).ToArray()).Trim();
            return string.IsNullOrEmpty(clean)?"전차장":clean;
        }
        public static List<Entry> Load()
        {
            try {return (JsonUtility.FromJson<Table>(PlayerPrefs.GetString(Key,"{}"))?.entries??new List<Entry>()).Where(e=>e!=null&&!string.IsNullOrEmpty(e.nickname)).OrderByDescending(e=>e.waves).ThenByDescending(e=>e.kills).ThenByDescending(e=>e.seconds).Take(10).ToList();}
            catch(ArgumentException){return new List<Entry>();}
        }
        static bool Better(Entry a,Entry b)=>a.waves>b.waves || a.waves==b.waves&&(a.kills>b.kills || a.kills==b.kills&&a.seconds>b.seconds);
        public static void Submit(string nickname,int waves,int kills,float seconds)
        {
            var entries=Load();var next=new Entry{nickname=CleanName(nickname),waves=Mathf.Max(0,waves),kills=Mathf.Max(0,kills),seconds=Mathf.Max(0,seconds)};
            var old=entries.Find(e=>e.nickname==next.nickname);
            if(old!=null&&!Better(next,old))return;
            if(old!=null)entries.Remove(old);entries.Add(next);
            entries=entries.OrderByDescending(e=>e.waves).ThenByDescending(e=>e.kills).ThenByDescending(e=>e.seconds).Take(10).ToList();
            PlayerPrefs.SetString(Key,JsonUtility.ToJson(new Table{entries=entries}));PlayerPrefs.Save();
        }
        public static string Display()
        {
            var entries=Load();if(entries.Count==0)return "아직 기록이 없습니다. 첫 기록을 남겨보세요.";
            return string.Join("\n",entries.Select((e,i)=>$"{i+1:00}  {e.nickname}  |  {e.waves} 웨이브  /  {e.kills} 처치"));
        }
    }
}
