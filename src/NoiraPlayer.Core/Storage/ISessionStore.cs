using System.Threading.Tasks;
using NoiraPlayer.Core.Emby;

namespace NoiraPlayer.Core.Storage
{
    public interface ISessionStore
    {
        Task<EmbySession?> LoadAsync();
        Task SaveAsync(EmbySession session);
        Task ClearAsync();
    }
}
