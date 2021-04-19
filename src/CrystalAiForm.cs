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

        //Game constants
        const uint BattleMode = 0xD22D;
        UInt16 CurMonWriteAddress = 0x56CE;
        UInt16 CurPartyMon = 0xD109;
        List<byte> ExpectedData = new List<byte> { 0x21, 0xA7, 0xD2 };

        const UInt16 InitBattleTrainer = 0x7594;

        private bool battleModeChanged = false;

        public CrystalAiForm()
        {
            Text = "Hello, world!";
            SuspendLayout();
            Controls.Add(new LabelEx { Text = "loaded" });
            InitializeComponent();
            MemoryCallbackFlags flags;

            //How to get core / rom loaded callback

            ResumeLayout();
        }

        /// <summary>
        /// Restart gets called after the apis are loaded - I think wasn't working before because of emulation not being started
        /// </summary>
        public void Restart() {

            Console.WriteLine("Restart called");

            //In case of battlemode - good enough to read the value at the end of the frame
            //_maybeMemoryEventsAPI.AddWriteCallback((_, written_val, flags) => {
            //    battleModeChanged = true;
            //    Console.WriteLine("BattleMode written");
            //}, BattleMode, "System Bus");
            //Switched to exec InitEnemyTrainer, because wBattleMode addr is re-used in Crystal
            _maybeMemoryEventsAPI.AddExecCallback((_, written_val, flags) => {
                battleModeChanged = true;
                Console.WriteLine("Init enemy trainer called");
            }, InitBattleTrainer, "System Bus");


            //This is to rewrite wCurPartyMon in LoadEnemyMonToSwitchTo before it is used
            //TODO: Should be a breakpoint?

            _maybeMemoryEventsAPI.AddExecCallback((_, cbAddr, _) =>
            {
                //Reading bytes at the program counter location and comparing with data I know should be there
                //This is a hacky way to make sure we're in the correct ROM bank
                var bytes = _maybeMemAPI.ReadByteRange(CurMonWriteAddress, 3, "System Bus");
                if (bytes.SequenceEqual(ExpectedData))
                {
                    //TODO: pause emulation - Set up UI for move selection
                    Console.WriteLine("enemy selecting poke");
                    _maybeClientAPI.Pause();
                }

            }, CurMonWriteAddress, "System Bus");

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
            this.btnMove3.Location = new System.Drawing.Point(87, 48);
            this.btnMove3.Name = "btnMove3";
            this.btnMove3.Size = new System.Drawing.Size(75, 23);
            this.btnMove3.TabIndex = 0;
            this.btnMove3.Text = "Move 3";
            this.btnMove3.UseVisualStyleBackColor = true;
            // 
            // btnMove2
            // 
            this.btnMove2.Location = new System.Drawing.Point(6, 48);
            this.btnMove2.Name = "btnMove2";
            this.btnMove2.Size = new System.Drawing.Size(75, 23);
            this.btnMove2.TabIndex = 0;
            this.btnMove2.Text = "Move 2";
            this.btnMove2.UseVisualStyleBackColor = true;
            // 
            // btnMove1
            // 
            this.btnMove1.Location = new System.Drawing.Point(87, 19);
            this.btnMove1.Name = "btnMove1";
            this.btnMove1.Size = new System.Drawing.Size(75, 23);
            this.btnMove1.TabIndex = 0;
            this.btnMove1.Text = "Move 1";
            this.btnMove1.UseVisualStyleBackColor = true;
            // 
            // btnMove0
            // 
            this.btnMove0.Location = new System.Drawing.Point(6, 19);
            this.btnMove0.Name = "btnMove0";
            this.btnMove0.Size = new System.Drawing.Size(75, 23);
            this.btnMove0.TabIndex = 0;
            this.btnMove0.Text = "Move 0";
            this.btnMove0.UseVisualStyleBackColor = true;
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
            // CrystalAiForm
            // 
            this.ClientSize = new System.Drawing.Size(241, 349);
            this.Controls.Add(this.lblCurrentState);
            this.Controls.Add(this.grpMons);
            this.Controls.Add(this.grpMoves);
            this.Name = "CrystalAiForm";
            this.grpMoves.ResumeLayout(false);
            this.grpMons.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private void btnMon0_Click(object sender, EventArgs e)
        {
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
    }
}