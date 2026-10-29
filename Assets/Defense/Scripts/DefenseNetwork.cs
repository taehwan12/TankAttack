using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace IronKeep
{
    // Photon room flow for the lobby: create / join / quick join, plus offline mode for solo play.
    public class DefenseNetwork : MonoBehaviourPunCallbacks
    {
        public const string DifficultyKey = "d", TankKey = "t";
        public const byte MaxPlayers = 4;
        static DefenseNetwork instance;
        public static DefenseNetwork Instance
        {
            get
            {
                if (!instance)
                {
                    var go = new GameObject("Defense Network"); DontDestroyOnLoad(go);
                    instance = go.AddComponent<DefenseNetwork>();
                }
                return instance;
            }
        }
        public string Status = "솔로 / 오프라인";
        public readonly Dictionary<string, RoomInfo> Rooms = new Dictionary<string, RoomInfo>();
        System.Action pending;
        bool soloAfterDisconnect;

        public static bool Online => PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode;
        public static bool IsHost => !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;

        void Awake() { PhotonNetwork.AutomaticallySyncScene = true; }

        void Connect(System.Action then)
        {
            PhotonNetwork.AutomaticallySyncScene = true;
            PhotonNetwork.NickName = DefenseRanking.Nickname;
            if (PhotonNetwork.OfflineMode) PhotonNetwork.OfflineMode = false;
            if (PhotonNetwork.InRoom) { Status = "이미 방에 있습니다. 먼저 방을 나가세요."; return; }
            pending = then;
            if (PhotonNetwork.InLobby) { RunPending(); return; }
            if (!PhotonNetwork.IsConnected) { Status = "Photon 서버 연결 중..."; PhotonNetwork.ConnectUsingSettings(); }
            else if (PhotonNetwork.IsConnectedAndReady) PhotonNetwork.JoinLobby();
        }
        void RunPending() { var p = pending; pending = null; p?.Invoke(); }
        static RoomOptions Options() => new RoomOptions { MaxPlayers = MaxPlayers, IsVisible = true, IsOpen = true, CleanupCacheOnLeave = true };
        public static string CleanRoomName(string value)
            => string.IsNullOrWhiteSpace(value) ? "IronKeep-" + Random.Range(1000, 9999) : DefenseRanking.CleanName(value);

        public void Browse() => Connect(() => Status = "로비 접속 / 방을 만들거나 참가하세요");
        public void CreateRoom(string name) { string n = CleanRoomName(name); Connect(() => { Status = $"방 '{n}' 만드는 중..."; PhotonNetwork.CreateRoom(n, Options()); }); }
        public void JoinRoom(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) { Status = "참가할 방 이름을 입력하거나 목록에서 고르세요."; return; }
            Connect(() => { Status = $"방 '{name}' 참가 중..."; PhotonNetwork.JoinRoom(name.Trim()); });
        }
        public void QuickJoin() => Connect(() => { Status = "빈 방 찾는 중..."; PhotonNetwork.JoinRandomOrCreateRoom(roomOptions: Options()); });
        public void LeaveRoom() { if (Online) { Status = "방에서 나가는 중..."; PhotonNetwork.LeaveRoom(); } }

        // Called by the deploy button. Online: only the host starts. Otherwise start a solo offline room.
        public bool Deploy()
        {
            if (Online)
            {
                if (!PhotonNetwork.IsMasterClient) { Status = "방장이 출격을 시작할 때까지 기다리세요."; return false; }
                PhotonNetwork.CurrentRoom.IsOpen = false;
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { DifficultyKey, DefenseOptions.DifficultyIndex } });
                PhotonNetwork.LoadLevel("Main");
                return true;
            }
            if (PhotonNetwork.IsConnected && !PhotonNetwork.OfflineMode)
            { soloAfterDisconnect = true; Status = "솔로 출격 준비 중..."; PhotonNetwork.Disconnect(); return true; }
            StartOffline(); PhotonNetwork.LoadLevel("Main");
            return true;
        }
        public static void StartOffline()
        {
            if (!PhotonNetwork.OfflineMode) PhotonNetwork.OfflineMode = true;
            if (!PhotonNetwork.InRoom) PhotonNetwork.CreateRoom("solo");
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { DifficultyKey, DefenseOptions.DifficultyIndex } });
        }
        public static int RoomDifficulty()
        {
            if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(DifficultyKey, out object d) && d is int value)
                return Mathf.Clamp(value, 0, 3);
            return DefenseOptions.DifficultyIndex;
        }
        public static void SetDifficulty(int index)
        {
            DefenseOptions.SelectedDifficulty = Mathf.Clamp(index, 0, 3);
            if (Online && PhotonNetwork.IsMasterClient)
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { DifficultyKey, DefenseOptions.DifficultyIndex } });
        }
        public static void SetTank(int index)
        {
            if (Online) PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { TankKey, index } });
        }

        // Non-lobby scene changes. The online host moves everyone; others and solo players move alone.
        public static void LoadScene(string scene)
        {
            Time.timeScale = 1;
            if (scene == "Main") { Instance.Deploy(); return; }
            if (Online && PhotonNetwork.IsMasterClient) { PhotonNetwork.LoadLevel(scene); return; }
            if (PhotonNetwork.OfflineMode && scene == "Intro") { PhotonNetwork.OfflineMode = false; Instance.Status = "솔로 / 오프라인"; }
            SceneManager.LoadScene(scene);
        }

        public string RoomSummary()
        {
            if (!Online) return Status;
            var room = PhotonNetwork.CurrentRoom;
            var names = new List<string>();
            foreach (var p in room.Players.Values)
            {
                string tank = p.CustomProperties.TryGetValue(TankKey, out object t) && t is int i ? DefenseOptions.Tanks[Mathf.Clamp(i, 0, 2)].name : "?";
                names.Add($"{p.NickName} ({tank}){(p.IsMasterClient ? " ★방장" : "")}");
            }
            return $"방 '{room.Name}'  {room.PlayerCount}/{room.MaxPlayers}명\n" + string.Join(" · ", names);
        }

        public override void OnConnectedToMaster()
        {
            if (PhotonNetwork.OfflineMode) return;
            Status = "서버 연결됨 / 로비 입장 중...";
            if (!PhotonNetwork.InLobby) PhotonNetwork.JoinLobby();
        }
        public override void OnJoinedLobby() { Status = "로비 접속 / 방을 만들거나 참가하세요"; RunPending(); }
        public override void OnRoomListUpdate(List<RoomInfo> list)
        {
            foreach (var r in list) { if (r.RemovedFromList || !r.IsOpen || !r.IsVisible) Rooms.Remove(r.Name); else Rooms[r.Name] = r; }
        }
        public override void OnJoinedRoom()
        {
            if (PhotonNetwork.OfflineMode) return;
            Rooms.Clear();
            Status = $"방 '{PhotonNetwork.CurrentRoom.Name}' 참가 완료";
            SetTank(DefenseOptions.TankIndex);
            if (PhotonNetwork.IsMasterClient) SetDifficulty(DefenseOptions.DifficultyIndex);
            else DefenseOptions.SelectedDifficulty = RoomDifficulty();
        }
        public override void OnLeftRoom() { if (!PhotonNetwork.OfflineMode) Status = "방에서 나왔습니다."; }
        public override void OnRoomPropertiesUpdate(Hashtable changed)
        {
            if (changed.ContainsKey(DifficultyKey)) DefenseOptions.SelectedDifficulty = RoomDifficulty();
        }
        public override void OnCreateRoomFailed(short code, string message) => Status = "방 만들기 실패: " + message;
        public override void OnJoinRoomFailed(short code, string message) => Status = "방 참가 실패: " + message;
        public override void OnJoinRandomFailed(short code, string message) => Status = "빠른 참가 실패: " + message;
        public override void OnMasterClientSwitched(Player newMaster)
        {
            // Wave state lives on the host, so a host leaving mid-battle ends the battle for everyone.
            if (SceneManager.GetActiveScene().name == "Main" && Online)
            { Status = "방장이 나가 전투가 종료되었습니다."; SceneManager.LoadScene("Intro"); }
        }
        public override void OnDisconnected(DisconnectCause cause)
        {
            Rooms.Clear();
            if (soloAfterDisconnect)
            {
                soloAfterDisconnect = false;
                StartOffline(); PhotonNetwork.LoadLevel("Main");
                return;
            }
            if (PhotonNetwork.OfflineMode) return;
            Status = cause == DisconnectCause.DisconnectByClientLogic ? "연결 해제" : "연결 끊김: " + cause;
            if (SceneManager.GetActiveScene().name == "Main") SceneManager.LoadScene("Intro");
        }
    }

    // Maps short network ids to the prefabs referenced by DefenseGame and attaches the PhotonView at runtime.
    public class DefensePool : IPunPrefabPool
    {
        readonly DefenseGame game;
        public DefensePool(DefenseGame value) { game = value; }
        GameObject Prefab(string id)
        {
            int i = id.Length > 1 ? id[1] - '0' : 0;
            if (id == "T") return game.turretPrefab;
            if (id[0] == 'P') return game.playerPrefabs != null && game.playerPrefabs.Length == 3 ? game.playerPrefabs[i] : game.tankPrefab;
            return game.enemyPrefabs != null && game.enemyPrefabs.Length == 4 ? game.enemyPrefabs[i] : game.tankPrefab;
        }
        public GameObject Instantiate(string id, Vector3 position, Quaternion rotation)
        {
            var go = Object.Instantiate(Prefab(id), position, rotation);
            go.SetActive(false);
            var view = go.GetComponent<PhotonView>();
            if (!view) view = go.AddComponent<PhotonView>();
            var sync = go.GetComponent<DefenseSync>();
            if (!sync) sync = go.AddComponent<DefenseSync>();
            view.ObservedComponents = new List<Component> { sync };
            view.Synchronization = ViewSynchronization.UnreliableOnChange;
            view.OwnershipTransfer = OwnershipOption.Fixed;
            return go;
        }
        public void Destroy(GameObject go) => Object.Destroy(go);
    }
}
