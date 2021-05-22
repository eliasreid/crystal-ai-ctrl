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
        public void ExampleTestCase()
        {
            Assert.AreEqual(3, DataHelpers.add(1, 2));
        }

        [TestMethod]
        public void TestResource()
        {
            Assert.AreEqual("value", DataHelpers.PokemonNames[0]);
        }


        //msg tests
        [TestMethod]
        public void ParseBattleStart()
        {
            string json = @"{""msgType"":""battleStart"",
                             ""trainerInfo"":{
                                ""pokemonNames"":[""rat"", ""pidg"" ],
                                ""trainerName"":""Youngster Joey""
                              }
                            }";
            BattleStartMsg obj = JsonConvert.DeserializeObject<BattleStartMsg>(json);
            BattleStartMsg gold_val = new BattleStartMsg(
                new TrainerInfo("Youngster Joey", new List<string> { "rat", "pidg" })
                );
            //gold_val.trainerInfo.pokemonNames = new List<string> { "rat", "pidg" };
            //gold_val.trainerInfo.trainerName = "Youngster Joey";
            Assert.AreEqual(obj, gold_val);
        }
    }
}
