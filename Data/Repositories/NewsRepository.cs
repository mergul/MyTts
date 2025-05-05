
using MyTts.Data.Context;
using MyTts.Data.Entities;
using MyTts.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MyTts.Data.Repositories
{
    public class NewsRepository : Repository<News>, INewsRepository
    {
        public NewsRepository(AppDbContext context) : base(context) { }

        // News'a özel metodları burada implemente edebilirsin
    }
}
