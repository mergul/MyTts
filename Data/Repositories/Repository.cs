using Microsoft.EntityFrameworkCore;
using MyTts.Data.Context;
using MyTts.Data.Interfaces;
using MyTts.Data.Entities;
using AutoMapper; // Ensure the namespace containing BaseEntity is imported

namespace MyTts.Data.Repositories
{
    public class Repository<TEntity, TModel> : IRepository<TEntity, TModel>
    where TEntity : BaseEntity
    where TModel : class, IModel

    {
        protected readonly AppDbContext _context;
        protected readonly DbSet<TEntity> _dbSet;
        protected readonly IMapper _mapper;

        public Repository(AppDbContext context, IMapper mapper)
        {
            _context = context;
            _dbSet = _context.Set<TEntity>();
            _mapper = mapper;
        }

        public async Task<IEnumerable<TEntity>> GetAllAsync() => await _dbSet.ToListAsync();

        public async Task<TEntity> GetByIdAsync(int id)
        {
            var entity = await _dbSet.FindAsync(id);
            return entity ?? throw new InvalidOperationException($"Entity with id {id} not found");
        }

        public async Task AddAsync(TEntity entity)
        {
            await _dbSet.AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        public void Update(TEntity entity)
        {
            _dbSet.Update(entity);
        }

        public void Delete(TEntity entity)
        {
            _dbSet.Remove(entity);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<TEntity> FindAsync(Func<TEntity, bool> predicate)
        {
            var result = await _dbSet.FirstOrDefaultAsync(x => predicate(x));
            return result ?? throw new InvalidOperationException("Entity not found");
        }

        Task<IEnumerable<TEntity>> IRepository<TEntity, TModel>.FindAsync(Func<TEntity, bool> predicate)
        {
            return Task.FromResult(_dbSet.Where(predicate).AsEnumerable());
        }
        public async Task<bool> ExistByIdAsync(int id)
        {
            return await Task.FromResult(_dbSet.Any(entity => entity.Id == id));
        }

    }
}
