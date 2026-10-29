using UnityEngine;

namespace IronKeep
{
    public static class DefenseOptions
    {
        public struct TankSpec
        {
            public string name, role, weakness;
            public float hp, speed, damage, reload;
            public Color color;
            public Vector3 scale;
            public TankSpec(string n,string r,string w,float h,float s,float d,float cooldown,Color c,Vector3 size)
            { name=n;role=r;weakness=w;hp=h;speed=s;damage=d;reload=cooldown;color=c;scale=size; }
        }
        public static readonly TankSpec[] Tanks = {
            new TankSpec("VANGUARD","BALANCED / dependable armor and firepower","No exceptional speed or burst damage",190,12,32,.65f,new Color(.15f,.62f,.65f),Vector3.one),
            new TankSpec("KESTREL","SCOUT / fastest movement and rapid fire","Light armor and lower damage per shell",130,17,23,.42f,new Color(.45f,.65f,.24f),new Vector3(.86f,.86f,1)),
            new TankSpec("BASTION","HEAVY / strongest armor and shell damage","Slow movement and long reload",300,8,48,1.05f,new Color(.7f,.48f,.2f),new Vector3(1.2f,1.1f,1.08f))
        };
        public static readonly string[] DifficultyNames = { "EASY", "NORMAL", "HARD", "무한 모드" };
        public struct EnemySpec
        {
            public string name;
            public float hp,speed,damage,range,reload,size;
            public Color color;
            public EnemySpec(string n,float h,float s,float d,float r,float cooldown,float scale,Color c)
            { name=n;hp=h;speed=s;damage=d;range=r;reload=cooldown;size=scale;color=c; }
        }
        public static readonly EnemySpec[] Enemies = {
            new EnemySpec("RAIDER",65,6.8f,8,20,2.2f,.95f,new Color(.8f,.28f,.16f)),
            new EnemySpec("SIEGE",135,3.6f,18,28,3.3f,1.18f,new Color(.48f,.25f,.13f)),
            new EnemySpec("MARKSMAN",75,4.2f,14,38,3.6f,1,new Color(.5f,.32f,.75f)),
            new EnemySpec("DEMOLISHER",48,8.7f,25,11,3.8f,.85f,new Color(.95f,.7f,.15f))
        };
        public static readonly string[] DifficultyDetails = {
            "Fewer enemies / low incoming damage / stronger core",
            "Recommended / gentler than the original mission",
            "More enemies / full enemy strength / limited repairs",
            "끝없는 웨이브 / 점진적 강화 / 닉네임별 로컬 랭킹"
        };
        public static int SelectedTank = 0, SelectedDifficulty = 1;
        public static int TankIndex => Mathf.Clamp(SelectedTank,0,Tanks.Length-1);
        public static int DifficultyIndex => Mathf.Clamp(SelectedDifficulty,0,3);
        public static int WaveCount(int stage,int difficulty) => difficulty == 3 ? Mathf.Min(24,4+Mathf.Max(0,stage-1)/2) : difficulty == 2 ? 3+stage*2 : (difficulty == 0 ? 2 : 3)+stage;
        public static int TotalEnemies(int difficulty)
        { int total=0;for(int i=1;i<=5;i++)total+=WaveCount(i,difficulty);return total; }
        public static float EnemyHealth(int difficulty) => difficulty==0?.65f:difficulty==1||difficulty==3?.8f:1;
        public static float EnemyDamage(int difficulty) => difficulty==0?.5f:difficulty==1||difficulty==3?.7f:1;
        public static float CoreHealth(int difficulty) => difficulty==0?950:difficulty==1||difficulty==3?800:600;
        public static int RepairCost(int difficulty) => difficulty==0?40:difficulty==1?50:60;
        public static string RecordKey(int difficulty) => "IronKeepBest_"+DifficultyNames[difficulty];
        public static void ApplyLook(GameObject obj,int tankIndex,float baseScale)
        {
            // Imported variants already carry their original meshes, materials and normalized scale.
            if(obj.GetComponent<DefenseModel>())return;
            obj.transform.localScale=Vector3.one*baseScale;
        }
    }
}
