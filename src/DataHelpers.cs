using System.Collections.Generic;
using static CrystalAiCtrl.Properties.Resources;
using System.Resources;

namespace CrystalAiCtrl
{
    public class DataHelpers
    {
        static public List<string> PokemonNames { get; private set; }

        static DataHelpers()
        {
            //static constructor - will run once to initialize things before any DataHelpers
            //functions are called
            PokemonNames = new List<string>();
            PokemonNames.Add(Properties.Resources.sample_resource);
        }

        // Example function. Tested in Tests.ExampleTest() method
        public static int add(int a, int b)
        {
            return a + b;
        }

    }

}
