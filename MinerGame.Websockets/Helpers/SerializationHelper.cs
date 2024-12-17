using MiningGame.Core.Models;
using System.Collections.Generic;

public static class SerializationHelper
{
    public static List<List<Block>> ConvertToSerializableMap(Block[,] map)
    {
        var list = new List<List<Block>>();

        for (int i = 0; i < map.GetLength(0); i++)
        {
            var row = new List<Block>();
            for (int j = 0; j < map.GetLength(1); j++)
            {
                row.Add(map[i, j]);
            }
            list.Add(row);
        }

        return list;
    }
}
