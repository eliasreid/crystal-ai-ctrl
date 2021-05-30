using Microsoft.VisualStudio.TestTools.UnitTesting;
using CrystalAiCtrl;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Tests
{
    [TestClass]
    public class Tests
    {
        private TestContext testContextInstance;

        /// <summary>
        /// Gets or sets the test context which provides
        /// information about and functionality for the current test run.
        /// </summary>
        public TestContext TestContext
        {
            get { return testContextInstance; }
            set { testContextInstance = value; }
        }

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

        [TestMethod]
        public void ItemNamesTest()
        {
            Assert.AreEqual("No Item", DataHelpers.itemName(0));
            Assert.AreEqual("Master Ball", DataHelpers.itemName(1));
            Assert.AreEqual("Antidote", DataHelpers.itemName(9));
        }

        [TestMethod]
        public void ActionMsg()
        {
            var msg = new AvailableActionsMsg();

            msg.moves = new List<string> { "tackle" };
            msg.pokemon = new List<MsgsCommon.MonInfo> { new MsgsCommon.MonInfo("pikachu", MsgsCommon.Status.none) };
            msg.items = new List<string> { "hyper potion" };

            string json = JsonConvert.SerializeObject(msg, Formatting.None);

            TestContext.WriteLine(json);


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
