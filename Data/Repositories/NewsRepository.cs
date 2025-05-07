
using MyTts.Data.Context;
using MyTts.Data.Entities;
using MyTts.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using AutoMapper;

namespace MyTts.Data.Repositories
{
    public class NewsRepository : Repository<News, INews>, INewsRepository
    {
        public NewsRepository(AppDbContext context, IMapper mapper) : base(context, mapper) { }
        // News'a özel metodları burada implemente edebilirsin
        public async Task<IEnumerable<News>> GetAllNewsAsync()
        {
            return await _context.News.ToListAsync();
        }
    }
}
