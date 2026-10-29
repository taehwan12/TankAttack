using Photon.Pun;
using UnityEngine;

namespace IronKeep
{
    // Built by the host; it aims and fires on the host and its head rotation is streamed by DefenseSync.
    public class DefenseTurret : MonoBehaviourPun, IPunInstantiateMagicCallback
    {
        public Transform head,muzzle;
        public int level=1;
        public float Damage=>15+(level-1)*8;
        public float Range=>27+(level-1)*2;
        public float Reload=>2f/(1+(level-1)*.2f);
        float nextShot;
        public void OnPhotonInstantiate(PhotonMessageInfo info)
        {
            int index=(int)photonView.InstantiationData[0];
            name=index==0?"Left Defense Turret":"Right Defense Turret";
            if(DefenseGame.Instance)DefenseGame.Instance.turrets[index]=this;
        }
        void Update()
        {
            var g=DefenseGame.Instance;if(!g||g.finished||g.Frozen||!DefenseGame.Authority)return;
            DefenseUnit target=null;float nearest=Range*Range;
            foreach(var enemy in g.enemies)
            {
                if(!enemy||enemy.Dead)continue;
                float distance=(enemy.transform.position-transform.position).sqrMagnitude;
                if(distance>=nearest)continue;
                Vector3 aim=enemy.transform.position+Vector3.up*1.1f;
                if(Physics.Linecast(muzzle.position,aim,out var hit)&&hit.collider.GetComponentInParent<DefenseUnit>()!=enemy)continue;
                nearest=distance;target=enemy;
            }
            if(!target)return;
            Vector3 point=target.transform.position+Vector3.up*1.1f;
            Vector3 flat=point-head.position;flat.y=0;
            if(flat.sqrMagnitude<.01f)return;
            head.rotation=Quaternion.RotateTowards(head.rotation,Quaternion.LookRotation(flat),130*Time.deltaTime);
            if(Time.time<nextShot||Vector3.Angle(head.forward,flat)>10)return;
            Vector3 delta=point-muzzle.position;float travel=delta.magnitude/DefenseTank.ShellSpeed;
            delta.y+=.5f*DefenseTank.ShellGravity*travel*travel;
            g.FireShell(muzzle.position,delta.normalized,g.baseUnit,Damage,0);
            nextShot=Time.time+Reload;
        }
    }
}
