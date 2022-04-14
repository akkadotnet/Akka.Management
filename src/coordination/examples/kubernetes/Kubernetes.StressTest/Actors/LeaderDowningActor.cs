using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;
using Akka.Event;

namespace KubernetesCluster.Actors
{
    public class LeaderDowningActor:ReceiveActor
    {
        private readonly Cluster _cluster;
        private bool _shuttingDown;
        private bool _isLeader;
        
        public LeaderDowningActor()
        {
            var system = (ExtendedActorSystem) Context.System;
            _cluster = Cluster.Get(system);
            _cluster.Subscribe(Self, ClusterEvent.SubscriptionInitialStateMode.InitialStateAsEvents, typeof(ClusterEvent.LeaderChanged));

            ReceiveAsync<Debug>(async d =>
            {
                if (_shuttingDown || !_isLeader)
                    return;
                
                var msg = d.Message.ToString();
                if (msg.Contains("Lease after update:"))
                {
                    _shuttingDown = true;
                    await Task.Delay(200);
                    system.Guardian.Stop(); // Crash this system
                }
            });

            Receive<ClusterEvent.LeaderChanged>(e =>
            {
                _isLeader = e.Leader?.Equals(_cluster.SelfUniqueAddress.Address) ?? false;
            });
        }
    }
}