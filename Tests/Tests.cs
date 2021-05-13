using Microsoft.VisualStudio.TestTools.UnitTesting;
using CrystalAiCtrl;

namespace Tests
{
    [TestClass]
    public class Tests
    {
        [TestMethod]
        public void TestMethod1()
        {
            Assert.AreEqual("hello", DataHelpers.hello());
        }
    }
}
