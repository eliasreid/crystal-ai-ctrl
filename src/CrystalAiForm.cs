using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Newtonsoft.Json;

using BizHawk.Client.Common;
using BizHawk.WinForms.Controls;
using CrystalAiCtrl;
using System.Text;
using System.IO;

namespace BizHawk.Tool.CrystalCtrl

{
    
    [ExternalTool("CrystalAiCtrl")]
    [ExternalToolApplicability.SingleSystem(CoreSystem.GameBoy)]
    public sealed class CrystalAiForm : Form, IExternalToolForm
    {

        [RequiredApi]
        public ICommApi? _maybeCommAPI { get; set; }
        [RequiredApi]
        public IMemoryEventsApi? _maybeMemoryEventsAPI { get; set; }
        [RequiredApi]
        public IEmuClientApi? _maybeClientAPI { get; set; }
        [RequiredApi]
        public IEmulationApi? _maybeEmuAPI { get; set; }
        [RequiredApi]
        public IGameInfoApi? _maybeGameInfoAPI { get; set; }
        [RequiredApi]
        public IGuiApi? _maybeGuiAPI { get; set; }
        [RequiredApi]
        public IMemoryApi? _maybeMemAPI { get; set; }

        private ApiContainer? _apis;
        private GroupBox grpMoves;
        private Button btnMove1;
        private Button btnMove0;
        private Button btnMove2;
        private Button btnMove3;
        private GroupBox grpMons;
        private Button btnMon5;
        private Button btnMon4;
        private Button btnMon3;
        private Button btnMon2;
        private Button btnMon1;
        private Button btnMon0;
        private Label lblCurrentState;

        //the null-coalescing assignment operator ??= assigns the value of its right-hand operand to its left-hand operand
        //only if the left-hand operand evaluates to null. The ??= operator doesn't evaluate its
        //right-hand operand if the left-hand operand evaluates to non-null.

        //The null-coalescing operator ?? returns the value of its left-hand operand if it isn't null; otherwise, it evaluates the right-hand operand and returns its result
        private ApiContainer APIs => _apis ??= new ApiContainer(new Dictionary<Type, IExternalApi>
        {
            [typeof(ICommApi)] = _maybeCommAPI ?? throw new NullReferenceException(),
            [typeof(IEmuClientApi)] = _maybeClientAPI ?? throw new NullReferenceException(),
            [typeof(IEmulationApi)] = _maybeEmuAPI ?? throw     new NullReferenceException(),
            [typeof(IGameInfoApi)] = _maybeGameInfoAPI ?? throw new NullReferenceException(),
            [typeof(IGuiApi)] = _maybeGuiAPI ?? throw new NullReferenceException(),
            [typeof(IMemoryApi)] = _maybeMemAPI ?? throw new NullReferenceException(),
            [typeof(IMemoryEventsApi)] = _maybeMemoryEventsAPI ?? throw new NullReferenceException()
        });

        //Mem domain WRAM
        //Mem domain ROM
        //Mem domain VRAM
        //Mem domain OAM
        //Mem domain HRAM
        //Mem domain System Bus
        //Mem domain CartRAM

//register PCl : 25
//register PCh : 2
//register SPl : 248
//register SPh : 255
//register A : 62
//register F : 160
//register B : 78
//register C : 62
//register D : 221
//register E : 0
//register H : 255
//register L : 15
//register W : 252
//register Z : 0
//register PC : 537
//register Flag I : 0
//register Flag C : 0
//register Flag H : 1
//register Flag N : 0
//register Flag Z : 1

        //Game constants
        const uint BattleMode = 0xD22D;
        UInt16 CurPartyMon = 0xD109;
        UInt16 EnemyMonMoves = 0xd208;
        List<byte> ExpectedData = new List<byte> { 0x21, 0xA7, 0xD2 };

        UInt16 EnemyCurrentMoveNum = 0xC6E9;
        UInt16 EnemyCurrentMove = 0xC6E4;

        UInt16 LoadEnemyMonToSwitchTo = 0x56CA;

        const UInt16 OTPartyMon1Species = 0xd288;
        const UInt16 BattleStructSize = 48;
        const UInt16 OTPartyCount = 0xd280;

        const UInt16 InitBattleTrainer = 0x7594;
        //end of function (0f:68eb LoadEnemyMon)
        const UInt16 LoadEnemyMonRet = 0x6B37;
        const UInt16 ExitBattle = 0x769e;
        const UInt16 ParseEnemyAction = 0x67C1;
        const UInt16 BattleMenu = 0x6139;
        const UInt16 SwitchOrTryItemOk = 0x4032;
        private CheckBox chkJoypadDisable;
        //private bool battleModeChanged = false;

        const UInt16 RomBank = 0xff9d;

        //TOOD: inBattle should be "controllingBattle" - thta way we can start with false, even if start in middle of battle, 
        //Will just start working on next battle
        private bool enemyCtrlActive = false;

        private int? chosenMove = null;
        private int? chosenMon = null;
        private List<byte> enemyMoves;

        //Data for switching logic
        //Trainer class has to be modifiy temporarily to simplify forcing enemy to switch
        private uint savedTrainerClass = 0;
        const UInt16 TrainerClass = 0xD233;
        const UInt16 EnemySwitchMonIndex = 0xc718;
        const UInt16 AiTrySwitch = 0x444B;
        const UInt16 DontSwitchRet = 0x4044;

        const UInt16 ReadTrainerPartyDone = 0x57d0;

        bool inputDisabled = false;
        private Button btnConnect;
        private Button btnTestSend;

        //ClientWebSocket ws = new ClientWebSocket();

        WsClient wsClient = new WsClient();

        public CrystalAiForm()
        {
            Text = "Hello, world!";
            SuspendLayout();
            Controls.Add(new LabelEx { Text = "loaded" });
            InitializeComponent();

            ResumeLayout();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            wsClient.Disconnect();
        }

        /// <summary>
        /// Restart gets called after the apis are loaded - I think wasn't working before because of emulation not being started
        /// </summary>
        public void Restart() {

            wsClient.MessageReceiveCallback((data) =>
            {
                Console.WriteLine($"message received in callback, size {data.Count}");
            });

            Console.WriteLine("Restart called, available registers");
            foreach(KeyValuePair<string, ulong> entry in _maybeEmuAPI.GetRegisters())
            {
                Console.WriteLine($"{entry.Key}");
            }

            //set inBattle flag when enemy trainer is inited
            //TODO: eventually meaning will change - only set inBattle if enemy is being controlled by net / ui, etc
            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) => {
                enemyCtrlActive = true;
                Console.WriteLine("Init enemy trainer called");
            }, InitBattleTrainer, "System Bus");

            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) => {
                enemyCtrlActive = false;
                Console.WriteLine("Exit battle called");
            }, ExitBattle, "System Bus");            

            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) => {

                if (enemyCtrlActive && _maybeMemAPI.ReadByte(RomBank, "System Bus") == 0x0E)
                {
                    if (chosenMon.HasValue)
                    {
                        //Try jumping to AI_TrySwitch with wEnemySwitchMonIndexSet
                        _maybeEmuAPI.SetRegister("PC", AiTrySwitch);

                        //TOOD: make sure works, I think index is actually 1-6
                        Console.WriteLine($"Switching mon to {chosenMon.Value}");
                        _maybeMemAPI.WriteByte(EnemySwitchMonIndex, (uint)chosenMon.Value + 1, "System Bus");
                        chosenMon = null;
                    }else if (chosenMove.HasValue)
                    {
                        //Jump to return statement in DontSwitch - this is to ensure selected move doesn't get overwritten
                        // AI choice to use item or switch
                        Console.WriteLine($"Jumping to DontSwitchRet to ensure item / switch not used");
                        _maybeEmuAPI.SetRegister("PC", DontSwitchRet);
                        chosenMove = null;
                    }
                }
            }, SwitchOrTryItemOk, "System Bus");

            //don't remeber why I added this
            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) => {
                if (enemyCtrlActive && _maybeMemAPI.ReadByte(RomBank, "System Bus") == 0x0E)
                {
                    //TODO: look into this
                    Console.WriteLine("Executing .ok + 1, SHOULD NOT HAPPEN IF PC JUMP IS WORKING");
                }
            }, SwitchOrTryItemOk + 1, "System Bus");

            //Executed when new enemy pokmeon is switched in
            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) =>
            {
                if (enemyCtrlActive)
                {
                    Console.WriteLine("enemy poke loaded");
                    //Read in move IDs from list
                    enemyMoves = _maybeMemAPI.ReadByteRange(EnemyMonMoves, 4, "System Bus");
                    setupMoveButtons(enemyMoves);

                    //TODO: Figure out available switches
                    
                    //flag which pokemon are available for switching

                    //setupSwitchButtons();
                }
                
            }, LoadEnemyMonRet, "System Bus");

            //This is where enemy attack is re-written, if enemy selects an attack
            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) =>
            {
                //TOOD: check if flag is needed
                if (enemyCtrlActive)
                {
                    Console.WriteLine("Parsing enemy action");
                    if (chosenMove.HasValue)
                    {
                        //TODO: So I also have to force enemy AI NOT to use item or swi
                        Console.WriteLine("Overwriting enemy move with chosen move");
                        _maybeMemAPI.WriteByte(EnemyCurrentMove, enemyMoves[chosenMove.Value], "System Bus");
                        _maybeMemAPI.WriteByte(EnemyCurrentMoveNum, (uint)chosenMove.Value, "System Bus");
                        chosenMove = null;
                    }
                    else
                    {
                        Console.WriteLine("No move override selected, going with AI generated move");
                    }
                }

            }, ParseEnemyAction, "System Bus");

            //This is where the player enters their battle menu.
            //Pause user input until enemy selects their action
            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) =>
            {
                //If Enemy has not yet chosen a move
                //TODO: OR SWITCH / ITEM. ETC - ACTION
                if (enemyCtrlActive && !chosenMove.HasValue && !chosenMon.HasValue)
                {
                    Console.WriteLine("Disabling input, waiting for opponent to select action");
                    InputDisable(true);
                }
                //TODO: ideally we only disable input AFTER user has selected an action
                //That is, intercept their action, cancel it, they run it after enemy has selected a move
            }, BattleMenu, "System Bus");

            //Executed when enemey pokemon is loading from party
            //can rewrite register b to change party index 
            //Only need to rewrite register here when battle starts (first mon), or when enemy mon faints, has to choose next pokemon
            //Probably also when forced to switch for other reasons (like??)
            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) =>
            {
                if(enemyCtrlActive && _maybeMemAPI.ReadByte(RomBank, "System Bus") == 0x0F)
                {
                    if (chosenMon.HasValue)
                    {
                        Console.WriteLine($"in LoadEnemyMonToSwitchTo callback, setting mon index to {chosenMon}");
                        _maybeEmuAPI.SetRegister("B", chosenMon.Value);
                        chosenMon = null;
                    }
                    else
                    {
                        //TODO: only an "error" if switch action on turn
                        Console.WriteLine($"ERROR: in LoadEnemyMonToSwitchTo callback, chosenMon null (going with AI decision)");
                    }
                }
            }, LoadEnemyMonToSwitchTo, "System Bus");

            //when trainer party is finished being read (wOTParty___ stuff addresses should be loaded)
            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) =>
            {
                //write register b to 0x01
                //TODO: check for variable "enemyNextMon" (different from switchMon)
                if (enemyCtrlActive && _maybeMemAPI.ReadByte(RomBank, "System Bus") == 0x0E)
                {
                    var partyCount = _maybeMemAPI.ReadByte(OTPartyCount, "System Bus");

                    //TODO: maybe better to single byte range, rather than multiple API calls?
                    List<byte> partyMonIDs = new List<byte>();
                    for (uint i = 0; i < partyCount; i++)
                    {
                        var monID = (byte)_maybeMemAPI.ReadByte(OTPartyMon1Species + BattleStructSize * i, "System Bus");
                        partyMonIDs.Add(monID);
                    }
                    Console.WriteLine($"in ReadTrainerPartyDone callback, enemy party (decimal IDs): {String.Join(",", partyMonIDs)}");
                    setupMonButtons(partyMonIDs);

                }
            }, ReadTrainerPartyDone, "System Bus");

        }

        private void setupMoveButtons(List<byte> moveIds)
        {
            //TODO: translate move hex codes into strings
            //TODO: Check assumption that there's always one move
            //TODO: put buttons in a list to simply function
            Console.WriteLine("Setting up move buttons");

            //Check if connected to websocket, send JSON to server.

            chosenMove = null;
            foreach (Control ctrl in grpMoves.Controls)
            {
                ctrl.Enabled = false;
            }
            btnMove0.Text = $"{moveIds[0]:X}";
            btnMove0.Enabled = true;
            if(moveIds[1] == 0)
            {
                return;
            }
            btnMove1.Text = $"{moveIds[1]:X}";
            btnMove1.Enabled = true;
            if (moveIds[2] == 0)
            {
                return;
            }
            btnMove2.Text = $"{moveIds[2]:X}";
            btnMove2.Enabled = true;
            if (moveIds[3] == 0)
            {
                return;
            }
            btnMove3.Text = $"{moveIds[3]:X}";
            btnMove3.Enabled = true;
        }

        //takes in list of pokemon IDs for enemy party.
        private void setupMonButtons(List<byte> monIDs)
        {
            Console.WriteLine("setting up enemy buttons");

            foreach (Control ctrl in grpMons.Controls)
            {
                ctrl.Enabled = false;
            }
            btnMon0.Text = $"{monIDs[0]:X}";
            btnMon0.Enabled = true;

            if(monIDs.Count < 2)
            {
                return;
            }

            btnMon1.Text = $"{monIDs[1]:X}";
            btnMon1.Enabled = true;
            if (monIDs.Count < 3)
            {
                return;
            }
            btnMon2.Text = $"{monIDs[2]:X}";
            btnMon2.Enabled = true;
            if (monIDs.Count < 4)
            {
                return;
            }
            btnMon3.Text = $"{monIDs[3]:X}";
            btnMon3.Enabled = true;
            if (monIDs.Count < 5)
            {
                return;
            }
            btnMon4.Text = $"{monIDs[4]:X}";
            btnMon4.Enabled = true;
            if (monIDs.Count < 6)
            {
                return;
            }
            btnMon5.Text = $"{monIDs[5]:X}";
            btnMon5.Enabled = true;
            Console.WriteLine("setting up enemy buttons - end");
        }

		public bool AskSaveChanges() => true;

		public void UpdateValues(ToolFormUpdateType type)
		{
            switch (type)
            {
                case ToolFormUpdateType.PreFrame:
                    if (inputDisabled)
                    {
                        _maybeGuiAPI.Text(50, 50, "Waiting for enemy to select action");
                    }
                    
                    break;
                case ToolFormUpdateType.PostFrame:
                    //if (battleModeChanged)
                    //{
                    //    //check mem address
                    //    var currMode = _maybeMemAPI.ReadByte(BattleMode);
                    //    switch (currMode)
                    //    {
                    //        case 0x00:
                    //            Console.WriteLine("BattleMode: Overworld");
                    //            break;
                    //        case 0x01:
                    //            Console.WriteLine("BattleMode: Wild Mon");
                    //            break;
                    //        case 0x02:
                    //            Console.WriteLine("Battlemode: Trainer ----");
                    //            break;
                    //        default:
                    //            Console.WriteLine($"Battlemode: unknown ({currMode})");
                    //            break;
                    //    }
                    //    battleModeChanged = false;
                    //}
                    break;
                default:
                    break;
            }
		}

        private void InputDisable(bool disable)
        {
            inputDisabled = disable;
            _maybeMemAPI.WriteByte(0xCFBE, disable ? (uint)0b00010000 : 0);
        }

        private void InitializeComponent()
        {
            this.grpMoves = new System.Windows.Forms.GroupBox();
            this.btnMove3 = new System.Windows.Forms.Button();
            this.btnMove2 = new System.Windows.Forms.Button();
            this.btnMove1 = new System.Windows.Forms.Button();
            this.btnMove0 = new System.Windows.Forms.Button();
            this.grpMons = new System.Windows.Forms.GroupBox();
            this.btnMon5 = new System.Windows.Forms.Button();
            this.btnMon4 = new System.Windows.Forms.Button();
            this.btnMon3 = new System.Windows.Forms.Button();
            this.btnMon2 = new System.Windows.Forms.Button();
            this.btnMon1 = new System.Windows.Forms.Button();
            this.btnMon0 = new System.Windows.Forms.Button();
            this.lblCurrentState = new System.Windows.Forms.Label();
            this.chkJoypadDisable = new System.Windows.Forms.CheckBox();
            this.btnConnect = new System.Windows.Forms.Button();
            this.btnTestSend = new System.Windows.Forms.Button();
            this.grpMoves.SuspendLayout();
            this.grpMons.SuspendLayout();
            this.SuspendLayout();
            // 
            // grpMoves
            // 
            this.grpMoves.Controls.Add(this.btnMove3);
            this.grpMoves.Controls.Add(this.btnMove2);
            this.grpMoves.Controls.Add(this.btnMove1);
            this.grpMoves.Controls.Add(this.btnMove0);
            this.grpMoves.Location = new System.Drawing.Point(12, 12);
            this.grpMoves.Name = "grpMoves";
            this.grpMoves.Size = new System.Drawing.Size(169, 81);
            this.grpMoves.TabIndex = 0;
            this.grpMoves.TabStop = false;
            this.grpMoves.Text = "Moves";
            // 
            // btnMove3
            // 
            this.btnMove3.Enabled = false;
            this.btnMove3.Location = new System.Drawing.Point(87, 48);
            this.btnMove3.Name = "btnMove3";
            this.btnMove3.Size = new System.Drawing.Size(75, 23);
            this.btnMove3.TabIndex = 0;
            this.btnMove3.Text = "Move 3";
            this.btnMove3.UseVisualStyleBackColor = true;
            this.btnMove3.Click += new System.EventHandler(this.btnMove3_Click);
            // 
            // btnMove2
            // 
            this.btnMove2.Enabled = false;
            this.btnMove2.Location = new System.Drawing.Point(6, 48);
            this.btnMove2.Name = "btnMove2";
            this.btnMove2.Size = new System.Drawing.Size(75, 23);
            this.btnMove2.TabIndex = 0;
            this.btnMove2.Text = "Move 2";
            this.btnMove2.UseVisualStyleBackColor = true;
            this.btnMove2.Click += new System.EventHandler(this.btnMove2_Click);
            // 
            // btnMove1
            // 
            this.btnMove1.Enabled = false;
            this.btnMove1.Location = new System.Drawing.Point(87, 19);
            this.btnMove1.Name = "btnMove1";
            this.btnMove1.Size = new System.Drawing.Size(75, 23);
            this.btnMove1.TabIndex = 0;
            this.btnMove1.Text = "Move 1";
            this.btnMove1.UseVisualStyleBackColor = true;
            this.btnMove1.Click += new System.EventHandler(this.btnMove1_Click);
            // 
            // btnMove0
            // 
            this.btnMove0.Enabled = false;
            this.btnMove0.Location = new System.Drawing.Point(6, 19);
            this.btnMove0.Name = "btnMove0";
            this.btnMove0.Size = new System.Drawing.Size(75, 23);
            this.btnMove0.TabIndex = 0;
            this.btnMove0.Text = "Move 0";
            this.btnMove0.UseVisualStyleBackColor = true;
            this.btnMove0.Click += new System.EventHandler(this.btnMove0_Click);
            // 
            // grpMons
            // 
            this.grpMons.Controls.Add(this.btnMon5);
            this.grpMons.Controls.Add(this.btnMon4);
            this.grpMons.Controls.Add(this.btnMon3);
            this.grpMons.Controls.Add(this.btnMon2);
            this.grpMons.Controls.Add(this.btnMon1);
            this.grpMons.Controls.Add(this.btnMon0);
            this.grpMons.Location = new System.Drawing.Point(12, 99);
            this.grpMons.Name = "grpMons";
            this.grpMons.Size = new System.Drawing.Size(169, 115);
            this.grpMons.TabIndex = 1;
            this.grpMons.TabStop = false;
            this.grpMons.Text = "Pokemon";
            // 
            // btnMon5
            // 
            this.btnMon5.Location = new System.Drawing.Point(87, 77);
            this.btnMon5.Name = "btnMon5";
            this.btnMon5.Size = new System.Drawing.Size(75, 23);
            this.btnMon5.TabIndex = 0;
            this.btnMon5.Text = "Mon 5";
            this.btnMon5.UseVisualStyleBackColor = true;
            this.btnMon5.Click += new System.EventHandler(this.btnMon5_Click);
            // 
            // btnMon4
            // 
            this.btnMon4.Location = new System.Drawing.Point(6, 77);
            this.btnMon4.Name = "btnMon4";
            this.btnMon4.Size = new System.Drawing.Size(75, 23);
            this.btnMon4.TabIndex = 0;
            this.btnMon4.Text = "Mon 4";
            this.btnMon4.UseVisualStyleBackColor = true;
            this.btnMon4.Click += new System.EventHandler(this.btnMon4_Click);
            // 
            // btnMon3
            // 
            this.btnMon3.Location = new System.Drawing.Point(87, 48);
            this.btnMon3.Name = "btnMon3";
            this.btnMon3.Size = new System.Drawing.Size(75, 23);
            this.btnMon3.TabIndex = 0;
            this.btnMon3.Text = "Mon 3";
            this.btnMon3.UseVisualStyleBackColor = true;
            this.btnMon3.Click += new System.EventHandler(this.btnMon3_Click);
            // 
            // btnMon2
            // 
            this.btnMon2.Location = new System.Drawing.Point(6, 48);
            this.btnMon2.Name = "btnMon2";
            this.btnMon2.Size = new System.Drawing.Size(75, 23);
            this.btnMon2.TabIndex = 0;
            this.btnMon2.Text = "Mon 2";
            this.btnMon2.UseVisualStyleBackColor = true;
            this.btnMon2.Click += new System.EventHandler(this.btnMon2_Click);
            // 
            // btnMon1
            // 
            this.btnMon1.Location = new System.Drawing.Point(87, 19);
            this.btnMon1.Name = "btnMon1";
            this.btnMon1.Size = new System.Drawing.Size(75, 23);
            this.btnMon1.TabIndex = 0;
            this.btnMon1.Text = "Mon 1";
            this.btnMon1.UseVisualStyleBackColor = true;
            this.btnMon1.Click += new System.EventHandler(this.btnMon1_Click);
            // 
            // btnMon0
            // 
            this.btnMon0.Location = new System.Drawing.Point(6, 19);
            this.btnMon0.Name = "btnMon0";
            this.btnMon0.Size = new System.Drawing.Size(75, 23);
            this.btnMon0.TabIndex = 0;
            this.btnMon0.Text = "Mon 0";
            this.btnMon0.UseVisualStyleBackColor = true;
            this.btnMon0.Click += new System.EventHandler(this.btnMon0_Click);
            // 
            // lblCurrentState
            // 
            this.lblCurrentState.AutoSize = true;
            this.lblCurrentState.Location = new System.Drawing.Point(18, 221);
            this.lblCurrentState.Name = "lblCurrentState";
            this.lblCurrentState.Size = new System.Drawing.Size(85, 13);
            this.lblCurrentState.TabIndex = 2;
            this.lblCurrentState.Text = "Game state: ???";
            // 
            // chkJoypadDisable
            // 
            this.chkJoypadDisable.AutoSize = true;
            this.chkJoypadDisable.Location = new System.Drawing.Point(30, 256);
            this.chkJoypadDisable.Name = "chkJoypadDisable";
            this.chkJoypadDisable.Size = new System.Drawing.Size(80, 17);
            this.chkJoypadDisable.TabIndex = 3;
            this.chkJoypadDisable.Text = "checkBox1";
            this.chkJoypadDisable.UseVisualStyleBackColor = true;
            this.chkJoypadDisable.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            // 
            // btnConnect
            // 
            this.btnConnect.Location = new System.Drawing.Point(18, 296);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(75, 23);
            this.btnConnect.TabIndex = 4;
            this.btnConnect.Text = "Connect";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            // 
            // btnTestSend
            // 
            this.btnTestSend.Location = new System.Drawing.Point(106, 296);
            this.btnTestSend.Name = "btnTestSend";
            this.btnTestSend.Size = new System.Drawing.Size(75, 23);
            this.btnTestSend.TabIndex = 4;
            this.btnTestSend.Text = "Test Send";
            this.btnTestSend.UseVisualStyleBackColor = true;
            this.btnTestSend.Click += new System.EventHandler(this.btnTestSend_Click);
            // 
            // CrystalAiForm
            // 
            this.ClientSize = new System.Drawing.Size(241, 349);
            this.Controls.Add(this.btnTestSend);
            this.Controls.Add(this.btnConnect);
            this.Controls.Add(this.chkJoypadDisable);
            this.Controls.Add(this.lblCurrentState);
            this.Controls.Add(this.grpMons);
            this.Controls.Add(this.grpMoves);
            this.Name = "CrystalAiForm";
            this.grpMoves.ResumeLayout(false);
            this.grpMons.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        //TODO: btnMonN_Clicks should be used for two things:
        // - choosing next pokemon, after a faint or start of match
        // - chooisng next action (e.g. choosing to switch instead of using a move.

        //I think can handle both with a chosenMon variable
        private void btnMon0_Click(object sender, EventArgs e)
        {
            ChooseMon(0);
        }

        private void btnMon1_Click(object sender, EventArgs e)
        {
            ChooseMon(1);
        }

        private void btnMon2_Click(object sender, EventArgs e)
        {
            ChooseMon(2);
        }

        private void btnMon3_Click(object sender, EventArgs e)
        {
            ChooseMon(3);
        }

        private void btnMon4_Click(object sender, EventArgs e)  
        {
            ChooseMon(4);
        }

        private void btnMon5_Click(object sender, EventArgs e)
        {
            ChooseMon(5);
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            InputDisable(chkJoypadDisable.Checked);
        }

        private void ChooseMon(int index)
        {
            chosenMon = index;
            InputDisable(false);
        }

        private void ChooseMove(int index)
        {
            chosenMove = index;
            InputDisable(false);
        }

        private void btnMove0_Click(object sender, EventArgs e)
        {
            ChooseMove(0);
        }

        private void btnMove1_Click(object sender, EventArgs e)
        {
            ChooseMove(1);
        }

        private void btnMove2_Click(object sender, EventArgs e)
        {
            ChooseMove(2);
        }

        private void btnMove3_Click(object sender, EventArgs e)
        {
            ChooseMove(3);
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            Console.WriteLine("connecting to ws");

            var connectTask = wsClient.Connect(new Uri("ws://localhost:8999?type=emulator"));
            connectTask.ContinueWith((task) =>
            {
                if (task.Result == WsClient.ConnectResult.Success)
                {
                    //Update GUI with link for client to connect
                    Console.WriteLine($"connection to server success!");
                }
                else
                {
                    Console.WriteLine($"connection to server fail!");
                }
            });
        }

        private void btnTestSend_Click(object sender, EventArgs e)
        {
            //JSON 
            //StringBuilder sb = new StringBuilder();
            //StringWriter sw = new StringWriter(sb);

            //using (JsonWriter writer = new JsonTextWriter(sw))
            //{
            //    writer.Formatting = Formatting.Indented;

            //    writer.WriteStartObject();
            //    writer.WritePropertyName("value");
            //    writer.WriteValue(42);
            //    writer.WriteEndObject();
            //}

            //wsClient.SendMessage(sb.ToString());

            BattleStartMsg startMsg = new BattleStartMsg();
            startMsg.trainerInfo.trainerName = "name";
            startMsg.trainerInfo.pokemonNames = new List<string> { "Pidgey", "Rattata" };

            string json = JsonConvert.SerializeObject(startMsg, Formatting.None);
            wsClient.SendMessage(json);
            Console.WriteLine("sending: " + json);
            
        }

    }
}