using System.Collections.Generic;
using static CrystalAiCtrl.Properties.Resources;
using System.Linq;
using System;

namespace CrystalAiCtrl
{
    public class DataHelpers
    {
        static public List<string> PokemonNames;
        static public List<string> MoveNames;

        static DataHelpers()
        {
            //functions are called
            PokemonNames = pokemon_names
                .Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList<string>();
            MoveNames = move_names
                .Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList<string>();
        }

        public static string pokemonName(byte monId)
        {
            return PokemonNames[(int)monId];
        }

        public static string moveName(byte moveId)
        {
            return MoveNames[(int)moveId];
        }
    }

}
