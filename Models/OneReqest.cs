namespace MyTts.Models
{
    /// <summary>
    /// Request model for the "one" endpoint.
    /// </summary>
    public class OneRequest
    {
        public dynamic News { get; set; }
        public string Language { get; set; }
    }
}