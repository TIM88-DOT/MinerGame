namespace MiningGame.WebSockets
{
    public class GameAction
    {
        public string Type { get; set; }
        public string Direction { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }
}
