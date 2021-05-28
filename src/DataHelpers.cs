using System.Collections.Generic;
using static CrystalAiCtrl.Properties.Resources;
using System.Linq;
using System;

namespace CrystalAiCtrl
{
    public class DataHelpers
    {
        static private List<string> PokemonNames;
        static private List<string> MoveNames;
        static private List<string> ItemNames;

        static DataHelpers()
        {
            //functions are called
            PokemonNames = pokemon_names
                .Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList<string>();
            MoveNames = move_names
                .Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList<string>();
            ItemNames = item_names
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
        public static string itemName(byte itemId)
        {
            return ItemNames[(int)itemId];
        }
    }

}
