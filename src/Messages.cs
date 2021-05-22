using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CrystalAiCtrl
{

    public class MsgsCommon
    {
        public static readonly string battleStart = "battleStart";
        public static readonly string chosenAction = "chosenAction";

        public enum ActionType
        {
            useMove,
            pokemonSwitch,
            useItem
        }
    };

    public class BattleStartMsg
    {
        public readonly string msgType = MsgsCommon.battleStart;
        public TrainerInfo trainerInfo = new TrainerInfo();
    }
    
    public class TrainerInfo
    {
        public string trainerName = "";
        public List<string> pokemonNames = new List<string>();
    }

    public class ChosenAction 
    {
        public readonly string msgType = MsgsCommon.chosenAction;

        //For now, only need to communicate this for the enemy actions
        //action can be fully described by a type and index.
        public MsgsCommon.ActionType actionType;
        public int actionIndex;
        
    }
}
