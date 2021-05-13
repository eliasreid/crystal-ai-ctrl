using Microsoft.VisualStudio.TestTools.UnitTesting;
using CrystalAiCtrl;

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
    }
}
