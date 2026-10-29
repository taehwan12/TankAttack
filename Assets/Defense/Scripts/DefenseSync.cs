using Photon.Pun;
using UnityEngine;

namespace IronKeep
{
    // Streams body, turret and barrel transforms from the owner; remote copies interpolate.
    public class DefenseSync : MonoBehaviourPun, IPunObservable
    {
        Transform head, barrel;
        Vector3 netPosition;
        Quaternion netRotation, netHead, netBarrel;
        bool received;
        void Start()
        {
            var tank = GetComponent<DefenseTank>();
            if (tank) { head = tank.turret; barrel = tank.barrel; }
            else { var turret = GetComponent<DefenseTurret>(); if (turret) head = turret.head; }
        }
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(transform.position); stream.SendNext(transform.rotation);
                stream.SendNext(head ? head.rotation : Quaternion.identity);
                stream.SendNext(barrel ? barrel.localRotation : Quaternion.identity);
                return;
            }
            netPosition = (Vector3)stream.ReceiveNext(); netRotation = (Quaternion)stream.ReceiveNext();
            netHead = (Quaternion)stream.ReceiveNext(); netBarrel = (Quaternion)stream.ReceiveNext();
            if (!received) { transform.SetPositionAndRotation(netPosition, netRotation); received = true; }
        }
        void Update()
        {
            if (!received || photonView.IsMine) return;
            float t = 14 * Time.deltaTime;
            transform.position = (transform.position - netPosition).sqrMagnitude > 64 ? netPosition : Vector3.Lerp(transform.position, netPosition, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, netRotation, t);
            if (head) head.rotation = Quaternion.Slerp(head.rotation, netHead, t);
            if (barrel) barrel.localRotation = Quaternion.Slerp(barrel.localRotation, netBarrel, t);
        }
    }
}
