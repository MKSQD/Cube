namespace Cube.Networking.Replicas {
    public static class Constants {
        // #Todo adjust update rate to client settings
        const int replicaUpdatesPerSecond = 20;
        public const int replicaUpdateRateMS = 1000 / replicaUpdatesPerSecond;

        const float replicaFullUpdatesPerSecond = 0.25f;
        public const int replicaFullUpdateRateMS = (int)(1000 / replicaFullUpdatesPerSecond);

        public const float clientReplicaInactiveDestructionTimeSec = 6;
        public const float serverReplicaIdRecycleTime = clientReplicaInactiveDestructionTimeSec * 1.2f;
    }
}
