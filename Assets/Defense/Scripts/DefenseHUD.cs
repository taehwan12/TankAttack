using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

namespace IronKeep
{
    public class DefenseHUD : MonoBehaviour
    {
        DefenseGame game;
        Transform canvas;
        TextMeshProUGUI armor, core, status, ammo, message, credits, effects;
        TextMeshProUGUI loadoutStats,loadoutRole,loadoutWeakness,difficultyInfo;
        TMP_InputField nicknameInput, roomInput;
        TextMeshProUGUI networkStatus, deployLabel;
        readonly UnityEngine.UI.Button[] roomButtons=new UnityEngine.UI.Button[4];
        readonly string[] roomNames=new string[4];
        GameObject rankingPanel;
        readonly UnityEngine.UI.Button[] tankButtons=new UnityEngine.UI.Button[3],difficultyButtons=new UnityEngine.UI.Button[4];
        UnityEngine.UI.Image playerBar, coreBar;
        GameObject pausePanel;
        float messageUntil, nextRefresh;
        static readonly Color Ink = new Color(.035f,.055f,.075f,.94f);
        static readonly Color Cyan = new Color(.35f,.94f,.9f);
        static readonly Color Muted = new Color(.64f,.72f,.76f);
        static readonly Color Gold = new Color(1,.74f,.32f);
        public void Build(DefenseGame value, string scene)
        {
            game = value;
            var go = new GameObject("Defense UI", typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            canvas = go.transform;
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600,900); scaler.matchWidthOrHeight = .5f;
            if (!FindAnyObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            if (scene == "Main") BuildHud(); else BuildMenu(scene);
        }
        RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size, Vector2 anchor)
        {
            var o = new GameObject(name, typeof(RectTransform));
            var r = o.GetComponent<RectTransform>(); r.SetParent(parent,false);
            r.anchorMin = r.anchorMax = anchor; r.pivot = anchor; r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }
        UnityEngine.UI.Image Panel(string name, Transform parent, Vector2 pos, Vector2 size, Color color, Vector2 anchor)
        {
            var i = Rect(name,parent,pos,size,anchor).gameObject.AddComponent<UnityEngine.UI.Image>();
            i.color = color; i.raycastTarget = false; return i;
        }
        TextMeshProUGUI Text(string value, Transform parent, Vector2 pos, Vector2 size, int fontSize, Color color, Vector2 anchor)
        {
            var t = Rect(value,parent,pos,size,anchor).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = game.font; t.text = DefenseKorean.Translate(value); t.fontSize = fontSize; t.color = color;
            t.enableAutoSizing = false; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }
        UnityEngine.UI.Button Button(string value, Transform parent, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction action, bool accent = false)
        {
            var p = Panel(value,parent,pos,size,accent ? Cyan : new Color(.13f,.19f,.23f),new Vector2(0,1));
            p.raycastTarget = true;
            var b = p.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic = p; b.onClick.AddListener(action);
            var c = b.colors; c.highlightedColor = new Color(.75f,.95f,1); c.pressedColor = new Color(.5f,.65f,.72f); b.colors = c;
            var label = Text(value,p.transform,Vector2.zero,size,22,accent ? Ink : Color.white,new Vector2(.5f,.5f));
            label.alignment = TextAlignmentOptions.Center; label.fontStyle = FontStyles.Bold;
            return b;
        }
        void BuildMenu(string scene)
        {
            if(scene=="Intro"){BuildLobby();return;}
            bool intro = scene == "Intro", won = scene == "Victory";
            var p = Panel("Briefing Panel",canvas,new Vector2(50,-50),new Vector2(680,800),Ink,new Vector2(0,1));
            Panel("Accent",p.transform,new Vector2(32,-36),new Vector2(5,96),intro || won ? Cyan : Gold,new Vector2(0,1));
            Text("TACTICAL DEFENSE  /  OPERATION 01",p.transform,new Vector2(55,-32),new Vector2(590,30),16,Cyan,new Vector2(0,1));
            Text(intro ? "IRON KEEP" : won ? "SECTOR SECURED" : "DEFENSE FAILED",p.transform,new Vector2(52,-78),new Vector2(600,90),intro ? 68 : 49,Color.white,new Vector2(0,1));
            Text(intro ? "HOLD THE LINE. PROTECT THE CORE." : DefenseGame.LastReason,p.transform,new Vector2(55,-170),new Vector2(575,60),21,Muted,new Vector2(0,1));
            Panel("Divider",p.transform,new Vector2(55,-243),new Vector2(570,2),new Color(.25f,.34f,.38f),new Vector2(0,1));
            if (intro)
            {
                Text("MISSION BRIEF",p.transform,new Vector2(55,-270),new Vector2(570,35),23,Gold,new Vector2(0,1));
                Text("Survive 5 escalating waves. Keep both your tank and the command core alive. Collect supplies and spend credits on emergency core repairs.",p.transform,new Vector2(55,-316),new Vector2(570,106),22,Color.white,new Vector2(0,1));
                Text("WASD / ARROWS     Move & turn\nMOUSE / LEFT CLICK     Aim & fire\nQ / E / WHEEL     Barrel angle (-8 to 28)\n1 / 2     Armor-piercing / explosive shells\nR     Repair core nearby (60 credits)\nESC     Pause",p.transform,new Vector2(55,-442),new Vector2(580,204),20,Muted,new Vector2(0,1));
            }
            else
            {
                Text("AFTER-ACTION REPORT",p.transform,new Vector2(55,-275),new Vector2(570,35),22,Gold,new Vector2(0,1));
                int mins=(int)DefenseGame.LastDuration/60,secs=(int)DefenseGame.LastDuration%60;
                string bestReport=DefenseGame.LastDifficulty==3?$"완료 웨이브    {DefenseGame.LastClearedWaves}":$"BEST CLEAR    {PlayerPrefs.GetInt(DefenseOptions.RecordKey(DefenseGame.LastDifficulty),0)} KILLS";
                Text($"{DefenseOptions.Tanks[DefenseGame.LastTank].name} / {DefenseOptions.DifficultyNames[DefenseGame.LastDifficulty]}\nWAVES REACHED    {DefenseGame.LastWave} / {(DefenseGame.LastDifficulty==3?"∞":"5")}\nHOSTILES ELIMINATED    {DefenseGame.LastKills}\nCORE ARMOR    {Mathf.CeilToInt(DefenseGame.LastBaseHp)}\nMISSION TIME    {mins:00}:{secs:00}\nUPGRADES    AREA {DefenseGame.LastArea} / RATE {DefenseGame.LastHaste} / DMG {DefenseGame.LastDamage}\n{bestReport}",p.transform,new Vector2(55,-332),new Vector2(570,246),21,Color.white,new Vector2(0,1));
                if(DefenseGame.LastDifficulty==3)
                    Text($"{DefenseGame.LastNickname} / 완료 {DefenseGame.LastClearedWaves} 웨이브\n닉네임별 최고 기록을 로컬 랭킹에 저장했습니다.",p.transform,new Vector2(55,-565),new Vector2(570,43),17,Cyan,new Vector2(0,1));
                Button("RETURN TO LOADOUT LOBBY",p.transform,new Vector2(55,-630),new Vector2(570,52),()=>DefenseGame.Load("Intro"));
            }
            Button(intro ? "DEPLOY  /  ENTER" : "DEPLOY AGAIN  /  ENTER",p.transform,new Vector2(55,-686),new Vector2(570,64),()=>DefenseGame.Load("Main"),true);
            networkStatus=Text("",p.transform,new Vector2(55,-755),new Vector2(570,40),17,Cyan,new Vector2(0,1));
            Text(won?"THE CORE STANDS.\nTHE SECTOR IS SAFE.":"REGROUP. REARM.\nRETURN TO THE FRONT.",canvas,new Vector2(-55,55),new Vector2(600,100),28,won?Cyan:Gold,new Vector2(1,0)).alignment=TextAlignmentOptions.Right;
        }
        void BuildLobby()
        {
            var p=Panel("Loadout Lobby",canvas,new Vector2(40,-40),new Vector2(700,820),Ink,new Vector2(0,1));
            Text("IRON KEEP",p.transform,new Vector2(35,-26),new Vector2(630,80),61,Color.white,new Vector2(0,1));
            Text("LOADOUT LOBBY / PROTECT THE COMMAND CORE",p.transform,new Vector2(38,-118),new Vector2(630,30),17,Cyan,new Vector2(0,1));
            Text("01 / CHOOSE YOUR TANK",p.transform,new Vector2(38,-181),new Vector2(630,32),22,Gold,new Vector2(0,1));
            for(int i=0;i<3;i++)
            {
                int index=i;
                tankButtons[i]=Button(DefenseOptions.Tanks[i].name,p.transform,new Vector2(38+i*210,-230),new Vector2(198,57),()=>SelectTank(index));
            }
            loadoutStats=Text("",p.transform,new Vector2(38,-315),new Vector2(625,83),23,Color.white,new Vector2(0,1));
            loadoutRole=Text("",p.transform,new Vector2(38,-417),new Vector2(625,55),21,Cyan,new Vector2(0,1));
            loadoutWeakness=Text("",p.transform,new Vector2(38,-480),new Vector2(625,52),20,Muted,new Vector2(0,1));
            Text("02 / SELECT DIFFICULTY",p.transform,new Vector2(38,-553),new Vector2(630,32),22,Gold,new Vector2(0,1));
            for(int i=0;i<4;i++)
            {
                int index=i;
                difficultyButtons[i]=Button(DefenseOptions.DifficultyNames[i],p.transform,new Vector2(38+i*157,-598),new Vector2(146,48),()=>SelectDifficulty(index));
            }
            difficultyInfo=Text("",p.transform,new Vector2(38,-665),new Vector2(625,53),18,Muted,new Vector2(0,1));
            deployLabel=Button("DEPLOY SELECTED TANK / ENTER",p.transform,new Vector2(38,-738),new Vector2(618,60),Deploy,true).GetComponentInChildren<TextMeshProUGUI>();
            var controls=Panel("Mission Controls",canvas,new Vector2(-35,35),new Vector2(760,164),Ink,new Vector2(1,0));
            Text("5웨이브 임무 / 무한 모드 / 적 전차 4종 / 최대 4인 협동",controls.transform,new Vector2(22,-16),new Vector2(715,28),18,Gold,new Vector2(0,1));
            Text("WASD 이동 / 마우스 시점·조준 / 클릭 발사 / 휠 줌 / V 1인칭·3인칭\n1,2 포탄 / R 코어 수리 / T 포탑 건설·강화 / M 음악 / ESC 메뉴\n포탄 강화: 범위 피해 / 공격 속도 / 피해량",controls.transform,new Vector2(22,-59),new Vector2(715,94),19,Color.white,new Vector2(0,1));
            BuildMultiplayer();
            Button("로컬 랭킹 보기 / 닫기",canvas,new Vector2(1140,-35),new Vector2(425,48),()=>rankingPanel.SetActive(!rankingPanel.activeSelf));
            rankingPanel=Panel("Local Ranking",canvas,new Vector2(-35,-100),new Vector2(730,410),Ink,new Vector2(1,1)).gameObject;
            Text("무한 모드 / 이 PC의 랭킹",rankingPanel.transform,new Vector2(25,-18),new Vector2(680,35),26,Gold,new Vector2(0,1));
            Text("완료 웨이브 → 처치 수 → 생존 시간 / 닉네임별 최고 기록",rankingPanel.transform,new Vector2(25,-60),new Vector2(680,28),17,Muted,new Vector2(0,1));
            Text(DefenseRanking.Display(),rankingPanel.transform,new Vector2(25,-102),new Vector2(680,290),19,Color.white,new Vector2(0,1));
            rankingPanel.SetActive(false);
            nicknameInput=InputBox(canvas,new Vector2(-35,215),new Vector2(400,50),new Vector2(1,0),12,DefenseRanking.Nickname);
            nicknameInput.onEndEdit.AddListener(v=>{DefenseRanking.Nickname=v;nicknameInput.SetTextWithoutNotify(DefenseRanking.Nickname);Photon.Pun.PhotonNetwork.NickName=DefenseRanking.Nickname;});
            Text("닉네임 (랭킹·멀티 공통, 최대 12자)",canvas,new Vector2(-460,225),new Vector2(330,34),18,Color.white,new Vector2(1,0));
            if(DefenseNetwork.Online&&Photon.Pun.PhotonNetwork.IsMasterClient)Photon.Pun.PhotonNetwork.CurrentRoom.IsOpen=true;
            RefreshLobby();
        }
        TMP_InputField InputBox(Transform parent,Vector2 pos,Vector2 size,Vector2 anchor,int limit,string value,string hint="")
        {
            var panel=Panel("Input",parent,pos,size,new Color(.13f,.19f,.23f),anchor);
            panel.raycastTarget=true;var field=panel.gameObject.AddComponent<TMP_InputField>();
            var text=Text("",panel.transform,new Vector2(14,(size.y-34)*.5f),new Vector2(size.x-30,34),22,Color.white,new Vector2(0,0));
            field.textViewport=panel.rectTransform;field.textComponent=text;
            if(hint!="")field.placeholder=Text(hint,panel.transform,new Vector2(14,(size.y-34)*.5f),new Vector2(size.x-30,34),20,Muted,new Vector2(0,0));
            field.characterLimit=limit;field.richText=false;field.text=value;
            return field;
        }
        // Photon co-op: create / join / quick join rooms. Created before the ranking panel so the ranking overlays it.
        void BuildMultiplayer()
        {
            var m=Panel("Multiplayer",canvas,new Vector2(-35,-100),new Vector2(730,420),Ink,new Vector2(1,1));
            Text("03 / 멀티플레이 / 방 만들기 · 참가",m.transform,new Vector2(25,-16),new Vector2(680,32),22,Gold,new Vector2(0,1));
            networkStatus=Text("",m.transform,new Vector2(25,-56),new Vector2(680,74),17,Cyan,new Vector2(0,1));
            roomInput=InputBox(m.transform,new Vector2(25,-140),new Vector2(330,46),new Vector2(0,1),12,"","방 이름 (비우면 자동)");
            Button("방 만들기",m.transform,new Vector2(370,-140),new Vector2(160,46),()=>{CommitNickname();DefenseNetwork.Instance.CreateRoom(roomInput.text);});
            Button("방 참가",m.transform,new Vector2(545,-140),new Vector2(160,46),()=>{CommitNickname();DefenseNetwork.Instance.JoinRoom(roomInput.text);});
            Button("빠른 참가",m.transform,new Vector2(25,-198),new Vector2(330,46),()=>{CommitNickname();DefenseNetwork.Instance.QuickJoin();});
            Button("방 나가기",m.transform,new Vector2(370,-198),new Vector2(335,46),()=>DefenseNetwork.Instance.LeaveRoom());
            Text("열린 방 (클릭해서 참가)",m.transform,new Vector2(25,-258),new Vector2(400,28),17,Muted,new Vector2(0,1));
            Button("방 목록 새로고침",m.transform,new Vector2(470,-254),new Vector2(235,34),()=>{CommitNickname();DefenseNetwork.Instance.Browse();});
            for(int i=0;i<4;i++)
            {
                int index=i;
                roomButtons[i]=Button("",m.transform,new Vector2(25+i%2*345,-296-i/2*54),new Vector2(335,46),()=>{if(roomNames[index]!=null){CommitNickname();DefenseNetwork.Instance.JoinRoom(roomNames[index]);}});
            }
        }
        public bool EditingNickname=>nicknameInput&&nicknameInput.isFocused||roomInput&&roomInput.isFocused;
        public void CommitNickname(){if(nicknameInput)DefenseRanking.Nickname=nicknameInput.text;}
        public void Deploy(){CommitNickname();DefenseNetwork.Instance.Deploy();}
        public void SelectTank(int index)
        {DefenseOptions.SelectedTank=Mathf.Clamp(index,0,2);DefenseNetwork.SetTank(DefenseOptions.SelectedTank);RefreshLobby();game.RefreshShowcase();}
        public void SelectDifficulty(int index)
        {
            if(DefenseNetwork.Online&&!Photon.Pun.PhotonNetwork.IsMasterClient){DefenseNetwork.Instance.Status="난이도는 방장이 정합니다.";return;}
            DefenseNetwork.SetDifficulty(index);RefreshLobby();
        }
        void RefreshNetwork()
        {
            var net=DefenseNetwork.Instance;
            if(networkStatus)networkStatus.text=DefenseNetwork.Online?net.RoomSummary()+"\n"+net.Status:net.Status;
            if(deployLabel)deployLabel.text=!DefenseNetwork.Online?"혼자 출격 / ENTER":Photon.Pun.PhotonNetwork.IsMasterClient?"전원 출격 (방장) / ENTER":"방장의 출격 대기 중";
            if(roomButtons[0]==null)return;
            int i=0;
            foreach(var room in net.Rooms.Values)
            {
                if(i>=4)break;
                roomNames[i]=room.Name;roomButtons[i].gameObject.SetActive(true);
                roomButtons[i].GetComponentInChildren<TextMeshProUGUI>().text=$"{room.Name}  ({room.PlayerCount}/{room.MaxPlayers})";i++;
            }
            for(;i<4;i++){roomNames[i]=null;roomButtons[i].gameObject.SetActive(false);}
            if(DefenseNetwork.Online&&DefenseOptions.DifficultyIndex!=shownDifficulty)RefreshLobby();
        }
        int shownDifficulty=-1;
        void RefreshLobby()
        {
            if(!loadoutStats)return;
            var spec=DefenseOptions.Tanks[DefenseOptions.TankIndex];
            loadoutStats.text=DefenseKorean.Translate($"ARMOR {spec.hp:F0}    SPEED {spec.speed:F0}\nSHELL DAMAGE {spec.damage:F0}    RELOAD {spec.reload:F2}s");
            loadoutRole.text=DefenseKorean.Translate(spec.role);loadoutWeakness.text=DefenseKorean.Translate("WEAKNESS / "+spec.weakness);
            int d=DefenseOptions.DifficultyIndex;shownDifficulty=d;
            difficultyInfo.text=d==3?"끝없는 웨이브 / 조금씩 강해지는 적 / 로컬 랭킹\n코어 800 HP / 수리 60 크레딧 / 제한 없이 생존":DefenseKorean.Translate(DefenseOptions.DifficultyDetails[d]+$"\n{DefenseOptions.TotalEnemies(d)} enemies total / core {DefenseOptions.CoreHealth(d):F0} HP / repair {DefenseOptions.RepairCost(d)} credits");
            if(rankingPanel&&d!=3)rankingPanel.SetActive(false);
            for(int i=0;i<3;i++)
            {
                SetSelected(tankButtons[i],i==DefenseOptions.TankIndex);

            }
            for(int i=0;i<4;i++)SetSelected(difficultyButtons[i],i==d);
        }
        void SetSelected(UnityEngine.UI.Button button,bool selected)
        {
            button.targetGraphic.color=selected?Cyan:new Color(.13f,.19f,.23f);
            button.GetComponentInChildren<TextMeshProUGUI>().color=selected?Ink:Color.white;
        }
        void BuildHud()
        {
            var left = Panel("Armor Panel",canvas,new Vector2(28,-25),new Vector2(370,150),Ink,new Vector2(0,1));
            Text(DefenseOptions.Tanks[game.tankIndex].name+" / "+DefenseOptions.DifficultyNames[game.difficultyIndex],left.transform,new Vector2(22,-15),new Vector2(330,25),16,Cyan,new Vector2(0,1));
            armor=Text("TANK",left.transform,new Vector2(22,-48),new Vector2(325,26),20,Color.white,new Vector2(0,1));
            playerBar=Bar(left.transform,-81,Cyan);
            core=Text("CORE",left.transform,new Vector2(22,-96),new Vector2(325,24),20,Color.white,new Vector2(0,1));
            coreBar=Bar(left.transform,-128,Gold);
            var top=Panel("Wave Panel",canvas,new Vector2(0,-25),new Vector2(500,96),Ink,new Vector2(.5f,1));
            status=Text("WAVE",top.transform,new Vector2(0,-17),new Vector2(470,70),23,Color.white,new Vector2(.5f,1)); status.alignment=TextAlignmentOptions.Center;
            var right=Panel("Economy Panel",canvas,new Vector2(-28,-25),new Vector2(370,160),Ink,new Vector2(1,1));
            credits=Text("",right.transform,new Vector2(-18,-15),new Vector2(335,135),19,Gold,new Vector2(1,1)); credits.alignment=TextAlignmentOptions.Right;
            var bottom=Panel("Weapon Panel",canvas,new Vector2(28,25),new Vector2(470,110),Ink,new Vector2(0,0));
            ammo=Text("",bottom.transform,new Vector2(20,18),new Vector2(430,78),21,Color.white,new Vector2(0,0));
            effects=Text("",canvas,new Vector2(-28,25),new Vector2(590,94),19,Cyan,new Vector2(1,0)); effects.alignment=TextAlignmentOptions.Right;
            message=Text("",canvas,new Vector2(0,-152),new Vector2(1000,50),26,Gold,new Vector2(.5f,1)); message.alignment=TextAlignmentOptions.Center;
            Crosshair();
            pausePanel=Panel("Pause",canvas,Vector2.zero,new Vector2(1600,900),new Color(.015f,.025f,.04f,.94f),new Vector2(.5f,.5f)).gameObject;
            Text("OPERATIONS PAUSED",pausePanel.transform,new Vector2(0,150),new Vector2(800,70),46,Color.white,new Vector2(.5f,.5f)).alignment=TextAlignmentOptions.Center;
            Button("RESUME  /  ESC",pausePanel.transform,new Vector2(560,-420),new Vector2(480,65),()=>game.SetPaused(false),true);
            if(!game.Multiplayer)Button("RESTART MISSION",pausePanel.transform,new Vector2(560,-510),new Vector2(480,65),()=>DefenseGame.Load("Main"));
            else Text("멀티플레이 중에는 전투가 멈추지 않습니다.",pausePanel.transform,new Vector2(0,-60),new Vector2(800,40),20,Muted,new Vector2(.5f,.5f)).alignment=TextAlignmentOptions.Center;
            Button("RETURN TO LOADOUT LOBBY",pausePanel.transform,new Vector2(560,-600),new Vector2(480,65),()=>DefenseGame.Load("Intro"));
            pausePanel.SetActive(false);
        }
        // Four ticks and a centre dot, each drawn over a dark outline so it reads on both sky and ground.
        void Crosshair()
        {
            var centre=new Vector2(.5f,.5f);
            const float gap=9,length=18,width=4,outline=2;
            var bars=new[]{(new Vector2(0,gap+length*.5f),new Vector2(width,length)),(new Vector2(0,-gap-length*.5f),new Vector2(width,length)),
                           (new Vector2(gap+length*.5f,0),new Vector2(length,width)),(new Vector2(-gap-length*.5f,0),new Vector2(length,width)),
                           (Vector2.zero,new Vector2(width,width))};
            foreach(var (pos,size) in bars)
            {
                Panel("Crosshair Outline",canvas,pos,size+Vector2.one*outline*2,new Color(0,0,0,.85f),centre);
                Panel("Crosshair",canvas,pos,size,new Color(1,1,1,.95f),centre);
            }
        }
        UnityEngine.UI.Image Bar(Transform parent,float y,Color color)
        {
            var bg=Panel("Armor Track",parent,new Vector2(22,y),new Vector2(325,7),new Color(.2f,.25f,.28f),new Vector2(0,1));
            return Panel("Armor Fill",bg.transform,Vector2.zero,new Vector2(325,7),color,new Vector2(0,1));
        }
        public void SetPause(bool value) { if(pausePanel) pausePanel.SetActive(value); }
        public void Message(string value) { if(message){message.text=DefenseKorean.Translate(value);messageUntil=Time.unscaledTime+3;} }
        void Update()
        {
            if(!armor){if(networkStatus&&Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.25f;RefreshNetwork();}return;}
            if(!game.playerUnit || !game.baseUnit || Time.unscaledTime<nextRefresh) return;
            nextRefresh=Time.unscaledTime+.1f;
            armor.text=DefenseKorean.Translate($"TANK ARMOR     {Mathf.CeilToInt(game.playerUnit.hp)} / {game.playerUnit.maxHp:F0}");
            core.text=DefenseKorean.Translate($"CORE ARMOR     {Mathf.CeilToInt(game.baseUnit.hp)} / {game.baseUnit.maxHp:F0}");
            playerBar.rectTransform.sizeDelta=new Vector2(325*game.playerUnit.hp/game.playerUnit.maxHp,7);
            coreBar.rectTransform.sizeDelta=new Vector2(325*game.baseUnit.hp/game.baseUnit.maxHp,7);
            status.text=DefenseKorean.Translate($"WAVE {game.wave} / {(game.Endless?"∞":game.totalWaves.ToString())}\n"+(game.waveActive?$"HOSTILES {game.Alive}  /  KILLS {game.kills}":$"INBOUND IN {Mathf.Max(0,Mathf.CeilToInt(game.countdown))}s"));
            credits.text=$"{game.credits} 크레딧{(game.Multiplayer?$" (공용) / 아군 {game.players.Count}명":"")}\nR / 코어 수리 {game.RepairPrice}\nT / {game.TurretAction}\n왼쪽 Lv.{(game.turrets[0]?game.turrets[0].level:0)} · 오른쪽 Lv.{(game.turrets[1]?game.turrets[1].level:0)}";
            var tank=game.playerUnit.GetComponent<DefenseTank>();
            ammo.text=DefenseKorean.Translate($"{(tank.ammo==0?"1 / ARMOR-PIERCING":"2 / HIGH-EXPLOSIVE")}\nELEVATION {tank.pitch:F0}°  |  {(Time.time>=tank.NextShot?"READY":"RELOADING")}");
            effects.text=DefenseKorean.Translate($"RUN UPGRADES / AREA {tank.areaLevel}/3 / RATE {tank.hasteLevel}/5 / DMG {tank.damageLevel}/5\nRADIUS {tank.SplashRadius:F0}m / FIRE RATE x{tank.FireRateMultiplier:F1} / DAMAGE x{tank.DamageMultiplier:F2}\n마우스 시점·조준 / 휠 줌 / V {(DefenseCamera.Active&&DefenseCamera.Active.firstPerson?"3인칭":"1인칭")} / ESC 메뉴");
            if(game.playerUnit.Dead)ammo.text="전차 파괴 / 재배치 대기 중";
            if(Time.unscaledTime>messageUntil) message.text="";
        }
    }
}
