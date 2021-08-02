using System.Threading;
using System.Threading.Tasks;

namespace Akka.Management.Dsl
{
    public interface IHealthCheck
    {
        Task<bool> Execute(CancellationToken token);
    }
}