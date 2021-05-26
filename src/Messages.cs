using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CrystalAiCtrl
{

    public class MsgsCommon
    {
        public static readonly string battleStart = "battleStart";
        public static readonly string chosenAction = "chosenAction";
        public static readonly string availableActions = "availableActions";

        public enum ActionType
        {
            useMove,
            pokemonSwitch,
            useItem
        }
        [JsonConverter(typeof(StringEnumConverter))]
        public enum Status{
          none,
          fainted, // maybe there isn't a fainted status. Just HP 0?
          poisoned,
          paralyzed,
          burned,
          frozen,
          sleep 
        };

        //Information that needs to be transmitted when telling the browser about pokemon in party
        public class MonInfo{
            public string name = "";
            public Status status = Status.none;
            public MonInfo(string nameIn, Status statusIn){
                name = nameIn;
                status = statusIn;
            }
        };
    };

    //TODO: remove this message
    public class BattleStartMsg
    {
        public readonly string msgType = MsgsCommon.battleStart;
        public TrainerInfo trainerInfo = new TrainerInfo();

        public BattleStartMsg(){}

        public BattleStartMsg(TrainerInfo trainerInfoIn){
            trainerInfo = trainerInfoIn;
        }
    }

    public class AvailableActionsMsg
    {
        public readonly string msgType = MsgsCommon.availableActions;
        public List<string> moves = new List<string>();
        public List<MsgsCommon.MonInfo> pokemon = new List<MsgsCommon.MonInfo>();
        public List<string> items = new List<string>();
    }
    
    public class TrainerInfo
    {
        public string trainerName = "";
        public List<string> pokemonNames = new List<string>();
    }

    public class ChosenActionMsg 
    {
        public readonly string msgType = MsgsCommon.chosenAction;

        //For now, only need to communicate this for the enemy actions
        //action can be fully described by a type and index.
        public MsgsCommon.ActionType actionType;
        public int actionIndex;
    }



}
