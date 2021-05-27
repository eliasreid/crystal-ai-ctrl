using System.Collections.Generic;
using static CrystalAiCtrl.Properties.Resources;
using System.Linq;

namespace CrystalAiCtrl
{
    public class DataHelpers
    {
        static public List<string> PokemonNames;

        static DataHelpers()
        {
            //functions are called
            PokemonNames = pokemon_names.Split(System.Environment.NewLine.ToCharArray(), System.StringSplitOptions.RemoveEmptyEntries).ToList<string>();
        }

        public static string pokemonName(byte monId)
        {
            return PokemonNames[(int)monId];
        }
    }

}
