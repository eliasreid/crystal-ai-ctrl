using Microsoft.VisualStudio.TestTools.UnitTesting;
using CrystalAiCtrl;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Tests
{
    [TestClass]
    public class Tests
    {

        [TestMethod] 
        public void PokemonNamesTest()
        {
            Assert.AreEqual("Bulbasaur", DataHelpers.pokemonName(1));
            Assert.AreEqual("Celebi", DataHelpers.pokemonName(251));
        }

        [TestMethod]
        public void MoveNamesTest()
        {
            Assert.AreEqual("Pound", DataHelpers.moveName(1));
            Assert.AreEqual("Beat Up", DataHelpers.moveName(251));
        }

        //msg tests
        // [TestMethod]
        // public void ParseBattleStart()
        // {
        //     string json = @"{""msgType"":""battleStart"",
        //                      ""trainerInfo"":{
        //                         ""pokemonNames"":[""rat"", ""pidg"" ],
        //                         ""trainerName"":""Youngster Joey""
        //                       }
        //                     }";
        //     BattleStartMsg obj = JsonConvert.DeserializeObject<BattleStartMsg>(json);
        //     BattleStartMsg gold_val = new BattleStartMsg(
        //         new TrainerInfo("Youngster Joey", new List<string> { "rat", "pidg" })
        //         );
        //     //gold_val.trainerInfo.pokemonNames = new List<string> { "rat", "pidg" };
        //     //gold_val.trainerInfo.trainerName = "Youngster Joey";
        //     Assert.AreEqual(obj, gold_val);
        // }
    }
}
