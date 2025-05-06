namespace MyTts.Storage
{
    public class DiskConfiguration
    {
        public string Driver { get; set; } = "local";
        public string Root { get; set; } = string.Empty;
        public Dictionary<string, string> Config { get; set; } = new();

    }
}
