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
            _maybeMemoryEventsAPI.AddWriteCallback((_, written_val, flags) => {
                battleModeChanged = true;
                Console.WriteLine("BattleMode written");
            }, BattleMode, "System Bus");


            //This is to rewrite wCurPartyMon in LoadEnemyMonToSwitchTo before it is used
            //TODO: Should be a breakpoint?
            UInt16 CurMonWriteAddress = 0x5589;
            List<byte> ExpectedData = new List<byte> { 0x21, 0x7C, 0xDD };
            _maybeMemoryEventsAPI.AddExecCallback((_, cbAddr, _) =>
            {
                //Reading bytes at the program counter location and comparing with data I know should be there
                //This is a hacky way to make sure we're in the correct ROM bank
                var bytes = _maybeMemAPI.ReadByteRange(CurMonWriteAddress, 3, "System Bus");
                if (bytes.SequenceEqual(ExpectedData))
                {
                    //TODO: pause emulation - Set up UI for move selection
                    Console.WriteLine("enemy selecting poke");
                }

            }, CurMonWriteAddress, "System Bus");

        }

		public bool AskSaveChanges() => true;

		public void UpdateValues(ToolFormUpdateType type)
		{
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
		}

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}