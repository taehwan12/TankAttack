using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

namespace IronKeep.Editor
{
    public static class DefenseBuilder
    {
        const string Root = "Assets/Defense";
        static Material ground, road, concrete, steel, trim, cyan, gold, red, dark;
        static GameObject tank, explosion;
        static AudioClip shot;
        static TMP_FontAsset font;
        [MenuItem("Iron Keep/Build Defense Scenes")]
        public static string Build()
        {
            if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save your open scenes before rebuilding Defense scenes.");
            Directory.CreateDirectory(Root+"/Scenes"); Directory.CreateDirectory(Root+"/Materials");
            Directory.CreateDirectory(Root+"/Prefabs"); Directory.CreateDirectory(Root+"/Fonts");
            AssetDatabase.Refresh();
            ground=Mat("Ground",new Color(.22f,.25f,.23f)); road=Mat("Road",new Color(.09f,.12f,.14f));
            concrete=Mat("Concrete",new Color(.34f,.38f,.37f)); steel=Mat("Steel",new Color(.16f,.24f,.29f));
            trim=Mat("Hazard",new Color(.85f,.51f,.16f)); dark=Mat("Dark",new Color(.035f,.05f,.06f));
            cyan=Mat("Core Glow",new Color(.15f,.8f,.78f),true); gold=Mat("Shell Glow",new Color(1,.64f,.15f),true);
            red=Mat("Raider",new Color(.64f,.21f,.13f));
            var heavyMat=Mat("Siege",new Color(.33f,.18f,.13f));
            var playerMat=Mat("Player",new Color(.17f,.38f,.4f));
            var pickupGreen=Mat("Repair",new Color(.3f,.9f,.55f),true);
            var pickupBlue=Mat("Speed",new Color(.2f,.6f,1),true);
            var pickupPower=Mat("Power",new Color(1,.5f,.2f),true);
            var pickupRapid=Mat("Rapid Fire",new Color(.8f,.35f,1),true);
            explosion=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03.Prefabs/ExplosionPack/Prefabs/Small Explosion.prefab");
            shot=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/CannonFire.wav");
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"/Fonts/Defense Font.asset");
            if (!font)
            {
                var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/Photon/PhotonChat/Demos/Demo Chat/Resources/OpenSans/OpenSans-Semibold.ttf");
                font=TMP_FontAsset.CreateFontAsset(source,80,8,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024);
                string chars=new string(Enumerable.Range(32,95).Select(c=>(char)c).ToArray())+"°";
                font.TryAddCharacters(chars); font.atlasPopulationMode=AtlasPopulationMode.Static;
                AssetDatabase.CreateAsset(font,Root+"/Fonts/Defense Font.asset");
                foreach(var tex in font.atlasTextures) AssetDatabase.AddObjectToAsset(tex,font);
                AssetDatabase.AddObjectToAsset(font.material,font); EditorUtility.SetDirty(font);
            }
            var original=EditorSceneManager.OpenScene("Assets/01.Scenes/scBattleField.unity",OpenSceneMode.Single);
            var sourceTank=original.GetRootGameObjects().First(o=>o.name=="Tank");
            var copy=UnityEngine.Object.Instantiate(sourceTank);
            copy.name="Defense Tank";
            foreach(var script in copy.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(script);
            foreach(var c in copy.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
            copy.transform.position=Vector3.zero; copy.transform.rotation=Quaternion.identity; copy.transform.localScale=Vector3.one*.32f;
            var collider=copy.AddComponent<BoxCollider>(); collider.center=new Vector3(0,3,0); collider.size=new Vector3(7.5f,7,16);
            var rb=copy.GetComponent<Rigidbody>(); rb.mass=80; rb.useGravity=false;
            copy.AddComponent<DefenseUnit>();
            var tankLogic=copy.AddComponent<DefenseTank>();
            tankLogic.turret=copy.transform.Find("Turret"); tankLogic.barrel=tankLogic.turret.Find("Cannon");
            tankLogic.muzzle=tankLogic.barrel.Find("FirePos");
            foreach(var r in copy.GetComponentsInChildren<Renderer>())
                if(!r.name.ToLowerInvariant().Contains("track")&&!r.name.ToLowerInvariant().Contains("wheel")) r.sharedMaterial=playerMat;
            tank=PrefabUtility.SaveAsPrefabAsset(copy,Root+"/Prefabs/Defense Tank.prefab");
            UnityEngine.Object.DestroyImmediate(copy);
            // Preserve pre-existing build settings once, so the classroom example is recoverable.
            string backup=Root+"/OriginalBuildScenes.txt";
            if(!File.Exists(backup)) File.WriteAllLines(backup,EditorBuildSettings.scenes.Select(s=>(s.enabled?"1 ":"0 ")+s.path));
            foreach(string name in new[]{"Intro","Main","Victory","Defeat"})
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                SceneManager.SetActiveScene(scene);
                ConfigureLighting();
                var manager=new GameObject("Defense Game").AddComponent<DefenseGame>();
                manager.tankPrefab=tank; manager.explosionPrefab=explosion; manager.shotClip=shot;
                manager.font=font; manager.shellMaterial=gold; manager.lightEnemyMaterial=red; manager.heavyEnemyMaterial=heavyMat;
                manager.pickupMaterials=new[]{pickupGreen,pickupBlue,pickupRapid,pickupPower};
                if(name=="Main") BuildMain(manager); else BuildHangar(name);
                if(!EditorSceneManager.SaveScene(scene,Root+"/Scenes/"+name+".unity"))throw new IOException("Could not save "+name);
            }
            EditorBuildSettings.scenes=new[]{"Intro","Main","Victory","Defeat"}.Select(n=>new EditorBuildSettingsScene(Root+"/Scenes/"+n+".unity",true)).ToArray();
            PlayerSettings.companyName="Student Project"; PlayerSettings.productName="Iron Keep";
            PlayerSettings.defaultScreenWidth=1600; PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground=true;
            AssetDatabase.SaveAssets();
            // All original scenes were clean when inspected. Never overwrite the source scene.
            EditorSceneManager.OpenScene(Root+"/Scenes/Intro.unity",OpenSceneMode.Single);
            return "Created Intro, Main, Victory, Defeat; source example preserved.";
        }
        static Material Mat(string name,Color color,bool glow=false)
        {
            string path=Root+"/Materials/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}
            mat.color=color; mat.SetFloat("_Glossiness",.22f);
            if(glow){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*1.8f);}
            return mat;
        }
        static GameObject Box(string name,Vector3 position,Vector3 size,Material material,Transform parent=null,bool solid=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;
            if(parent)go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;
            if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        static void ConfigureLighting()
        {
            RenderSettings.skybox=null; RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.5f,.57f,.6f); RenderSettings.fog=false;
            var l=new GameObject("Late Afternoon Sun").AddComponent<Light>();l.type=LightType.Directional;l.intensity=1.25f;
            l.color=new Color(1,.87f,.71f);l.shadows=LightShadows.Soft;l.transform.rotation=Quaternion.Euler(48,-35,0);
            QualitySettings.shadowDistance=160; QualitySettings.antiAliasing=4;
        }
        static Camera Camera(Vector3 position,Vector3 angles,bool ortho)
        {
            var go=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));go.tag="MainCamera";
            go.transform.position=position;go.transform.rotation=Quaternion.Euler(angles);
            var c=go.GetComponent<Camera>();c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.065f,.09f,.12f);
            c.nearClipPlane=.3f;c.farClipPlane=300;c.orthographic=ortho;c.orthographicSize=29;c.fieldOfView=45;
            return c;
        }
        static void BuildMain(DefenseGame game)
        {
            var world=new GameObject("Industrial Defense Sector").transform;
            Box("Sector Ground",new Vector3(0,-.6f,0),new Vector3(100,1,114),ground,world);
            Box("Main Boulevard",new Vector3(0,-.04f,0),new Vector3(78,.1f,102),road,world,false);
            for(int lane=-1;lane<=1;lane++)
            {
                for(int z=-20;z<46;z+=8)Box("Lane Marker",new Vector3(lane*24,.04f,z),new Vector3(.16f,.03f,3),trim,world,false);
                Box("Enemy Entry Strip",new Vector3(lane*24,.06f,47),new Vector3(12,.05f,.4f),red,world,false);
            }
            // Buildings stay outside all three enemy lanes, so AI has open routes to the core.
            var rng=new System.Random(1701);
            for(int side=-1;side<=1;side+=2)
            {
                for(int z=-40;z<=48;z+=13)
                {
                    float h=rng.Next(5,13);
                    var b=Box("Industrial Block",new Vector3(side*48,h*.5f,z),new Vector3(8,h,10),concrete,world);
                    Box("Roof",new Vector3(0,.52f,0),new Vector3(1.05f,.04f,1.05f),steel,b.transform,false);
                    for(int row=0;row<3;row++)Box("Facade Strip",new Vector3(-side*.505f,-.25f+row*.22f,0),new Vector3(.02f,.055f,.78f),dark,b.transform,false);
                }
                Box("Boundary",new Vector3(side*44,1.2f,0),new Vector3(1,2.4f,104),concrete,world);
                for(int z=-36;z<48;z+=14)
                {
                    Box("Streetlamp",new Vector3(side*41,3,z),new Vector3(.22f,6,.22f),steel,world,false);
                    Box("Lamp",new Vector3(side*40.6f,6,z),new Vector3(1.2f,.18f,.5f),gold,world,false);
                }
            }
            Box("North Wall",new Vector3(0,1,53),new Vector3(90,2,1),concrete,world);
            Box("South Wall",new Vector3(0,1,-50),new Vector3(90,2,1),concrete,world);
            Box("Core Platform",new Vector3(0,.15f,-34),new Vector3(18,.5f,15),steel,world,false);
            var core=new GameObject("Command Core");core.transform.position=new Vector3(0,0,-34);
            Box("Armored Foundation",new Vector3(0,1,0),new Vector3(7,2,7),concrete,core.transform);
            Box("Reactor Housing",new Vector3(0,3.1f,0),new Vector3(5,2.5f,5),steel,core.transform);
            Box("Core Beacon",new Vector3(0,5.1f,0),new Vector3(2,2,2),cyan,core.transform,false);
            for(int side=-1;side<=1;side+=2)
                Box("Core Status Strip",new Vector3(side*2.53f,3.1f,0),new Vector3(.08f,.4f,4),cyan,core.transform,false);
            game.baseUnit=core.AddComponent<DefenseUnit>();game.baseUnit.Initialize(DefenseUnit.Team.Base,600);
            var player=(GameObject)PrefabUtility.InstantiatePrefab(tank);player.name="Player Tank";
            player.transform.position=new Vector3(0,.35f,-21);
            game.playerUnit=player.GetComponent<DefenseUnit>();game.playerUnit.Initialize(DefenseUnit.Team.Player,150);
            var cam=Camera(new Vector3(0,38,-50),new Vector3(53,0,0),true);
            cam.gameObject.AddComponent<DefenseCamera>().target=player.transform;
            for(int x=-1;x<=1;x+=2)
            {
                Box("Supply Container",new Vector3(x*26,1.5f,-38),new Vector3(9,3,5),steel,world);
                for(int i=0;i<6;i++)Box("Container Rib",new Vector3(x*26-4+i*1.5f,1.5f,-35.45f),new Vector3(.12f,2.8f,.12f),concrete,world,false);
            }
        }
        static void BuildHangar(string scene)
        {
            var root=new GameObject("Presentation Hangar").transform;
            Box("Hangar Floor",new Vector3(0,-.7f,0),new Vector3(100,1,100),road,root);
            Box("Deployment Pad",new Vector3(9,-.05f,1),new Vector3(19,.25f,23),steel,root);
            for(int i=-1;i<=1;i++)Box("Pad Stripe",new Vector3(9+i*7,.12f,1),new Vector3(.2f,.05f,22),scene=="Defeat"?red:cyan,root,false);
            var display=(GameObject)PrefabUtility.InstantiatePrefab(tank);display.name="Showcase Tank";
            display.transform.position=new Vector3(9,.25f,1);display.transform.rotation=Quaternion.Euler(0,-25,0);display.transform.localScale*=2.2f;
            var rb=display.GetComponent<Rigidbody>();rb.isKinematic=true;
            display.GetComponent<DefenseTank>().enabled=false;
            if(scene=="Defeat")
            {
                display.transform.rotation=Quaternion.Euler(8,-25,14);
                for(int i=0;i<9;i++)
                {
                    var debris=Box("Battle Wreckage",new Vector3(2+i*1.9f,.4f,-6+i%3*4),new Vector3(1.5f,.8f,2.2f),dark,root,false);
                    debris.transform.rotation=Quaternion.Euler(i*13,i*41,0);
                }
                Box("Burned Ground",new Vector3(9,.15f,1),new Vector3(14,.03f,18),dark,root,false);
                var sun=UnityEngine.Object.FindAnyObjectByType<Light>();sun.color=new Color(1,.52f,.36f);sun.intensity=.7f;
            }
            if(scene=="Victory")
            {
                Box("Secured Core Pedestal",new Vector3(20,1,10),new Vector3(5,2,5),steel,root,false);
                Box("Secured Core Signal",new Vector3(20,4,10),new Vector3(2.5f,4,2.5f),cyan,root,false);
                for(int i=0;i<3;i++)Box("Victory Signal Ring",new Vector3(20,3+i*1.8f,10),new Vector3(4,.18f,4),gold,root,false);
            }
            for(int i=0;i<6;i++)
            {
                var pillar=Box("Hangar Pillar",new Vector3(-25+i*12,6,20),new Vector3(2,12,3),concrete,root);
                Box("Safety Light",new Vector3(0,.2f,-.52f),new Vector3(.4f,.45f,.05f),scene=="Defeat"?red:gold,pillar.transform,false);
            }
            Box("Rear Beam",new Vector3(5,12,20),new Vector3(65,2,4),steel,root);
            var c=Camera(new Vector3(22,21,-28),new Vector3(31,-15,0),false);
            c.transform.LookAt(new Vector3(0,1,1));
        }
        [MenuItem("Iron Keep/Validate Defense")]
        public static string Validate()
        {
            var paths=new[]{"Intro","Main","Victory","Defeat"}.Select(n=>Root+"/Scenes/"+n+".unity").ToArray();
            if(paths.Any(p=>!File.Exists(p)))throw new Exception("Missing required scene");
            var p=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Defense Tank.prefab");
            if(!p || !p.GetComponent<DefenseTank>().muzzle || p.GetComponentsInChildren<MonoBehaviour>().Any(c=>!c))throw new Exception("Invalid tank prefab");
            if(EditorBuildSettings.scenes.Length!=4)throw new Exception("Build must include four scenes");
            if(!font)font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"/Fonts/Defense Font.asset");
            if(!font || !font.HasCharacter('A'))throw new Exception("Font missing");
            return "PASS: four scenes, build order, tank references, font";
        }
    }
}
