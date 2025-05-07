using MyTts.Data.Interfaces;

namespace MyTts.Data.Entities
{
    public abstract class BaseEntity : IEntity
{
    public int Id { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
}