namespace Cube.Replication {
    public static class Constants {
        const float replicaFullUpdatesPerSecond = 0.25f;
        public const int replicaFullUpdateRateMS = (int)(1000 / replicaFullUpdatesPerSecond);

        public const float clientReplicaInactiveDestructionTimeSec = 6;
        public const float serverReplicaIdRecycleTime = clientReplicaInactiveDestructionTimeSec * 1.2f;
    }
}
