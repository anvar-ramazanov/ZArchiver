namespace ZArchiver
{
    public enum Action
    {
        Compress,
        Decompress,
        Unknown
    }

    public class ActionParser
    {
        public static Action Parse(string action)
        {
            switch(action)
            {
                case "compress": return Action.Compress;
                case "decompress": return Action.Decompress;
                default: return Action.Unknown;
            }
        }
    }
}
