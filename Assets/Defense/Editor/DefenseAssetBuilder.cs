using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace IronKeep.Editor
{
    public static class DefenseAssetBuilder
    {
        const string Source="Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/";
        const string Root="Assets/Defense/Prefabs/";
        static GameObject Asset(string path)=>AssetDatabase.LoadAssetAtPath<GameObject>(Source+path+".prefab")??throw new Exception("Missing asset "+path);
        static Transform Pivot(string name,Transform parent,Vector3 position)
        {var p=new GameObject(name).transform;p.SetParent(parent,false);p.position=position;return p;}
        static Bounds Bounds(GameObject o)
        {var r=o.GetComponentsInChildren<Renderer>();var b=r[0].bounds;foreach(var item in r.Skip(1))b.Encapsulate(item.bounds);return b;}
        static void Clean(GameObject o)
        {foreach(var c in o.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);foreach(var c in o.GetComponentsInChildren<MonoBehaviour>())UnityEngine.Object.DestroyImmediate(c);}
        // Modern tank A has its gun welded into the turret mesh. Split its original triangles
        // into a saved gun mesh so elevation still moves only the barrel, preserving materials.
        static Mesh Part(Mesh original,List<int>[] triangles,string name)
        {
            var mesh=new Mesh{name=name};var old=original.vertices;var normals=original.normals;var uv=original.uv;
            var map=new Dictionary<int,int>();var vertices=new List<Vector3>();var ns=new List<Vector3>();var uvs=new List<Vector2>();
            var result=new List<int[]>();
            foreach(var sub in triangles)
            {
                var output=new List<int>();foreach(int index in sub)
                {if(!map.TryGetValue(index,out int n)){n=vertices.Count;map[index]=n;vertices.Add(old[index]);if(normals.Length==old.Length)ns.Add(normals[index]);if(uv.Length==old.Length)uvs.Add(uv[index]);}output.Add(n);}
                result.Add(output.ToArray());
            }
            mesh.SetVertices(vertices);if(ns.Count==vertices.Count)mesh.SetNormals(ns);if(uvs.Count==vertices.Count)mesh.SetUVs(0,uvs);
            mesh.subMeshCount=result.Count;for(int i=0;i<result.Count;i++)mesh.SetTriangles(result[i],i);
            mesh.RecalculateBounds();if(ns.Count==0)mesh.RecalculateNormals();
            string path=Root+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);return existing;}
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static GameObject Tank(string asset,string name,float width,bool split=false)
        {
            var root=new GameObject(name);var model=UnityEngine.Object.Instantiate(Asset(asset),root.transform);model.name="Asset Model";Clean(model);
            var all=model.GetComponentsInChildren<Transform>();
            Transform head=all.First(t=>t.name.ToLowerInvariant().Contains("head"));
            Transform gun=all.FirstOrDefault(t=>t.name.ToLowerInvariant().Contains("-gun")||t.name.ToLowerInvariant().Contains("-cannon"));
            if(split)
            {
                var mf=head.GetComponent<MeshFilter>();var source=mf.sharedMesh;var vertices=source.vertices;
                var upper=new List<int>[source.subMeshCount];var barrel=new List<int>[source.subMeshCount];
                for(int sub=0;sub<source.subMeshCount;sub++)
                {
                    upper[sub]=new List<int>();barrel[sub]=new List<int>();var triangles=source.GetTriangles(sub);
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        Vector3 center=(vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3;
                        var dest=center.z>.3f&&Mathf.Abs(center.x)<.6f?barrel[sub]:upper[sub];
                        dest.Add(triangles[i]);dest.Add(triangles[i+1]);dest.Add(triangles[i+2]);
                    }
                }
                mf.sharedMesh=Part(source,upper,"Modern A Turret");
                var go=new GameObject("Original Asset Gun",typeof(MeshFilter),typeof(MeshRenderer));gun=go.transform;
                gun.SetParent(head.parent,false);gun.position=head.position;
                go.GetComponent<MeshFilter>().sharedMesh=Part(source,barrel,"Modern A Gun");
                go.GetComponent<MeshRenderer>().sharedMaterials=head.GetComponent<MeshRenderer>().sharedMaterials;
            }
            Vector3 headCenter=asset.Contains("Military")?new Vector3(0,head.GetComponent<Renderer>().bounds.center.y,0):head.position;
            var turret=Pivot("Turret Yaw",model.transform,headCenter);head.SetParent(turret,true);
            Transform pitch;
            if(gun)
            {
                Vector3 pivot=split?head.position+new Vector3(0,0,.3f):gun.position;
                pitch=Pivot("Barrel Pitch",turret,pivot);gun.SetParent(pitch,true);
            }
            else pitch=Pivot("Barrel Pitch",turret,head.position);
            var bounds=Bounds(root);float scale=width/bounds.size.x;model.transform.localScale*=scale;
            bounds=Bounds(root);model.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z*.15f);
            var muzzle=new GameObject("Muzzle").transform;muzzle.SetParent(pitch,false);
            var gunBounds=gun?gun.GetComponent<Renderer>().bounds:head.GetComponent<Renderer>().bounds;
            muzzle.position=new Vector3(0,gun?gunBounds.center.y:head.GetComponent<Renderer>().bounds.center.y,gunBounds.max.z+.15f);
            var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,1.05f,0);collider.size=new Vector3(width,2.1f,width*1.85f);
            root.AddComponent<Rigidbody>().mass=80;
            root.AddComponent<DefenseUnit>();var logic=root.AddComponent<DefenseTank>();logic.turret=turret;logic.barrel=pitch;logic.muzzle=muzzle;
            root.AddComponent<DefenseModel>().assetName=asset;
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+name+".prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
        }
        static GameObject Turret()
        {
            var root=new GameObject("Core Defense Turret");
            var baseModel=UnityEngine.Object.Instantiate(Asset("Scifi_M/Scifi_Turret_Base_Circle_Small"),root.transform);Clean(baseModel);baseModel.transform.localScale=Vector3.one*3.5f;
            var pivot=Pivot("Rotating Cannon",root.transform,new Vector3(0,1.4f,0));
            var head=UnityEngine.Object.Instantiate(Asset("Scifi_M/Scifi_Turret_Head_Cannon_Small"),pivot);Clean(head);head.transform.localScale=Vector3.one*3.5f;head.transform.localPosition=new Vector3(0,-1.4f,0);
            var bounds=Bounds(head);var muzzle=new GameObject("Turret Muzzle").transform;muzzle.SetParent(pivot,false);muzzle.position=new Vector3(0,bounds.center.y,bounds.max.z+.15f);
            var logic=root.AddComponent<DefenseTurret>();logic.head=pivot;logic.muzzle=muzzle;
            // Turrets have no blocking colliders: they cannot trap tanks or block the core repair route.
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"Core Defense Turret.prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
        }
        static GameObject Decor(string asset,Vector3 pos,float width,Quaternion rotation)
        {
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(Asset(asset));obj.transform.rotation=rotation;
            var bounds=Bounds(obj);obj.transform.localScale*=width/bounds.size.x;bounds=Bounds(obj);
            obj.transform.position=pos-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            foreach(var c in obj.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
            return obj;
        }
        public static string Apply()
        {
            if(UnityEditor.EditorApplication.isPlaying)throw new Exception("Stop play first");
            var balanced=Tank("WW2_M/Sherman","VANGUARD",2.6f);
            var scout=Tank("Military_M/Tank_A_Military","KESTREL",2.45f,true);
            var heavy=Tank("WW2_M/Tiger","BASTION",3.1f);
            var modern=Tank("Military_M/Tank_B_Military","RAIDER",2.6f);
            var turret=Turret();
            foreach(string name in new[]{"Intro","Main","Victory","Defeat"})
            {
                var scene=EditorSceneManager.OpenScene("Assets/Defense/Scenes/"+name+".unity",OpenSceneMode.Single);
                var game=UnityEngine.Object.FindAnyObjectByType<DefenseGame>();game.playerPrefabs=new[]{balanced,scout,heavy};game.enemyPrefabs=new[]{modern,heavy,balanced,scout};game.turretPrefab=turret;
                if(name=="Main")
                {
                    foreach(var block in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name=="Industrial Block").ToArray())
                    {var pos=block.position;pos.y=0;Decor(((int)Mathf.Abs(pos.z))%2==0?"Military_M/Military_Barracks":"WW2_M/Bunker_Big",pos,8,Quaternion.identity);UnityEngine.Object.DestroyImmediate(block.gameObject);}
                    Decor("Military_M/Military_Headquarters",new Vector3(0,0,-44),13,Quaternion.identity);
                    for(int side=-1;side<=1;side+=2)
                    {
                        Decor("WW2_M/Sandbags_Straight",new Vector3(side*12,0,-29),7,Quaternion.identity);
                        var pad=GameObject.CreatePrimitive(PrimitiveType.Cylinder);pad.name=side<0?"Left Turret Construction Pad":"Right Turret Construction Pad";pad.transform.position=new Vector3(side*12,-.03f,-34);pad.transform.localScale=new Vector3(6,.15f,6);
                        pad.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Defense/Materials/Steel.mat");UnityEngine.Object.DestroyImmediate(pad.GetComponent<Collider>());
                    }
                }
                var old=GameObject.Find(name=="Main"?"Player Tank":"Showcase Tank");
                Vector3 position=old.transform.position;Quaternion rotation=old.transform.rotation;UnityEngine.Object.DestroyImmediate(old);
                var replacement=(GameObject)PrefabUtility.InstantiatePrefab(balanced);replacement.name=name=="Main"?"Player Tank":"Showcase Tank";replacement.transform.SetPositionAndRotation(position,rotation);
                if(name=="Main"){game.playerUnit=replacement.GetComponent<DefenseUnit>();UnityEngine.Object.FindAnyObjectByType<DefenseCamera>().target=replacement.transform;}
                else{replacement.transform.localScale*=2.2f;replacement.GetComponent<Rigidbody>().isKinematic=true;replacement.GetComponent<DefenseTank>().enabled=false;}
                EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Defense/Scenes/Intro.unity",OpenSceneMode.Single);
            return "PASS: 4 original tank meshes, 3 selectable variants, 4 enemy variants, asset cannon/base turret and military scenery";
        }
    }
}
