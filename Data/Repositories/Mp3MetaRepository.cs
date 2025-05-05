
using System.Linq;
using MyTts.Data.Context;
using MyTts.Data.Entities;
using MyTts.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MyTts.Data.Repositories
{
    public class Mp3MetaRepository : Repository<Mp3Meta>, IMp3MetaRepository
    {
        public Mp3MetaRepository(AppDbContext context) : base(context) { }

        // Mp3File'a özel metodları burada implemente edebilirsin

    }
}