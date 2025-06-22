using Grpc.Core;

namespace ColorAnalyzer.Util
{
    public static class ServiceUtil
    {
        public static void CheckContext(ServerCallContext context)
        {
            if (context.CancellationToken.IsCancellationRequested)
                throw new RpcException(new Status(StatusCode.Cancelled, "Operação cancelada."));
            if (DateTime.Now > context.Deadline)
                throw new RpcException(new Status(StatusCode.Cancelled, "Operação interrompida por exceder o tempo de espera."));
        }
    }
}
