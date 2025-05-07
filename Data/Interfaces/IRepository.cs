using System.Collections.Generic;
using System.Threading.Tasks;
using MyTts.Data.Entities;
using MyTts.Data.Interfaces;

namespace MyTts.Data.Interfaces
{
    public interface IRepository<TEntity, TModel> 
    
    where TEntity : BaseEntity 
    where TModel : class, IModel
    {
        Task<TEntity> GetByIdAsync(int id);
        Task<IEnumerable<TEntity>> GetAllAsync();
        Task AddAsync(TEntity entity);
        Task SaveChangesAsync();
        Task<IEnumerable<TEntity>> FindAsync(Func<TEntity, bool> predicate);
        void Update(TEntity entity);
        void Delete(TEntity entity);
    }
    
}
