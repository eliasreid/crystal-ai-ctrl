using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;

using BizHawk.Client.Common;
using BizHawk.WinForms.Controls;
using BizHawk.Emulation.Common;

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
        UInt16 CurMonWriteAddress = 0x56CE;
        UInt16 CurPartyMon = 0xD109;
        UInt16 EnemyMonMoves = 0xd208;
        List<byte> ExpectedData = new List<byte> { 0x21, 0xA7, 0xD2 };

        UInt16 EnemyCurrentMoveNum = 0xC6E9;
        UInt16 EnemyCurrentMove = 0xC6E4;

        const UInt16 InitBattleTrainer = 0x7594;
        const UInt16 LoadEnemyMonRet = 0x6B37;
        const UInt16 ExitBattle = 0x769e;
        const UInt16 ParseEnemyAction = 0x67C1;
        const UInt16 BattleMenu = 0x6139;
        const UInt16 SwitchOrTryItemOk = 0x4032;
        private CheckBox chkJoypadDisable;
        //private bool battleModeChanged = false;

        //TODO: can't assume we start out NOT in battle

        //TOOD: inBattle should be "controllingBattle" - thta way we can start with false, even if start in middle of battle, 
        //Will just start working on next battle
        private bool inBattle = false;

        private int? chosenMove = null;
        private List<byte> enemyMoves;

        //Data for switching logic
        //Trainer class has to be modifiy temporarily to simplify forcing enemy to switch
        private uint savedTrainerClass = 0;
        const UInt16 TrainerClass = 0xD233;

        public CrystalAiForm()
        {
            Text = "Hello, world!";
            SuspendLayout();
            Controls.Add(new LabelEx { Text = "loaded" });
            InitializeComponent();

            ResumeLayout();
        }

        /// <summary>
        /// Restart gets called after the apis are loaded - I think wasn't working before because of emulation not being started
        /// </summary>
        public void Restart() {

            Console.WriteLine("Restart called, available registers");
            foreach(KeyValuePair<string, ulong> entry in _maybeEmuAPI.GetRegisters())
            {
                Console.WriteLine($"{entry.Key}");
            }


            //In case of battlemode - good enough to read the value at the end of the frame
            //_maybeMemoryEventsAPI.AddWriteCallback((_, written_val, flags) => {
            //    //battleModeChanged = true;
            //    Console.WriteLine("BattleMode written");
            //}, BattleMode, "System Bus");
            //Switched to exec InitEnemyTrainer, because wBattleMode addr is re-used in Crystal
            _maybeMemoryEventsAPI.AddExecCallback((_, written_val, flags) => {
                //battleModeChanged = true;
                inBattle = true;
                Console.WriteLine("Init enemy trainer called");
            }, InitBattleTrainer, "System Bus");


            _maybeMemoryEventsAPI.AddExecCallback((_, written_val, flags) => {
                //battleModeChanged = true;
                inBattle = false;
                Console.WriteLine("Exit battle called");
            }, ExitBattle, "System Bus");            

            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) => {
            if (inBattle && _maybeMemAPI.ReadByte(0xff9d, "System Bus") == 14)
            {
                Console.WriteLine($"Try switch / item ok label - try to write next opcode to garbage.");
                //Write swtich mon to 1
                _maybeMemAPI.WriteByte(0xc718, 1);

                //Trying manual Jump - not sure if this will actually work
                //DOES NOT WORK IN THIS EMU
                _maybeMemAPI.WriteByteRange(SwitchOrTryItemOk + 1, new List<byte>{0xc3, 0x11, 0x11}, "System Bus");
                    //_maybeEmuAPI.SetRegister("PC", 0x444b);

                }
            }, SwitchOrTryItemOk, "System Bus");


            //This is to rewrite wCurPartyMon in LoadEnemyMonToSwitchTo before it is used
            //TODO: delete this?
            _maybeMemoryEventsAPI.AddExecCallback((_, cbAddr, _) =>
            {

                //Reading bytes at the program counter location and comparing with data I know should be there
                //This is a hacky way to make sure we're in the correct ROM bank
                var bytes = _maybeMemAPI.ReadByteRange(CurMonWriteAddress, 3, "System Bus");
                if (bytes.SequenceEqual(ExpectedData))
                {
                    //TODO: Pause doesn't work as expected - so have to have pre-written value for which pokemon to select
                    // at this point.
                    //Need to read in party in previous callback.
                    Console.WriteLine("enemy selecting poke");
                    //ChooseNextPokemon(0);
                    //_maybeClientAPI.Pause();
                    //_maybeMemAPI.WriteByte(CurPartyMon, 0);
                }
                else
                {
                    //Console.WriteLine("not a match");
                }

            }, CurMonWriteAddress, "System Bus");

            //Executed when new enemy pokmeon is switched in
            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) =>
            {
                if (inBattle)
                {
                    Console.WriteLine("enemy poke loaded");
                    //Read in move IDs from list
                    enemyMoves = _maybeMemAPI.ReadByteRange(EnemyMonMoves, 4, "System Bus");
                    setupMoveButtons(enemyMoves);

                    //TODO: Figure out available switches (not yet figured out)

                    //setupSwitchButtons();
                }
                
            }, LoadEnemyMonRet, "System Bus");

            //This is where enemy attack is re-written, if enemy selects an attack
            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) =>
            {
                //TOOD: check if flag is needed
                if (inBattle)
                {
                    Console.WriteLine("Parsing enemy action");
                    if (chosenMove.HasValue)
                    {
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
                if (!chosenMove.HasValue)
                {
                    Console.WriteLine("Disabling input, waiting for opponent to select move");
                    InputDisable(true);
                }
                //TODO: ideally we only disable input AFTER user has selected an action
                //That is, intercept their action, cancel it, they run it after enemy has selected a move
            }, BattleMenu, "System Bus");

        }

        private void setupMoveButtons(List<byte> moveIds)
        {
            //TODO: maybe move ids to strings
            //TODO: Check assumption that there's always one move
            //TODO: put buttons in a list to simply function
            //Console.WriteLine($"Hexadecimal value of {letter} is {value:X}")
            Console.WriteLine("Setting up move buttons");
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

        /// <summary>
        /// Must be valid index based on available mons.
        /// Emulation should be paused on CurMonWriteAddress
        /// </summary>
        /// <param name="partyIndex"></param>
        private void ChooseNextPokemon(byte partyIndex)
        {
            _maybeMemAPI.WriteByte(CurPartyMon, partyIndex);
        }

		public bool AskSaveChanges() => true;

		public void UpdateValues(ToolFormUpdateType type)
		{
            switch (type)
            {
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

        private void InputDisable(bool en)
        {
            //TODO: comment back in
            //_maybeMemAPI.WriteByte(0xCFBE, en ? (uint)0b00010000 : 0);
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
            // CrystalAiForm
            // 
            this.ClientSize = new System.Drawing.Size(241, 349);
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

        //TODO: btnMonN_Clicks should set some variable, that will be written to memory later on when poke is loaded
        private void btnMon0_Click(object sender, EventArgs e)
        {
            var execAddr = _maybeEmuAPI.GetRegister("PC");
            Console.WriteLine($"Current PC {execAddr:X4}");
            ChooseNextPokemon(0);
            _maybeClientAPI.Unpause();
        }

        private void btnMon1_Click(object sender, EventArgs e)
        {
            ChooseNextPokemon(1);
            _maybeClientAPI.Unpause();
        }

        private void btnMon2_Click(object sender, EventArgs e)
        {
            ChooseNextPokemon(2);
            _maybeClientAPI.Unpause();
        }

        private void btnMon3_Click(object sender, EventArgs e)
        {
            ChooseNextPokemon(3);
            _maybeClientAPI.Unpause();
        }

        private void btnMon4_Click(object sender, EventArgs e)  
        {
            ChooseNextPokemon(4);
            _maybeClientAPI.Unpause();
        }

        private void btnMon5_Click(object sender, EventArgs e)
        {
            ChooseNextPokemon(5);
            _maybeClientAPI.Unpause();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            InputDisable(chkJoypadDisable.Checked);
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
    }
}