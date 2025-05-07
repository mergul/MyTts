using AutoMapper;

namespace MyTts.Data.Interfaces{
public interface IEntity
{
    int Id { get; set; }
    DateTime CreatedDate { get; set; }
    DateTime? ModifiedDate { get; set; }
}

// Base model interface
public interface IModel
{
    int Id { get; set; }
}

// Base mapping interface
public interface IMapFrom<T> where T : IEntity
{
    void Mapping(Profile profile);
}

    public interface INews : IModel
    {
        new int Id { get; set; }
        string Title { get; set; }
        string Content { get; set; }
    }

}