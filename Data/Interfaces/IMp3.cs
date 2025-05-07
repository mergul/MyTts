
namespace MyTts.Data.Interfaces
{
    public interface IMp3 : IModel
    {
        new int Id { get; set; }
        string FilePath { get; set; }
        string FileName { get; set; }
        string Language { get; set; }
        DateTime? CreatedDate { get; set; }
    }
}