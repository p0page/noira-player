using System.Threading.Tasks;
using NextGenEmby.Core.Emby;

namespace NextGenEmby.Core.Storage
{
    public interface ISessionStore
    {
        Task<EmbySession?> LoadAsync();
        Task SaveAsync(EmbySession session);
        Task ClearAsync();
    }
}
