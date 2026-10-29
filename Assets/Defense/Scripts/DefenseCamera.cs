using UnityEngine;
namespace IronKeep {
    // Behind-the-tank third-person camera with mouse look and zoom; V switches to a gunner (first-person) view.
    public class DefenseCamera : MonoBehaviour
    {
        public static DefenseCamera Active { get; private set; }
        public Transform target;
        public bool firstPerson;
        public float sensitivity = 2.2f;
        float yaw, pitch = 8, distance = 11;
        Transform lastTarget;
        void Awake()
        {
            Active = this;
            var cam = GetComponent<Camera>();
            if (cam) { cam.orthographic = false; cam.fieldOfView = 60; cam.nearClipPlane = .1f; cam.farClipPlane = 400; }
        }
        void OnDestroy() { if (Active == this) Active = null; }
        void LateUpdate()
        {
            if (!target) return;
            if (target != lastTarget) { lastTarget = target; yaw = target.eulerAngles.y; }
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                yaw += Input.GetAxis("Mouse X") * sensitivity;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * sensitivity, firstPerson ? -15 : -5, firstPerson ? 30 : 55);
                distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * 1.2f, 6, 20);
                if (Input.GetKeyDown(KeyCode.V)) firstPerson = !firstPerson;
            }
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 pivot = target.position + Vector3.up * 2.6f;
            Vector3 position;
            // Commander's hatch: above the turret and right of the barrel so the gun does not cover the crosshair.
            if (firstPerson) position = target.position + Vector3.up * 3.3f + Quaternion.Euler(0, yaw, 0) * new Vector3(.75f, 0, .3f);
            else
            {
                Vector3 desired = pivot - rotation * Vector3.forward * distance + rotation * Vector3.right * 1.1f;
                Vector3 dir = desired - pivot; float length = dir.magnitude;
                position = desired;
                foreach (var hit in Physics.SphereCastAll(pivot, .3f, dir / length, length, ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.transform.IsChildOf(target) && hit.distance > 0 && hit.distance - .2f < (position - pivot).magnitude)
                        position = pivot + dir / length * Mathf.Max(.5f, hit.distance - .2f);
            }
            transform.SetPositionAndRotation(position, rotation);
        }
        // World point under the screen-centre crosshair, ignoring the given tank's own colliders.
        public Vector3 AimPoint(Transform self)
        {
            var hits = Physics.RaycastAll(transform.position, transform.forward, 400, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; Vector3 point = transform.position + transform.forward * 200;
            foreach (var hit in hits)
                if (!hit.transform.IsChildOf(self) && hit.distance < best) { best = hit.distance; point = hit.point; }
            return point;
        }
    }
}
