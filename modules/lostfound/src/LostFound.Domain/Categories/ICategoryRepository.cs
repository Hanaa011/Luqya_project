using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace LostFound.Categories
{
    public interface ICategoryRepository : IRepository<Category, Guid>
    {
        Task<Category?> FindByNameAsync(string name);
    }
}
