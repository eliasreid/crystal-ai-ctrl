using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CrystalAiCtrl
{

    class MsgsCommon
    {
        public static readonly string battleStart = "battleStart";
    };

    class BattleStartMsg
    {
        public readonly string msgType = MsgsCommon.battleStart;
        public TrainerInfo trainerInfo = new TrainerInfo();
    }
    
    class TrainerInfo
    {
        public string trainerName = "";
        public List<string> pokemonNames = new List<string>();
    }
}
