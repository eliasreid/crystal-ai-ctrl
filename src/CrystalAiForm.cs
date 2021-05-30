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
        private Button btnMove0;
        private Button btnMove1;
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
        private Button btnConnect;
        private Button btnTestSend;
        private GroupBox grpItems;
        private Button btnItem1;
        private Button btnItem0;

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

        //Game constants
        const uint BattleMode = 0xD22D;
        UInt16 CurPartyMon = 0xD109;
        UInt16 EnemyMonMoves = 0xd208;
        List<byte> ExpectedData = new List<byte> { 0x21, 0xA7, 0xD2 };

        UInt16 EnemyCurrentMoveNum = 0xC6E9;
        UInt16 EnemyCurrentMove = 0xC6E4;

        UInt16 LoadEnemyMonToSwitchTo = 0x56CA;
        UInt16 BattleTurn = 0x412F;
        

        const UInt16 OTPartyMon1Species = 0xd288;
        const UInt16 OTPartyMon1Status = 0xd2a8;

        const UInt16 BattleStructSize = 48;
        const UInt16 OTPartyCount = 0xd280;
        //unused, delete?
        const UInt16 OTPartyMon0 = 0xd281;

        const UInt16 InitBattleTrainer = 0x7594;
        //end of function (0f:68eb LoadEnemyMon)
        const UInt16 LoadEnemyMonRet = 0x6B37;
        const UInt16 ExitBattle = 0x769e;
        const UInt16 ParseEnemyAction = 0x67C1;
        const UInt16 BattleMenu = 0x6139;
        const UInt16 SwitchOrTryItem = 0x4000;
        const UInt16 SwitchOrTryItemOk = 0x4032;
        private CheckBox chkJoypadDisable;

        const UInt16 RomBank = 0xff9d;

        const UInt16 BattleStartMessage = 0x7c8b;

        //Data for switching logic
        const UInt16 EnemySwitchMonIndex = 0xc718;
        const UInt16 AiTrySwitch = 0x444B;
        const UInt16 DontSwitch = 0x4041;
        const UInt16 DontSwitchRet = 0x4044;

        const UInt16 ReadTrainerPartyDone = 0x57d0;

        const UInt16 EnemyTrainerItem1 = 0xc650;
        const UInt16 EnemyTrainerItem2 = 0xc651;
        const UInt16 AI_TryItem = 0x4105;
        const UInt16 AI_TryItemHasItem = 0x413f;
        const UInt16 AI_ItemsHealItem = 0x422c;
        const UInt16 AI_ItemsStatus = 0x41ca;
        const UInt16 AI_ItemsUse = 0x4385;
        const UInt16 AI_Items = 0x4196;

        //TOOD: inBattle should be "controllingBattle" - thta way we can start with false, even if start in middle of battle, 
        //Will just start working on next battle
        private bool enemyCtrlActive = false;
        private ChosenActionMsg? currentChosenAction = null;
        bool inputDisabled = false;

        private void resetBattleState()
        {
            enemyCtrlActive = false;
            currentChosenAction = null;
            inputDisabled = false;

            //Send blank available acitons msg
            updateClientActions(new AvailableActionsMsg());
        }

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

            resetBattleState();

            wsClient.MessageReceiveCallback( data =>
            {
                string msgString = Encoding.UTF8.GetString(data.Array, 0, data.Count);
                //Console.WriteLine($"message received from websocket: {msgString}");
                try{
                    //iterate over json fields until see "chosenAction" string
                    //Probably better ways to check json for field before deserializing object.
                    JsonTextReader reader = new JsonTextReader(new StringReader(msgString));
                    while (reader.Read())
                    {
                        //not sure if this will work - better way would be to have a message 
                        if (reader.Value != null && reader.Value.ToString() == MsgsCommon.chosenAction)
                        {
                            //handle chosen action message
                            var chosenAction = JsonConvert.DeserializeObject<ChosenActionMsg>(msgString);
                            handleChosenAction(chosenAction);
                        }
                    }
                }catch(JsonReaderException e){
                    //Console.WriteLine($"Json parse exception {e.ToString()}");
                }
            });

            Console.WriteLine("Restart called, available registers");
            foreach(KeyValuePair<string, ulong> entry in _maybeEmuAPI.GetRegisters())
            {
                Console.WriteLine($"{entry.Key}");
            }

            //reset varts when save state is loaded, otherwise get into weird state.
            //As a conseqeunce, have to wait until next battle start to regain control
            _maybeClientAPI.StateLoaded += (_, _) =>
            {
                resetBattleState();
            };

            //set inBattle flag when enemy trainer is initialized
            //TODO: eventually meaning will change - only set inBattle if enemy is being controlled by net / ui, etc
            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) => {
                enemyCtrlActive = true;
                Console.WriteLine("Init enemy trainer called");
            }, InitBattleTrainer, "System Bus");

            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) => {
                enemyCtrlActive = false;
                Console.WriteLine("Exit battle called");
            }, ExitBattle, "System Bus");

            //Executed when new enemy pokmeon is switched in
            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) =>
            {
                if (enemyCtrlActive && _maybeMemAPI.ReadByte(RomBank, "System Bus") == 0x0F)
                {
                    Console.WriteLine("new turn, disabling input and sending out updated actions");
                    //Disable player input until receive response from enemy controller
                    InputDisable(true);
                    
                    AvailableActionsMsg msg = new AvailableActionsMsg();
                    msg.pokemon = readEnemyParty();
                    msg.moves = readEnemyMoves();
                    msg.items = readEnemyItems();
                    updateClientActions(msg);
                }
            }, BattleTurn, "System Bus");


            //This is where enemy attack is re-written, if enemy selects an attack
            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) =>
            {
                //TOOD: check if flag is needed
                if (enemyCtrlActive)
                {
                    Console.WriteLine("Parsing enemy action");
                    if (currentChosenAction?.actionType == MsgsCommon.ActionType.useMove)
                    {
                        Console.WriteLine("Overwriting enemy move with chosen move");

                    //TODO: read move from index 
                        var moveId = _maybeMemAPI.ReadByte(EnemyMonMoves + currentChosenAction.actionIndex, "System Bus");
                        _maybeMemAPI.WriteByte(EnemyCurrentMove, moveId, "System Bus");
                        _maybeMemAPI.WriteByte(EnemyCurrentMoveNum, (uint)currentChosenAction.actionIndex, "System Bus");
                    }
                    else
                    {
                        Console.WriteLine("No move override selected, going with AI generated move");
                    }
                }

            }, ParseEnemyAction, "System Bus");

            //If opponent has chosen to use a move, exit immediately from AI_SwitchOrTryItem
            //function, so that the move decision doesn't get overriden with a item or switch
            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) => {

                if (enemyCtrlActive && _maybeMemAPI.ReadByte(RomBank, "System Bus") == 0x0E)
                {
                    if (currentChosenAction?.actionType == MsgsCommon.ActionType.useMove)
                    {
                        forceReturn();
                        currentChosenAction = null;
                    }
                }
            }, SwitchOrTryItem, "System Bus");

            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) => {

                if (enemyCtrlActive && _maybeMemAPI.ReadByte(RomBank, "System Bus") == 0x0E)
                {
                    //currentChosenAction is null when I am choosing a move.
                    //Shouldn't this be set?
                    switch (currentChosenAction?.actionType)
                    {
                        case MsgsCommon.ActionType.pokemonSwitch:
                            //Execute switch by jumping to AI_TrySwitch and setting wEnemySwitchMonIndex to chosen value
                            _maybeEmuAPI.SetRegister("PC", AiTrySwitch);
                            Console.WriteLine($"Switching mon to index: {currentChosenAction.actionIndex}");
                            _maybeMemAPI.WriteByte(EnemySwitchMonIndex, (uint)currentChosenAction.actionIndex + 1, "System Bus");
                            currentChosenAction = null;
                            break;
                        case MsgsCommon.ActionType.useMove:
                            //Jump to return statement in DontSwitch - this is to ensure selected move doesn't get
                            // overwritten with AI choice to use item or switch
                            Console.WriteLine($"Jumping to DontSwitchRet to ensure item / switch not used");
                            _maybeEmuAPI.SetRegister("PC", DontSwitchRet);
                            break;
                        case MsgsCommon.ActionType.useItem:
                            // Jump to don't switch, which calls AI_TryItem
                            // In AI_TryItem, handle selecting item
                            _maybeEmuAPI.SetRegister("PC", DontSwitch);
                            Console.WriteLine("using item, jump to don't switch");
                            break;
                        case null:
                            //allow emulation to proceed as normal
                            break;

                    }
                }
            }, SwitchOrTryItemOk, "System Bus");

            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) => {
                if (enemyCtrlActive && _maybeMemAPI.ReadByte(RomBank, "System Bus") == 0x0E)
                {
                    _maybeEmuAPI.SetRegister("PC", AI_TryItemHasItem);
                    //AI_Items:
                    //    dbw FULL_RESTORE, .FullRestore
                    //    dbw MAX_POTION,   .MaxPotion
                    //    dbw HYPER_POTION, .HyperPotion
                    //    dbw SUPER_POTION, .SuperPotion
                    //    dbw POTION,       .Potion
                    //    dbw X_ACCURACY,   .XAccuracy
                    //    dbw FULL_HEAL,    .FullHeal
                    //    dbw GUARD_SPEC,   .GuardSpec
                    //    dbw DIRE_HIT,     .DireHit
                    //    dbw X_ATTACK,     .XAttack
                    //    dbw X_DEFEND,     .XDefend
                    //    dbw X_SPEED,      .XSpeed
                    //    dbw X_SPECIAL,    .XSpecial

                    //item ADDRESS to use
                    var itemAddress = currentChosenAction.actionIndex == 0 ? EnemyTrainerItem1 : EnemyTrainerItem2;
                    var itemId = _maybeMemAPI.ReadByte(itemAddress, "System Bus");

                    var itemsTable = _maybeMemAPI.ReadByteRange(AI_Items, 13 * 3, "System Bus");
                    UInt16 rowAddress = 0;

                    for (int i = 0; i < 13; i++)
                    {
                        if (itemsTable[i * 3] == itemId) { 
                            rowAddress = (UInt16)(AI_Items + (UInt16)i *3);
                            break;
                        }
                    }

                    if(rowAddress == 0)
                    {
                        Console.WriteLine("something went terribly wrong");
                        throw new Exception("Failed to modify emulator stuff to force item use, crashing");
                    }

                    //This should force to "attempt" to use the item, even if ultimately doesnt use
                    //due to pokemon status, trianer tendencies, etc.
                    set16BitRegister(itemAddress, "D", "E");
                    set16BitRegister(rowAddress, "H", "L");
                    Console.WriteLine("addresses set, should be forcing ot attempt to use item");
                }
            }, AI_TryItem, "System Bus");

            //TODO: other callbacks to handle different items types (to elim randomness)
            // Status - maybe that's the only other?
            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) =>
            {
                if (enemyCtrlActive && _maybeMemAPI.ReadByte(RomBank, "System Bus") == 0x0E && 
                    currentChosenAction?.actionType == MsgsCommon.ActionType.useItem)
                {
                    Console.WriteLine("using a healing item");
                    _maybeEmuAPI.SetRegister("PC", (int)AI_ItemsUse);
                    currentChosenAction = null;
                }
            }, AI_ItemsHealItem, "System Bus");

            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) =>
            {
                if (enemyCtrlActive && _maybeMemAPI.ReadByte(RomBank, "System Bus") == 0x0E && 
                    currentChosenAction?.actionType == MsgsCommon.ActionType.useItem)
                {
                    Console.WriteLine("using a status item");
                    _maybeEmuAPI.SetRegister("PC", (int)AI_ItemsUse);
                    currentChosenAction = null;
                }
            }, AI_ItemsStatus, "System Bus");

            //This is where the player enters their battle menu.
            //Pause user input until enemy selects their action
            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) =>
            {
                if (enemyCtrlActive && currentChosenAction == null)
                {
                    Console.WriteLine("Disabling input, waiting for opponent to select action");
                    InputDisable(true);
                }
                //TODO: ideally we only disable input AFTER user has selected an action
                //That is, intercept their action, cancel it, they run it after enemy has selected a move
                //Current limitation means that enemy has to choose move first
            }, BattleMenu, "System Bus");

            //Executed when enemey pokemon is loading from party
            //can rewrite register b to change party index 
            //Only need to rewrite register here when battle starts (first mon), or when enemy mon faints, has to choose next pokemon
            //Probably also when forced to switch for other reasons (like??)
            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) =>
            {
                if(enemyCtrlActive && _maybeMemAPI.ReadByte(RomBank, "System Bus") == 0x0F)
                {
                    if(currentChosenAction?.actionType == MsgsCommon.ActionType.pokemonSwitch)
                    {
                        Console.WriteLine($"in LoadEnemyMonToSwitchTo callback, setting mon index to {currentChosenAction.actionIndex}");
                        _maybeEmuAPI.SetRegister("B", currentChosenAction.actionIndex);
                        currentChosenAction = null;
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
                    //only pokemon selection is available (no moves or items)
                    AvailableActionsMsg availActions = new AvailableActionsMsg();
                    availActions.pokemon = readEnemyParty();
                    updateClientActions(availActions);

                }
            }, ReadTrainerPartyDone, "System Bus");

            //When the "TRAINER wants to battle!" text is about to appear, game hasn't yet
            //decided which pokemon will go out first. So this is a good time to disable 
            // player input to wait for opponent to choose mon
            _maybeMemoryEventsAPI.AddExecCallback((_, _, _) =>
            {
                if(_maybeMemAPI.ReadByte(RomBank, "System Bus") != 0x0F)
                {
                    //wrong memory bank active, not the address we're actually interested in
                    return;
                }
                var partyCount = _maybeMemAPI.ReadByte(OTPartyCount, "System Bus");
                if (enemyCtrlActive && partyCount > 1 && currentChosenAction == null)
                {
                    //Waiting for enemy to choose pokemon, disable input until we receive which
                    //mon should be used
                    Console.WriteLine("Disabling input, waiting for opponent to select pokemon");
                    InputDisable(true);
                }
            }, BattleStartMessage, "System Bus");
        }

        
        private List<MsgsCommon.MonInfo> readEnemyParty(){
            var partyCount = _maybeMemAPI.ReadByte(OTPartyCount, "System Bus");

            //TODO: maybe better to single byte range, rather than multiple API calls?
            var partyInfo = new List<MsgsCommon.MonInfo>();
            for (uint i = 0; i < partyCount; i++)
            {
                var monID = (byte)_maybeMemAPI.ReadByte(OTPartyMon1Species + BattleStructSize * i, "System Bus");
                var statusFlags = (byte)_maybeMemAPI.ReadByte(OTPartyMon1Status + BattleStructSize * i, "System Bus");
                MsgsCommon.Status status = readStatusFlags(statusFlags);
                //TODO: check if pokemon is fainted, to override status condition.
                //TODO: translate ID bytes to proper names (using resource file)
                partyInfo.Add(new MsgsCommon.MonInfo(DataHelpers.pokemonName(monID), status));
            }
            Console.WriteLine("Read enemy party");
            foreach(var mon in partyInfo){
                Console.WriteLine($"name: {mon.name}, status: {mon.status}");
            }
            return partyInfo;
        }

        private List<string> readEnemyItems()
        {
            var itemIds = _maybeMemAPI.ReadByteRange(EnemyTrainerItem1, 2, "System Bus");
            itemIds.RemoveAll(id => id == 0);
            return itemIds.ConvertAll<string>(id => DataHelpers.itemName(id));
        }

        private List<string> readEnemyMoves()
        {
            //Read in move IDs from list - remove moves with id of 0 (represent empty move slots)
            var moveIds = _maybeMemAPI.ReadByteRange(EnemyMonMoves, 4, "System Bus");
            moveIds.RemoveAll(move => move == 0);
            var moveStrings = new List<string>();
            foreach (byte moveId in moveIds)
            {
                moveStrings.Add(DataHelpers.moveName(moveId));
            }
            return moveStrings;
        }

        private static MsgsCommon.Status readStatusFlags(byte statusFlags){
            // Looks like psn burn, frz, par are single bits, but sleep is 3 bits 
            //TODO: I think looks like this - need to confirm.
            //    p  f  b psn slp slp slp
            // b  b  b  b  b  b  b  b 

            //         ; status condition bit flags
            // SLP EQU %111 ; 0-7 turns
            // 	const_def 3
            // 	const PSN
            // 	const BRN
            // 	const FRZ
            // 	const PAR
            byte sleepmask = 0b00000111;
            byte psnmask = 0b00001000;
            byte brnmask = 0b00010000;
            byte frzmask = 0b00100000;
            byte parmask = 0b01000000;
            
            if((sleepmask & statusFlags) != 0){
                return MsgsCommon.Status.sleep;
            }
            if((psnmask & statusFlags) != 0){
                return MsgsCommon.Status.poisoned;
            }
            if((brnmask & statusFlags) != 0){
                return MsgsCommon.Status.burned;
            }
            if((frzmask & statusFlags) != 0){
                return MsgsCommon.Status.frozen;
            }
            if((parmask & statusFlags) != 0){
                return MsgsCommon.Status.paralyzed;
            }
            return MsgsCommon.Status.none;
        }

        private void setupMoveButtons(List<string> moveIds)
        {
            Console.WriteLine("Setting up move buttons");

            foreach (Control ctrl in grpMoves.Controls)
            {
                ctrl.Text = "";
                ctrl.Enabled = false;
            }

            if(moveIds.Count < 1)
            {
                return;
            }
            
            btnMove0.Text = $"{moveIds[0]}";
            btnMove0.Enabled = true;

            if(moveIds.Count < 2)
            {
                return;
            }
            btnMove1.Text = $"{moveIds[1]}";
            btnMove1.Enabled = true;
            if (moveIds.Count < 3)
            {
                return;
            }
            btnMove2.Text = $"{moveIds[2]}";
            btnMove2.Enabled = true;
            if (moveIds.Count < 4)
            {
                return;
            }
            btnMove3.Text = $"{moveIds[3]}";
            btnMove3.Enabled = true;
        }

        private void setupMonButtons(List<MsgsCommon.MonInfo> monIDs)
        {
            Console.WriteLine("setting up enemy mon buttons");

            foreach (Control ctrl in grpMons.Controls)
            {
                ctrl.Enabled = false;
                ctrl.Text = "";
            }
            if (monIDs.Count < 1)
            {
                return;
            }

            btnMon0.Text = $"{monIDs[0].name}";
            btnMon0.Enabled = true;

            if(monIDs.Count < 2)
            {
                return;
            }

            btnMon1.Text = $"{monIDs[1].name}";
            btnMon1.Enabled = true;
            if (monIDs.Count < 3)
            {
                return;
            }
            btnMon2.Text = $"{monIDs[2].name}";
            btnMon2.Enabled = true;
            if (monIDs.Count < 4)
            {
                return;
            }
            btnMon3.Text = $"{monIDs[3].name}";
            btnMon3.Enabled = true;
            if (monIDs.Count < 5)
            {
                return;
            }
            btnMon4.Text = $"{monIDs[4].name}";
            btnMon4.Enabled = true;
            if (monIDs.Count < 6)
            {
                return;
            }
            btnMon5.Text = $"{monIDs[5].name}";
            btnMon5.Enabled = true;
        }

        private void setupItemButtons(List<string> items)
        {
            foreach (Control ctrl in grpItems.Controls)
            {
                ctrl.Enabled = false;
                ctrl.Text = "";
            }
            if (items.Count < 1)
            {
                return;
            }

            btnItem0.Text = $"{items[0]}";
            btnItem0.Enabled = true;

            if (items.Count < 2)
            {
                return;
            }

            btnItem1.Text = $"{items[1]}";
            btnItem1.Enabled = true;
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
                    break;
                default:
                    break;
            }
		}

        private void InputDisable(bool disable)
        {
            if (chkJoypadDisable.Checked)
            {
                disable = false;
            }
            inputDisabled = disable;
            _maybeMemAPI.WriteByte(0xCFBE, disable ? (uint)0b00010000 : 0);
        }

        private void InitializeComponent()
        {
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
            this.btnMove0 = new System.Windows.Forms.Button();
            this.btnMove1 = new System.Windows.Forms.Button();
            this.btnMove2 = new System.Windows.Forms.Button();
            this.btnMove3 = new System.Windows.Forms.Button();
            this.grpMoves = new System.Windows.Forms.GroupBox();
            this.grpItems = new System.Windows.Forms.GroupBox();
            this.btnItem1 = new System.Windows.Forms.Button();
            this.btnItem0 = new System.Windows.Forms.Button();
            this.grpMons.SuspendLayout();
            this.grpMoves.SuspendLayout();
            this.grpItems.SuspendLayout();
            this.SuspendLayout();
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
            this.grpMons.Size = new System.Drawing.Size(242, 115);
            this.grpMons.TabIndex = 1;
            this.grpMons.TabStop = false;
            this.grpMons.Text = "Pokemon";
            // 
            // btnMon5
            // 
            this.btnMon5.Location = new System.Drawing.Point(121, 77);
            this.btnMon5.Name = "btnMon5";
            this.btnMon5.Size = new System.Drawing.Size(115, 23);
            this.btnMon5.TabIndex = 0;
            this.btnMon5.Text = "Mon 5";
            this.btnMon5.UseVisualStyleBackColor = true;
            this.btnMon5.Click += new System.EventHandler(this.btnMon5_Click);
            // 
            // btnMon4
            // 
            this.btnMon4.Location = new System.Drawing.Point(6, 77);
            this.btnMon4.Name = "btnMon4";
            this.btnMon4.Size = new System.Drawing.Size(109, 23);
            this.btnMon4.TabIndex = 0;
            this.btnMon4.Text = "Mon 4";
            this.btnMon4.UseVisualStyleBackColor = true;
            this.btnMon4.Click += new System.EventHandler(this.btnMon4_Click);
            // 
            // btnMon3
            // 
            this.btnMon3.Location = new System.Drawing.Point(121, 48);
            this.btnMon3.Name = "btnMon3";
            this.btnMon3.Size = new System.Drawing.Size(115, 23);
            this.btnMon3.TabIndex = 0;
            this.btnMon3.Text = "Mon 3";
            this.btnMon3.UseVisualStyleBackColor = true;
            this.btnMon3.Click += new System.EventHandler(this.btnMon3_Click);
            // 
            // btnMon2
            // 
            this.btnMon2.Location = new System.Drawing.Point(6, 48);
            this.btnMon2.Name = "btnMon2";
            this.btnMon2.Size = new System.Drawing.Size(109, 23);
            this.btnMon2.TabIndex = 0;
            this.btnMon2.Text = "Mon 2";
            this.btnMon2.UseVisualStyleBackColor = true;
            this.btnMon2.Click += new System.EventHandler(this.btnMon2_Click);
            // 
            // btnMon1
            // 
            this.btnMon1.Location = new System.Drawing.Point(121, 19);
            this.btnMon1.Name = "btnMon1";
            this.btnMon1.Size = new System.Drawing.Size(115, 23);
            this.btnMon1.TabIndex = 0;
            this.btnMon1.Text = "Mon 1";
            this.btnMon1.UseVisualStyleBackColor = true;
            this.btnMon1.Click += new System.EventHandler(this.btnMon1_Click);
            // 
            // btnMon0
            // 
            this.btnMon0.Location = new System.Drawing.Point(6, 19);
            this.btnMon0.Name = "btnMon0";
            this.btnMon0.Size = new System.Drawing.Size(109, 23);
            this.btnMon0.TabIndex = 0;
            this.btnMon0.Text = "Mon 0";
            this.btnMon0.UseVisualStyleBackColor = true;
            this.btnMon0.Click += new System.EventHandler(this.btnMon0_Click);
            // 
            // lblCurrentState
            // 
            this.lblCurrentState.AutoSize = true;
            this.lblCurrentState.Location = new System.Drawing.Point(15, 304);
            this.lblCurrentState.Name = "lblCurrentState";
            this.lblCurrentState.Size = new System.Drawing.Size(85, 13);
            this.lblCurrentState.TabIndex = 2;
            this.lblCurrentState.Text = "Game state: ???";
            // 
            // chkJoypadDisable
            // 
            this.chkJoypadDisable.AutoSize = true;
            this.chkJoypadDisable.Location = new System.Drawing.Point(27, 339);
            this.chkJoypadDisable.Name = "chkJoypadDisable";
            this.chkJoypadDisable.Size = new System.Drawing.Size(131, 17);
            this.chkJoypadDisable.TabIndex = 3;
            this.chkJoypadDisable.Text = "Don\'t wait for ctrl input";
            this.chkJoypadDisable.UseVisualStyleBackColor = true;
            this.chkJoypadDisable.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            // 
            // btnConnect
            // 
            this.btnConnect.Location = new System.Drawing.Point(15, 379);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(75, 23);
            this.btnConnect.TabIndex = 4;
            this.btnConnect.Text = "Connect";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            // 
            // btnTestSend
            // 
            this.btnTestSend.Location = new System.Drawing.Point(103, 379);
            this.btnTestSend.Name = "btnTestSend";
            this.btnTestSend.Size = new System.Drawing.Size(75, 23);
            this.btnTestSend.TabIndex = 4;
            this.btnTestSend.Text = "Test Send";
            this.btnTestSend.UseVisualStyleBackColor = true;
            this.btnTestSend.Click += new System.EventHandler(this.btnTestSend_Click);
            // 
            // btnMove0
            // 
            this.btnMove0.Enabled = false;
            this.btnMove0.Location = new System.Drawing.Point(6, 19);
            this.btnMove0.Name = "btnMove0";
            this.btnMove0.Size = new System.Drawing.Size(109, 23);
            this.btnMove0.TabIndex = 0;
            this.btnMove0.Text = "Move 0";
            this.btnMove0.UseVisualStyleBackColor = true;
            this.btnMove0.Click += new System.EventHandler(this.btnMove0_Click);
            // 
            // btnMove1
            // 
            this.btnMove1.Enabled = false;
            this.btnMove1.Location = new System.Drawing.Point(121, 19);
            this.btnMove1.Name = "btnMove1";
            this.btnMove1.Size = new System.Drawing.Size(115, 23);
            this.btnMove1.TabIndex = 0;
            this.btnMove1.Text = "Move 1";
            this.btnMove1.UseVisualStyleBackColor = true;
            this.btnMove1.Click += new System.EventHandler(this.btnMove1_Click);
            // 
            // btnMove2
            // 
            this.btnMove2.Enabled = false;
            this.btnMove2.Location = new System.Drawing.Point(6, 48);
            this.btnMove2.Name = "btnMove2";
            this.btnMove2.Size = new System.Drawing.Size(109, 23);
            this.btnMove2.TabIndex = 0;
            this.btnMove2.Text = "Move 2";
            this.btnMove2.UseVisualStyleBackColor = true;
            this.btnMove2.Click += new System.EventHandler(this.btnMove2_Click);
            // 
            // btnMove3
            // 
            this.btnMove3.Enabled = false;
            this.btnMove3.Location = new System.Drawing.Point(121, 48);
            this.btnMove3.Name = "btnMove3";
            this.btnMove3.Size = new System.Drawing.Size(115, 23);
            this.btnMove3.TabIndex = 0;
            this.btnMove3.Text = "Move 3";
            this.btnMove3.UseVisualStyleBackColor = true;
            this.btnMove3.Click += new System.EventHandler(this.btnMove3_Click);
            // 
            // grpMoves
            // 
            this.grpMoves.Controls.Add(this.btnMove3);
            this.grpMoves.Controls.Add(this.btnMove2);
            this.grpMoves.Controls.Add(this.btnMove1);
            this.grpMoves.Controls.Add(this.btnMove0);
            this.grpMoves.Location = new System.Drawing.Point(12, 12);
            this.grpMoves.Name = "grpMoves";
            this.grpMoves.Size = new System.Drawing.Size(242, 81);
            this.grpMoves.TabIndex = 0;
            this.grpMoves.TabStop = false;
            this.grpMoves.Text = "Moves";
            // 
            // grpItems
            // 
            this.grpItems.Controls.Add(this.btnItem1);
            this.grpItems.Controls.Add(this.btnItem0);
            this.grpItems.Location = new System.Drawing.Point(9, 220);
            this.grpItems.Name = "grpItems";
            this.grpItems.Size = new System.Drawing.Size(245, 54);
            this.grpItems.TabIndex = 5;
            this.grpItems.TabStop = false;
            this.grpItems.Text = "Items";
            // 
            // btnItem1
            // 
            this.btnItem1.Enabled = false;
            this.btnItem1.Location = new System.Drawing.Point(124, 19);
            this.btnItem1.Name = "btnItem1";
            this.btnItem1.Size = new System.Drawing.Size(115, 23);
            this.btnItem1.TabIndex = 0;
            this.btnItem1.Text = "Item 1";
            this.btnItem1.UseVisualStyleBackColor = true;
            this.btnItem1.Click += new System.EventHandler(this.btnItem1_Click);
            // 
            // btnItem0
            // 
            this.btnItem0.Enabled = false;
            this.btnItem0.Location = new System.Drawing.Point(6, 19);
            this.btnItem0.Name = "btnItem0";
            this.btnItem0.Size = new System.Drawing.Size(112, 23);
            this.btnItem0.TabIndex = 0;
            this.btnItem0.Text = "Item 0";
            this.btnItem0.UseVisualStyleBackColor = true;
            this.btnItem0.Click += new System.EventHandler(this.btnItem0_Click);
            // 
            // CrystalAiForm
            // 
            this.ClientSize = new System.Drawing.Size(268, 460);
            this.Controls.Add(this.grpItems);
            this.Controls.Add(this.btnTestSend);
            this.Controls.Add(this.btnConnect);
            this.Controls.Add(this.chkJoypadDisable);
            this.Controls.Add(this.lblCurrentState);
            this.Controls.Add(this.grpMons);
            this.Controls.Add(this.grpMoves);
            this.Name = "CrystalAiForm";
            this.grpMons.ResumeLayout(false);
            this.grpMoves.ResumeLayout(false);
            this.grpItems.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private void btnMon0_Click(object sender, EventArgs e)
        {
            handleChosenAction(new ChosenActionMsg(MsgsCommon.ActionType.pokemonSwitch, 0));
        }

        private void btnMon1_Click(object sender, EventArgs e)
        {
            handleChosenAction(new ChosenActionMsg(MsgsCommon.ActionType.pokemonSwitch, 1));
        }

        private void btnMon2_Click(object sender, EventArgs e)
        {
            handleChosenAction(new ChosenActionMsg(MsgsCommon.ActionType.pokemonSwitch, 2));
        }

        private void btnMon3_Click(object sender, EventArgs e)
        {
            handleChosenAction(new ChosenActionMsg(MsgsCommon.ActionType.pokemonSwitch, 3));
        }

        private void btnMon4_Click(object sender, EventArgs e)  
        {
            handleChosenAction(new ChosenActionMsg(MsgsCommon.ActionType.pokemonSwitch, 4));
        }

        private void btnMon5_Click(object sender, EventArgs e)
        {
            handleChosenAction(new ChosenActionMsg(MsgsCommon.ActionType.pokemonSwitch, 5));
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            //TODO: may want to save whether the input SHOULD be disabled or not
            InputDisable(false);
        }

        private void btnMove0_Click(object sender, EventArgs e)
        {
            handleChosenAction(new ChosenActionMsg(MsgsCommon.ActionType.useMove, 0));
        }

        private void btnMove1_Click(object sender, EventArgs e)
        {
            handleChosenAction(new ChosenActionMsg(MsgsCommon.ActionType.useMove, 1));
        }

        private void btnMove2_Click(object sender, EventArgs e)
        {
            handleChosenAction(new ChosenActionMsg(MsgsCommon.ActionType.useMove, 2));
        }

        private void btnMove3_Click(object sender, EventArgs e)
        {
            handleChosenAction(new ChosenActionMsg(MsgsCommon.ActionType.useMove, 3));
        }
        private void btnItem0_Click(object sender, EventArgs e)
        {
            handleChosenAction(new ChosenActionMsg(MsgsCommon.ActionType.useItem, 0));
        }

        private void btnItem1_Click(object sender, EventArgs e)
        {
            handleChosenAction(new ChosenActionMsg(MsgsCommon.ActionType.useItem, 1));
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            Console.WriteLine("connecting to ws");

            //TODO: make url configurable
            var connectTask = wsClient.Connect(new Uri("ws://localhost:8999/?type=emulator"));
            connectTask.ContinueWith((task) =>
            {
                if (task.Result == WsClient.ConnectResult.Success)
                {
                    //Update GUI with link for client to connect
                    //Console.WriteLine($"connection to server success!");
                }
                else
                {
                    //Console.WriteLine($"connection to server fail!");
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

        //For handling moves chosen from browser client or ui buttons
        private void handleChosenAction(ChosenActionMsg chosenAction){
            if(currentChosenAction == null)
            {
                currentChosenAction = chosenAction;
                InputDisable(false);
            }
        }

        private void updateClientActions(AvailableActionsMsg availableActions)
        {
            currentChosenAction = null;

            //update plugin UI
            setupItemButtons(availableActions.items);
            setupMonButtons(availableActions.pokemon);
            setupMoveButtons(availableActions.moves);

            //Send message to browser to updatae web client
            string json = JsonConvert.SerializeObject(availableActions, Formatting.None);
            wsClient.SendMessage(json);
        }

        private void set16BitRegister(UInt16 value, string msbRegister, string lsbRegister)
        {
            var bytes = BitConverter.GetBytes(value);

            //flip to make sure first byte is higher byte
            if (BitConverter.IsLittleEndian)
            {
                bytes = new byte [] {bytes[1], bytes[0] };
            }

            _maybeEmuAPI.SetRegister(msbRegister, bytes[0]);
            _maybeEmuAPI.SetRegister(lsbRegister, bytes[1]);
        }

        private void forceReturn()
        {
            //TODO: can simplify this maybe?
            UInt16 currentPC = (UInt16)_maybeEmuAPI.GetRegister("PC");
            byte currentSPl = (byte)_maybeEmuAPI.GetRegister("SPl");
            byte currentSPh = (byte)_maybeEmuAPI.GetRegister("SPh");

            UInt16 currentSP = BitConverter.IsLittleEndian ? 
                BitConverter.ToUInt16(new byte[] {currentSPl, currentSPh}, 0) :
                BitConverter.ToUInt16(new byte[] { currentSPh, currentSPl }, 0);

            //read memory at current SP, set PC to stored address (account for GB endianess)
            List<byte> retAddress = _maybeMemAPI.ReadByteRange(currentSP, 2, "System Bus");
            //Flipped because GB stores address in little endian
            _maybeEmuAPI.SetRegister("PCh", (int)retAddress[1]);
            _maybeEmuAPI.SetRegister("PCl", (int)retAddress[0]);
            UInt16 newSP = (UInt16)(currentSP + 2);
            set16BitRegister(newSP, "SPh", "SPl");
        }

    }

}