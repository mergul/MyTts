
using System.Linq;
using MyTts.Data.Context;
using MyTts.Data.Entities;
using MyTts.Data.Interfaces;
using MyTts.Models;
using Microsoft.EntityFrameworkCore;
using AutoMapper;

namespace MyTts.Data.Repositories
{
    public class Mp3MetaRepository : Repository<Entities.Mp3Meta, Interfaces.IMp3>, IMp3MetaRepository
    {
        public Mp3MetaRepository(AppDbContext context, IMapper mapper) : base(context, mapper) { }

        // Mp3File'a özel metodları burada implemente edebilirsin

    }
}