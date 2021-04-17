using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using BizHawk.Client.Common;
using BizHawk.WinForms.Controls;
using BizHawk.Emulation.Common;

namespace BizHawk.Tool.CrystalCtrl
{
    [ExternalTool("CrystalAiCtrl")]
    [ExternalToolApplicability.SingleSystem(CoreSystem.GameBoy)]
    public sealed class CrystalAiForm : Form, IExternalToolForm
    {
        private Button button1;
        private Label label1;

        const uint BattleMode = 0xD116;

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



        //the null-coalescing assignment operator ??= assigns the value of its right-hand operand to its left-hand operand
        //only if the left-hand operand evaluates to null. The ??= operator doesn't evaluate its
        //right-hand operand if the left-hand operand evaluates to non-null.

        //The null-coalescing operator ?? returns the value of its left-hand operand if it isn't null; otherwise, it evaluates the right-hand operand and returns its result
        private ApiContainer APIs => _apis ??= new ApiContainer(new Dictionary<Type, IExternalApi>
        {
            [typeof(ICommApi)] = _maybeCommAPI ?? throw new NullReferenceException(),
            [typeof(IEmuClientApi)] = _maybeClientAPI ?? throw new NullReferenceException(),
            [typeof(IEmulationApi)] = _maybeEmuAPI ?? throw new NullReferenceException(),
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

        private bool battleModeChanged = false;

        public CrystalAiForm()
        {

            
            Console.WriteLine("Hello World write line");
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

            //In case of battlemode - good enough to read the value at the end of the frame
            _maybeMemoryEventsAPI.AddWriteCallback((_, written_val, flags) => {
                //var read_val = _maybeMemAPI.ReadByte(addr);
                battleModeChanged = true;
                Console.WriteLine("BattleMode written");
                //Console.WriteLine($"battle mode written value: {written_val}");
                //Console.WriteLine($"battle mode read value: {read_val}");

                //Pretty sure flags just gives us the callback type. But already know, because of functino name


                //AccessExecute

                //_maybeClientAPI.Pause();
            }, BattleMode, "System Bus");


            //Rom Already loaded, so don't have to worry about this
            //_apis.EmuClient.RomLoaded += romLoaded;
            //_apis.EmuClient.RomLoaded += (_, _) => { Console.WriteLine(" does lambda callback work? rom loaded"); };
        }

        private void romLoaded(object sender, EventArgs e)
        {
            
        }


		public bool AskSaveChanges() => true;

		

		public void UpdateValues(ToolFormUpdateType type)
		{
            //What does FastPreFrame mean?
			//if (type != ToolFormUpdateType.PreFrame && type != ToolFormUpdateType.FastPreFrame) return;

            switch (type)
            {
                case ToolFormUpdateType.PostFrame:
                    if (battleModeChanged)
                    {
                        //check mem address
                        var currMode = _maybeMemAPI.ReadByte(BattleMode);
                        switch (currMode)
                        {
                            case 0x00:
                                Console.WriteLine("BattleMode: Overworld");
                                break;
                            case 0x01:
                                Console.WriteLine("BattleMode: Wild Mon");
                                break;
                            case 0x02:
                                Console.WriteLine("Battlemode: Trainer");
                                break;
                            default:
                                Console.WriteLine("Battlemode: ???");
                                break;
                        }
                        battleModeChanged = false;
                    }
                    break;
                default:
                    break;
            }
			//TODO do stuff
		}

        private void InitializeComponent()
        {
            Console.WriteLine($"init components");
            this.button1 = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(29, 50);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 0;
            this.button1.Text = "button1";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(48, 102);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(35, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "label1";
            // 
            // CrystalAiForm
            // 
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.button1);
            this.Name = "CrystalAiForm";
            this.ResumeLayout(false);
            this.PerformLayout();
            Console.WriteLine($"done init components");
        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

    }
}